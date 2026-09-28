import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/continuous-sections';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
const page = await context.newPage();
page.on('pageerror', error => report.errors.push(error.message));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(condition, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await condition()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function click(name) {
  let c;
  await until(async () => {
    c = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0), name);
    return c && c.x + c.width / 2 >= 0 && c.x + c.width / 2 < 1440 && c.y + c.height / 2 >= 0 && c.y + c.height / 2 < 1000;
  }, 'Visible control not found: ' + name);
  await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); await page.waitForTimeout(250);
}
async function ready(value = null, tag = 'TEXTAREA') {
  await until(() => page.evaluate(({ value, tag }) => {
    const e = document.activeElement;
    return e?.id === 'uno-input' && e.tagName === tag && !e.readOnly && (value === null || e.value === value);
  }, { value, tag }), 'Native input not ready: ' + tag);
}
async function fill(name, value) { await click(name); await ready(null, 'INPUT'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value); await ready(value, 'INPUT'); }
async function blank() {
  await click('File tab'); await click('Blank document template'); await page.waitForTimeout(300);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => (await state()).text === '', 'Blank template failed'); await ready('');
}
async function section(kind) { await click('Layout tab'); await click('Section Break'); await click(kind + ' Section'); await ready((await state()).text); }
async function columns(name) { await click('Layout tab'); await click('Columns'); await click(name); await ready((await state()).text); }
async function insertField(code) {
  await click('Insert tab'); await click('insert-field'); await fill('Field code', code); await click('Insert');
  await until(async () => !(await state()).dialog, 'Field dialog did not close'); await ready((await state()).text);
}
async function saveNative(name) {
  await ready((await state()).text); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download = await pending; await download.saveAs(output + '/' + name + '.textspace');
  return JSON.parse(await fs.readFile(output + '/' + name + '.textspace', 'utf8'));
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS CONTINUOUS', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.pageGeometry?.[0]?.regions || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await blank();
  await check('continuous section shares physical page and changes column geometry', async () => {
    await page.keyboard.insertText('Introduction'); await ready('Introduction');
    await section('Continuous'); await page.keyboard.insertText('Left column');
    await until(async () => (await state()).text.endsWith('Left column'), 'New region typing failed'); await columns('Two');
    const s = await state(); assert.equal(s.sections, 2); assert.equal(s.pages, 1);
    assert.equal(s.pageGeometry[0].regions[1].columns, 2); assert.equal(s.currentSection, 1);
    assert.ok(Math.abs(s.ruler.width - 222) < 0.01);
  });
  await check('next-column section uses the second column and correct ruler', async () => {
    await section('Next Column'); await page.keyboard.insertText('Right column ');
    await until(async () => (await state()).text.endsWith('Right column '), 'Next-column input failed');
    const s = await state(); assert.equal(s.pages, 1); assert.equal(s.sections, 3);
    assert.equal(s.pageGeometry[0].regions[2].firstColumn, 1);
    assert.ok(Math.abs(s.ruler.left - 318) < 0.01); assert.ok(Math.abs(s.ruler.width - 222) < 0.01);
  });
  await check('same-page SECTION and PAGE fields evaluate from the containing region', async () => {
    await insertField('SECTION'); await until(async () => (await state()).fields.at(-1).value === '3', 'SECTION uses page owner instead of region');
    await page.keyboard.press('Control+End'); await page.keyboard.insertText(' / '); await insertField('PAGE');
    await until(async () => (await state()).fields.at(-1).value === '1', 'PAGE on shared region is incorrect');
  });
  await check('return to full-width flow below both earlier columns', async () => {
    await page.keyboard.press('Control+End'); await section('Continuous'); await columns('One'); await page.keyboard.insertText('Summary');
    await until(async () => (await state()).text.endsWith('Summary'), 'Summary input failed');
    const s = await state(); assert.equal(s.pages, 1); assert.equal(s.sections, 4);
    const regions = s.pageGeometry[0].regions; assert.equal(regions[3].columns, 1);
    assert.ok(regions[3].top >= Math.max(regions[1].bottom, regions[2].bottom) - 0.01);
    await page.screenshot({ path: output + '/same-page-sections.png' });
  });
  await check('section start conversion and settings are atomically undoable', async () => {
    await click('Layout tab'); await click('section-settings'); await fill('Section start', 'NextPage'); await click('Apply');
    await until(async () => (await state()).pages === 2, 'Next-page conversion did not repaginate');
    await ready((await state()).text); await page.keyboard.press('Control+z');
    await until(async () => (await state()).pages === 1 && (await state()).sections === 4, 'Undo did not restore continuous flow');
    await page.keyboard.press('Control+y'); await until(async () => (await state()).pages === 2, 'Redo did not restore page break');
    await page.keyboard.press('Control+z'); await until(async () => (await state()).pages === 1, 'Second undo failed');
  });
  await check('selection and scrolling reuse captured text statistics', async () => {
    await ready((await state()).text); await page.waitForTimeout(400);
    const count = (await state()).textSnapshotBuilds;
    await page.keyboard.press('Control+Home'); await page.keyboard.press('Control+Shift+End');
    await page.waitForTimeout(400); assert.equal((await state()).textSnapshotBuilds, count);
    assert.match((await state()).displayedWordCount, /of .*words/);
    await page.keyboard.press('Control+End'); await page.mouse.move(850, 600); await page.mouse.wheel(0, 100); await page.waitForTimeout(400);
    assert.equal((await state()).textSnapshotBuilds, count);
    const saved = await saveNative('same-page');
    assert.deepEqual(saved.blocks.filter(b => b.$type === 'sectionBreak').map(b => b.kind), ['Continuous', 'NextColumn', 'Continuous']);
  });
  await check('DOCX reimport preserves same-page and next-column semantics', async () => {
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('Word document');
    const download = await pending; await download.saveAs(output + '/same-page.docx');
    await click('Back to document'); await ready((await state()).text);
    const chooser = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await (await chooser).setFiles(output + '/same-page.docx');
    await page.waitForTimeout(500); if ((await state()).dialog) await click('Continue without a copy');
    await until(async () => !(await state()).dialog && (await state()).sections === 4, 'DOCX import did not finish');
    assert.equal((await state()).pages, 1); assert.equal((await state()).fields.length, 2);
    await ready((await state()).text); const saved = await saveNative('imported');
    assert.deepEqual(saved.blocks.filter(b => b.$type === 'sectionBreak').map(b => b.kind), ['Continuous', 'NextColumn', 'Continuous']);
  });
  await check('paragraph-only two-column band balances before the continuous break', async () => {
    await blank(); const text = Array.from({ length: 8 }, (_, i) => 'Paragraph ' + (i + 1)).join('\n');
    await page.keyboard.insertText(text); await ready(text); await columns('Two');
    await section('Continuous'); await columns('One'); await page.keyboard.insertText('Full-width conclusion');
    await until(async () => (await state()).text.endsWith('Full-width conclusion'), 'Conclusion was not inserted');
    const s = await state(); assert.equal(s.pages, 1); assert.equal(s.pageGeometry[0].regions[0].balanced, true);
    assert.equal(s.pageGeometry[0].regions[0].lastColumn, 1); assert.ok(s.pageGeometry[0].regions[1].top < 230);
    await page.screenshot({ path: output + '/balanced-columns.png' });
  });
  await check('recovery restores continuous geometry and acquires real input focus', async () => {
    const before = (await state()).text; await page.waitForTimeout(2200); await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => globalThis.__textSpaceState?.pageGeometry?.[0]?.regions, null, { timeout: 150000 });
    await until(async () => (await state()).text === before, 'Recovery text mismatch');
    const s = await state(); assert.equal(s.pages, 1); assert.equal(s.pageGeometry[0].regions[0].balanced, true);
    await page.mouse.click(s.canvas.x + s.canvas.paperLeft + 75 * s.canvas.scale, s.canvas.y + 18 + 75 * s.canvas.scale - s.canvas.scrollY);
    await ready(before); const saved = await saveNative('recovered');
    assert.equal(saved.blocks.find(b => b.$type === 'sectionBreak').kind, 'Continuous');
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null);
  report.input = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('CONTINUOUS ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure }));
  await browser.close();
}
