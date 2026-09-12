// ============================================================================
// format-text.js — özet gibi kısa metinler için HAFİF biçimlendirme
// ----------------------------------------------------------------------------
// Zengin metin editörü YOK (tek bir <textarea>). Yine de kullanıcı üç basit
// kuralla okunaklı bir sonuç alır:
//
//   • Boş satır          → yeni paragraf
//   • "- ", "* ", "• "   → madde işaretli liste satırı
//   • **kalın**          → <strong>
//
// Güvenlik: metin HER ZAMAN DOM metin düğümü olarak eklenir (el() → innerHTML
// DEĞİL), yani hiçbir yerde HTML enjeksiyonu riski yok.
// ============================================================================

import { el } from "./dom.js";

const BULLET_RE = /^[-*•]\s+(.*)$/;

/**
 * Ham özet metnini biçimlendirilmiş blok düğümlerine (<p>/<ul>) çevirir.
 * @param {string} raw
 * @returns {Node[]}
 */
export function formatText(raw) {
  const text = (raw || "").trim();
  if (!text) return [];

  const lines = text.replace(/\r\n?/g, "\n").split("\n");
  const blocks = [];
  let paraLines = [];
  let listItems = [];

  const flushPara = () => {
    if (!paraLines.length) return;
    blocks.push(el("p", { class: "fmt-p" }, joinWithBreaks(paraLines)));
    paraLines = [];
  };
  const flushList = () => {
    if (!listItems.length) return;
    blocks.push(el("ul", { class: "fmt-list" }, listItems.map((t) => el("li", {}, inlineNodes(t)))));
    listItems = [];
  };

  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (line === "") { flushPara(); flushList(); continue; }

    const bullet = line.match(BULLET_RE);
    if (bullet) {
      flushPara();
      listItems.push(bullet[1]);
    } else {
      flushList();
      paraLines.push(line);
    }
  }
  flushPara();
  flushList();

  return blocks;
}

/** Bir paragraf içindeki satırları <br> ile birleştirir. */
function joinWithBreaks(lines) {
  const nodes = [];
  lines.forEach((line, i) => {
    if (i > 0) nodes.push(el("br"));
    nodes.push(...inlineNodes(line));
  });
  return nodes;
}

/** Satır içi **kalın** işaretleyicisini <strong>'e çevirir; gerisi düz metindir. */
function inlineNodes(text) {
  return text
    .split(/(\*\*[^*]+\*\*)/g)
    .filter((part) => part !== "")
    .map((part) => {
      const m = part.match(/^\*\*([^*]+)\*\*$/);
      return m ? el("strong", {}, [m[1]]) : part;
    });
}
