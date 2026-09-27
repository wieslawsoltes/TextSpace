import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
await fs.mkdir('test-results', { recursive: true });
const report = { base, checks: [], errors: [], console: [], failedRequests: [] };
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
const page = await context.newPage();
page.on('pageerror', error => { report.errors.push(error.message); console.error('PAGE ERROR', error.message); });
page.on('requestfailed', request => { const failure = { url: request.url(), error: request.failure()?.errorText }; report.failedRequests.push(failure); console.error('REQUEST FAILED', JSON.stringify(failure)); });
page.on('response', response => { if (response.status() >= 400) console.error('HTTP ERROR', response.status(), response.url()); });
page.on('console', message => { if (message.type() === 'error' || message.type() === 'warning') { report.console.push(message.text()); console.error('BROWSER', message.type(), message.text()); } });
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(test, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await test()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function click(name) {
  let control;
  await until(async () => {
    control = await page.evaluate(name => (globalThis.__textSpaceState?.controls || []).find(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 >= 0 && c.y + c.height / 2 >= 0 && c.x + c.width / 2 < innerWidth && c.y + c.height / 2 < innerHeight), name);
    return !!control;
  }, 'Missing control: ' + name);
  await page.mouse.click(control.x + control.width / 2, control.y + control.height / 2);
  await page.waitForTimeout(250);
}
// Managed model notifications can precede the native DOM input's focus/layout.
// Observe that boundary, never focus an element or inject text through a test API.
async function inputReady(expectedText, tag = 'TEXTAREA') {
  await until(() => page.evaluate(({ expectedText, tag }) => {
    const input = document.activeElement;
    return input?.id === 'uno-input' && input.tagName === tag && !input.readOnly && !input.disabled && input.value === expectedText;
  }, { expectedText, tag }), 'Native input did not acquire focus with the expected document text');
}
async function typeText(text) {
  const expected = (await state()).text;
  await inputReady(expected);
  await page.keyboard.insertText(text);
}
async function boot() {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.ready || globalThis.__textSpaceError, null, { timeout: 120000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await until(async () => (await state())?.canvas.width > 200, 'Editor did not lay out');
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS', name); }
try {
  await check('publication provenance', async () => {
    let info;
    await until(async () => {
      try {
        const response = await fetch(base + 'build-info.json?t=' + Date.now());
        if (!response.ok) return false;
        info = await response.json();
        return !process.env.TEXTSPACE_EXPECTED_COMMIT || info.commit === process.env.TEXTSPACE_EXPECTED_COMMIT;
      } catch { return false; }
    }, 'Build metadata unavailable or outdated', 90000);
    assert.equal(info.host, 'Uno WebAssembly'); report.build = info;
  });
  await check('runtime module is statically served', async () => {
    for (const name of ['_framework/dotnet.js', '_framework/dotnet.js?version=static-preflight']) {
      const response = await fetch(base + name, { redirect: 'error' });
      const body = await response.text();
      assert.equal(response.status, 200, `Runtime module returned ${response.status}: ${body.slice(0, 120)}`);
      assert.match(response.headers.get('content-type') || '', /javascript/);
      assert.ok(body.length > 1000, 'Runtime entry is empty');
      assert.ok(!body.trimStart().startsWith('<'), 'Runtime path returned HTML');
      console.log('RUNTIME HTTP VERIFIED', name, body.length);
    }
  });
  await check('Uno application startup', async () => {
    await boot(); const current = await state();
    assert.equal(current.runtime, 'Uno WebAssembly / Skia'); assert.ok(current.words > 100); assert.ok(current.pages > 0);
    await page.screenshot({ path: 'test-results/desktop.png' });
  });
  await check('ribbon interaction', async () => {
    await click('Insert tab'); await until(async () => (await state()).selectedTab === 'Insert', 'Insert ribbon did not open'); await click('Home tab');
  });
  await check('new document and typing', async () => {
    await click('File tab'); await click('Blank document template'); await until(async () => (await state()).text === '', 'Blank template did not load');
    // Wait for template completion before calculating coordinates from its layout.
    await inputReady('');
    const { canvas: c } = await state();
    await page.mouse.click(c.x + c.paperLeft + 75 * c.scale, c.y + 18 + 75 * c.scale - c.scrollY);
    await typeText('Hello TextSpace'); await until(async () => (await state()).text === 'Hello TextSpace', 'Typing failed');
  });
  await check('formatting and history', async () => {
    await inputReady('Hello TextSpace');
    await page.keyboard.press('Control+a'); await page.keyboard.press('Control+b'); await until(async () => (await state()).style.bold, 'Bold failed');
    await page.keyboard.press('Control+End'); await page.keyboard.press('Enter');
    await until(async () => (await state()).text === 'Hello TextSpace\n', 'Paragraph break failed');
    await typeText('Second paragraph');
    await until(async () => (await state()).text.endsWith('Second paragraph'), 'Paragraph failed');
    await page.keyboard.press('Control+z'); await until(async () => !(await state()).text.includes('Second paragraph'), 'Undo failed');
    await page.keyboard.press('Control+y'); await until(async () => (await state()).text.endsWith('Second paragraph'), 'Redo failed');
  });
  await check('native download', async () => {
    await inputReady((await state()).text);
    const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const download = await pending;
    await download.saveAs('test-results/document.textspace');
    const model = JSON.parse(await fs.readFile('test-results/document.textspace', 'utf8'));
    assert.equal(model.formatVersion, 1); assert.ok(model.blocks.length >= 2);
  });
  await check('recovery after reload', async () => {
    const text = (await state()).text;
    await until(() => page.evaluate(async expected => {
      const value = await globalThis.TextSpaceHost.loadRecovery();
      if (!value) return false;
      const document = JSON.parse(value);
      return document.blocks.map(p => (p.runs || []).map(r => r.text).join('')).join('\n') === expected;
    }, text), 'Recovery transaction did not commit');
    await boot(); assert.equal((await state()).text, text);
  });
  await check('DOCX export', async () => {
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('Word document');
    const download = await pending; await download.saveAs('test-results/document.docx');
    const data = await fs.readFile('test-results/document.docx'); assert.equal(data.subarray(0, 2).toString(), 'PK'); await click('Back to document');
  });
  await check('compact layout', async () => {
    await page.setViewportSize({ width: 1000, height: 760 }); await page.waitForTimeout(600);
    await page.screenshot({ path: 'test-results/compact.png' }); assert.ok((await state()).canvas.width > 300);
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); report.state = await state().catch(() => null);
  report.nativeInput = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, inputs: [...document.querySelectorAll('input,textarea')].map(e => ({ id: e.id, tag: e.tagName, value: e.value, start: e.selectionStart, end: e.selectionEnd })) })).catch(() => null);
  process.exitCode = 1; console.error(error);
  report.dom = await page.content().catch(() => '');
  await page.screenshot({ path: 'test-results/failure.png' }).catch(() => {});
} finally {
  await fs.writeFile('test-results/report.json', JSON.stringify(report, null, 2));
  console.log('ACCEPTANCE SUMMARY', JSON.stringify({ checks: report.checks, errors: report.errors, failure: report.failure, failedRequests: report.failedRequests }));
  await browser.close();
}
