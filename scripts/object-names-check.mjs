import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { deflateSync } from 'node:zlib';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/object-names';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, acceptDownloads: true });
page.on('filechooser', () => {});
page.on('pageerror', error => report.errors.push(error.message));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(test, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await test()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function click(name) {
  let item;
  await until(async () => {
    item = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 > 0 && c.y + c.height / 2 > 0 && c.x + c.width / 2 < innerWidth && c.y + c.height / 2 < innerHeight), name);
    return !!item;
  }, 'Missing visible control: ' + name);
  await page.mouse.click(item.x + item.width / 2, item.y + item.height / 2); await page.waitForTimeout(200);
}
async function fill(name, value) {
  await click(name);
  await until(() => page.evaluate(() => document.hasFocus() && document.activeElement?.id === 'uno-input' && !document.activeElement.readOnly), 'Native name input not ready');
  await page.keyboard.press('Control+a');
  // Empty replacement must delete the selection; insertText('') is not a deletion event.
  if (value === '') await page.keyboard.press('Backspace'); else await page.keyboard.insertText(value);
  await until(() => page.evaluate(value => document.activeElement?.value === value, value), 'Native field does not contain requested value');
}
async function pane() {
  if (!(await state()).controls.some(c => c.name === 'Object name')) { await click('Home tab'); await click('Selection Pane'); }
}
async function select(id) {
  await pane(); await fill('Find object', ''); await click('Select object: ' + id);
  await until(async () => (await state()).visuals.selected === id, 'Object was not selected');
}
async function save(name) {
  await click('File tab'); await click('File Save As');
  const pending = page.waitForEvent('download'); await click('TextSpace document'); const file = await pending;
  await file.saveAs(`${output}/${name}.textspace`); await click('Back to document');
  return JSON.parse(await fs.readFile(`${output}/${name}.textspace`, 'utf8'));
}
async function open(path, expectedText) {
  await click('File tab'); await click('File Open');
  const pending = page.waitForEvent('filechooser'); await click('Browse this device'); await (await pending).setFiles(path);
  await page.waitForTimeout(500); if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => !(await state()).dialog && !(await state()).visuals.editor && (await state()).text === expectedText, 'Document open failed');
}
async function rename(id, name) {
  await select(id); const before = await state(); await fill('Object name', name);
  assert.equal((await state()).revision, before.revision, 'Typing a name committed it before Rename');
  await click('Rename selected');
  await until(async () => (await state()).revision === before.revision + 1, 'Rename must commit exactly once');
  assert.equal((await state()).text, before.text, 'Rename changed the main story');
  assert.equal((await state()).visuals.selected, id, 'Rename changed object identity');
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS OBJECT NAMES', name); }
function fixturePng() {
  const crc = buffer => { let c = 0xffffffff; for (const b of buffer) { c ^= b; for (let i = 0; i < 8; i++) c = c & 1 ? (c >>> 1) ^ 0xedb88320 : c >>> 1; } return (c ^ 0xffffffff) >>> 0; };
  const chunk = (name, data) => { const body = Buffer.concat([Buffer.from(name), data]); const size = Buffer.alloc(4); size.writeUInt32BE(data.length); const sum = Buffer.alloc(4); sum.writeUInt32BE(crc(body)); return Buffer.concat([size, body, sum]); };
  const width = 128, height = 96, pixels = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const offset = y * (width * 4 + 1) + 1 + x * 4; pixels[offset] = x * 2; pixels[offset + 1] = y * 2; pixels[offset + 2] = 120; pixels[offset + 3] = 255; }
  const header = Buffer.alloc(13); header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', deflateSync(pixels)), chunk('IEND', Buffer.alloc(0))]);
}

