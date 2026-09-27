import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
await fs.mkdir('test-results', { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const report = { base, checks: [], errors: [] };
page.on('pageerror', error => report.errors.push(error.message));
const text = async expected => page.waitForFunction(expected => globalThis.__textSpaceState?.text === expected, expected, { timeout: 20000 });
const ready = async expected => page.waitForFunction(expected => document.activeElement?.id === 'uno-input' && document.activeElement.tagName === 'TEXTAREA' && document.activeElement.value === expected, expected, { timeout: 20000 });
async function click(name) {
  await page.waitForFunction(name => globalThis.__textSpaceState?.controls.some(c => c.name === name && c.width > 1), name, { timeout: 30000 });
  const control = await page.evaluate(name => __textSpaceState.controls.find(c => c.name === name && c.width > 1), name);
  await page.mouse.click(control.x + control.width / 2, control.y + control.height / 2);
  await page.waitForTimeout(250);
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS KEYBOARD', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.canvas.width > 200 || globalThis.__textSpaceError, null, { timeout: 120000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await click('File tab'); await click('Blank document template'); await ready('');
  await check('rapid physical key burst preserves every character', async () => {
    await page.keyboard.type('rapid input', { delay: 0 }); await text('rapid input');
  });
  await check('one Enter produces one paragraph break', async () => {
    await page.keyboard.press('Enter'); await text('rapid input\n');
  });
  await check('typing immediately after a handled key is retained', async () => {
    await page.keyboard.type('ABC', { delay: 0 }); await text('rapid input\nABC');
  });
  await check('Backspace and Delete each apply once', async () => {
    await page.keyboard.press('Backspace'); await text('rapid input\nAB');
    await page.keyboard.press('ArrowLeft'); await page.keyboard.press('Delete'); await text('rapid input\nA');
  });
  await check('select-all deletion is a single document edit', async () => {
    await page.keyboard.press('Control+a'); await page.keyboard.press('Backspace'); await text(''); await ready('');
  });
  await check('emoji grapheme deletion retains neighboring characters', async () => {
    await page.keyboard.insertText('A👩‍💻B'); await text('A👩‍💻B');
    await page.keyboard.press('ArrowLeft'); await page.keyboard.press('Backspace'); await text('AB');
  });
  await check('document undo and redo are independent of native input history', async () => {
    await page.keyboard.press('Control+z'); await text('A👩‍💻B');
    await page.keyboard.press('Control+y'); await text('AB');
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await page.evaluate(() => globalThis.__textSpaceState).catch(() => null);
  report.input = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, value: document.activeElement?.value, start: document.activeElement?.selectionStart, end: document.activeElement?.selectionEnd })).catch(() => null);
  await page.screenshot({ path: 'test-results/keyboard-failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile('test-results/keyboard-report.json', JSON.stringify(report, null, 2));
  console.log('KEYBOARD ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure }));
  await browser.close();
}
