// ============================================================================
// pages/yazi-duzenle.js — YENİ YAZI / YAZI DÜZENLE formu
// ----------------------------------------------------------------------------
//   yazi-duzenle.html            → yeni yazı
//   yazi-duzenle.html?id=123     → var olan yazıyı düzenle
//
// Kurallar:
//   • "Taslak olarak kaydet"  → başlık ve PDF İSTEĞE BAĞLI (sonra tamamlanır)
//   • "Yayınla"               → başlık ve PDF ZORUNLU (yoksa alanın altında kibar uyarı)
//   • Özet 500 karakteri aşarsa canlı uyarı; altına inince uyarı kendiliğinden kalkar
//   • Bir yazıda TEK PDF olur. Seçilen dosya "kart" olarak görünür; yanlışsa çöp
//     kutusuyla kaldırılır.
//   • Gönderimde: dosya varsa gerçek yüzdeli çubuk, yoksa "çalışıyor" animasyonu.
//     Kayıttan sonra düğme yeşil "✓ …" olur (üst üste basmayı da engeller).
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { qs, el, render, setYear, toast, formatBytes, queryParam } from "../dom.js";
import { requireAuth } from "../session.js";
import { ICONS } from "../icons.js";
import { t } from "../i18n.js";
import { openPdfOverlay } from "../pdf-overlay.js";

const articleId = queryParam("id");
const isEdit = Boolean(articleId);

let form, titleInput, summaryInput, pdfInput, errorBox, formActions;
let titleWarn, summaryWarn, pdfWarn;
let progWrap, progFill, progLabel;

let currentPdf = null;   // sunucudaki mevcut PDF → { name, size }
let pendingFile = null;  // kullanıcının yeni seçtiği, kaydedilmemiş PDF (File)
let pendingUrl = null;   // pendingFile'ın önizleme için blob adresi
let savedId = articleId; // yeni yazı kaydedilince buraya id yazılır

async function init() {
  const user = await requireAuth();
  if (!user) return;

  renderNav("profilim");
  setYear();

  form = qs("#article-form");
  titleInput = qs("#article-title");
  summaryInput = qs("#article-summary");
  pdfInput = qs("#article-pdf");
  errorBox = qs("#article-error");
  formActions = qs("#form-actions");
  titleWarn = qs("#title-warn");
  summaryWarn = qs("#summary-warn");
  pdfWarn = qs("#pdf-warn");
  progWrap = qs("#upload-progress");
  progFill = qs("#upload-progress .upload-progress__track > span");
  progLabel = qs("#upload-progress .upload-progress__label");

  qs("#form-title").textContent = isEdit ? t("yaziDuzenle.headingEdit") : t("yaziDuzenle.headingNew");

  titleInput.addEventListener("input", () => hide(titleWarn));
  summaryInput.addEventListener("input", checkSummary);
  pdfInput.addEventListener("change", onPickFile);
  form.addEventListener("submit", onSubmit);

  if (isEdit) await loadExisting();
  else renderPdfSlot();
}

async function loadExisting() {
  let article;
  try {
    article = await api.get(`/api/articles/${articleId}`);
  } catch {
    showError(t("yaziDuzenle.notFound"));
    form.hidden = true;
    return;
  }
  titleInput.value = article.title || "";
  summaryInput.value = article.summary || "";
  currentPdf = article.originalFileName
    ? { name: article.originalFileName, size: article.fileSizeBytes }
    : null;
  renderPdfSlot();
}

// --------------------------------------------------------------------
// Uyarılar (alanların altında sade metin)
// --------------------------------------------------------------------
function show(node, msg) { node.textContent = msg; node.hidden = false; }
function hide(node) { node.hidden = true; }

function checkSummary() {
  const len = summaryInput.value.length;
  if (len > 500) show(summaryWarn, t("yaziDuzenle.summaryTooLong", { count: len }));
  else hide(summaryWarn);
}

// --------------------------------------------------------------------
// PDF alanı
// --------------------------------------------------------------------
function hasPdf() { return !!pendingFile || !!currentPdf; }

