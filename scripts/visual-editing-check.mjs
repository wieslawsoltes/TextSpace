import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { deflateSync } from 'node:zlib';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/visual-editing';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [], console: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const context = await browser.newContext({ viewport: { width: 1600, height: 1100 }, acceptDownloads: true });
const page = await context.newPage();
page.on('pageerror', error => report.errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') report.console.push(message.text()); });
// Register interception before navigation. The listener observes actual chooser
// events only; every picker is opened by a real ribbon or keyboard action.
let chooserCount = 0;
page.on('filechooser', () => chooserCount++);
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(test, message, timeout = 30000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { if (await test()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function control(name) {
  let found;
  await until(async () => {
    found = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 >= 0 && c.y + c.height / 2 >= 0 && c.x + c.width / 2 < innerWidth && c.y + c.height / 2 < innerHeight), name);
    return !!found;
  }, 'Missing visible control: ' + name);
  return found;
}
async function click(name) { const c = await control(name); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); await page.waitForTimeout(200); }
async function nativeReady(readOnly = false) {
  await until(() => page.evaluate(readOnly => document.hasFocus() && document.activeElement?.id === 'uno-input' && (readOnly === null || document.activeElement.readOnly === readOnly), readOnly), 'Native input not focused');
}
async function fill(name, text) { await click(name); await nativeReady(); await page.keyboard.press('Control+a'); await page.keyboard.insertText(text); }
async function blank() {
  await click('File tab'); await click('Blank document template'); await page.waitForTimeout(300);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => (await state()).text === '' && !(await state()).dialog, 'Blank document not ready');
  await nativeReady();
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS VISUAL', name); }
async function object(id) { return (await state()).visuals.objects.find(o => o.id === id); }
async function selectObject(id) {
  const item = await object(id); assert.ok(item, 'Object not present');
  await page.mouse.click(item.x + item.width / 2, item.y + item.height / 2);
  await until(async () => (await state()).visuals.selected === id, 'Object was not selected');
  // Observe native focus separately from managed TextBox state, then exercise
  // actual input to prove that object selection cannot mutate body text.
  await nativeReady(null);
  await until(async () => (await state()).visuals.bodyReadOnly, 'Managed body input must be read-only for object selection');
  const before = await state();
  await page.keyboard.insertText('BODY INPUT MUST BE REJECTED');
  await page.waitForTimeout(200);
  assert.equal((await state()).text, before.text, 'Object-mode typing changed body text');
  assert.equal((await state()).revision, before.revision, 'Rejected typing changed document history');
  await until(() => page.evaluate(text => document.activeElement?.value === text, before.text), 'Rejected input was not restored in the native textarea');
}
async function drag(from, to) {
  await page.mouse.move(from.x, from.y); await page.mouse.down();
  await page.mouse.move(to.x, to.y, { steps: 8 });
  await until(async () => (await state()).visuals.gesture, 'Gesture preview did not start');
}
async function finishDrag() { await page.mouse.up(); await until(async () => !(await state()).visuals.gesture, 'Gesture did not complete'); }
async function saveNative(name) {
  await page.bringToFront(); await click('File tab'); await click('File Save As');
  const pending = page.waitForEvent('download'); await click('TextSpace document'); const file = await pending;
  await file.saveAs(`${output}/${name}.textspace`); await click('Back to document');
  return JSON.parse(await fs.readFile(`${output}/${name}.textspace`, 'utf8'));
}
async function openFile(path) {
  await click('File tab'); await click('File Open');
  const previous = chooserCount; const pending = page.waitForEvent('filechooser'); await click('Browse this device');
  await (await pending).setFiles(path); assert.equal(chooserCount, previous + 1);
  await page.waitForTimeout(400); if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => !(await state()).dialog && !(await state()).visuals.editor, 'Open document did not complete');
}
function fixturePng() {
  const crc = buffer => { let c = 0xffffffff; for (const b of buffer) { c ^= b; for (let i = 0; i < 8; i++) c = c & 1 ? (c >>> 1) ^ 0xedb88320 : c >>> 1; } return (c ^ 0xffffffff) >>> 0; };
  const chunk = (name, data) => { const body = Buffer.concat([Buffer.from(name), data]); const size = Buffer.alloc(4); size.writeUInt32BE(data.length); const sum = Buffer.alloc(4); sum.writeUInt32BE(crc(body)); return Buffer.concat([size, body, sum]); };
  const width = 128, height = 96, pixels = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const offset = y * (width * 4 + 1) + 1 + x * 4; pixels[offset] = x * 2; pixels[offset + 1] = y * 2; pixels[offset + 2] = 120; pixels[offset + 3] = 255; }
  const header = Buffer.alloc(13); header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', deflateSync(pixels)), chunk('IEND', Buffer.alloc(0))]);
}
let shapeId, equationId, pictureId;
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.visuals || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await blank();
  await check('insert retained shape with contextual controls and eight handles', async () => {
    await click('Insert tab'); await click('Shapes'); await click('Rectangle');
    await until(async () => (await state()).visuals.objects.length === 1, 'Shape insertion failed');
    const current = await state(); shapeId = current.visuals.objects[0].id;
    assert.equal(current.visuals.selected, shapeId); assert.equal(current.selectedTab, 'Shape Format');
    assert.equal(current.visuals.objects[0].handles.filter(h => h.handle !== 'Rotate').length, 8);
  });
  await check('move preview is detached and commits one undoable transaction', async () => {
    const before = await object(shapeId); const revision = (await state()).revision;
    await drag({ x: before.x + before.width / 2, y: before.y + before.height / 2 }, { x: before.x + before.width / 2 + 45, y: before.y + before.height / 2 + 24 });
    assert.equal((await state()).revision, revision); assert.equal((await object(shapeId)).x, before.x);
    await page.screenshot({ path: output + '/move-preview.png' }); await finishDrag();
    await until(async () => (await state()).revision === revision + 1, 'Move did not commit once');
    assert.ok((await object(shapeId)).x > before.x + 15);
    await page.keyboard.press('Control+z'); await until(async () => Math.abs((await object(shapeId)).x - before.x) < 0.1, 'Move undo failed');
    await page.keyboard.press('Control+y'); await until(async () => (await object(shapeId)).x > before.x + 15, 'Move redo failed');
  });
  await check('corner resizing and rotation use pointer handles', async () => {
    let item = await object(shapeId); let handle = item.handles.find(h => h.handle === 'SouthEast'); const width = item.width;
    await drag(handle, { x: handle.x + 36, y: handle.y + 20 }); await finishDrag();
    await until(async () => (await object(shapeId)).width > width + 15, 'Resize did not change shape geometry');
    item = await object(shapeId); handle = item.handles.find(h => h.handle === 'Rotate');
    await drag(handle, { x: item.x + item.width + 30, y: item.y + item.height / 2 }); await finishDrag();
    await until(async () => Math.abs((await object(shapeId)).rotation - 90) < 1, 'Rotation did not reach 90 degrees');
    await page.screenshot({ path: output + '/rotated-shape.png' });
    await page.keyboard.press('Control+z'); await until(async () => Math.abs((await object(shapeId)).rotation) < 0.1, 'Rotation undo failed');
  });
  await check('Escape cancels a draft without changing the document', async () => {
    const item = await object(shapeId); const revision = (await state()).revision;
    await drag({ x: item.x + item.width / 2, y: item.y + item.height / 2 }, { x: item.x + item.width / 2 + 60, y: item.y + item.height / 2 });
    await page.keyboard.press('Escape'); await page.mouse.up();
    await until(async () => !(await state()).visuals.gesture, 'Escape did not cancel');
    assert.equal((await state()).revision, revision); assert.equal((await object(shapeId)).x, item.x);
  });
  await check('shape text editing is isolated and applies atomically', async () => {
    const body = (await state()).text; const revision = (await state()).revision;
    await click('edit-object'); await fill('Shape text', 'Editable vector text');
    assert.equal((await state()).revision, revision); assert.equal((await state()).text, body);
    await click('Apply shape text'); await until(async () => (await object(shapeId)).text === 'Editable vector text', 'Shape text was not applied');
    assert.equal((await state()).text, body);
    await click('edit-object'); await fill('Shape text', 'Discard this draft'); await page.keyboard.press('Escape');
    await until(async () => !(await state()).visuals.editor, 'Shape cancellation failed');
    assert.equal((await object(shapeId)).text, 'Editable vector text');
  });
  await check('equation slots edit fractions without changing body text', async () => {
    await page.keyboard.press('Escape'); await click('Insert tab'); await click('insert-equation');
    await until(async () => (await state()).visuals.editor && (await state()).visuals.slots.length > 0, 'Equation editor not opened');
    equationId = (await state()).visuals.selected;
    await click('Equation Fraction');
    await until(async () => (await state()).visuals.slots.length === 2, 'Fraction slots not created');
    const before = await state();
    async function writeSlot(fragment, value) {
      const slot = (await state()).visuals.slots.find(s => s.role.toLowerCase().includes(fragment)); assert.ok(slot, 'Slot missing: ' + fragment);
      await page.mouse.click(slot.x + slot.width / 2, slot.y + slot.height / 2);
      await nativeReady(); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value);
      await until(async () => (await state()).visuals.slots.some(s => s.id === slot.id && s.text === value), 'Equation slot text failed');
    }
    await writeSlot('numerator', 'a+b'); await writeSlot('denominator', 'c');
    assert.equal((await state()).revision, before.revision); assert.equal((await state()).text, before.text);
    await page.screenshot({ path: output + '/fraction-editor.png' }); await click('Apply equation');
    await until(async () => !(await state()).visuals.editor && (await object(equationId)).text === '(a+b)/(c)', 'Equation commit failed');
    await page.keyboard.press('Control+z'); await until(async () => (await object(equationId)).text === '', 'Equation undo failed');
    await page.keyboard.press('Control+y'); await until(async () => (await object(equationId)).text === '(a+b)/(c)', 'Equation redo failed');
  });
  await check('equation structure cancellation leaves the original tree', async () => {
    await click('edit-object'); await click('Equation Radical'); await page.keyboard.press('Escape');
    await until(async () => !(await state()).visuals.editor, 'Equation editor did not cancel');
    assert.equal((await object(equationId)).text, '(a+b)/(c)');
  });
  await check('matrix row and column edits have independent local history', async () => {
    const revision = (await state()).revision;
    await click('edit-object'); await click('Equation Matrix');
    await until(async () => (await state()).visuals.slots.length === 5, 'Matrix template was not inserted into the active fraction slot');
    await click('Matrix layout'); await click('Insert Matrix Column Right');
    await until(async () => (await state()).visuals.slots.length === 7, 'Matrix column insertion failed');
    await click('Equation Undo'); await until(async () => (await state()).visuals.slots.length === 5, 'Local matrix undo failed');
    await click('Equation Redo'); await until(async () => (await state()).visuals.slots.length === 7, 'Local matrix redo failed');
    await click('Matrix layout'); await click('Insert Matrix Row Below');
    await until(async () => (await state()).visuals.slots.length === 10, 'Matrix row insertion failed');
    await page.screenshot({ path: output + '/matrix-editor.png' });
    await click('Equation structure operations'); await click('Convert Structure to Linear Text');
    await until(async () => (await state()).visuals.slots.length === 2, 'Linear conversion lost surrounding fraction slots');
    await click('Equation Undo'); await until(async () => (await state()).visuals.slots.length === 10, 'Linear conversion undo failed');
    assert.equal((await state()).revision, revision, 'Local equation edits changed document history before Apply');
    await click('Cancel equation'); await until(async () => !(await state()).visuals.editor, 'Matrix draft did not cancel');
    assert.equal((await object(equationId)).text, '(a+b)/(c)');
  });
  await check('picture crop is nondestructive and resettable', async () => {
    await page.keyboard.press('Escape'); await click('Insert tab');
    const path = output + '/source.png'; await fs.writeFile(path, fixturePng());
    const chooser = page.waitForEvent('filechooser'); await click('insert-picture'); await (await chooser).setFiles(path);
    await until(async () => (await state()).visuals.objects.some(o => o.kind === 'Picture'), 'Picture not inserted');
    pictureId = (await state()).visuals.objects.find(o => o.kind === 'Picture').id; await selectObject(pictureId); await click('crop-picture');
    const item = await object(pictureId); const west = item.handles.find(h => h.handle === 'West');
    await drag(west, { x: west.x + 15, y: west.y }); await finishDrag();
    const cropped = await saveNative('cropped'); const picture = cropped.blocks.find(b => b.$type === 'image');
    assert.ok(picture.crop.left > 0); assert.equal(Buffer.from(picture.data, 'base64').compare(await fs.readFile(path)), 0);
    await selectObject(pictureId); await click('reset-crop');
    const reset = await saveNative('reset-crop'); assert.equal(reset.blocks.find(b => b.$type === 'image').crop.left, 0);
  });
  await check('visual objects survive native and editable DOCX round trips', async () => {
    const native = await saveNative('objects');
    assert.equal(native.blocks.filter(b => ['shape', 'equation', 'image'].includes(b.$type)).length, 3);
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('Word document'); const file = await pending;
    const path = output + '/objects.docx'; await file.saveAs(path); await click('Back to document');
    await openFile(path);
    await until(async () => (await state()).visuals.objects.length === 3, 'DOCX lost visual objects');
    const objects = (await state()).visuals.objects;
    assert.equal(objects.find(o => o.kind === 'Rectangle').text, 'Editable vector text'); assert.equal(objects.find(o => o.kind === 'Equation').text, '(a+b)/(c)');
    await page.screenshot({ path: output + '/imported-objects.png' });
  });
  await check('HTML uses SVG and MathML rather than dropping objects', async () => {
    await click('File tab'); await click('File Export'); const pending = page.waitForEvent('download'); await click('Web page'); const file = await pending;
    const path = output + '/objects.html'; await file.saveAs(path); await click('Back to document');
    const html = await fs.readFile(path, 'utf8'); assert.match(html, /<svg\b/); assert.match(html, /<math\b/); assert.match(html, /<mfrac\b/); assert.ok(html.includes('Editable vector text'));
  });
  await check('table column and row boundaries resize directly on the page', async () => {
    await blank(); await click('Insert tab'); await click('Table'); await click('Insert 2 by 2 table');
    await until(async () => (await state()).visuals.cells.length === 4, 'Table not ready');
    let cells = (await state()).visuals.cells; const first = cells.find(c => c.row === 0 && c.column === 0); const second = cells.find(c => c.row === 0 && c.column === 1);
    const revision = (await state()).revision;
    await drag({ x: first.x + first.width, y: first.y + first.height / 2 }, { x: first.x + first.width + 35, y: first.y + first.height / 2 });
    assert.equal((await state()).revision, revision); await finishDrag();
    await until(async () => (await state()).visuals.cells.find(c => c.row === 0 && c.column === 0).width > first.width + 15, 'Column resize failed');
    cells = (await state()).visuals.cells;
    assert.ok(Math.abs(cells.filter(c => c.row === 0).reduce((sum, c) => sum + c.width, 0) - first.width - second.width) < 0.1);
    const row = cells.find(c => c.row === 1 && c.column === 0);
    await drag({ x: row.x + row.width / 2, y: row.y + row.height }, { x: row.x + row.width / 2, y: row.y + row.height + 25 }); await finishDrag();
    await until(async () => (await state()).visuals.cells.find(c => c.row === 1 && c.column === 0).height > row.height + 10, 'Row resize failed');
    await page.screenshot({ path: output + '/table-resizing.png' });
  });
  await check('Alt-drag cell rectangle can be merged and undone', async () => {
    const cells = (await state()).visuals.cells; const first = cells.find(c => c.row === 0 && c.column === 0); const last = cells.find(c => c.row === 1 && c.column === 1);
    await page.keyboard.down('Alt');
    await drag({ x: first.x + 12, y: first.y + first.height / 2 }, { x: last.x + last.width / 2, y: last.y + last.height / 2 });
    await finishDrag(); await page.keyboard.up('Alt'); await click('Table Layout tab'); await click('merge-cells');
    await until(async () => (await state()).visuals.cells.some(c => c.rowSpan === 2 && c.columnSpan === 2), 'Merge rectangle failed');
    await page.keyboard.press('Control+z'); await until(async () => (await state()).visuals.cells.length === 4, 'Merge undo failed');
  });
  await check('all object metadata survives recovery reload', async () => {
    await openFile(output + '/objects.textspace'); await until(async () => (await state()).visuals.objects.length === 3, 'Native objects not opened');
    await page.waitForTimeout(2200); const before = (await state()).visuals.objects.map(o => ({ kind: o.kind, text: o.text, rotation: o.rotation }));
    await page.reload({ waitUntil: 'domcontentloaded' }); await page.waitForFunction(() => globalThis.__textSpaceState?.visuals, null, { timeout: 150000 });
    await until(async () => (await state()).visuals.objects.length === 3, 'Recovery lost visual objects');
    assert.deepEqual((await state()).visuals.objects.map(o => ({ kind: o.kind, text: o.text, rotation: o.rotation })), before);
    await page.screenshot({ path: output + '/recovered-objects.png' });
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null);
  report.input = await page.evaluate(() => ({ focused: document.hasFocus(), active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('VISUAL ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure, state: report.success ? undefined : report.state, input: report.input }));
  await browser.close();
}
