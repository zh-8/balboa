// Deterministic Excalidraw scene -> SVG renderer for the element subset Balboa uses:
// rectangle, ellipse, diamond, text, line, arrow. Clean geometry on purpose (no roughness):
// reviewable in a PR and stable across machines. Always paints an opaque light background.
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const num = (n) => +n.toFixed(2);
const FONT = { 1: 'Segoe Print, Comic Sans MS, cursive', 2: 'Helvetica, Arial, sans-serif', 3: 'Consolas, Menlo, monospace' };
const BG = '#ffffff';
const PAD = 24;

function paint(e) {
  const fill = e.backgroundColor && e.backgroundColor !== 'transparent' ? e.backgroundColor : 'none';
  const dash = e.strokeStyle === 'dashed' ? ` stroke-dasharray="${e.strokeWidth * 4} ${e.strokeWidth * 3}"`
    : e.strokeStyle === 'dotted' ? ` stroke-dasharray="${e.strokeWidth} ${e.strokeWidth * 3}"` : '';
  const stroke = e.strokeColor === 'transparent' ? 'none' : e.strokeColor;
  return `fill="${fill}" stroke="${stroke}" stroke-width="${e.strokeWidth}"${dash}` +
    (e.opacity != null && e.opacity < 100 ? ` opacity="${e.opacity / 100}"` : '');
}

function absPoints(e) { return (e.points ?? [[0, 0], [e.width, e.height]]).map(([px, py]) => [e.x + px, e.y + py]); }

function arrowhead(tip, from, color, w) {
  const a = Math.atan2(tip[1] - from[1], tip[0] - from[0]);
  const len = 10 + w * 2, spread = Math.PI / 7;
  const p = (d) => `${num(tip[0] - len * Math.cos(a + d))},${num(tip[1] - len * Math.sin(a + d))}`;
  return `<polygon points="${num(tip[0])},${num(tip[1])} ${p(-spread)} ${p(spread)}" fill="${color}" stroke="${color}" stroke-linejoin="round"/>`;
}

function bounds(els) {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  const add = (x, y) => { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); };
  for (const e of els) {
    if (e.type === 'arrow' || e.type === 'line') absPoints(e).forEach(([x, y]) => add(x, y));
    else { add(e.x, e.y); add(e.x + e.width, e.y + e.height); }
  }
  return [x0 - PAD, y0 - PAD, x1 + PAD, y1 + PAD];
}

function text(e) {
  const lines = String(e.text).split('\n');
  const lh = e.fontSize * (e.lineHeight ?? 1.25);
  const anchor = { left: 'start', center: 'middle', right: 'end' }[e.textAlign] ?? 'start';
  const x = e.textAlign === 'center' ? e.x + e.width / 2 : e.textAlign === 'right' ? e.x + e.width : e.x;
  const top = e.verticalAlign === 'middle' ? e.y + (e.height - lh * lines.length) / 2 : e.y;
  const bg = !e.containerId && e.backgroundColor && e.backgroundColor !== 'transparent'
    ? `<rect x="${num(e.x - 3)}" y="${num(e.y - 1)}" width="${num(e.width + 6)}" height="${num(e.height + 2)}" rx="3" fill="${e.backgroundColor}"/>` : '';
  const tspans = lines.map((l, i) =>
    `<tspan x="${num(x)}" y="${num(top + lh * i + e.fontSize)}">${esc(l) || ' '}</tspan>`).join('');
  return `${bg}<text font-family="${FONT[e.fontFamily] ?? FONT[2]}" font-size="${e.fontSize}" text-anchor="${anchor}" fill="${e.strokeColor}">${tspans}</text>`;
}

function shape(e) {
  const p = paint(e);
  switch (e.type) {
    case 'rectangle': {
      const r = e.roundness ? Math.min(12, Math.min(e.width, e.height) / 4) : 0;
      return `<rect x="${num(e.x)}" y="${num(e.y)}" width="${num(e.width)}" height="${num(e.height)}" rx="${num(r)}" ${p}/>`;
    }
    case 'ellipse':
      return `<ellipse cx="${num(e.x + e.width / 2)}" cy="${num(e.y + e.height / 2)}" rx="${num(e.width / 2)}" ry="${num(e.height / 2)}" ${p}/>`;
    case 'diamond': {
      const [cx, cy] = [e.x + e.width / 2, e.y + e.height / 2];
      return `<polygon points="${num(cx)},${num(e.y)} ${num(e.x + e.width)},${num(cy)} ${num(cx)},${num(e.y + e.height)} ${num(e.x)},${num(cy)}" ${p} stroke-linejoin="round"/>`;
    }
    case 'line':
    case 'arrow': {
      const pts = absPoints(e);
      const d = pts.map(([x, y], i) => `${i ? 'L' : 'M'}${num(x)} ${num(y)}`).join(' ');
      let out = `<path d="${d}" fill="none" stroke="${e.strokeColor}" stroke-width="${e.strokeWidth}" stroke-linejoin="round" stroke-linecap="round"${p.includes('dasharray') ? ' ' + /stroke-dasharray="[^"]*"/.exec(p)[0] : ''}/>`;
      if (e.type === 'arrow') {
        if (e.endArrowhead) out += arrowhead(pts.at(-1), pts.at(-2), e.strokeColor, e.strokeWidth);
        if (e.startArrowhead) out += arrowhead(pts[0], pts[1], e.strokeColor, e.strokeWidth);
      }
      return out;
    }
    case 'text': return text(e);
    default: throw new Error(`Unsupported element type "${e.type}" (id ${e.id}). Extend tools/diagrams/render.mjs.`);
  }
}

export function renderSvg(scene, srcHash) {
  const els = (scene.elements ?? []).filter((e) => !e.isDeleted);
  if (!els.length) throw new Error('Scene has no elements.');
  const [x0, y0, x1, y1] = bounds(els);
  const w = Math.ceil(x1 - x0), h = Math.ceil(y1 - y0);
  const body = els.map((e) => `  ${shape(e)}`).join('\n');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="${num(x0)} ${num(y0)} ${w} ${h}" role="img">
<!-- src-sha256:${srcHash} -->
<!-- Generated by tools/diagrams/export.mjs. Do not edit; edit the .excalidraw and run \`make diagrams\`. -->
  <rect x="${num(x0)}" y="${num(y0)}" width="${w}" height="${h}" fill="${BG}"/>
${body}
</svg>
`;
}
