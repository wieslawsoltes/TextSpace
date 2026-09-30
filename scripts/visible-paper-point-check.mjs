import assert from 'node:assert/strict';
import { visiblePaperPoint } from './visible-paper-point.mjs';

const viewport = { width: 1440, height: 1000 };
const base = { canvas: { x: 0, y: 197, width: 1424, height: 762, paperLeft: 304, scale: 4 / 3, scrollY: 0 },
  pageGeometry: [{ left: 0, top: 0, width: 612, height: 792, blank: false }] };
function inside(state, target) {
  assert.ok(target);
  const c = state.canvas, p = state.pageGeometry[target.pageIndex];
  const left = c.x + c.paperLeft + p.left * c.scale;
  const top = c.y + 18 + p.top * c.scale - c.scrollY;
  assert.ok(target.x > Math.max(0, c.x, left) && target.x < Math.min(viewport.width, c.x + c.width, left + p.width * c.scale));
  assert.ok(target.y > Math.max(0, c.y, top) && target.y < Math.min(viewport.height, c.y + c.height, top + p.height * c.scale));
}
inside(base, visiblePaperPoint(base, viewport));
// Exact CI regression: the old fixed point was y=135, above the canvas at y=197.
const scrolled = structuredClone(base); scrolled.canvas.scrollY = 180;
inside(scrolled, visiblePaperPoint(scrolled, viewport));
assert.equal(visiblePaperPoint(scrolled, viewport).y, 205);
const horizontal = structuredClone(scrolled); horizontal.canvas.paperLeft = -300;
inside(horizontal, visiblePaperPoint(horizontal, viewport));
const later = structuredClone(base); later.canvas.scrollY = 1200;
later.pageGeometry.push({ left: 90, top: 816, width: 792, height: 612, blank: false });
assert.equal(visiblePaperPoint(later, viewport).pageIndex, 1); inside(later, visiblePaperPoint(later, viewport));
const blank = structuredClone(base); blank.pageGeometry[0].blank = true;
assert.equal(visiblePaperPoint(blank, viewport), null);
assert.equal(visiblePaperPoint(null, viewport), null);
assert.equal(visiblePaperPoint(base, null), null);
for (const key of Object.keys(base.canvas)) {
  const invalid = structuredClone(base); invalid.canvas[key] = NaN;
  assert.equal(visiblePaperPoint(invalid, viewport), null);
}
for (const scale of [0.2, 0.5, 1, 4 / 3, 2, 4]) {
  for (const scrollY of [0, 100, 180, 500, 800, 1100, 2000]) {
    const value = structuredClone(later); value.canvas.scale = scale; value.canvas.scrollY = scrollY;
    const target = visiblePaperPoint(value, viewport);
    if (target) inside(value, target);
  }
}
console.log('PASS visible paper pointer geometry: scrolling, zoom, mixed pages, clipping and unarranged views');
