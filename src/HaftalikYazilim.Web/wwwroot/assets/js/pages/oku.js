// ============================================================================
// pages/oku.js — YAZI OKUMA sayfası
// ----------------------------------------------------------------------------
// Adres:  oku.html?id=123
// • Ayrı başlık şeridi YOK (yazının başlığı PDF'in içinde). Yazar künyesi +
//   beğen/kaydet/paylaş, PDF araç çubuğuna eklenir.
// • Altta yorumlar bölümü.
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { CONFIG } from "../config.js";
import { qs, el, render, setYear, queryParam, relativeTime, formatDate, toast } from "../dom.js";
import { PdfViewer } from "../pdf-viewer.js";
import { avatarEl } from "../avatar.js";
import { socialActions, shareButton } from "../social.js";
import { getCurrentUser } from "../session.js";
import { t } from "../i18n.js";

let viewer = null;
let articleId = null;

// ?preview=1 → yazı düzenleme sayfasından gelen ÖNİZLEME:
//   sadece "Paylaş" + "Çık"; beğen/kaydet/yorum/indir ve yorumlar bölümü yok.
const isPreview = queryParam("preview") === "1";

/** "Çık" düğmesi: geldiği sayfaya döner (geçmiş yoksa Yazılarım'a). */
function goBack() {
  if (window.history.length > 1) window.history.back();
  else window.location.href = isPreview ? "profilim.html" : "yazilar.html";
}

async function init() {
  renderNav(null);
  setYear();

  articleId = queryParam("id");
  const viewerEl = qs("#pdf");
  const commentsSection = qs("#yorumlar");

  if (isPreview) commentsSection.hidden = true;

  if (!articleId) {
    showError(viewerEl, commentsSection, t("oku.noId"));
    return;
  }

  let article;
  try {
    article = await api.get(`/api/articles/${articleId}`);
  } catch (err) {
    const notFound = err instanceof ApiError && err.status === 404;
    showError(viewerEl, commentsSection,
      notFound ? t("oku.notFound") : t("oku.loadFail"));
    return;
  }

  document.title = `${article.title} — ${CONFIG.siteName}`;

  // --- Araç çubuğu başı: yazar künyesi (önizlemede yok — kendi yazın) ---
  const author = article.author || {};
  const authorChip = isPreview ? null : el("a", {
    class: "pdfv__author",
    href: `profil.html?id=${author.id}`,
    title: author.displayName,
  }, [
    avatarEl(author, 24),
    el("span", {}, [author.displayName || "—"]),
  ]);

  // --- Araç çubuğu sağı ---
  //   normal   : beğen / kaydet / paylaş / yorum   + indir + çık
  //   önizleme : sadece paylaş                      + çık
  const actions = isPreview
    ? shareButton(article, { compact: true })
    : socialActions(article, { compact: true, showComment: true });

  const progressFill = qs("#read-progress > span");
  viewer = new PdfViewer(viewerEl, {
    url: article.fileUrl,
    downloadUrl: isPreview ? null : article.downloadUrl,
    toolbarStart: authorChip,
    toolbarExtra: actions,
    onExit: goBack,
    onProgress: (page, total) => {
      if (progressFill) progressFill.style.width = `${Math.round((page / total) * 100)}%`;
    },
  });
  await viewer.load();

  if (isPreview) return;

  await loadComments();

  // #yorumlar bağlantısıyla gelindiyse oraya kaydır.
  if (location.hash === "#yorumlar") {
    commentsSection.scrollIntoView({ behavior: "smooth" });
  }
}

// --------------------------------------------------------------------
// Yorumlar
// --------------------------------------------------------------------
async function loadComments() {
  const listEl = qs("#comment-list");
  const formSlot = qs("#comment-form-slot");

  const user = await getCurrentUser();

  // Yorum formu (giriş varsa) / giriş çağrısı (yoksa)
  if (user) {
    render(formSlot, buildCommentForm());
  } else {
    render(formSlot, el("p", { class: "muted text-sm" }, [
      el("a", { href: `giris.html?next=${encodeURIComponent(location.pathname + location.search)}` }, [t("oku.loginToComment1")]),
      t("oku.loginToComment2"),
      el("a", { href: "kayit.html" }, [t("oku.loginToComment3")]),
      t("oku.loginToComment4"),
    ]));
  }

  let comments;
  try {
    comments = await api.get(`/api/articles/${articleId}/comments`);
  } catch {
    render(listEl, el("p", { class: "muted" }, [t("oku.commentsLoadFail")]));
    return;
  }

  if (comments.length === 0) {
    render(listEl, el("p", { class: "muted text-sm" }, [t("oku.firstComment")]));
    return;
  }

  render(listEl, ...comments.map(commentRow));
}

function buildCommentForm() {
  const textarea = el("textarea", {
    class: "textarea",
    rows: "3",
    maxlength: "4000",
    placeholder: t("oku.commentPh"),
  });
  const submit = el("button", { class: "btn btn--primary btn--sm", type: "submit" }, [t("oku.send")]);

  const form = el("form", { class: "comment-form" }, [textarea, el("div", { class: "row" }, [submit])]);
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const body = textarea.value.trim();
    if (!body) return;

    submit.disabled = true;
    try {
      const created = await api.post(`/api/articles/${articleId}/comments`, { body });
      textarea.value = "";
      // Boş-durum mesajını temizleyip yeni yorumu EN ÜSTE ekle (yeni → eski sıralama).
      const listEl = qs("#comment-list");
      if (listEl.querySelector(".muted")) render(listEl);
      listEl.prepend(commentRow(created));
    } catch (err) {
      toast(err instanceof ApiError ? err.message : t("oku.commentAddFail"), "error");
    } finally {
      submit.disabled = false;
    }
  });
  return form;
}

function commentRow(c) {
  const author = c.author || {};
  const row = el("div", { class: "comment" }, [
    avatarEl(author, 34),
    el("div", { class: "comment__body" }, [
      el("div", { class: "comment__head" }, [
        el("a", { class: "comment__author", href: `profil.html?id=${author.id}` }, [author.displayName || "—"]),
        el("time", { class: "muted text-sm", title: formatDate(c.createdAtUtc) }, [relativeTime(c.createdAtUtc)]),
        c.canDelete
          ? el("button", {
              class: "comment__delete",
              type: "button",
              title: t("oku.deleteComment"),
              onclick: async () => {
                if (!window.confirm(t("oku.confirmDeleteComment"))) return;
                try {
                  await api.del(`/api/comments/${c.id}`);
                  row.remove();
                } catch (err) {
                  toast(err instanceof ApiError ? err.message : t("common.deleteFailed"), "error");
                }
              },
            }, [t("oku.deleteCommentShort")])
          : null,
      ].filter(Boolean)),
      el("p", { class: "comment__text mb-0" }, [c.body]),
    ]),
  ]);
  return row;
}

function showError(viewerEl, commentsSection, text) {
  const bar = qs("#read-progress");
  if (bar) bar.hidden = true;
  if (commentsSection) commentsSection.hidden = true;
  render(viewerEl, el("div", { class: "state" }, [
    el("div", { class: "state__title" }, [text]),
    el("a", { class: "btn", href: "yazilar.html" }, [t("common.backToArticles")]),
  ]));
}

window.addEventListener("pagehide", () => viewer?.destroy());

init();
