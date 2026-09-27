import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(process.argv[2] || 'site');
const ready = JSON.parse(await fs.readFile(process.argv[3] || 'artifacts/server-ready.json', 'utf8'));
const base = ready.url;
async function walk(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await walk(full));
    else if (entry.isFile()) files.push(full);
  }
  return files;
}
const files = await walk(root);
const checked = files.filter(file => /(?:index\.html|uno-config\.js|uno-bootstrap\.js|dotnet(?:\.[^/]+)?\.js|build-info\.json)$/.test(file));
assert.ok(checked.some(file => path.basename(file) === 'dotnet.js'), 'Stable dotnet.js entry is missing');
const results = [];
for (const file of checked) {
  const relative = path.relative(root, file).split(path.sep).join('/');
  const response = await fetch(new URL(relative, base), { redirect: 'error' });
  const bytes = Buffer.from(await response.arrayBuffer());
  const expected = await fs.readFile(file);
  assert.equal(response.status, 200, `${relative}: ${response.status} ${bytes.subarray(0, 100)}`);
  assert.equal(response.headers.get('x-textspace-static-root'), 'verified-publication', 'Unexpected HTTP server');
  assert.equal(createHash('sha256').update(bytes).digest('hex'), createHash('sha256').update(expected).digest('hex'), `${relative}: server returned different bytes`);
  console.log('STATIC VERIFIED', relative, bytes.length);
  results.push({ relative, bytes: bytes.length });
  if (path.basename(file) === 'index.html') console.log('INDEX HTML', expected.toString());
}
// The published index must not reference an absent old runtime fingerprint.
const html = await fs.readFile(path.join(root, 'index.html'), 'utf8');
for (const match of html.matchAll(/(?:src|href)=["']([^"']+)["']/g)) {
  const url = new URL(match[1], base);
  if (url.origin !== new URL(base).origin || url.hash) continue;
  const response = await fetch(url, { redirect: 'manual' });
  console.log('INDEX REFERENCE', url.pathname, response.status);
  assert.equal(response.status, 200, `Broken index reference: ${url}`);
}
await fs.writeFile('artifacts/publication-check.json', JSON.stringify({ base, results }, null, 2));
