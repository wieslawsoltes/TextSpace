import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = (process.env.TEXTSPACE_BASE_URL || 'http://127.0.0.1:4173/TextSpace/').replace(/\/?$/, '/');
const output = 'test-results/recovery';
await fs.mkdir(output, { recursive: true });
const report = { base, checks: [], errors: [], downloads: [] };
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-dev-shm-usage'] });
let context, page;
const hash = text => createHash('sha256').update(text, 'utf8').digest('hex');
const text = 'Preserve my words — żółć 😀.\nA second paragraph.';
function fixture(tabs, title = 'Recovery fixture', first = text.split('\n')[0]) {
  return JSON.stringify({ formatVersion: 1, id: 'recovery-fixture', title, subject: 'Keep metadata',
    blocks: [
      { $type: 'paragraph', id: 'first', format: { tabStops: tabs }, runs: [{ text: first, style: { bold: true } }] },
      { $type: 'paragraph', id: 'second', runs: [{ text: text.split('\n')[1] }] }
    ], bookmarks: [{ id: 'bm1', name: 'Start', start: 0, end: 8 }],
    comments: [{ id: 'comment1', start: 0, end: 8, author: 'Reviewer', text: 'Retain the comment', replies: [] }]
  });
}
const excessive = Array.from({ length: 129 }, (_, position) => ({ position, alignment: 'Right', leader: 'Dot' }));
const state = () => page.evaluate(() => globalThis.__textSpaceState);
const recovery = () => page.evaluate(() => globalThis.__textSpaceRecoveryState);
async function until(predicate, message, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await predicate()) return; await page.waitForTimeout(120); }
  throw new Error(message);
}
async function click(name, startup = false, prefix = false) {
  let control;
  await until(async () => {
    const snapshot = startup ? await recovery() : await state();
    control = snapshot?.controls?.findLast(c => (prefix ? c.name.startsWith(name) : c.name === name || c.command === name)
      && c.enabled && c.width > 0 && c.height > 0 && c.x + c.width / 2 >= 0 && c.x + c.width / 2 < 1440 && c.y + c.height / 2 >= 0 && c.y + c.height / 2 < 1000);
    return !!control;
  }, 'Control not visible: ' + name);
  await page.mouse.click(control.x + control.width / 2, control.y + control.height / 2);
  await page.waitForTimeout(220);
}
async function ready() {
  await page.waitForFunction(() => globalThis.__textSpaceState?.ready || globalThis.__textSpaceError, null, { timeout: 150000 });
  assert.equal(await page.evaluate(() => globalThis.__textSpaceError ?? null), null);
  await until(async () => (await state())?.canvas.width > 200, 'Workbench is not laid out');
}
async function recovered() {
  // Retained startup errors are not the result of the new asynchronous adoption.
  await page.waitForFunction(() => globalThis.__textSpaceState?.ready
    && !globalThis.__textSpaceError && !globalThis.__textSpaceRecoveryState?.active,
    null, { timeout: 150000 });
  await ready();
  assert.equal(await recovery(), undefined, 'Recovery center must close after successful adoption');
}
async function initial() {
  if (context) await context.close();
  context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, acceptDownloads: true });
  page = await context.newPage();
  page.on('pageerror', error => report.errors.push(error.message));
  page.on('filechooser', () => {});
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' }); await ready();
}
async function seed(original, entries = [], oldVersion = null) {
  await page.evaluate(async ({ original, entries, oldVersion }) => {
    // Deliberate persisted-input/failure fixtures, not an editing shortcut.
    const db = await new Promise((resolve, reject) => {
      const request = indexedDB.open('TextSpace', 1); request.onsuccess = () => resolve(request.result); request.onerror = () => reject(request.error);
    });
    try {
      await new Promise((resolve, reject) => {
        const tx = db.transaction(['workspace', 'history'], 'readwrite'), workspace = tx.objectStore('workspace');
        workspace.put({ document: original, title: 'Broken recovery', savedAt: new Date().toISOString() }, 'latest');
        if (entries.length) {
          workspace.put(entries.map(({ id, savedAt, bytes }) => ({ id, savedAt, bytes })), 'protected-originals-index');
          for (const entry of entries) workspace.put(entry.original, 'protected-original/' + entry.id);
        }
        if (oldVersion) tx.objectStore('history').put({ id: 'previous-version', title: 'Previous valid version', document: oldVersion, bytes: new TextEncoder().encode(oldVersion).length, savedAt: '2026-01-01T10:00:00Z' });
        tx.oncomplete = resolve; tx.onabort = tx.onerror = () => reject(tx.error);
      });
      sessionStorage.removeItem('TextSpace-emergency');
    } finally { db.close(); }
  }, { original, entries, oldVersion });
  await page.reload({ waitUntil: 'domcontentloaded' });
}
async function rawLatest() {
  return page.evaluate(async () => {
    const db = await new Promise((resolve, reject) => { const r = indexedDB.open('TextSpace', 1); r.onsuccess = () => resolve(r.result); r.onerror = () => reject(r.error); });
    try { return await new Promise((resolve, reject) => { const r = db.transaction('workspace').objectStore('workspace').get('latest'); r.onsuccess = () => resolve(r.result?.document ?? null); r.onerror = () => reject(r.error); }); }
    finally { db.close(); }
  });
}
async function recoveryReady() {
  await page.waitForFunction(() => globalThis.__textSpaceRecoveryState?.controls?.length > 0, null, { timeout: 150000 });
}
async function download(label, filename, startup = false, prefix = false) {
  const pending = page.waitForEvent('download', { timeout: 30000 }); await click(label, startup, prefix);
  const item = await pending; const path = output + '/' + filename; await item.saveAs(path);
  const bytes = await fs.readFile(path);
  // Original preservation is a byte contract. Keep the encoding marker as data
  // in the secondary text check and record raw evidence alongside its identity.
  const decoded = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes);
  report.downloads.push({ file: filename, suggestedName: item.suggestedFilename(),
    bytes: bytes.length, sha256: hash(bytes), prefix: [...bytes.subarray(0, 6)], firstCodeUnit: decoded.charCodeAt(0) });
  return decoded;
}
async function focusEditor() {
  const { canvas } = await state(); await page.bringToFront();
  await page.mouse.click(canvas.x + canvas.paperLeft + 75 * canvas.scale, canvas.y + 18 + 75 * canvas.scale - canvas.scrollY);
  await until(() => page.evaluate(() => document.activeElement?.id === 'uno-input' && document.activeElement.tagName === 'TEXTAREA'), 'Native editor did not focus');
}
async function repairFile(original, title, filename) {
  const path = output + '/' + filename; await fs.writeFile(path, original, 'utf8');
  await click('Recovery tab'); const chooser = page.waitForEvent('filechooser');
  await click('Open and Repair'); await (await chooser).setFiles(path);
  await until(async () => (await state()).dialog, 'Repair confirmation did not open');
  await click('Protect original and open');
  await until(async () => {
    const s = await state();
    return s.title.startsWith(title) || s.controls.some(c => c.name === 'Continue without a copy' && c.enabled);
  }, 'Repair did not open a document or replacement confirmation');
  if (!(await state()).title.startsWith(title)) await click('Continue without a copy');
  await until(async () => (await state()).title.startsWith(title) && !(await state()).dialog, 'Repaired import did not load');
  assert.equal((await state()).text, text);
  assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), hash(original)), original);
}
async function check(name, action) { await action(); report.checks.push(name); console.log('PASS RECOVERY', name); }
try {
  await check('legacy null tabs boot without losing text or metadata', async () => {
    await initial(); const original = fixture(null); await seed(original); await ready();
    assert.equal((await state()).text, text); assert.equal((await state()).comments.length, 1);
    await focusEditor(); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
    const item = await pending; await item.saveAs(output + '/legacy-null.textspace');
    const saved = JSON.parse(await fs.readFile(output + '/legacy-null.textspace', 'utf8'));
    assert.deepEqual(saved.blocks[0].format.tabStops, []); assert.equal(saved.bookmarks[0].name, 'Start'); assert.equal(saved.subject, 'Keep metadata');
  });
  await check('oversized recovery stays unchanged until an explicit repair decision', async () => {
    await initial(); const original = fixture(excessive); await seed(original); await recoveryReady();
    assert.equal(await rawLatest(), original);
    await click('Protect original and open repaired copy', true);
    await until(async () => (await recovery()).message.includes('Preview'), 'Repair requires a reviewed preview');
    assert.equal(await rawLatest(), original);
    assert.equal(await download('Download original recovery', 'original-before-repair.textspace', true), original);
    await click('Preview tab-stop repair', true);
    await until(async () => (await recovery()).repairPreviewed, 'Repair preview not produced');
    assert.ok((await recovery()).message.includes('1 invalid, duplicate or excess'));
    assert.equal(await rawLatest(), original);
    await page.screenshot({ path: output + '/repair-preview.png' });
  });
  await check('adopting repair commits a checksum-verified original before AutoSave', async () => {
    const original = fixture(excessive);
    await click('Protect original and open repaired copy', true); await recovered();
    assert.equal((await state()).text, text); assert.equal((await state()).comments.length, 1);
    const id = hash(original);
    assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), id), original);
    await until(async () => { const raw = await rawLatest(); return raw && JSON.parse(raw).blocks[0].format.tabStops.length === 128; }, 'Repaired recovery was not committed');
    await focusEditor(); await page.keyboard.press('Control+End'); await page.keyboard.insertText(' Edited after repair.');
    await until(async () => (await state()).text.endsWith(' Edited after repair.'), 'Recovered copy is not editable');
    await page.waitForTimeout(1800);
    assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), id), original);
    await page.screenshot({ path: output + '/repaired-workbench.png' });
    await page.reload({ waitUntil: 'domcontentloaded' }); await ready();
    assert.ok((await state()).text.endsWith(' Edited after repair.'));
  });
  await check('protected original downloads from the actual Recovery ribbon', async () => {
    const original = fixture(excessive), id = hash(original);
    await click('Recovery tab'); await click('Protected Originals');
    assert.equal(await download('Download original ' + id.slice(0, 12), 'original-after-reload.textspace', false, true), original);
    await click('Close');
  });
  await check('unrepairable JSON offers a protected blank rather than a startup loop', async () => {
    await initial(); const original = '{this JSON is broken but belongs to the user'; await seed(original); await recoveryReady();
    await click('Preview tab-stop repair', true);
    assert.equal(await rawLatest(), original); assert.equal((await recovery()).repairPreviewed, false);
    await click('Protect original and start blank', true); await recovered();
    assert.equal((await state()).text, '');
    assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), hash(original)), original);
  });
  await check('archive capacity failure never replaces active recovery or evicts originals', async () => {
    await initial(); const original = fixture(excessive);
    const entries = Array.from({ length: 16 }, (_, i) => { const raw = 'Retained original ' + i; return { id: hash(raw), original: raw, bytes: Buffer.byteLength(raw), savedAt: '2026-01-01T10:00:00Z' }; });
    await seed(original, entries); await recoveryReady();
    await click('Protect original and start blank', true);
    await until(async () => (await recovery()).message.includes('full'), 'Archive-full failure was not surfaced');
    assert.equal(await rawLatest(), original);
    assert.equal(JSON.parse(await page.evaluate(() => globalThis.TextSpaceRecovery.list())).length, 16);
    assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), entries[0].id), entries[0].original);
  });
  await check('earlier valid snapshot restores only after protecting current corruption', async () => {
    await initial(); const original = fixture(excessive), prior = fixture([], 'Earlier document', 'Earlier valid text');
    await seed(original, [], prior); await recoveryReady(); await click('Show previous recovery versions', true);
    await click('Restore Previous valid version', true, true); await recovered();
    assert.ok((await state()).text.startsWith('Earlier valid text'));
    assert.equal(await page.evaluate(id => globalThis.TextSpaceRecovery.read(id), hash(original)), original);
  });
  await check('Open and Repair imports a native copy through a real file chooser', async () => {
    await repairFile(fixture(excessive, 'Imported repair'), 'Imported repair', 'import-repair.textspace');
  });
  await check('file repair retains the original UTF-8 BOM and bytes', async () => {
    const original = '\uFEFF' + fixture(excessive, 'BOM original');
    await repairFile(original, 'BOM original', 'bom-source.textspace');
    await click('Recovery tab'); await click('Protected Originals');
    const saved = await download('Download original ' + hash(original).slice(0, 12), 'bom-retained.textspace', false, true);
    const sourceBytes = await fs.readFile(output + '/bom-source.textspace');
    const retainedBytes = await fs.readFile(output + '/bom-retained.textspace');
    assert.deepEqual([...retainedBytes.subarray(0, 3)], [0xEF, 0xBB, 0xBF]);
    assert.deepEqual(retainedBytes, sourceBytes);
    assert.equal(hash(retainedBytes), hash(original));
    assert.equal(report.downloads.at(-1).suggestedName, 'TextSpace-original-' + hash(original).slice(0, 12) + '.textspace');
    assert.equal(saved.charCodeAt(0), 0xFEFF);
    assert.deepEqual(Buffer.from(saved, 'utf8'), Buffer.from(original, 'utf8'));
    await click('Close');
  });
  assert.deepEqual(report.errors, []); report.success = true;
} catch (error) {
  report.success = false; report.failure = String(error.stack || error); process.exitCode = 1;
  report.state = await state().catch(() => null); report.recovery = await recovery().catch(() => null);
  report.startupError = await page?.evaluate(() => globalThis.__textSpaceError ?? null).catch(() => null);
  await page?.screenshot({ path: output + '/failure.png' }).catch(() => {}); console.error(error);
} finally {
  await fs.writeFile(output + '/report.json', JSON.stringify(report, null, 2));
  console.log('RECOVERY ACCEPTANCE', JSON.stringify({ success: report.success, checks: report.checks, failure: report.failure, downloads: report.downloads }));
  await browser.close();
}
