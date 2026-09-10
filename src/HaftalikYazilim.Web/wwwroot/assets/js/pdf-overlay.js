// ============================================================================
// pdf-overlay.js — sayfa ÜSTÜNE açılan PDF önizleme katmanı
// ----------------------------------------------------------------------------
// Yazı düzenleme formunda "Görüntüle" bununla çalışır: yeni sekme AÇMAZ,
// başka sayfaya GİTMEZ (form ve seçili dosya kaybolmaz). Aynı PdfViewer'ı
// tam ekran bir katmanın içinde çalıştırır; "Çık" ya da Esc kapatır.
//
//   openPdfOverlay({ url })                        → henüz kaydedilmemiş (blob) PDF
//   openPdfOverlay({ url, article: {id, title} })  → kayıtlı yazının PDF'i (+ Paylaş)
// ============================================================================

import { PdfViewer } from "./pdf-viewer.js";
import { el } from "./dom.js";
import { shareButton } from "./social.js";

let active = null;

export function openPdfOverlay({ url, article = null } = {}) {
  closePdfOverlay();

  const inner = el("div", { class: "pdf-overlay__viewer" });
  const overlay = el("div", { class: "pdf-overlay", role: "dialog", "aria-modal": "true" }, [inner]);
  document.body.appendChild(overlay);
  document.body.style.overflow = "hidden";   // arka sayfa kaymasın

  const viewer = new PdfViewer(inner, {
    url,
    downloadUrl: null,                        // önizlemede indirme yok
    toolbarExtra: article ? shareButton(article, { compact: true }) : null,
    onExit: closePdfOverlay,                  // araç çubuğundaki "Çık"
  });
  viewer.load();

  const onKey = (e) => { if (e.key === "Escape") closePdfOverlay(); };
  document.addEventListener("keydown", onKey);

  active = { overlay, viewer, onKey };
}

export function closePdfOverlay() {
  if (!active) return;
  document.removeEventListener("keydown", active.onKey);
  try { active.viewer?.destroy(); } catch { /* yok say */ }
  active.overlay.remove();
  document.body.style.overflow = "";
  active = null;
}