let shapeId, equationId, pictureId, savedDocument;
const shapeName = 'Pump P-101 🧪', equationName = 'Flow calculation α', pictureName = 'Reference diagram';
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.visuals || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await click('File tab'); await click('Blank document template');
  await until(async () => (await state()).text === '', 'Blank template failed');
  await check('shape naming is separate from painted text and commits only on Rename', async () => {
    await click('Insert tab'); await click('Shapes'); await click('Rectangle');
    await until(async () => (await state()).visuals.objects.length === 1, 'Shape insertion failed'); shapeId = (await state()).visuals.selected;
    await click('edit-object'); await fill('Shape text', 'Painted label'); await click('Apply shape text');
    await until(async () => !(await state()).visuals.editor, 'Shape editor did not close');
    await rename(shapeId, shapeName);
    const saved = await save('named-shape'), shape = saved.blocks.find(b => b.id === shapeId);
    assert.equal(shape.name, shapeName); assert.equal(shape.text, 'Painted label');
    await pane(); await fill('Find object', 'P-101');
    assert.ok((await state()).controls.some(c => c.name === 'Select object: ' + shapeId));
    await fill('Find object', 'no-object-has-this-name');
    await until(async () => !(await state()).controls.some(c => c.name.startsWith('Select object: ')), 'Name search did not filter');
    await fill('Find object', '');
  });
  await check('rename undo, redo and clearing a name preserve visual content', async () => {
    await click('undo');
    assert.equal((await save('undo-name')).blocks.find(b => b.id === shapeId).name, '');
    await click('redo');
    assert.equal((await save('redo-name')).blocks.find(b => b.id === shapeId).name, shapeName);
    await rename(shapeId, '');
    const cleared = (await save('cleared-name')).blocks.find(b => b.id === shapeId);
    assert.equal(cleared.name, ''); assert.equal(cleared.text, 'Painted label');
    await click('undo'); assert.equal((await save('restored-name')).blocks.find(b => b.id === shapeId).name, shapeName);
  });
  await check('open shape drafts disable renaming instead of overwriting object state', async () => {
    await select(shapeId); await click('Edit selected');
    await until(async () => (await state()).visuals.editor, 'Shape draft did not open');
    await until(async () => (await state()).controls.some(c => c.name === 'Object name' && !c.enabled), 'Rename field was enabled during an object draft');
    assert.ok((await state()).controls.some(c => c.name === 'Rename selected' && !c.enabled));
    await click('Cancel shape text');
    await until(async () => !(await state()).visuals.editor, 'Shape cancellation failed');
    await click('Close Selection');
  });
  await check('equation names persist independently of the structural math tree', async () => {
    await page.keyboard.press('Escape'); await click('Insert tab'); await click('insert-equation');
    await until(async () => (await state()).visuals.editor, 'Equation draft did not open');
    await click('Equation Fraction'); await click('Apply equation');
    await until(async () => !(await state()).visuals.editor && (await state()).visuals.objects.some(o => o.kind === 'Equation'), 'Equation insertion failed');
    equationId = (await state()).visuals.objects.find(o => o.kind === 'Equation').id;
    const before = (await save('before-equation-name')).blocks.find(b => b.id === equationId);
    await rename(equationId, equationName);
    const after = (await save('named-equation')).blocks.find(b => b.id === equationId);
    assert.equal(after.name, equationName); assert.deepEqual(after.root, before.root); assert.deepEqual(after.placement, before.placement);
    await click('Close Selection');
  });
  await check('picture names do not replace alternative text or resample source bytes', async () => {
    await page.keyboard.press('Escape'); const path = output + '/source.png', bytes = fixturePng(); await fs.writeFile(path, bytes);
    await click('Insert tab'); const pending = page.waitForEvent('filechooser'); await click('insert-picture'); await (await pending).setFiles(path);
    await until(async () => (await state()).visuals.objects.some(o => o.kind === 'Picture'), 'Picture insertion failed');
    pictureId = (await state()).visuals.objects.find(o => o.kind === 'Picture').id;
    const before = (await save('before-picture-name')).blocks.find(b => b.id === pictureId);
    await rename(pictureId, pictureName); savedDocument = await save('named-document');
    const after = savedDocument.blocks.find(b => b.id === pictureId);
    assert.equal(after.name, pictureName); assert.equal(after.altText, before.altText); assert.equal(after.data, before.data);
    assert.equal(Buffer.compare(Buffer.from(after.data, 'base64'), bytes), 0); assert.deepEqual(after.placement, before.placement);
    await page.screenshot({ path: output + '/named-selection-pane.png' });
  });
  await check('names survive native and DOCX reopen through real file pickers', async () => {
    const text = (await state()).text;
    await open(output + '/named-document.textspace', text);
    assert.deepEqual((await save('native-reopened')).blocks.filter(b => b.name).map(b => b.name).sort(), [shapeName, equationName, pictureName].sort());
    await click('File tab'); await click('File Export');
    const pending = page.waitForEvent('download'); await click('Word document'); const download = await pending;
    await download.saveAs(output + '/named-document.docx'); await click('Back to document');
    await open(output + '/named-document.docx', text);
    const imported = await save('docx-reopened');
    assert.deepEqual(imported.blocks.filter(b => b.name).map(b => b.name).sort(), [shapeName, equationName, pictureName].sort());
    const picture = imported.blocks.find(b => b.$type === 'image');
    assert.equal(picture.data, savedDocument.blocks.find(b => b.id === pictureId).data);
    assert.equal(picture.altText, savedDocument.blocks.find(b => b.id === pictureId).altText);
  });
  await check('local recovery retains names and the original content after reload', async () => {
    const text = (await state()).text; await page.waitForTimeout(2400); await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => globalThis.__textSpaceState?.visuals, null, { timeout: 150000 });
    await until(async () => (await state()).text === text && (await state()).visuals.objects.length === 3, 'Recovered document is not ready');
    const recovered = await save('recovered-names');
    assert.deepEqual(recovered.blocks.filter(b => b.name).map(b => b.name).sort(), [shapeName, equationName, pictureName].sort());
    assert.equal(recovered.blocks.find(b => b.$type === 'shape').text, 'Painted label');
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null);
  report.input = await page.evaluate(() => ({ focused: document.hasFocus(), active: document.activeElement?.outerHTML, value: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('OBJECT NAMES ACCEPTANCE', JSON.stringify(report)); await browser.close();
}
