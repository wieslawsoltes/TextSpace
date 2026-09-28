import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/typography';
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
  let control;
  await until(async () => {
    control = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0), name);
    return control && control.x + control.width / 2 >= 0 && control.x + control.width / 2 < 1440 && control.y + control.height / 2 >= 0 && control.y + control.height / 2 < 1000;
  }, 'Visible control not found: ' + name);
  await page.mouse.click(control.x + control.width / 2, control.y + control.height / 2); await page.waitForTimeout(250);
}
async function readyInput(value = null, tag = 'TEXTAREA') {
  await until(() => page.evaluate(({ value, tag }) => {
    const element = document.activeElement;
    return element?.id === 'uno-input' && element.tagName === tag && !element.readOnly && (value === null || element.value === value);
  }, { value, tag }), 'Native input not ready: ' + tag);
}
async function fill(name, value) { await click(name); await readyInput(null, 'INPUT'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value); await readyInput(value, 'INPUT'); }
async function blank() {
  await click('File tab'); await click('Blank document template'); await page.waitForTimeout(300);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => (await state()).text === '', 'Blank template failed'); await readyInput('');
}
async function native(name) {
  await readyInput((await state()).text); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const download = await pending;
  await download.saveAs(output + '/' + name + '.textspace'); return JSON.parse(await fs.readFile(output + '/' + name + '.textspace', 'utf8'));
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS TYPOGRAPHY', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.typography || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await blank();
  await check('right-edge dot leader configured through the Tabs dialog', async () => {
    await page.keyboard.insertText('Chapter\t12'); await until(async () => (await state()).text === 'Chapter\t12', 'Typing failed');
    await click('Layout tab'); await click('tab-stops');
    await fill('Default tab interval (points)', '48'); await fill('Tab stop position (points)', '0');
    await fill('Tab alignment', 'Right'); await fill('Tab leader', 'Dot'); await click('Position from right edge'); await click('Set tab stop');
    await page.screenshot({ path: output + '/tabs-dialog.png' }); await click('Apply');
    await until(async () => (await state()).typography.tabStops.length === 1, 'Tab stop was not applied');
    const typography = (await state()).typography;
    assert.equal(typography.defaultTabStop, 48); assert.equal(typography.tabStops[0].leader, 'Dot'); assert.equal(typography.tabStops[0].relative, true);
    const last = typography.lines[0].chunks.at(-1); assert.ok(Math.abs(last.x + last.width - 540) < 0.01);
    await page.screenshot({ path: output + '/dot-leader-document.png' });
  });
  await check('tab history preserves one atomic edit and changes to default interval', async () => {
    await readyInput((await state()).text); await page.keyboard.press('Control+z');
    await until(async () => (await state()).typography.tabStops.length === 0, 'Undo did not remove stop'); assert.equal((await state()).typography.defaultTabStop, 36);
    await page.keyboard.press('Control+y'); await until(async () => (await state()).typography.tabStops.length === 1, 'Redo did not restore stop');
  });
  await check('right-edge stop follows landscape section width', async () => {
    await click('Orientation'); await click('Landscape');
    await until(async () => (await state()).pageGeometry[0].width === 792, 'Orientation did not change');
    const last = (await state()).typography.lines[0].chunks.at(-1); assert.ok(Math.abs(last.x + last.width - 720) < 0.01);
  });
  await check('paragraph pagination options persist in native document', async () => {
    await click('pagination'); await click('Keep lines together'); await click('Keep with next'); await click('Widow/orphan control'); await click('Apply');
    await until(async () => (await state()).typography.keepLinesTogether, 'Keep lines did not apply');
    const saved = await native('tabs-and-pagination'); const p = saved.blocks.find(b => b.$type === 'paragraph');
    assert.equal(saved.defaultTabStop, 48); assert.equal(p.format.keepLinesTogether, true); assert.equal(p.format.keepWithNext, true); assert.equal(p.format.widowControl, false); assert.equal(p.format.tabStops[0].alignment, 'Right');
  });
  await check('DOCX export and reimport preserves typography semantics', async () => {
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('Word document'); const download = await pending;
    await download.saveAs(output + '/typography.docx'); assert.equal((await fs.readFile(output + '/typography.docx')).subarray(0, 2).toString(), 'PK');
    await click('Back to document');
    // A completed download may leave browser-window focus outside the page even
    // when its shared native textarea is still document.activeElement. Acquire
    // real page/editor focus before testing the keyboard-open command.
    await page.bringToFront();
    const { canvas } = await state();
    await page.mouse.click(canvas.x + canvas.paperLeft + 75 * canvas.scale,
      canvas.y + 18 + 75 * canvas.scale - canvas.scrollY);
    await until(() => page.evaluate(() => document.hasFocus()), 'Browser page did not regain focus');
    await readyInput((await state()).text);
    const chooser = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await (await chooser).setFiles(output + '/typography.docx'); await page.waitForTimeout(500);
    if ((await state()).dialog) await click('Continue without a copy');
    await until(async () => !(await state()).dialog && (await state()).typography.tabStops[0]?.relative === false, 'DOCX tab normalization not imported');
    const t = (await state()).typography; assert.equal(t.tabStops[0].alignment, 'Right'); assert.equal(t.tabStops[0].leader, 'Dot'); assert.equal(t.defaultTabStop, 48); assert.equal(t.keepLinesTogether, true); assert.equal(t.widowControl, false);
  });
  await check('clear all tabs is editable and undoable', async () => {
    await click('Layout tab'); await click('tab-stops'); await click('Clear all tab stops'); await click('Apply');
    await until(async () => (await state()).typography.tabStops.length === 0, 'Clear tabs failed');
    await readyInput((await state()).text); await page.keyboard.press('Control+z'); await until(async () => (await state()).typography.tabStops.length === 1, 'Undo tabs clear failed');
  });
  await check('special-character menu inserts Unicode without visible optional hyphens', async () => {
    await blank(); await page.keyboard.insertText('ab'); await click('Insert tab'); await click('Special Characters'); await click('Optional hyphen');
    await readyInput('ab\u00ad'); await page.keyboard.insertText('cd'); await until(async () => (await state()).text === 'ab\u00adcd', 'Optional hyphen input failed');
    const visible = (await state()).typography.lines.flatMap(l => l.chunks).map(c => c.display).join(''); assert.equal(visible, 'abcd');
    await click('Special Characters'); await click('Nonbreaking space'); await readyInput('ab\u00adcd\u00a0');
    await click('Special Characters'); await click('Nonbreaking hyphen'); await until(async () => (await state()).text === 'ab\u00adcd\u00a0\u2011', 'Nonbreaking character insertion failed');
  });
  await check('live contents have geometric dot leaders without extra text characters', async () => {
    await blank(); await page.keyboard.insertText('Introduction\nBody\nResults'); await readyInput('Introduction\nBody\nResults');
    await page.keyboard.press('Control+Home'); await click('Home tab'); await click('Heading 1 style');
    await readyInput((await state()).text); await page.keyboard.press('Control+End'); await click('Heading 2 style');
    await readyInput((await state()).text); await page.keyboard.press('Control+Home'); await click('References tab'); await click('toc');
    await until(async () => (await state()).fields.length === 4, 'Contents did not generate fields');
    const saved = await native('live-contents-with-leaders'); const entries = saved.blocks.filter(b => /^TOC[1-9]$/.test(b.format?.styleName || ''));
    assert.equal(entries.length, 2); for (const entry of entries) { assert.equal(entry.format.tabStops[0].leader, 'Dot'); assert.equal(entry.format.tabStops[0].alignment, 'Right'); }
    assert.ok((await state()).text.startsWith('Contents\nIntroduction\t1\nResults\t1')); await page.screenshot({ path: output + '/live-contents-leaders.png' });
  });
  await check('typography and field metadata survive local recovery reload', async () => {
    const before = (await state()).text; await page.waitForTimeout(2200); await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => globalThis.__textSpaceState?.typography, null, { timeout: 150000 });
    await until(async () => (await state()).text === before, 'Recovery text mismatch'); assert.equal((await state()).fields.length, 4);
    // Reload restores data, not browser focus. A real pointer action must acquire
    // native input before issuing Ctrl+S; diagnostics remain read-only.
    await until(async () => (await state()).canvas.width > 200, 'Recovered editor is not laid out');
    const { canvas } = await state();
    await page.mouse.click(canvas.x + canvas.paperLeft + 75 * canvas.scale,
      canvas.y + 18 + 75 * canvas.scale - canvas.scrollY);
    await readyInput(before);
    const saved = await native('recovered-contents'); assert.equal(saved.blocks.filter(b => b.format?.tabStops?.some(t => t.leader === 'Dot')).length, 2);
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null); report.input = await page.evaluate(() => ({ focused: document.hasFocus(), visibility: document.visibilityState, active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('TYPOGRAPHY ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure })); await browser.close();
}
