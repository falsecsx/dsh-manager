'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { run } = require('../dsh-gpt-compat.cjs');
const installDir = process.env.DSH_TEST_INSTALL || path.resolve(__dirname, '../../DeepSeek Harness');
const yaml = require(path.join(installDir, 'node_modules', 'js-yaml'));

async function main() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dsh-protocol-reasoning-'));
  const target = path.join(root, 'settings.yaml');
  const stateRoot = path.join(root, 'state');
  const original = Buffer.from('llm-pi-ai:\n  providers:\n    completion:\n      api: openai-completions\n      models:\n        - id: deepseek-chat\n    anthropic:\n      api: anthropic-messages\n      models:\n        - id: claude-sonnet\n    response:\n      api: openai-responses\n      models:\n        - id: gpt-test\n');
  fs.writeFileSync(target, original);
  const options = { target, stateRoot, installDir };
  await run({ ...options, mode: 'reasoning-on' });
  let settings = yaml.load(fs.readFileSync(target, 'utf8'));
  for (const id of ['completion', 'anthropic', 'response']) assert.deepEqual(settings['llm-pi-ai'].providers[id].models[0].reasoningEfforts, { low: 'low', medium: 'medium', high: 'high', xhigh: 'xhigh', max: 'max' });
  assert.equal(settings['llm-pi-ai'].providers.completion.compat.supportsReasoningEffort, true);
  assert.equal(settings['llm-pi-ai'].providers.anthropic.compat.forceAdaptiveThinking, true);
  await run({ ...options, mode: 'reasoning-off' });
  settings = yaml.load(fs.readFileSync(target, 'utf8'));
  assert.equal(settings['llm-pi-ai'].providers.completion.compat, undefined);
  assert.equal(settings['llm-pi-ai'].providers.anthropic.compat, undefined);
  assert.deepEqual(fs.readFileSync(target), original);
  console.log('PASS: all supported reasoning protocols and exact restore.');
}
main().catch(error => { console.error(error); process.exitCode = 1; });
