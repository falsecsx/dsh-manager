'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createRequire } = require('node:module');
const { pathToFileURL } = require('node:url');
const { run, locations } = require('../dsh-gpt-compat.cjs');
const installDir = process.env.DSH_TEST_INSTALL || path.resolve(__dirname, '../../DeepSeek Harness');
const localRequire = createRequire(path.join(installDir, 'package.json'));
const yaml = localRequire('js-yaml');
const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dsh-compat-test-'));
const target = path.join(root, 'settings.yaml');
const stateRoot = path.join(root, 'manager');
const options = { target, stateRoot, installDir };
const original = Buffer.from('\ufeff# Preserve original bytes and comments\r\n' + yaml.dump({
  'ui-theme': { preference: 'dark' },
  'llm-pi-ai': { providers: {
    gpt: { api: 'openai-responses', baseURL: 'https://example.invalid/v1', apiKeyEnv: 'GPT_TEST_KEY', compat: { supportsMaxOutputTokens: false }, models: [{ id: 'gpt-test', compat: { supportsStrictMode: false } }] },
    ds: { api: 'openai-responses', baseURL: 'https://example.invalid/v1', models: [{ id: 'deepseek-test' }] },
    chat: { api: 'openai-completions', baseURL: 'https://example.invalid/v1', models: [{ id: 'gpt-test' }] }
  } }
}).replace(/\n/g, '\r\n'));
async function main() {
  fs.writeFileSync(target, original);
  await run({ ...options, mode: 'off' });
  assert.deepEqual(fs.readFileSync(target), original);
  assert.equal(fs.existsSync(stateRoot), false, 'default off must not create state');
  await run({ ...options, mode: 'on' });
  const loc = locations(target, stateRoot);
  assert.deepEqual(fs.readFileSync(loc.backup), original);
  const patched = fs.readFileSync(target);
  const config = yaml.load(patched.toString());
  const routes = config['llm-pi-ai'].providers;
  assert.equal(routes.gpt.compat.supportsStrictMode, true);
  assert.equal(routes.gpt.models[0].compat.supportsStrictMode, true);
  assert.equal(routes.gpt.compat.supportsMaxOutputTokens, false);
  assert.equal(routes.ds.compat, undefined);
  assert.equal(routes.chat.compat, undefined);
  assert.equal(config['ui-theme'].preference, 'dark');
  await run({ ...options, mode: 'on' });
  assert.deepEqual(fs.readFileSync(loc.backup), original, 'reconcile must never replace original backup');
  assert.deepEqual(fs.readFileSync(target), patched, 'idempotent configuration');
  const piAiRoot = path.join(installDir, 'node_modules', '@earendil-works', 'pi-ai', 'dist');
  const { stream } = await import(pathToFileURL(path.join(piAiRoot, 'api', 'openai-responses.js')).href);
  const model = { id: 'gpt-test', name: 'GPT test', api: 'openai-responses', provider: 'gpt', baseUrl: 'https://example.invalid/v1', reasoning: false, input: ['text'], contextWindow: 32768, maxTokens: 1024, cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 }, compat: routes.gpt.compat };
  const tool = { name: 'pwsh', description: 'Read the current directory', parameters: { type: 'object', properties: { command: { type: 'string' }, description: { type: 'string' }, sandbox_permissions: { type: 'string', enum: ['workspace-write', 'danger-full-access'] }, justification: { type: 'string' } }, required: ['command', 'description'] } };
  let payload;
  await stream(model, { messages: [{ role: 'user', content: 'Show the current directory.', timestamp: 0 }], tools: [tool] }, { apiKey: 'offline', onPayload(p) { payload = p; throw new Error('offline-capture'); }, fetch() { throw new Error('network forbidden'); } }).result();
  assert.equal(payload.tools[0].strict, false);
  assert.deepEqual(payload.tools[0].parameters.required, ['command', 'description']);
  const sandboxRoot = path.dirname(localRequire.resolve('@deepseek-ai/dsh-sandbox'));
  const { validateEscalationArgs } = await import(pathToFileURL(path.join(sandboxRoot, 'index.js')).href);
  assert.doesNotThrow(() => validateEscalationArgs(undefined, undefined));
  assert.throws(() => validateEscalationArgs('workspace-write', ''), /invalid justification/);
  assert.throws(() => validateEscalationArgs('workspace-write', undefined), /requires a justification/);
  assert.throws(() => validateEscalationArgs(undefined, 'Reason.'), /only valid together/);
  await run({ ...options, mode: 'off' });
  assert.deepEqual(fs.readFileSync(target), original, 'restore must be byte-for-byte');
  assert.equal(fs.existsSync(loc.stateFile), false);
  assert.equal(fs.existsSync(loc.backup), false);
  await run({ ...options, mode: 'on' });
  fs.appendFileSync(target, '\n# Later user changes\n');
  const later = fs.readFileSync(target);
  await run({ ...options, mode: 'off' });
  assert.deepEqual(fs.readFileSync(target), original);
  const archived = fs.readdirSync(loc.dir).find(n => n.startsWith('settings.before-restore-'));
  assert.deepEqual(fs.readFileSync(path.join(loc.dir, archived)), later);
  await run({ ...options, mode: 'on' });
  fs.writeFileSync(loc.backup, 'corrupt');
  const beforeFailure = fs.readFileSync(target);
  await assert.rejects(run({ ...options, mode: 'off' }), /备份缺失或校验失败/);
  assert.deepEqual(fs.readFileSync(target), beforeFailure);
  fs.writeFileSync(loc.backup, original);
  await run({ ...options, mode: 'off' });
  fs.writeFileSync(target, 'llm-pi-ai: {providers: {}}');
  await assert.rejects(run({ ...options, mode: 'on' }), /未找到/);
  assert.equal(fs.existsSync(loc.stateFile), false);
  fs.writeFileSync(target, 'invalid: [yaml');
  await assert.rejects(run({ ...options, mode: 'on' }));
  assert.equal(fs.existsSync(loc.stateFile), false);
  console.log('PASS: default off, route selection, model overrides, schema validation, idempotence, strict:false payload, sandbox guards, byte-exact restore, later-edit archive, corrupt backup and invalid YAML.');
}
main().catch(error => { console.error(error); process.exitCode = 1; }).finally(() => {
  // Delete only the unique test directory created above.
  if (path.dirname(root) === os.tmpdir() && path.basename(root).startsWith('dsh-compat-test-')) fs.rmSync(root, { recursive: true, force: true });
});
