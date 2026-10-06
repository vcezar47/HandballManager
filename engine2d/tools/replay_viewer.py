"""
Replay viewer for the 2D match engine.

Turns a replay exported by Engine2DTests.ExportReplay (HM_ENGINE2D_REPLAY=<file.json>) into a
self-contained HTML page that plays it back on a canvas, drawn the way the game draws the
court: play/pause, speed, scrubbing, and the commentary alongside.

    python tools/replay_viewer.py replay.json replay.html
"""
import json
import sys

TEMPLATE = r"""<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>2D Engine Replay</title>
<style>
  :root { --bg: #1A1A2E; --card: #16213E; --text: #EAEAEA; --muted: #9AA0B4; --accent: #E94560; }
  body { margin: 0; background: var(--bg); color: var(--text); font: 14px/1.4 system-ui, sans-serif; }
  header { display: flex; align-items: center; gap: 12px; padding: 10px 16px; flex-wrap: wrap; }
  .score { font-size: 22px; font-weight: 700; color: #FFD54F; }
  .clock { font-variant-numeric: tabular-nums; color: var(--muted); }
  main { display: grid; grid-template-columns: minmax(0, 1fr) 300px; gap: 12px; padding: 0 16px 16px; }
  @media (max-width: 800px) { main { grid-template-columns: 1fr; } }
  canvas { width: 100%; height: auto; display: block; border-radius: 8px; }
  #feed { background: var(--card); border-radius: 8px; padding: 8px 10px; max-height: 70vh; overflow: auto; font-size: 12px; }
  #feed div { padding: 3px 0; border-bottom: 1px solid #ffffff14; }
  #feed .goal { color: #FFD54F; font-weight: 600; }
  .controls { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
  button { background: #ffffff22; color: var(--text); border: 0; border-radius: 6px; padding: 6px 10px; cursor: pointer; }
  button.on { background: var(--accent); }
  input[type=range] { width: 220px; }
</style></head>
<body>
<header>
  <span id="home"></span><span class="score" id="score">0 – 0</span><span id="away"></span>
  <span class="clock" id="clock">00:00</span>
  <div class="controls">
    <button id="play">Pause</button>
    <button data-speed="1" class="on">1×</button><button data-speed="2">2×</button><button data-speed="4">4×</button><button data-speed="16">16×</button>
    <input type="range" id="scrub" min="0" value="0">
  </div>
</header>
<main><canvas id="c" width="1300" height="720"></canvas><div id="feed"></div></main>
<script>
const R = __DATA__;
const P = new Map(R.players.map(p => [p.id, p]));
const th = R.theme;
const cv = document.getElementById('c'), g = cv.getContext('2d');
const MINX = -1.8, MAXX = 41.8, MINY = -2.6, MAXY = 20.8;
let scale, ox, oy;
function fit() {
  scale = Math.min(cv.width / (MAXX - MINX), cv.height / (MAXY - MINY));
  ox = (cv.width - (MAXX - MINX) * scale) / 2 - MINX * scale;
  oy = (cv.height - (MAXY - MINY) * scale) / 2 + MAXY * scale;
}
const X = x => ox + x * scale, Y = y => oy - y * scale, M = m => m * scale;
function areaPath(gx, dir, r, close) {
  const lo = 8.5, hi = 11.5; g.beginPath();
  let s = -Math.PI / 2; if (lo - r < 0) s = Math.asin(-lo / r);
  for (let i = 0; i <= 24; i++) { const a = s + (0 - s) * i / 24; const px = X(gx + dir * r * Math.cos(a)), py = Y(lo + r * Math.sin(a)); i ? g.lineTo(px, py) : g.moveTo(px, py); }
  g.lineTo(X(gx + dir * r), Y(hi));
  let e = Math.PI / 2; if (hi + r > 20) e = Math.asin((20 - hi) / r);
  for (let i = 0; i <= 24; i++) { const a = e * i / 24; g.lineTo(X(gx + dir * r * Math.cos(a)), Y(hi + r * Math.sin(a))); }
  if (close) g.closePath();
}
function court() {
  g.fillStyle = th.Surround; g.fillRect(0, 0, cv.width, cv.height);
  g.fillStyle = th.Floor; g.fillRect(X(0), Y(20), M(40), M(20));
  for (const [gx, dir] of [[0, 1], [40, -1]]) { g.fillStyle = th.GoalArea; areaPath(gx, dir, 6, true); g.fill(); }
  g.strokeStyle = th.Lines; g.lineWidth = 2; g.setLineDash([]);
  g.strokeRect(X(0), Y(20), M(40), M(20));
  g.beginPath(); g.moveTo(X(20), Y(0)); g.lineTo(X(20), Y(20)); g.stroke();
  g.beginPath(); g.arc(X(20), Y(10), M(2), 0, Math.PI * 2); g.stroke();
  for (const [gx, dir] of [[0, 1], [40, -1]]) {
    g.strokeStyle = th.Lines; areaPath(gx, dir, 6, false); g.stroke();
    g.strokeStyle = th.NineMetre; g.setLineDash([8, 6]); areaPath(gx, dir, 9, false); g.stroke(); g.setLineDash([]);
    g.strokeStyle = th.Lines; g.beginPath(); g.moveTo(X(gx + dir * 7), Y(9.5)); g.lineTo(X(gx + dir * 7), Y(10.5)); g.stroke();
    g.fillStyle = '#ffffff40'; g.fillRect(Math.min(X(gx), X(gx - dir)), Y(11.5), M(1), M(3));
    g.strokeStyle = '#E53935'; g.lineWidth = 4; g.setLineDash([5, 5]); g.beginPath(); g.moveTo(X(gx), Y(11.5)); g.lineTo(X(gx), Y(8.5)); g.stroke(); g.setLineDash([]); g.lineWidth = 2;
  }
  g.fillStyle = '#00000055'; g.fillRect(X(8), Y(-0.9), M(9.3), M(0.9)); g.fillRect(X(22.7), Y(-0.9), M(9.3), M(0.9));
}
function frame(i) {
  const f = R.frames[i]; court();
  const r = Math.max(7, M(0.42));
  const onCourt = f.p.filter(q => q[3] === 1 || q[3] === 2);
  for (const q of f.p.filter(q => !(q[3] === 1 || q[3] === 2)).concat(onCourt)) {
    const pl = P.get(q[0]); const x = X(q[1]), y = Y(q[2]);
    const shirt = pl.gk ? (pl.side ? R.kits.awayGk : R.kits.homeGk) : (pl.side ? R.kits.away : R.kits.home);
    const num = pl.gk ? '#111' : (pl.side ? R.kits.awayNum : R.kits.homeNum);
    g.globalAlpha = (q[3] >= 3) ? 0.45 : 1;
    g.fillStyle = '#00000055'; g.beginPath(); g.arc(x + 1.5, y + 2.5, r, 0, 7); g.fill();
    g.fillStyle = shirt; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill();
    g.strokeStyle = q[0] === f.b[3] ? '#fff' : '#000a'; g.lineWidth = q[0] === f.b[3] ? 2.5 : 1; g.stroke();
    g.fillStyle = num; g.font = `bold ${Math.round(r * 1.05)}px system-ui`; g.textAlign = 'center'; g.textBaseline = 'middle';
    g.fillText(pl.n, x, y + 1); g.globalAlpha = 1;
  }
  for (const rf of (f.r || [])) {
    const x = X(rf[0]), y = Y(rf[1]);
    g.fillStyle = '#111'; g.beginPath(); g.arc(x, y, r * 0.8, 0, 7); g.fill();
    g.strokeStyle = '#FF4081'; g.lineWidth = 2; g.stroke();
  }
  const bx = X(f.b[0]), by = Y(f.b[1]), br = Math.max(4, M(0.19));
  g.fillStyle = '#0006'; g.beginPath(); g.ellipse(bx, by, br, br / 2, 0, 0, 7); g.fill();
  g.fillStyle = '#FFD54F'; g.beginPath(); g.arc(bx + (f.b[3] ? r * 0.8 : 0), by - M(f.b[2] * 0.35) - (f.b[3] ? r * 0.6 : 0), br, 0, 7); g.fill();
  g.strokeStyle = '#1565C0'; g.lineWidth = 1.2; g.stroke();
  document.getElementById('score').textContent = `${f.s[0]} – ${f.s[1]}`;
  const m = Math.floor(f.t / 60), s = Math.floor(f.t % 60);
  document.getElementById('clock').textContent = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}
function feedUpTo(t) {
  const el = document.getElementById('feed');
  el.innerHTML = R.events.filter(e => e.t <= t).reverse().map(e =>
    `<div class="${e.text.startsWith('GOAL') ? 'goal' : ''}">${Math.floor(e.t / 60) + 1}′ ${e.text}</div>`).join('');
}
document.getElementById('home').textContent = R.home; document.getElementById('away').textContent = R.away;
fit();
const scrub = document.getElementById('scrub'); scrub.max = R.frames.length - 1;
let i = 0, playing = true, speed = 1, acc = 0, last = performance.now(), lastFeed = -1;
function loop(now) {
  const dt = Math.min(0.1, (now - last) / 1000); last = now;
  if (playing) { acc += dt * speed; while (acc >= 0.1 && i < R.frames.length - 1) { i++; acc -= 0.1; } scrub.value = i; }
  frame(i);
  if (Math.floor(R.frames[i].t) !== lastFeed) { lastFeed = Math.floor(R.frames[i].t); feedUpTo(R.frames[i].t); }
  requestAnimationFrame(loop);
}
document.getElementById('play').onclick = e => { playing = !playing; e.target.textContent = playing ? 'Pause' : 'Play'; };
document.querySelectorAll('[data-speed]').forEach(b => b.onclick = () => {
  speed = +b.dataset.speed; document.querySelectorAll('[data-speed]').forEach(x => x.classList.toggle('on', x === b)); });
scrub.oninput = () => { i = +scrub.value; lastFeed = -1; };
requestAnimationFrame(loop);
</script></body></html>
"""


def main():
    src, dst = sys.argv[1], sys.argv[2]
    data = json.load(open(src, encoding="utf-8"))
    html = TEMPLATE.replace("__DATA__", json.dumps(data, separators=(",", ":")))
    open(dst, "w", encoding="utf-8").write(html)
    print(f"{len(data['frames'])} frames -> {dst}")


if __name__ == "__main__":
    main()