function renderPdfSlot() {
  const slot = qs("#pdf-slot");

  const viewBtn = (onClick) => el("button", {
    class: "btn btn--sm", type: "button", onclick: onClick,
  }, [el("span", { class: "btn__ico", html: ICONS.open }), t("yaziDuzenle.pdfView")]);

  if (pendingFile) {
    render(slot, pdfCard({
      name: pendingFile.name,
      size: pendingFile.size,
      actions: [viewBtn(() => openPdfOverlay({ url: pendingUrl }))],
      onRemove: clearPending,
    }));
    return;
  }

  if (currentPdf) {
    render(slot, pdfCard({
      name: currentPdf.name,
      size: currentPdf.size,
      actions: [
        viewBtn(() => openPdfOverlay({
          url: `/api/articles/${savedId}/file`,
          article: { id: savedId, title: titleInput.value.trim() },
        })),
        el("button", { class: "btn btn--sm btn--ghost", type: "button", onclick: () => pdfInput.click() }, [t("yaziDuzenle.pdfReplace")]),
      ],
    }));
    return;
  }

  render(slot, el("button", {
    class: "btn pdf-pick", type: "button", onclick: () => pdfInput.click(),
  }, [el("span", { class: "btn__ico", html: ICONS.filePdf }), t("yaziDuzenle.pdfPick")]));
}

/** Seçili (kaydedilmemiş) dosyayı ve önizleme adresini temizle. */
function clearPending() {
  pendingFile = null;
  if (pendingUrl) { URL.revokeObjectURL(pendingUrl); pendingUrl = null; }
  pdfInput.value = "";
  renderPdfSlot();
}

function pdfCard({ name, size, onRemove = null, actions = [] }) {
  const right = [...actions];
  if (onRemove) {
    right.push(el("button", {
      class: "icon-btn icon-btn--danger", type: "button",
      title: t("yaziDuzenle.pdfRemove"), "aria-label": t("yaziDuzenle.pdfRemove"),
      onclick: onRemove,
    }, [el("span", { html: ICONS.trash })]));
  }
  return el("div", { class: "pdf-card" }, [
    el("span", { class: "pdf-card__icon", html: ICONS.filePdf }),
    el("div", { class: "pdf-card__meta" }, [
      el("span", { class: "pdf-card__name" }, [name || "belge.pdf"]),
      el("span", { class: "pdf-card__size" }, [formatBytes(size)]),
    ]),
    right.length ? el("div", { class: "pdf-card__actions" }, right) : null,
  ].filter(Boolean));
}

function onPickFile() {
  const file = pdfInput.files[0];
  if (!file) return;
  hide(pdfWarn);
  if (file.type !== "application/pdf") { show(pdfWarn, t("yaziDuzenle.errPdfType")); pdfInput.value = ""; return; }
  if (file.size > 50 * 1024 * 1024) { show(pdfWarn, t("yaziDuzenle.errPdfSize")); pdfInput.value = ""; return; }
  pendingFile = file;
  if (pendingUrl) URL.revokeObjectURL(pendingUrl);
  pendingUrl = URL.createObjectURL(file);   // "Görüntüle" bunu açar (yeni sekme yok)
  renderPdfSlot();
}

// --------------------------------------------------------------------
// Kaydet
// --------------------------------------------------------------------
async function onSubmit(e) {
  e.preventDefault();
  hide(errorBox); hide(titleWarn); hide(pdfWarn);

  const publish = e.submitter?.value === "publish";
  const title = titleInput.value.trim();
  const summary = summaryInput.value.trim();

  // Özet sınırı her iki modda da geçerli
  if (summary.length > 500) {
    checkSummary();
    summaryWarn.scrollIntoView({ behavior: "smooth", block: "center" });
    return;
  }

  // Yayınlamak için başlık + PDF şart
  if (publish) {
    let bad = null;
    if (title.length === 0) { show(titleWarn, t("yaziDuzenle.titleReqPublish")); bad ??= titleWarn; }
    if (!hasPdf()) { show(pdfWarn, t("yaziDuzenle.pdfReqPublish")); bad ??= pdfWarn; }
    if (bad) { bad.scrollIntoView({ behavior: "smooth", block: "center" }); return; }
  }

  const body = new FormData();
  body.append("Title", title);
  body.append("Summary", summary);
  body.append("Status", publish ? "Published" : "Draft");
  if (pendingFile) body.append("Pdf", pendingFile);

  const clicked = e.submitter;
  setBusy(true, !!pendingFile);

  try {
    const onProg = pendingFile ? setProgress : undefined;
    const saved = isEdit
      ? await api.uploadPut(`/api/articles/${articleId}`, body, onProg)
      : await api.uploadPost("/api/articles", body, onProg);

    if (saved?.id) savedId = saved.id;

    if (publish) {
      showPublishDone();
      return;
    }

    // TASLAK kaydedildi
    if (!isEdit) {
      toast(t("yaziDuzenle.createdDraft"), "success");
      window.location.href = `yazi-duzenle.html?id=${savedId}`;
      return;
    }
    // düzenlemede: kart tazele + düğmeyi kısa süre yeşil "✓" yap
    pendingFile = null;
    if (pendingUrl) { URL.revokeObjectURL(pendingUrl); pendingUrl = null; }
    pdfInput.value = "";
    if (saved) currentPdf = saved.originalFileName ? { name: saved.originalFileName, size: saved.fileSizeBytes } : null;
    renderPdfSlot();
    flashSaved(clicked);
  } catch (err) {
    setBusy(false);
    const msg = err instanceof ApiError ? err.message : t("common.saveFailed");
    // PDF eksik hatası → uyarıyı PDF alanının altında göster
    if (err instanceof ApiError && /PDF/i.test(msg)) show(pdfWarn, msg);
    else showError(msg);
  }
}

