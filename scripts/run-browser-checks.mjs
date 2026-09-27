import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// Preserve all diagnostics without ever converting a test failure into success.
const suites = ['browser-check.mjs', 'keyboard-input-check.mjs', 'document-feature-check.mjs', 'sections-fields-check.mjs', 'typography-check.mjs'];
let failed = false;
for (const suite of suites) {
  const result = spawnSync(process.execPath, [fileURLToPath(new URL(suite, import.meta.url))], {
    stdio: 'inherit', env: process.env, timeout: 10 * 60 * 1000
  });
  if (result.error || result.status !== 0) {
    failed = true;
    console.error(`Browser suite failed: ${suite}`, result.error || `exit ${result.status}`);
  }
}
process.exitCode = failed ? 1 : 0;
