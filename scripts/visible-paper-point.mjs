/** Select a real pointer target inside the intersection of paper, canvas and viewport.
 * Layout positions are in points; canvas bounds and scroll offsets are CSS pixels.
 * Returns null while layout is unavailable or no nonblank page is visible.
 */
export function visiblePaperPoint(state, viewport) {
  const c = state?.canvas;
  if (!c || !viewport || !Array.isArray(state.pageGeometry) ||
      ![c.x, c.y, c.width, c.height, c.paperLeft, c.scrollY, c.scale, viewport.width, viewport.height].every(Number.isFinite) ||
      c.width <= 0 || c.height <= 0 || c.scale <= 0 || viewport.width <= 0 || viewport.height <= 0) return null;
  for (let index = 0; index < state.pageGeometry.length; index++) {
    const p = state.pageGeometry[index];
    if (!p || p.blank || ![p.left, p.top, p.width, p.height].every(Number.isFinite) || p.width <= 0 || p.height <= 0) continue;
    const paperX = c.x + c.paperLeft + p.left * c.scale;
    const paperY = c.y + 18 + p.top * c.scale - c.scrollY;
    const left = Math.max(0, c.x, paperX) + 8;
    const right = Math.min(viewport.width, c.x + c.width, paperX + p.width * c.scale) - 8;
    const top = Math.max(0, c.y, paperY) + 8;
    const bottom = Math.min(viewport.height, c.y + c.height, paperY + p.height * c.scale) - 8;
    if (![left, right, top, bottom].every(Number.isFinite) || right <= left || bottom <= top) continue;
    return { x: Math.max(left, Math.min(right, paperX + 75 * c.scale)),
      y: Math.max(top, Math.min(bottom, paperY + 75 * c.scale)), pageIndex: index };
  }
  return null;
}
