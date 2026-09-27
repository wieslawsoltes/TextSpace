import { spawnSync } from 'node:child_process';

// Run each isolated scenario suite even when another fails, preserving all
// diagnostic evidence without ever converting a test failure into success.
const suites = ['browser-check.mjs', 'keyboard-input-check.mjs', 'document-feature-check.mjs'];
let failed = false;
for (const suite of suites) {
  const result = spawnSync(process.execPath, [new URL(suite, import.meta.url).pathname], {
    stdio: 'inherit', env: process.env, timeout: 10 * 60 * 1000
  });
  if (result.error || result.status !== 0) {
    failed = true;
    console.error(`Browser suite failed: ${suite}`, result.error || `exit ${result.status}`);
  }
}
process.exitCode = failed ? 1 : 0;
