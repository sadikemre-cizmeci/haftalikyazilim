// ============================================================================
// HaftalıkYazılım logo üreteci  (v2)
// ----------------------------------------------------------------------------
// Fırça darbesi gibi görünen AÇIK bir daire:
//   • başta boya "sıkılmış" gibi yuvarlak, koyu bir topak
//   • gövde kalın ve kendinden emin, kenarları organik
//   • gövdede kuru fırçanın atladığı küçük boşluklar
//   • soldan içeri kıvrılan kuyruk: kıllar ayrılıyor, incelerek bitiyor (fırça kalkışı)
//   • kuyrukta kırmızının yanından canlı renkler şerit şerit taşıyor
//
// SVG'de stroke sabit kalınlıktadır → darbeyi merkez çizgisinin iki yanına dik
// kaydırma yaparak bir DOLGU (fill) konturu olarak üretiyoruz.
// ============================================================================

import { writeFileSync } from "node:fs";

const CX = 100, CY = 100, R = 64;
const D2R = Math.PI / 180;

// --- Merkez çizgisi: 198° → 585° (saat yönünde ~387°) ---
const A0 = 198 * D2R;
const A1 = 585 * D2R;
const CURL_START = 0.72;   // bu orandan sonra yarıçap küçülür (içeri kıvrılma)
const CURL_END_R = 0.40;   // kuyruk bu yarıçap oranında biter (sketch'teki gibi içeride)

function centerline(t) {
  const a = A0 + (A1 - A0) * t;

  let r = R;
  if (t > CURL_START) {
    const u = (t - CURL_START) / (1 - CURL_START);
    const e = u * u * (3 - 2 * u);                    // smoothstep
    r = R - (R - R * CURL_END_R) * e;
  }
  // Elle çizilmiş hissi — ölçülü, "özensiz" değil "kendinden emin"
  r += Math.sin(a * 1.7 + 1.1) * 1.15 + Math.sin(a * 3.1 + 0.5) * 0.5;

  return { x: CX + r * Math.cos(a), y: CY + r * Math.sin(a) };
}

// --- Kalınlık profili (kalın fırça) ---
function width(t) {
  let w = 13.4;

  // Basınç dalgalanması
  w += Math.sin(t * Math.PI * 4.3 + 0.7) * 1.35;
  w += Math.sin(t * Math.PI * 9.7 + 2.1) * 0.6;

  // BAŞLANGIÇ TOPAĞI: boya sıkılmış, üzerinden geçilmiş
  w += Math.exp(-Math.pow((t - 0.024) / 0.055, 2)) * 10.5;
  if (t < 0.01) w *= 0.62 + (t / 0.01) * 0.38;        // fırça yeni değiyor

  // KUYRUK: geç başlar, önce hafif incelir, sonra hızla sivrilir
  const TAIL = 0.79;
  if (t > TAIL) {
    const u = (t - TAIL) / (1 - TAIL);
    w *= Math.max(1 - Math.pow(u, 2.3), 0.04);
  }

  return Math.max(w, 0.35);
}

// --- Kontur üretici ---
function strokeOutline(tFrom, tTo, widthFn, offset = 0, samples = 300) {
  const left = [], right = [];

  for (let i = 0; i <= samples; i++) {
    const t = tFrom + (tTo - tFrom) * (i / samples);
    const p = centerline(t);

    const d = 0.0008;
    const p1 = centerline(Math.min(1, t + d));
    const p0 = centerline(Math.max(0, t - d));
    let tx = p1.x - p0.x, ty = p1.y - p0.y;
    const len = Math.hypot(tx, ty) || 1;
    tx /= len; ty /= len;
    const nx = -ty, ny = tx;

    const w = widthFn(t) / 2;
    const ox = nx * offset, oy = ny * offset;

    left.push([p.x + ox + nx * w, p.y + oy + ny * w]);
    right.push([p.x + ox - nx * w, p.y + oy - ny * w]);
  }

  const f = (n) => n.toFixed(1);
  let d = `M${f(left[0][0])} ${f(left[0][1])}`;
  for (let i = 1; i < left.length; i++) d += `L${f(left[i][0])} ${f(left[i][1])}`;
  for (let i = right.length - 1; i >= 0; i--) d += `L${f(right[i][0])} ${f(right[i][1])}`;
  return d + "Z";
}

// --- Kuyrukta ayrılan tek bir kıl ---
function bristle({ from, to, offset, scale, fadeFrom, samples = 130 }) {
  const wf = (t) => {
    const base = width(t) * scale;
    const u = Math.max(0, (t - fadeFrom) / (1 - fadeFrom));
    return Math.max(base * (1 - Math.pow(u, 1.5)), 0.15);
  };
  return strokeOutline(from, to, wf, offset, samples);
}

// --- Başlangıç topağı: düzensiz, yuvarlak bir boya kütlesi ---
function blob(t, rx, ry, rot, wobble = 0.16) {
  const c = centerline(t);
  const pts = [];
  const N = 46;
  for (let i = 0; i < N; i++) {
    const a = (i / N) * Math.PI * 2;
    const k = 1
      + Math.sin(a * 3 + rot) * wobble
      + Math.sin(a * 5 + rot * 2.3) * wobble * 0.5;
    const x = Math.cos(a) * rx * k;
    const y = Math.sin(a) * ry * k;
    // döndür
    const xr = x * Math.cos(rot) - y * Math.sin(rot);
    const yr = x * Math.sin(rot) + y * Math.cos(rot);
    pts.push([c.x + xr, c.y + yr]);
  }
  const f = (n) => n.toFixed(1);
  return "M" + pts.map((p, i) => `${i ? "L" : ""}${f(p[0])} ${f(p[1])}`).join("") + "Z";
}

