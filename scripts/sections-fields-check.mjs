import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/sections-fields';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
const page = await context.newPage();
page.on('pageerror', e => report.errors.push(e.message));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(condition, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await condition()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function find(name) {
  return page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0), name);
}
async function click(name) {
  let c;
  await until(async () => {
    c = await find(name);
    return c && c.x + c.width / 2 >= 0 && c.x + c.width / 2 < 1440 && c.y + c.height / 2 >= 0 && c.y + c.height / 2 < 1000;
  }, 'Control not visible: ' + name);
  await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); await page.waitForTimeout(250);
}
async function readyInput(value = null, tag = 'TEXTAREA') {
  await until(() => page.evaluate(({ value, tag }) => {
    const e = document.activeElement;
    return e?.id === 'uno-input' && e.tagName === tag && !e.readOnly && (value === null || e.value === value);
  }, { value, tag }), 'Native input not ready: ' + tag);
}
async function fill(name, text, tag = 'INPUT') {
  await click(name); await readyInput(null, tag); await page.keyboard.press('Control+a'); await page.keyboard.insertText(text); await readyInput(text, tag);
}
async function blank() {
  await click('File tab'); await click('Blank document template');
  await page.waitForTimeout(300);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => (await state()).text === '', 'New blank document failed'); await readyInput('');
}
async function rename(title) {
  await click('File tab'); await click('File Info'); await click('Rename document');
  await fill('Document name', title); await click('Rename'); await click('Back to document');
  await until(async () => (await state()).title === title, 'Rename failed'); await readyInput((await state()).text);
}
async function field(code) {
  await click('Insert tab'); await click('insert-field'); await fill('Field code', code); await click('Insert');
  await until(async () => !(await state()).dialog, 'Field dialog did not close'); await readyInput((await state()).text);
}
async function saveNative(name) {
  await readyInput((await state()).text); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download = await pending; const path = output + '/' + name + '.textspace'; await download.saveAs(path);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS SECTIONS/FIELDS', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.canvas.width > 200 || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await blank();
  await check('live metadata field insertion and F9 update', async () => {
    await field('TITLE'); assert.equal((await state()).fields[0].value, (await state()).title);
    await rename('Section study'); await page.keyboard.press('F9');
    await until(async () => (await state()).fields[0].value === 'Section study', 'TITLE did not refresh');
  });
  await check('field locking and unlocking through management UI', async () => {
    await click('Insert tab'); await click('manage-fields'); await click('Lock'); await click('Close');
    await rename('Updated study'); await page.keyboard.press('F9'); await page.waitForTimeout(300);
    assert.equal((await state()).fields[0].value, 'Section study'); assert.equal((await state()).fields[0].locked, true);
    await click('manage-fields'); await click('Unlock'); await click('Close'); await readyInput((await state()).text); await page.keyboard.press('F9');
    await until(async () => (await state()).fields[0].value === 'Updated study', 'Unlocked field did not refresh');
  });
  await check('field unlink and document undo restore field metadata', async () => {
    await click('manage-fields'); await click('Unlink'); await click('Close'); await readyInput((await state()).text);
    assert.equal((await state()).fields.length, 0); assert.equal((await state()).text, 'Updated study');
    await page.keyboard.press('Control+z'); await until(async () => (await state()).fields.length === 1, 'Undo did not restore field');
  });
  await check('odd-page section break inserts a physical parity page', async () => {
    await readyInput((await state()).text); await page.keyboard.press('Control+End'); await page.keyboard.press('Enter');
    await click('Layout tab'); await click('Section Break'); await click('Odd Page Section');
    await until(async () => (await state()).sections === 2, 'Section break missing');
    assert.equal((await state()).pages, 3); assert.equal((await state()).pageGeometry[1].blank, true);
  });
  await check('mixed portrait and landscape paper in one document', async () => {
    await click('Orientation'); await click('Landscape');
    await until(async () => (await state()).pageGeometry.at(-1).width === 792, 'Landscape section not laid out');
    const s = await state(); assert.equal(s.pageGeometry[0].width, 612); assert.equal(s.pageGeometry.at(-1).height, 612);
    await readyInput(s.text); await page.keyboard.insertText('Landscape content ');
    await until(async () => (await state()).text.endsWith('Landscape content '), 'Typing on mixed-size page failed');
    await page.screenshot({ path: output + '/mixed-paper.png' });
  });
  await check('section page-number restart and Roman PAGE field', async () => {
    await click('section-settings'); await fill('Start page numbering at (blank to continue)', '10');
    await fill('Page number format', 'LowerRoman'); await click('Apply');
    await until(async () => (await state()).pageGeometry.at(-1).number === 10, 'Restart not applied');
    await field('PAGE'); await until(async () => (await state()).fields.at(-1).value === 'x', 'Roman PAGE result incorrect');
  });
  await check('section-scoped footer and header inheritance metadata', async () => {
    await click('Insert tab'); await click('footer'); await fill('Footer text', 'Section {SECTION}, page {PAGE} of {SECTIONPAGES}'); await click('Apply');
    const document = await saveNative('mixed-section-fields');
    const section = document.blocks.find(b => b.$type === 'sectionBreak');
    assert.equal(section.section.footer, 'Section {SECTION}, page {PAGE} of {SECTIONPAGES}');
    assert.equal(section.section.header, null); assert.equal(section.section.options.pageNumberStart, 10);
    assert.equal(document.fields.length, 2);
  });
  await check('DOCX, vector PDF and mixed-size PNG export', async () => {
    await click('File tab'); await click('File Export');
    let pending = page.waitForEvent('download'); await click('Word document'); let download = await pending; await download.saveAs(output + '/mixed.docx');
    assert.equal((await fs.readFile(output + '/mixed.docx')).subarray(0, 2).toString(), 'PK');
    pending = page.waitForEvent('download'); await click('PDF document'); download = await pending; await download.saveAs(output + '/mixed.pdf');
    const pdf = await fs.readFile(output + '/mixed.pdf', 'latin1'); assert.ok(pdf.startsWith('%PDF')); assert.ok(pdf.includes('/MediaBox [0 0 792 612]'));
    // The last export card is below the fold on a compact-height window: scroll using the actual pointer wheel.
    await page.mouse.move(800, 750); await page.mouse.wheel(0, 650); await page.waitForTimeout(350);
    pending = page.waitForEvent('download'); await click('Page image'); download = await pending; await download.saveAs(output + '/landscape.png');
    const png = await fs.readFile(output + '/landscape.png'); assert.equal(png.readUInt32BE(16), 1584); assert.equal(png.readUInt32BE(20), 1224);
    await click('Back to document');
  });
  await check('DOCX reimport preserves sections and live instructions', async () => {
    await readyInput((await state()).text);
    const chooser = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
    await (await chooser).setFiles(output + '/mixed.docx'); await page.waitForTimeout(500);
    if ((await state()).dialog) await click('Continue without a copy');
    await until(async () => (await state()).sections === 2 && (await state()).fields.length === 2 && !(await state()).dialog, 'DOCX reimport failed');
    assert.equal((await state()).pageGeometry.at(-1).width, 792);
    assert.deepEqual((await state()).fields.map(f => f.code), ['TITLE', 'PAGE']);
  });
  await check('multi-section fields survive local recovery reload', async () => {
    const before = (await state()).text; await page.waitForTimeout(2200);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => globalThis.__textSpaceState?.canvas.width > 200, null, { timeout: 150000 });
    await until(async () => (await state()).text === before, 'Recovery text mismatch'); assert.equal((await state()).fields.length, 2); assert.equal((await state()).sections, 2);
  });
  await check('live table of contents creation from heading styles', async () => {
    await blank(); await page.keyboard.insertText('Introduction\nBody\nResults');
    await until(async () => (await state()).text === 'Introduction\nBody\nResults', 'Heading text input failed');
    await page.keyboard.press('Control+Home'); await click('Home tab'); await click('Heading 1 style');
    await readyInput((await state()).text); await page.keyboard.press('Control+End'); await click('Heading 2 style');
    await readyInput((await state()).text); await page.keyboard.press('Control+Home');
    await click('References tab'); await click('toc');
    await until(async () => (await state()).fields.length === 4, 'Contents live references missing');
    assert.ok((await state()).text.startsWith('Contents\nIntroduction\t1\nResults\t1'));
    await page.screenshot({ path: output + '/live-contents.png' });
    await click('update-toc'); await until(async () => (await state()).fields.length === 4, 'Contents rebuild duplicated fields');
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null);
  report.input = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('SECTIONS/FIELDS ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure }));
  await browser.close();
}
