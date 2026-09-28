import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/tables';
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
async function click(name) {
  let c;
  await until(async () => {
    c = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0), name);
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
async function fill(name, value) { await click(name); await readyInput(null, 'INPUT'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value); await readyInput(value, 'INPUT'); }
async function blank() {
  await click('File tab'); await click('Blank document template'); await page.waitForTimeout(300);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => (await state()).text === '', 'Blank template failed'); await readyInput('');
}
async function table(rows, columns, nested = false) {
  await click(nested ? 'Table Layout tab' : 'Insert tab');
  await click(nested ? 'Nested Table' : 'Table'); await click(`Insert ${rows} by ${columns} table`);
  await readyInput((await state()).text);
}
async function cell(row, column, depth = 0) {
  let point;
  await until(async () => {
    const s = await state(); const t = s.tableModels?.find(t => t.depth === depth);
    const c = t?.regions.find(c => row >= c.row && row < c.row + c.rowSpan && column >= c.column && column < c.column + c.columnSpan);
    if (c?.page === undefined) return false;
    const p = s.pageGeometry[c.page], v = s.canvas;
    point = { x: v.x + v.paperLeft + (p.left + c.x + 2) * v.scale,
      y: v.y + 18 + (p.top + c.y + c.height / 2) * v.scale - v.scrollY };
    return point.y >= v.y && point.y < v.y + v.height && point.x >= v.x && point.x < v.x + v.width;
  }, 'Cell is not visible: ' + [depth, row, column]);
  await page.mouse.click(point.x, point.y); await readyInput((await state()).text);
}
async function native(name) {
  await readyInput((await state()).text); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download = await pending; const path = output + '/' + name + '.textspace'; await download.saveAs(path); return JSON.parse(await fs.readFile(path, 'utf8'));
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS TABLES', name); }
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.tableModels || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await blank();
  await check('real table editing and row-major navigation', async () => {
    await table(3, 3);
    for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) {
      await page.keyboard.insertText(`R${r}C${c}`);
      if (r !== 2 || c !== 2) { await page.keyboard.press('Tab'); await readyInput(); }
    }
    await until(async () => (await state()).tableModels[0].regions.every(c => c.text === `R${c.row}C${c.column}`), 'Cell text missing');
    assert.equal((await state()).tableModels[0].regions.length, 9);
  });
  await check('rectangular merged cells preserve all text without covered-slot duplicates', async () => {
    await cell(0, 0); await click('Table Layout tab'); await click('merge-cells');
    await fill('Start row', '1'); await fill('Start column', '1'); await fill('Rows to merge', '2'); await fill('Columns to merge', '2'); await click('Merge');
    await until(async () => (await state()).tableModels[0].regions.length === 6, 'Rectangle not merged');
    const d = await native('merged'); const t = d.blocks.find(b => b.$type === 'table');
    assert.equal(t.rows[0].cells[0].rowSpan, 2); assert.equal(t.rows[0].cells[0].columnSpan, 2);
    assert.deepEqual(t.rows[0].cells[1].blocks, []); assert.deepEqual(t.rows[1].cells[0].blocks, []); assert.deepEqual(t.rows[1].cells[1].blocks, []);
    for (let r = 0; r < 3; r++) for (let c = 0; c < 3; c++) assert.equal((await state()).text.split(`R${r}C${c}`).length - 1, 1);
    await page.screenshot({ path: output + '/merged-cells.png' });
  });
  await check('Tab skips logical slots covered by the active merged cell', async () => {
    await cell(0, 0); await page.keyboard.press('Tab');
    await until(async () => (await state()).selection.start === (await state()).text.indexOf('R0C2'), 'Tab did not skip the covered slots');
  });
  await check('vertical alignment and row pagination options are undoable', async () => {
    await cell(0, 2); await click('Table Layout tab'); await click('Vertical Align'); await click('Align Bottom');
    await until(async () => (await state()).tableModels[0].regions.find(c => c.row === 0 && c.column === 2).alignment === 'Bottom', 'Alignment did not apply');
    await click('row-options'); await fill('Minimum row height (points)', '100'); await click('Allow row to split across pages'); await click('Apply');
    const d = await native('row-options'); const t = d.blocks.find(b => b.$type === 'table'); assert.equal(t.rows[0].minimumHeight, 100); assert.equal(t.rows[0].allowSplit, false);
    await page.keyboard.press('Control+z'); const previous = await native('row-options-undo'); assert.equal(previous.blocks.find(b => b.$type === 'table').rows[0].minimumHeight, 0);
  });
  await check('splitting a merged cell and undo preserve content and spans', async () => {
    await cell(0, 0); await click('split-cell'); await until(async () => (await state()).tableModels[0].regions.length === 9, 'Split failed');
    const d = await native('split'); const t = d.blocks.find(b => b.$type === 'table'); assert.equal(t.rows[0].cells[0].blocks.filter(b => b.$type === 'paragraph').length, 4);
    await page.keyboard.press('Control+z'); await until(async () => (await state()).tableModels[0].regions.length === 6, 'Undo split failed');
  });
  await check('nested table insertion measures inner content inside a merged cell', async () => {
    await cell(0, 0); await table(2, 2, true); await page.keyboard.insertText('Inner A'); await page.keyboard.press('Tab'); await page.keyboard.insertText('Inner B');
    await until(async () => (await state()).tableModels.length === 2 && (await state()).text.includes('Inner B'), 'Nested table not editable');
    const s = await state(); const outer = s.tableModels.find(t => t.depth === 0).regions[0]; const inner = s.tableModels.find(t => t.depth === 1);
    assert.equal(inner.regions.length, 4); assert.ok(inner.regions[0].x > outer.x); assert.ok(inner.regions[0].y > outer.y);
    await page.screenshot({ path: output + '/nested-merged-table.png' });
    await native('nested-merged');
  });
  await check('DOCX and HTML exports preserve merged and nested table structure', async () => {
    await click('File tab'); await click('File Export');
    let pending = page.waitForEvent('download'); await click('Word document'); let download = await pending; await download.saveAs(output + '/tables.docx');
    assert.equal((await fs.readFile(output + '/tables.docx')).subarray(0, 2).toString(), 'PK');
    pending = page.waitForEvent('download'); await click('Web page'); download = await pending; await download.saveAs(output + '/tables.html');
    const html = await fs.readFile(output + '/tables.html', 'utf8'); assert.ok(html.includes('rowspan="2"')); assert.ok(html.includes('colspan="2"')); assert.equal((html.match(/<table/g) || []).length, 2);
    await click('Back to document'); await readyInput((await state()).text);
    const chooser = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await (await chooser).setFiles(output + '/tables.docx'); await page.waitForTimeout(500);
    if ((await state()).dialog) await click('Continue without a copy');
    await until(async () => !(await state()).dialog && (await state()).tableModels.length === 2, 'Table DOCX reimport failed');
    assert.equal((await state()).tableModels[0].regions[0].rowSpan, 2); assert.equal((await state()).tableModels[0].regions[0].columnSpan, 2); assert.ok((await state()).text.includes('Inner A'));
  });
  await check('nested merged tables survive local recovery', async () => {
    const before = (await state()).text; await page.waitForTimeout(2200); await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => globalThis.__textSpaceState?.tableModels?.length === 2, null, { timeout: 150000 });
    await until(async () => (await state()).text === before, 'Table recovery text mismatch');
    await cell(0, 0); const d = await native('recovered-tables'); const t = d.blocks.find(b => b.$type === 'table'); assert.equal(t.rows[0].cells[0].columnSpan, 2);
  });
  await check('long table rows paginate with presentation-only repeated headers', async () => {
    await blank(); await table(2, 2); await page.keyboard.insertText('Header A'); await page.keyboard.press('Tab'); await page.keyboard.insertText('Header B'); await page.keyboard.press('Tab');
    const body = Array.from({ length: 85 }, (_, i) => `Body line ${i}`).join('\n'); await page.keyboard.insertText(body);
    await until(async () => (await state()).pages > 1 && (await state()).repeatedHeaderLines > 0, 'Header repetition did not paginate');
    assert.equal((await state()).text.split('Header A').length - 1, 1);
    await page.screenshot({ path: output + '/repeated-header.png' });
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('PDF document'); const download = await pending;
    await download.saveAs(output + '/paginated-tables.pdf'); assert.ok((await fs.readFile(output + '/paginated-tables.pdf', 'latin1')).startsWith('%PDF'));
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null); report.input = await page.evaluate(() => ({ active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('TABLE ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure })); await browser.close();
}
