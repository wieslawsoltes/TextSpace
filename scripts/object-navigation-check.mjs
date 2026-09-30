import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { deflateSync } from 'node:zlib';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/object-navigation';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [], cropGestures: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, acceptDownloads: true });
page.on('filechooser', () => {});
page.on('pageerror', error => report.errors.push(error.message));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
async function until(test, message, timeout = 30000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { if (await test()) return; await page.waitForTimeout(100); }
  throw new Error(message);
}
async function click(name) {
  let item;
  await until(async () => {
    item = await page.evaluate(name => globalThis.__textSpaceState?.controls.findLast(c => (c.name === name || c.command === name) && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 > 0 && c.y + c.height / 2 > 0 && c.x + c.width / 2 < innerWidth && c.y + c.height / 2 < innerHeight), name);
    return !!item;
  }, 'Missing control: ' + name);
  await page.mouse.click(item.x + item.width / 2, item.y + item.height / 2); await page.waitForTimeout(200);
}
async function inputReady() {
  await until(() => page.evaluate(() => document.hasFocus() && document.activeElement?.id === 'uno-input'), 'Native input did not acquire focus');
}
async function fill(name, value) { await click(name); await inputReady(); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value); }
async function save(name) {
  await click('File tab'); await click('File Save As');
  const pending = page.waitForEvent('download'); await click('TextSpace document'); const file = await pending;
  await file.saveAs(`${output}/${name}.textspace`); await click('Back to document');
  return JSON.parse(await fs.readFile(`${output}/${name}.textspace`, 'utf8'));
}
async function open(path, expectedId) {
  await click('File tab'); await click('File Open'); const pending = page.waitForEvent('filechooser'); await click('Browse this device');
  await (await pending).setFiles(path); await page.waitForTimeout(400);
  if ((await state()).dialog) await click('Continue without a copy');
  await until(async () => !(await state()).dialog && (await state()).visuals.objects.some(o => o.id === expectedId), 'Native fixture did not open');
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS OBJECT NAVIGATION', name); }
async function cropDrag(id, handleName, rotation, dx, dy) {
  // A command can complete before the read-only diagnostics timer publishes its
  // new handle coordinates. Never start a drag from pre-rotation geometry.
  await until(async () => {
    const s = await state(); const image = s.visuals.objects.find(o => o.id === id);
    return s.visuals.selected === id && s.visuals.cropping && !s.visuals.gesture && image?.rotation === rotation;
  }, 'Crop transform and selection were not ready');
  const before = await state(); const image = before.visuals.objects.find(o => o.id === id);
  const handle = image.handles.find(h => h.handle === handleName); assert.ok(handle);
  const record = { id, handleName, rotation, from: handle, dx, dy, revision: before.revision };
  report.cropGestures.push(record);
  await page.mouse.move(handle.x, handle.y); await page.mouse.down();
  await until(async () => (await state()).visuals.gesture, 'Pointer did not capture a crop gesture');
  await page.mouse.move(handle.x + dx, handle.y + dy, { steps: 8 });
  await until(async () => (await state()).visuals.gesture, 'Crop preview ended before release');
  assert.equal((await state()).revision, before.revision, 'Crop preview changed document history');
  await page.mouse.up();
  await until(async () => !(await state()).visuals.gesture && (await state()).revision === before.revision + 1, 'Crop did not commit exactly one edit');
  const after = (await state()).visuals.objects.find(o => o.id === id);
  record.after = { x: after.x, y: after.y, width: after.width, height: after.height };
  assert.equal(after.x, image.x, 'Cropping moved the picture instead of its source edge');
  assert.equal(after.y, image.y, 'Cropping moved the picture instead of its source edge');
  assert.equal(after.width, image.width); assert.equal(after.height, image.height);
}
function png() {
  const crc = bytes => { let c = 0xffffffff; for (const b of bytes) { c ^= b; for (let i = 0; i < 8; i++) c = c & 1 ? (c >>> 1) ^ 0xedb88320 : c >>> 1; } return (c ^ 0xffffffff) >>> 0; };
  const chunk = (name, data) => { const body = Buffer.concat([Buffer.from(name), data]); const size = Buffer.alloc(4); size.writeUInt32BE(data.length); const sum = Buffer.alloc(4); sum.writeUInt32BE(crc(body)); return Buffer.concat([size, body, sum]); };
  const width = 128, height = 96, pixels = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) { const i = y * (width * 4 + 1) + x * 4 + 1; pixels[i] = x * 2; pixels[i + 1] = y * 2; pixels[i + 2] = 90; pixels[i + 3] = 255; }
  const header = Buffer.alloc(13); header.writeUInt32BE(width); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', deflateSync(pixels)), chunk('IEND', Buffer.alloc(0))]);
}
let baseline, first, second;
try {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.__textSpaceState?.visuals || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await click('File tab'); await click('Blank document template'); await until(async () => (await state()).text === '', 'Blank template failed');
  await check('Selection Pane reaches objects without changing history', async () => {
    await click('Insert tab'); await click('Shapes'); await click('Rectangle');
    await until(async () => (await state()).visuals.objects.length === 1, 'First shape not inserted'); first = (await state()).visuals.selected;
    await page.keyboard.press('Escape'); await click('Insert tab'); await click('Shapes'); await click('Ellipse');
    await until(async () => (await state()).visuals.objects.length === 2, 'Second shape not inserted'); second = (await state()).visuals.selected;
    const revision = (await state()).revision; const text = (await state()).text;
    await click('Selection Pane'); await click('Select object: ' + first);
    await until(async () => (await state()).visuals.selected === first, 'Pane selection failed');
    assert.equal((await state()).revision, revision); assert.equal((await state()).text, text);
  });
  await check('Tab and Shift+Tab cycle object selection, not body characters', async () => {
    await inputReady(); const before = await state(); await page.keyboard.press('Tab');
    await until(async () => (await state()).visuals.selected === second, 'Tab did not select next object');
    await page.keyboard.press('Shift+Tab'); await until(async () => (await state()).visuals.selected === first, 'Shift+Tab did not return');
    assert.equal((await state()).revision, before.revision); assert.equal((await state()).text, before.text);
  });
  await check('object search filters the list and explicit editing preserves the body', async () => {
    await fill('Find object', 'Ellipse');
    await until(async () => !(await state()).controls.some(c => c.name === 'Select object: ' + first) && (await state()).controls.some(c => c.name === 'Select object: ' + second), 'Object filter did not update');
    await click('Select object: ' + second); const text = (await state()).text;
    await click('Edit selected'); await fill('Shape text', 'Searchable ellipse'); await click('Apply shape text');
    await until(async () => (await state()).visuals.objects.some(o => o.id === second && o.text === 'Searchable ellipse'), 'Pane edit failed');
    assert.equal((await state()).text, text); await fill('Find object', '');
    await page.screenshot({ path: output + '/selection-pane.png' }); await click('Close Selection');
    baseline = await save('two-objects');
  });
  await check('double-click respects the floating paint layer over later inline objects', async () => {
    const fixture = structuredClone(baseline); fixture.id = randomUUID();
    const a = fixture.blocks.find(b => b.id === first); const b = fixture.blocks.find(b => b.id === second);
    a.placement = { floating: true, x: 0, y: 0 }; b.placement = { floating: false, x: 0, y: 0 };
    a.width = b.width = 140; a.height = b.height = 90; a.text = 'Floating foreground'; b.text = 'Covered inline';
    fixture.blocks = [a, b, ...fixture.blocks.filter(b => b.$type === 'paragraph')];
    await fs.writeFile(output + '/overlap.textspace', JSON.stringify(fixture)); await open(output + '/overlap.textspace', first);
    const item = (await state()).visuals.objects.find(o => o.id === first);
    await page.mouse.dblclick(item.x + item.width / 2, item.y + item.height / 2);
    await until(async () => (await state()).visuals.editor, 'Double-click did not open object editor');
    assert.equal((await state()).visuals.selected, first, 'Covered inline object intercepted the floating object'); await click('Cancel shape text');
  });
  await check('large selection lists use bounded pages and search all objects', async () => {
    const fixture = structuredClone(baseline); fixture.id = randomUUID(); const seed = fixture.blocks.find(b => b.$type === 'shape');
    const shapes = Array.from({ length: 105 }, (_, i) => ({ ...structuredClone(seed), id: randomUUID(), text: 'Selection specimen ' + i, placement: { floating: true, x: 0, y: 0 } }));
    fixture.blocks = [...shapes, ...fixture.blocks.filter(b => b.$type === 'paragraph')];
    await fs.writeFile(output + '/many-objects.textspace', JSON.stringify(fixture)); await open(output + '/many-objects.textspace', shapes[0].id);
    await click('Home tab'); await click('Selection Pane');
    await until(async () => (await state()).controls.filter(c => c.name.startsWith('Select object: ')).length === 100, 'Selection list did not bound its first page');
    await click('Next results'); await until(async () => (await state()).controls.filter(c => c.name.startsWith('Select object: ')).length === 5, 'Second result page incorrect');
    await click('Select object: ' + shapes[104].id); await until(async () => (await state()).visuals.selected === shapes[104].id, 'Last object inaccessible');
    await fill('Find object', 'specimen 104');
    await until(async () => (await state()).controls.filter(c => c.name.startsWith('Select object: ')).length === 1, 'Search did not cover all result pages');
    await fill('Find object', ''); await click('Close Selection');
  });
  await check('flipped and rotated picture crop changes visible source edges only', async () => {
    await open(output + '/two-objects.textspace', first);
    await click('Insert tab'); const source = png(); const path = output + '/source.png'; await fs.writeFile(path, source);
    const pending = page.waitForEvent('filechooser'); await click('insert-picture'); await (await pending).setFiles(path);
    await until(async () => (await state()).visuals.objects.some(o => o.kind === 'Picture'), 'Picture not inserted');
    const id = (await state()).visuals.objects.find(o => o.kind === 'Picture').id;
    await click('Home tab'); await click('Selection Pane'); await click('Select object: ' + id); await click('Close Selection');
    await click('Rotate'); await click('Flip Horizontal'); await click('crop-picture');
    await cropDrag(id, 'West', 0, 16, 0);
    let saved = await save('flipped-crop'); let picture = saved.blocks.find(b => b.id === id);
    assert.ok(picture.crop.right > 0); assert.equal(picture.crop.left, 0); assert.equal(Buffer.from(picture.data, 'base64').compare(source), 0);
    await click('Rotate'); await click('Flip Vertical'); await click('Rotate'); await click('Rotate Right 90°');
    await until(async () => (await state()).visuals.objects.find(o => o.id === id)?.rotation === 90, 'Rotation command did not finish');
    const rotated = (await save('before-rotated-crop')).blocks.find(b => b.id === id);
    assert.equal(rotated.placement.flipHorizontal, true); assert.equal(rotated.placement.flipVertical, true); assert.equal(rotated.placement.rotation, 90);
    await cropDrag(id, 'North', 90, -12, 0);
    saved = await save('rotated-flipped-crop'); picture = saved.blocks.find(b => b.id === id);
    assert.ok(picture.crop.bottom > 0); assert.equal(picture.crop.top, 0); assert.ok(picture.crop.right > 0);
    assert.deepEqual(picture.placement, rotated.placement, 'Cropping unexpectedly changed placement');
    assert.equal(Buffer.from(picture.data, 'base64').compare(source), 0);
    await page.screenshot({ path: output + '/flipped-crop.png' });
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null); report.input = await page.evaluate(() => ({ focused: document.hasFocus(), active: document.activeElement?.outerHTML, text: document.activeElement?.value })).catch(() => null);
  await page.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('OBJECT NAVIGATION ACCEPTANCE', JSON.stringify(report)); await browser.close();
}