/** Kaydettikten sonra kısa yeşil onay; sonra normale döner. */
function flashSaved(btn) {
  progWrap.hidden = true;
  if (btn) {
    btn.classList.add("btn--ok");
    btn.textContent = `✓ ${t("yaziDuzenle.draftSavedMsg")}`;
  }
  setTimeout(() => {
    if (btn) btn.classList.remove("btn--ok");
    setBusy(false);
  }, 1800);
}

/** Yayınlandıktan sonra düğme satırını başarı paneliyle değiştir. */
function showPublishDone() {
  progWrap.hidden = true;
  render(formActions, el("div", { class: "publish-done" }, [
    el("span", { class: "publish-done__msg" }, [`✓ ${t("yaziDuzenle.publishedMsg")}`]),
    el("a", { class: "btn btn--primary btn--block", href: "profilim.html" }, [t("yaziDuzenle.goToMyArticles")]),
    el("button", {
      class: "btn btn--ghost btn--sm btn--block", type: "button",
      onclick: () => {
        if (!articleId && savedId) { window.location.href = `yazi-duzenle.html?id=${savedId}`; return; }
        restoreActions();
      },
    }, [t("yaziDuzenle.keepEditing")]),
  ]));
}

function restoreActions() {
  render(formActions,
    el("button", { type: "submit", name: "act", value: "draft", class: "btn btn--block" }, [t("yaziDuzenle.saveDraft")]),
    el("button", { type: "submit", name: "act", value: "publish", class: "btn btn--primary btn--block" }, [t("yaziDuzenle.publishBtn")]),
  );
}

/** busy=true iken düğmeler kilitli + "çalışıyor" göstergesi. */
function setBusy(busy, hasUpload = false) {
  formActions.querySelectorAll("button").forEach((b) => (b.disabled = busy));

  if (busy) {
    progWrap.hidden = false;
    progWrap.classList.toggle("upload-progress--indet", !hasUpload);
    if (hasUpload) {
      progFill.style.width = "0%";
      progLabel.textContent = t("yaziDuzenle.uploading", { pct: 0 });
    } else {
      progFill.style.width = "";
      progLabel.textContent = t("yaziDuzenle.savingBtn");
    }
  } else {
    progWrap.hidden = true;
    progWrap.classList.remove("upload-progress--indet");
    progFill.style.width = "0%";
    // metinleri normale döndür
    formActions.querySelectorAll("button").forEach((b) => {
      if (b.value === "draft") b.textContent = t("yaziDuzenle.saveDraft");
      if (b.value === "publish") b.textContent = t("yaziDuzenle.publishBtn");
    });
  }
}

function setProgress(fraction) {
  progWrap.hidden = false;
  progWrap.classList.remove("upload-progress--indet");
  const pct = Math.max(0, Math.min(100, Math.round(fraction * 100)));
  progFill.style.width = `${pct}%`;
  progLabel.textContent = fraction >= 1 ? t("yaziDuzenle.processing") : t("yaziDuzenle.uploading", { pct });
}

function showError(message) {
  errorBox.textContent = message;
  errorBox.hidden = false;
  errorBox.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

init();
