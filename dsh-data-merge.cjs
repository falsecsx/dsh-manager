'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');

function copyMissing(src, dst) {
  if (!fs.existsSync(src)) return 0;
  let count = 0;
  const stat = fs.statSync(src);
  if (stat.isDirectory()) {
    fs.mkdirSync(dst, { recursive: true });
    for (const name of fs.readdirSync(src)) count += copyMissing(path.join(src, name), path.join(dst, name));
  } else if (!fs.existsSync(dst)) {
    fs.mkdirSync(path.dirname(dst), { recursive: true });
    fs.copyFileSync(src, dst); count++;
  }
  return count;
}
function mergeSettings(targetFile, sources, installDir) {
  const localRequire = createRequire(path.join(path.resolve(installDir), 'package.json'));
  const yaml = localRequire('js-yaml');
  const targetBytes = fs.readFileSync(targetFile);
  const target = yaml.load(targetBytes.toString('utf8')) || {};
  target['llm-pi-ai'] ||= {};
  target['llm-pi-ai'].providers ||= {};
  let providers = 0, models = 0;
  for (const sourceDir of sources) {
    const file = path.join(sourceDir, 'settings.yaml');
    if (!fs.existsSync(file)) continue;
    const source = yaml.load(fs.readFileSync(file, 'utf8')) || {};
    const incoming = source['llm-pi-ai']?.providers || {};
    for (const [id, srcRoute] of Object.entries(incoming)) {
      if (!srcRoute || typeof srcRoute !== 'object') continue;
      const dstRoute = target['llm-pi-ai'].providers[id];
      if (!dstRoute) { target['llm-pi-ai'].providers[id] = srcRoute; providers++; continue; }
      if (!Array.isArray(srcRoute.models)) continue;
      dstRoute.models ||= [];
      const ids = new Set(dstRoute.models.map(m => m && m.id).filter(Boolean));
      for (const model of srcRoute.models) if (model && model.id && !ids.has(model.id)) { dstRoute.models.push(model); ids.add(model.id); models++; }
    }
  }
  const output = yaml.dump(target, { noRefs: true, lineWidth: -1, sortKeys: false });
  const temp = targetFile + '.merge.tmp'; fs.writeFileSync(temp, output, 'utf8'); fs.renameSync(temp, targetFile);
  return { providers, models };
}
function run(target, sources, installDir) {
  fs.mkdirSync(target, { recursive: true });
  const backup = path.join(target, 'backups', 'manager-merge-' + new Date().toISOString().replace(/[:.]/g, '-'));
  fs.mkdirSync(backup, { recursive: true });
  if (fs.existsSync(path.join(target, 'settings.yaml'))) fs.copyFileSync(path.join(target, 'settings.yaml'), path.join(backup, 'settings.yaml'));
  const result = mergeSettings(path.join(target, 'settings.yaml'), sources, installDir);
  let files = 0;
  for (const source of sources) for (const dir of ['sessions', 'storages', 'profiles']) files += copyMissing(path.join(source, dir), path.join(target, dir));
  return { ok: true, backup, files, providers: result.providers, models: result.models };
}
const [target, sourceJson, installDir, report] = process.argv.slice(2);
try { const result = run(path.resolve(target), JSON.parse(sourceJson), installDir); fs.writeFileSync(report, JSON.stringify(result)); }
catch (e) { fs.writeFileSync(report, JSON.stringify({ ok: false, message: e.message })); process.exitCode = 1; }
