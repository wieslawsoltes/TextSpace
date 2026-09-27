import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [] };
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
let page = await context.newPage();
page.on('pageerror', e => report.errors.push(e.message));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(condition, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await condition()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function click(name) {
  let control;
  await until(async () => {
    control = await page.evaluate(name => globalThis.__textSpaceState?.controls.find(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 >= 0 && c.x + c.width / 2 < innerWidth && c.y + c.height / 2 >= 0 && c.y + c.height / 2 < innerHeight), name);
    return !!control;
  }, 'Missing visible control: ' + name);
  await page.mouse.click(control.x + control.width / 2, control.y + control.height / 2);
  await page.waitForTimeout(250);
}
async function readyInput(expected, tag = 'TEXTAREA') {
  await until(() => page.evaluate(({ expected, tag }) => {
    const e = document.activeElement;
    return e?.id === 'uno-input' && e.tagName === tag && !e.readOnly && (expected === null || e.value === expected);
  }, { expected, tag }), 'Native input not ready');
}
async function fill(name, text) {
  await click(name); await readyInput(null, 'INPUT');
  await page.keyboard.press('Control+a'); await page.keyboard.insertText(text);
  await readyInput(text, 'INPUT');
}
async function saveNative(name) {
  await readyInput((await state()).text);
  const pending = page.waitForEvent('download', { timeout: 30000 });
  await page.keyboard.press('Control+s');
  const download = await pending; const path = output + '/' + name + '.textspace';
  await download.saveAs(path); return JSON.parse(await fs.readFile(path, 'utf8'));
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS FEATURE', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.canvas.width > 200 || globalThis.__textSpaceError, null, { timeout: 120000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await click('File tab'); await click('Blank document template'); await readyInput('');
  const text = 'First link\nBookmark target';
  await page.keyboard.insertText(text); await until(async () => (await state()).text === text, 'Document typing failed');
  await readyInput(text);

  await check('bookmark range creation through Insert ribbon', async () => {
    await page.keyboard.press('Control+End'); await page.keyboard.press('Home'); await page.keyboard.press('Shift+End');
    await until(async () => (await state()).selection.length === 15, 'Target range selection failed');
    await click('Insert tab'); await click('Bookmark'); await fill('Bookmark name', 'Target'); await click('Add or update');
    await until(async () => !(await state()).dialog, 'Bookmark dialog did not close');
    const document = await saveNative('bookmark-created');
    assert.equal(document.bookmarks.length, 1); assert.equal(document.bookmarks[0].name, 'Target');
    assert.equal(document.bookmarks[0].start, 11); assert.equal(document.bookmarks[0].end, 26);
  });
  await check('internal hyperlink creation and navigation', async () => {
    await readyInput(text); await page.keyboard.press('Control+Home'); await page.keyboard.press('Shift+End');
    await until(async () => (await state()).selection.length === 10, 'Link text selection failed');
    await click('link'); await fill('Address', '#Target'); await click('Insert');
    await until(async () => !(await state()).dialog, 'Hyperlink dialog did not close');
    await readyInput(text); await click('Open Link');
    await until(async () => (await state()).selection.start === 11 && (await state()).selection.end === 26, 'Internal hyperlink did not navigate to bookmark');
  });
  await check('bookmark rename updates existing hyperlinks', async () => {
    await click('Bookmark'); await click('Target'); await fill('Bookmark name', 'Destination'); await click('Rename selected'); await click('Cancel');
    const document = await saveNative('bookmark-renamed');
    assert.equal(document.bookmarks[0].name, 'Destination');
    assert.ok(document.blocks.flatMap(p => p.runs || []).some(r => r.style.hyperlink === '#Destination'));
  });
  await check('bookmark anchors survive insertion before the target', async () => {
    await readyInput(text); await page.keyboard.press('Control+Home'); await page.keyboard.insertText('Intro ');
    await until(async () => (await state()).text === 'Intro ' + text, 'Prefix insertion failed');
    await click('Bookmark'); await click('Go to bookmark Destination');
    await until(async () => (await state()).selection.start === 17 && (await state()).selection.end === 32, 'Bookmark anchor did not move');
    const document = await saveNative('bookmark-moved'); assert.equal(document.bookmarks[0].start, 17); assert.equal(document.bookmarks[0].end, 32);
  });
  await check('bookmark deletion is undoable', async () => {
    await click('Bookmark'); await click('Delete bookmark Destination'); await click('Cancel');
    assert.equal((await saveNative('bookmark-deleted')).bookmarks.length, 0);
    await page.keyboard.press('Control+z');
    const document = await saveNative('bookmark-restored'); assert.equal(document.bookmarks[0].name, 'Destination');
  });
  await check('DOCX and HTML exports retain bookmark targets', async () => {
    await click('File tab'); await click('File Export');
    let pending = page.waitForEvent('download'); await click('Word document'); let download = await pending; await download.saveAs(output + '/bookmarks.docx');
    const docx = await fs.readFile(output + '/bookmarks.docx'); assert.equal(docx.subarray(0, 2).toString(), 'PK');
    pending = page.waitForEvent('download'); await click('Web page'); download = await pending; await download.saveAs(output + '/bookmarks.html');
    const html = await fs.readFile(output + '/bookmarks.html', 'utf8');
    assert.ok(html.includes('id="Destination"')); assert.ok(html.includes('href="#Destination"')); assert.ok(html.includes('Content-Security-Policy'));
    await click('Back to document');
    await page.screenshot({ path: output + '/bookmarks-ui.png' });
  });
  await check('table insertion, cell navigation and structural editing', async () => {
    await readyInput((await state()).text); await page.keyboard.press('Control+End'); await page.keyboard.press('Enter');
    await click('Insert tab'); await click('Table'); await click('Insert 2 by 2 table');
    await until(async () => (await state()).tables === 1, 'Table picker failed');
    await readyInput((await state()).text); await page.keyboard.insertText('Cell one');
    await until(async () => (await state()).text.includes('Cell one'), 'First cell typing failed');
    await page.keyboard.press('Tab'); await readyInput((await state()).text); await page.keyboard.insertText('Cell two');
    await until(async () => (await state()).text.includes('Cell one\nCell two'), 'Tab did not navigate to next cell');
    await click('Table Layout tab'); await click('table-row'); await click('table-column');
    const document = await saveNative('table-and-bookmark'); const table = document.blocks.find(b => b.$type === 'table');
    assert.equal(table.rows.length, 3); assert.ok(table.rows.every(r => r.cells.length === 3));
    assert.equal(document.bookmarks[0].name, 'Destination');
    await page.screenshot({ path: output + '/table-editing.png' });
  });
  assert.deepEqual(report.errors, []);
  await check('corrupt recovery is retained rather than replaced by welcome content', async () => {
    // Fault injection into browser persistence, not an application-editing shortcut.
    await page.waitForTimeout(2000);
    await page.evaluate(async () => {
      await new Promise((resolve, reject) => {
        const request = indexedDB.open('TextSpace', 1);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
          const db = request.result; const tx = db.transaction('workspace', 'readwrite');
          tx.objectStore('workspace').put({ title: 'Recovery fault fixture', document: '{invalid recovery bytes', savedAt: new Date().toISOString() }, 'latest');
          tx.oncomplete = () => { db.close(); resolve(); }; tx.onerror = () => reject(tx.error);
        };
      });
    });
    const faultPage = await context.newPage();
    await faultPage.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
    await faultPage.waitForFunction(() => !!globalThis.__textSpaceError, null, { timeout: 120000 });
    assert.equal(await faultPage.evaluate(() => globalThis.__textSpaceReady ?? false), false);
    assert.equal(await faultPage.evaluate(() => globalThis.TextSpaceHost.loadRecovery()), '{invalid recovery bytes');
    await faultPage.screenshot({ path: output + '/recovery-error.png' }); await faultPage.close();
  });
  report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); report.state = await state().catch(() => null); process.exitCode = 1;
  report.input = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/feature-failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/feature-report.json', JSON.stringify(report, null, 2));
  console.log('FEATURE ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure }));
  await browser.close();
}