// ============================================================================
const mainPath = strokeOutline(0, 1, width, 0, 340);

// Başlangıç: ana topak + iki küçük sıçrama
const startBlob = blob(0.022, 12.2, 10.4, 0.6);
const startSpec1 = blob(0.062, 4.2, 3.2, 1.9, 0.26);
const startSpec2 = blob(0.003, 6.0, 4.8, -0.8, 0.28);

// Kuyrukta ayrılan kırmızı kıllar — farklı boylarda bitsin (kopukluk)
const redBristles = [
  bristle({ from: 0.775, to: 1.00,  offset: 3.4,  scale: 0.40, fadeFrom: 0.90 }),
  bristle({ from: 0.79,  to: 0.985, offset: -3.3, scale: 0.36, fadeFrom: 0.91 }),
  bristle({ from: 0.80,  to: 1.00,  offset: 1.2,  scale: 0.30, fadeFrom: 0.93 }),
  bristle({ from: 0.815, to: 0.955, offset: -1.6, scale: 0.20, fadeFrom: 0.90 }),
  bristle({ from: 0.825, to: 0.975, offset: 6.2,  scale: 0.16, fadeFrom: 0.90 }),
];

// Kuyruğun yanlarından taşan canlı renkler — ŞERİT gibi, kıllara paralel
const colorBristles = [
  { d: bristle({ from: 0.755, to: 1.00,  offset: 4.3,  scale: 0.17, fadeFrom: 0.90 }), c: "#f59e0b", o: 0.79 },
  { d: bristle({ from: 0.765, to: 0.995, offset: -4.2, scale: 0.16, fadeFrom: 0.91 }), c: "#e8368f", o: 0.76 },
  { d: bristle({ from: 0.785, to: 1.00,  offset: 5.9,  scale: 0.12, fadeFrom: 0.92 }), c: "#ffd166", o: 0.79 },
  { d: bristle({ from: 0.795, to: 0.99,  offset: -5.8, scale: 0.11, fadeFrom: 0.92 }), c: "#7c4dff", o: 0.69 },
  { d: bristle({ from: 0.81,  to: 0.985, offset: 7.1,  scale: 0.09, fadeFrom: 0.90 }), c: "#12b3a6", o: 0.64 },
  { d: bristle({ from: 0.82,  to: 1.00,  offset: -7.0, scale: 0.085, fadeFrom: 0.93 }), c: "#ff7043", o: 0.69 },
];

// Gövdede kuru fırçanın atladığı boşluklar
const skips = [];
for (const [t, len, off, w] of [
  [0.175, 0.030, 3.0, 2.3],
  [0.335, 0.021, -3.4, 1.8],
  [0.495, 0.033, 2.0, 2.7],
  [0.60,  0.019, -2.4, 1.7],
  [0.685, 0.026, 3.6, 2.0],
]) {
  skips.push(strokeOutline(t, t + len, () => w, off, 20));
}

const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200" role="img" aria-label="HaftalıkYazılım">
<title>HaftalıkYazılım</title>
<defs>
<filter id="hy-b" x="-25%" y="-25%" width="150%" height="150%" color-interpolation-filters="sRGB">
<feTurbulence type="fractalNoise" baseFrequency="0.05 0.07" numOctaves="4" seed="7" result="n"/>
<feDisplacementMap in="SourceGraphic" in2="n" scale="3.2" xChannelSelector="R" yChannelSelector="G"/>
</filter>
<filter id="hy-t" x="-45%" y="-45%" width="190%" height="190%" color-interpolation-filters="sRGB">
<feTurbulence type="fractalNoise" baseFrequency="0.085 0.11" numOctaves="3" seed="19" result="n"/>
<feDisplacementMap in="SourceGraphic" in2="n" scale="2.0" xChannelSelector="R" yChannelSelector="G"/>
</filter>
<linearGradient id="hy-r" x1="15%" y1="8%" x2="72%" y2="96%">
<stop offset="0%" stop-color="#a51616"/>
<stop offset="18%" stop-color="#c62828"/>
<stop offset="60%" stop-color="#e03131"/>
<stop offset="100%" stop-color="#ef5350"/>
</linearGradient>
<mask id="hy-d" maskUnits="userSpaceOnUse" x="0" y="0" width="200" height="200">
<rect width="200" height="200" fill="#fff"/>
${skips.map((d) => `<path d="${d}" fill="#000" opacity="0.8"/>`).join("")}
</mask>
</defs>

<!-- 1) Ana fırça darbesi -->
<g filter="url(#hy-b)">
<path d="${mainPath}" fill="url(#hy-r)" mask="url(#hy-d)"/>
<path d="${startBlob}" fill="#a51616"/>
<path d="${startSpec2}" fill="#8f1212" opacity="0.9"/>
<path d="${startSpec1}" fill="#b81d1d" opacity="0.75"/>
</g>

<!-- 2) Kuyrukta ayrılan kırmızı kıllar -->
<g filter="url(#hy-t)" fill="#e03131">
${redBristles.map((d, i) => `<path d="${d}" opacity="${(0.92 - i * 0.1).toFixed(2)}"/>`).join("")}
</g>

<!-- 3) Kuyruktan taşan canlı renkler -->
<g filter="url(#hy-t)">
${colorBristles.map((b) => `<path d="${b.d}" fill="${b.c}" opacity="${b.o}"/>`).join("")}
</g>
</svg>
`;

writeFileSync(process.argv[2], svg);
console.log(`yazıldı: ${process.argv[2]} (${(svg.length / 1024).toFixed(1)} KB)`);
