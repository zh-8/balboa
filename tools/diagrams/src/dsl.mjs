// Tiny authoring DSL that emits valid Excalidraw scene JSON. The .excalidraw files are the
// source of truth (editable at excalidraw.com); this just makes them reproducible.
export const PALETTE = {
  blue:   { bg: '#dbe4ff', stroke: '#1864ab' }, // application / compute
  green:  { bg: '#d3f9d8', stroke: '#2b8a3e' }, // data, contracts, allowed
  orange: { bg: '#ffe8cc', stroke: '#d9480f' }, // external systems / actors
  purple: { bg: '#e5dbff', stroke: '#5f3dc4' }, // identity and security
  red:    { bg: '#ffe3e3', stroke: '#c92a2a' }, // forbidden / deferred
  grey:   { bg: '#f1f3f5', stroke: '#495057' }, // neutral / zones
  yellow: { bg: '#fff3bf', stroke: '#e67700' }, // ci / process step
};
const INK = '#1e1e1e';
const CW = 0.56; // average glyph width / font size for Helvetica

export class Diagram {
  constructor(name) { this.name = name; this.els = []; this.byId = new Map(); this.n = 0; }
  _base(type, x, y, w, h, o = {}) {
    const i = ++this.n;
    const e = {
      id: o.id ?? `${this.name}-${i}`, type, x, y, width: w, height: h, angle: 0,
      strokeColor: o.stroke ?? INK, backgroundColor: o.bg ?? 'transparent', fillStyle: 'solid',
      strokeWidth: o.sw ?? 2, strokeStyle: o.dashed ? 'dashed' : 'solid', roughness: 0, opacity: 100,
      groupIds: [], frameId: null, roundness: null, seed: 1000 + i, version: 1, versionNonce: 5000 + i,
      isDeleted: false, boundElements: [], updated: 1760832000000, link: null, locked: false,
    };
    this.els.push(e); this.byId.set(e.id, e); return e;
  }
  /** Free-standing or container-bound text. */
  text(x, y, str, o = {}) {
    const fs = o.size ?? 16, lines = String(str).split('\n');
    const w = Math.max(...lines.map((l) => l.length)) * fs * CW, h = lines.length * fs * 1.25;
    const e = this._base('text', x, y, Math.ceil(w), Math.ceil(h), { stroke: o.color ?? INK, sw: 1, bg: o.bg });
    Object.assign(e, { text: String(str), fontSize: fs, fontFamily: 2, textAlign: o.align ?? 'left',
      verticalAlign: o.container ? 'middle' : 'top', containerId: o.container ?? null,
      originalText: String(str), autoResize: true, lineHeight: 1.25 });
    return e;
  }
  /** Box (rectangle/ellipse/diamond) with a bound, centred label. */
  box(id, x, y, w, h, label, o = {}) {
    const c = PALETTE[o.color ?? 'grey'];
    const e = this._base(o.shape ?? 'rectangle', x, y, w, h, { id, stroke: c.stroke, bg: c.bg, dashed: o.dashed });
    if (e.type !== 'ellipse') e.roundness = { type: 3 };
    if (label) {
      const fs = o.size ?? 16, lines = label.split('\n');
      const need = Math.max(...lines.map((l) => l.length)) * fs * 0.52;
      if (need > w - 12) console.warn(`  [fit] ${id}: needs ~${Math.ceil(need)}px, box is ${w}px`);
      const t = this.text(0, 0, label, { size: fs, align: 'center', container: id });
      // first line is the title: keep it simple, one font size per box.
      t.x = x + (w - t.width) / 2; t.y = y + (h - t.height) / 2;
      e.boundElements.push({ type: 'text', id: t.id });
    }
    return e;
  }
  /** Large dashed zone with a title in its top-left corner (no bound text). */
  zone(id, x, y, w, h, title, o = {}) {
    const c = PALETTE[o.color ?? 'grey'];
    const e = this._base('rectangle', x, y, w, h, { id, stroke: c.stroke, bg: 'transparent', dashed: true, sw: 1 });
    e.roundness = { type: 3 };
    this.text(x + 12, y + 8, title, { size: 14, color: c.stroke });
    return e;
  }
  /** Anchor on a box edge: ('id', 't'|'b'|'l'|'r', offsetAlongEdge) or [x, y, side]. */
  _anchor(a) {
    if (typeof a[0] === 'number') return { x: a[0], y: a[1], side: a[2], id: null };
    const [id, side, off = 0] = a;
    const b = this.byId.get(id);
    if (!b) throw new Error(`unknown box ${id}`);
    const cx = b.x + b.width / 2, cy = b.y + b.height / 2;
    const p = { t: [cx + off, b.y], b: [cx + off, b.y + b.height], l: [b.x, cy + off], r: [b.x + b.width, cy + off] }[side];
    return { x: p[0], y: p[1], side, id };
  }
  /** Arrow between anchors, orthogonal routing. `via` overrides the auto-routed middle points. */
  arrow(from, to, o = {}) {
    const a = this._anchor(from), b = this._anchor(to);
    const horiz = (s) => s === 'l' || s === 'r';
    let mid = o.via;
    if (!mid) {
      if (horiz(a.side) && horiz(b.side)) { const mx = o.midX ?? (a.x + b.x) / 2; mid = a.y === b.y ? [] : [[mx, a.y], [mx, b.y]]; }
      else if (!horiz(a.side) && !horiz(b.side)) { const my = o.midY ?? (a.y + b.y) / 2; mid = a.x === b.x ? [] : [[a.x, my], [b.x, my]]; }
      else mid = horiz(a.side) ? [[b.x, a.y]] : [[a.x, b.y]];
    }
    const abs = [[a.x, a.y], ...mid, [b.x, b.y]];
    const xs = abs.map((p) => p[0]), ys = abs.map((p) => p[1]);
    const c = o.color ? PALETTE[o.color].stroke : INK;
    const e = this._base('arrow', a.x, a.y, Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys),
      { stroke: c, dashed: o.dashed, sw: o.sw ?? 2 });
    Object.assign(e, {
      points: abs.map(([x, y]) => [x - a.x, y - a.y]), lastCommittedPoint: null,
      startBinding: a.id ? { elementId: a.id, focus: 0, gap: 1 } : null,
      endBinding: b.id ? { elementId: b.id, focus: 0, gap: 1 } : null,
      startArrowhead: o.both ? 'arrow' : null, endArrowhead: 'arrow', elbowed: false,
    });
    for (const [x, id] of [[a, a.id], [b, b.id]]) if (id) this.byId.get(id).boundElements.push({ type: 'arrow', id: e.id });
    if (o.label) {
      const lines = o.label.split('\n');
      const w = Math.max(...lines.map((l) => l.length)) * 13 * CW;
      // label sits on the longest segment (or at lx,ly if given)
      let best = [abs[0], abs[1]], len = -1;
      for (let i = 0; i < abs.length - 1; i++) {
        const l = Math.hypot(abs[i + 1][0] - abs[i][0], abs[i + 1][1] - abs[i][1]);
        if (l > len) { len = l; best = [abs[i], abs[i + 1]]; }
      }
      const lx = o.lx ?? (best[0][0] + best[1][0]) / 2, ly = o.ly ?? (best[0][1] + best[1][1]) / 2;
      this.text(lx - w / 2, ly - (lines.length * 13 * 1.25) / 2, o.label, { size: 13, align: 'center', color: o.color ? PALETTE[o.color].stroke : INK, bg: '#ffffff' });
    }
    return e;
  }
  legend(x, y, items) {
    this.text(x, y, 'Legend', { size: 14, color: '#495057' });
    items.forEach(([color, label], i) => {
      const c = PALETTE[color];
      const sw = this._base('rectangle', x, y + 26 + i * 26, 22, 16, { stroke: c.stroke, bg: c.bg });
      sw.roundness = { type: 3 };
      this.text(x + 32, y + 25 + i * 26, label, { size: 13 });
    });
  }
  scene() {
    return {
      type: 'excalidraw', version: 2, source: 'https://excalidraw.com',
      elements: this.els,
      appState: { gridSize: null, viewBackgroundColor: '#ffffff' },
      files: {},
    };
  }
}
