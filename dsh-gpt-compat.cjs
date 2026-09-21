'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { createRequire } = require('node:module');

const digest = value => crypto.createHash('sha256').update(value).digest('hex');
function atomicWrite(file, bytes) { const temp = file + '.' + crypto.randomUUID() + '.tmp'; try { fs.writeFileSync(temp, bytes, { flag: 'wx' }); fs.renameSync(temp, file); } finally { if (fs.existsSync(temp)) fs.unlinkSync(temp); } }
function locations(target, stateRoot) { target = path.resolve(target); const dir = path.resolve(stateRoot); return { target, dir, stateFile: path.join(dir, 'state.json'), backup: path.join(dir, 'settings.original.yaml') }; }
function readState(loc) {
  if (!fs.existsSync(loc.stateFile)) return null;
  const raw = JSON.parse(fs.readFileSync(loc.stateFile, 'utf8'));
  let state = raw;
  if (raw.version === 1 && raw.target === loc.target && raw.backup === loc.backup) state = { version: 2, target: raw.target, backup: raw.backup, originalHash: raw.originalHash, createdAt: raw.createdAt, features: { strict: true, reasoning: false }, appliedHash: raw.appliedHash };
  if (state.version !== 2 || state.target !== loc.target || state.backup !== loc.backup) throw new Error('兼容修复记录与目标配置不匹配，已保留原文件。');
  if (!state.features || typeof state.features !== 'object' || typeof state.features.strict !== 'boolean' || typeof state.features.reasoning !== 'boolean') throw new Error('兼容修复状态无效，已保留原文件。');
  if (!fs.existsSync(loc.backup) || digest(fs.readFileSync(loc.backup)) !== state.originalHash) throw new Error('原始配置备份缺失或校验失败，已保留原文件。');
  return state;
}
function parseYaml(bytes, installDir) { const localRequire = createRequire(path.join(path.resolve(installDir), 'package.json')); const yaml = localRequire('js-yaml'); const settings = yaml.load(bytes.toString('utf8')); if (!settings || typeof settings !== 'object' || Array.isArray(settings)) throw new Error('DSH settings.yaml 不是有效的配置映射。'); return { yaml, settings }; }
const REASONING = { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' };
function patchYaml(original, installDir, features) {
  const { yaml, settings } = parseYaml(original, installDir); const routes = [];
  for (const [id, route] of Object.entries(settings['llm-pi-ai']?.providers || {})) {
    if (!route || route.api !== 'openai-responses' || !Array.isArray(route.models)) continue;
    const reasoningModels = route.models.filter(m => m && typeof m === 'object' && typeof m.id === 'string' && m.id.trim());
    const strictModels = reasoningModels.filter(m => /^gpt-/i.test(m.id));
    if (features.strict && strictModels.length) { if (route.compat != null && (typeof route.compat !== 'object' || Array.isArray(route.compat))) throw new Error('GPT 路由 compat 配置无效：' + id); route.compat ||= {}; route.compat.supportsStrictMode = true; for (const model of strictModels) { if (model.compat != null && (typeof model.compat !== 'object' || Array.isArray(model.compat))) throw new Error('GPT 模型 compat 配置无效：' + model.id); if (model.compat && Object.hasOwn(model.compat, 'supportsStrictMode') && model.compat.supportsStrictMode !== true) model.compat.supportsStrictMode = true; } }
    if (features.reasoning) for (const model of reasoningModels) model.reasoningEfforts = { ...REASONING };
    if ((features.strict && strictModels.length) || (features.reasoning && reasoningModels.length)) routes.push(id);
  }
  if (!routes.length) throw new Error('未找到可处理的 openai-responses 模型，请先在 DSH 中配置模型。');
  const output = Buffer.from(yaml.dump(settings, { noRefs: true, lineWidth: -1, sortKeys: false }), 'utf8'); return { output: output.equals(original) ? original : output, routes, settings };
}
async function validate(settings, installDir) { const localRequire = createRequire(path.join(path.resolve(installDir), 'package.json')); const { pathToFileURL } = require('node:url'); const { Config } = await import(pathToFileURL(localRequire.resolve('@deepseek-ai/dsh-llm-pi-ai')).href); Config(settings['llm-pi-ai']); }
function modeFeature(mode) { if (mode === 'on' || mode === 'strict-on') return ['strict', true]; if (mode === 'off' || mode === 'strict-off') return ['strict', false]; if (mode === 'reasoning-on') return ['reasoning', true]; if (mode === 'reasoning-off') return ['reasoning', false]; return null; }
async function run({ mode, target, stateRoot, installDir }) {
  const loc = locations(target, stateRoot); const feature = modeFeature(mode); if (!feature && mode !== 'status') throw new Error('未知兼容修复操作。'); let state = readState(loc);
  if (mode === 'status') return { ok: true, active: !!state, message: state ? '已保存原始配置备份。' : '未启用兼容修复。' };
  if (!state && feature && !feature[1]) return { ok: true, active: false, message: '使用官方默认配置。' };
  fs.mkdirSync(loc.dir, { recursive: true }); const lock = path.join(loc.dir, 'operation.lock'); let lockFd;
  try {
    lockFd = fs.openSync(lock, 'wx'); state = readState(loc); const [name, enabled] = feature;
    if (!state) { const original = fs.readFileSync(loc.target); const { output, routes, settings } = patchYaml(original, installDir, { strict: name === 'strict', reasoning: name === 'reasoning' }); await validate(settings, installDir); atomicWrite(loc.backup, original); state = { version: 2, target: loc.target, backup: loc.backup, originalHash: digest(original), createdAt: new Date().toISOString(), features: { strict: name === 'strict', reasoning: name === 'reasoning' } }; state.appliedHash = digest(output); atomicWrite(loc.stateFile, JSON.stringify(state, null, 2)); if (!output.equals(original)) atomicWrite(loc.target, output); return { ok: true, active: true, message: name === 'reasoning' ? '已启用模型思考强度调节：' + routes.join('、') : '已启用 GPT 工具调用兼容修复：' + routes.join('、') }; }
    const current = fs.existsSync(loc.target) ? fs.readFileSync(loc.target) : Buffer.alloc(0); if (digest(current) !== state.appliedHash && digest(current) !== state.originalHash && current.length) { const saved = path.join(loc.dir, 'settings.before-restore-' + crypto.randomUUID() + '.yaml'); fs.writeFileSync(saved, current, { flag: 'wx' }); }
    const features = { ...state.features, [name]: enabled }; const original = fs.readFileSync(loc.backup);
    if (!features.strict && !features.reasoning) { atomicWrite(loc.target, original); fs.unlinkSync(loc.stateFile); fs.unlinkSync(loc.backup); return { ok: true, active: false, message: '已完整恢复启用前的配置。' }; }
    const { output, routes, settings } = patchYaml(original, installDir, features); await validate(settings, installDir); const next = { ...state, features, appliedHash: digest(output) }; atomicWrite(loc.stateFile, JSON.stringify(next, null, 2)); if (!output.equals(current)) atomicWrite(loc.target, output); const message = name === 'reasoning' ? (enabled ? '已启用模型思考强度调节：' : '已关闭模型思考强度调节。') : (enabled ? '已启用 GPT 工具调用兼容修复：' : '已关闭 GPT 工具调用兼容修复。'); return { ok: true, active: true, message: message + (enabled ? routes.join('、') : '') };
  } finally { if (lockFd !== undefined) { fs.closeSync(lockFd); try { fs.unlinkSync(lock); } catch {} } }
}
module.exports = { run, locations, patchYaml };
if (require.main === module) { const [mode, target, stateRoot, installDir, report] = process.argv.slice(2); run({ mode, target, stateRoot, installDir }).then(result => fs.writeFileSync(report, JSON.stringify(result), 'utf8')).catch(error => { fs.writeFileSync(report, JSON.stringify({ ok: false, message: error.message }), 'utf8'); process.exitCode = 1; }); }
