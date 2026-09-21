'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { run, locations } = require('../dsh-gpt-compat.cjs');
const installDir = process.env.DSH_TEST_INSTALL || path.resolve(__dirname, '../../DeepSeek Harness');
const yaml = require(path.join(installDir, 'node_modules', 'js-yaml'));

async function main() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dsh-reasoning-test-'));
  const target = path.join(root, 'settings.yaml');
  const stateRoot = path.join(root, 'manager');
  const original = Buffer.from('llm-pi-ai:\n  providers:\n    falsefi:\n      api: openai-responses\n      models:\n        - id: gpt-test\n          reasoningEfforts:\n            medium: custom-medium\n    deepseek:\n      api: openai-responses\n      models:\n        - id: deepseek-test\n    chat:\n      api: openai-completions\n      models:\n        - id: chat-test\n');
  fs.writeFileSync(target, original);
  const options = { target, stateRoot, installDir };

  await run({ ...options, mode: 'reasoning-on' });
  let settings = yaml.load(fs.readFileSync(target, 'utf8'));
  const model = settings['llm-pi-ai'].providers.falsefi.models[0];
  assert.deepEqual(model.reasoningEfforts, { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' });
  assert.deepEqual(settings['llm-pi-ai'].providers.deepseek.models[0].reasoningEfforts, { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' });
  assert.equal(settings['llm-pi-ai'].providers.chat.models[0].reasoningEfforts, undefined);

  await run({ ...options, mode: 'on' });
  settings = yaml.load(fs.readFileSync(target, 'utf8'));
  assert.equal(settings['llm-pi-ai'].providers.falsefi.compat.supportsStrictMode, true);
  assert.deepEqual(settings['llm-pi-ai'].providers.falsefi.models[0].reasoningEfforts, { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' });

  await run({ ...options, mode: 'reasoning-off' });
  settings = yaml.load(fs.readFileSync(target, 'utf8'));
  assert.equal(settings['llm-pi-ai'].providers.falsefi.compat.supportsStrictMode, true);
  assert.deepEqual(settings['llm-pi-ai'].providers.falsefi.models[0].reasoningEfforts, { medium: 'custom-medium' });

  await run({ ...options, mode: 'off' });
  assert.deepEqual(fs.readFileSync(target), original);
  const loc = locations(target, stateRoot);
  assert.equal(fs.existsSync(loc.stateFile), false);
  assert.equal(fs.existsSync(loc.backup), false);

  const target2 = path.join(root, 'settings-with-later-model.yaml');
  const stateRoot2 = path.join(root, 'manager-with-later-model');
  fs.writeFileSync(target2, original);
  const options2 = { target: target2, stateRoot: stateRoot2, installDir };
  await run({ ...options2, mode: 'reasoning-on' });
  const later = yaml.load(fs.readFileSync(target2, 'utf8'));
  later['llm-pi-ai'].providers.grok = { api: 'openai-responses', models: [{ id: 'grok-test' }] };
  fs.writeFileSync(target2, yaml.dump(later, { noRefs: true, lineWidth: -1, sortKeys: false }));
  await run({ ...options2, mode: 'reasoning-on' });
  let laterSettings = yaml.load(fs.readFileSync(target2, 'utf8'));
  assert.deepEqual(laterSettings['llm-pi-ai'].providers.grok.models[0].reasoningEfforts, { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' });
  await run({ ...options2, mode: 'reasoning-off' });
  await run({ ...options2, mode: 'off' });
  laterSettings = yaml.load(fs.readFileSync(target2, 'utf8'));
  assert.equal(laterSettings['llm-pi-ai'].providers.grok.models[0].reasoningEfforts, undefined);
  assert.equal(laterSettings['llm-pi-ai'].providers.grok.models[0].id, 'grok-test');
  console.log('PASS: reasoning toggle, independent strict toggle, route filtering and exact restore.');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
