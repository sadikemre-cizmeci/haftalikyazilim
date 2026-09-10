// ============================================================================
// article-card.js — bir yazıyı "kart" olarak çizen ortak fonksiyon
// ----------------------------------------------------------------------------
// Kart yüzü SADE: başlık + (varsa) 2 satır özet + alt satırda kimlik/eylemler.
// Ayrıntı (tam özet, yazar bio, boyut, sayaçlar) ⓘ baloncuğunda (progressive disclosure).
//
// Alt satır düzeni (çirkin yığılma çözümü):
//   SOL:  [durum rozeti?] [avatar] [yazar adı] · [tarih]
//   SAĞ:  [Yeni rozeti?]  [PDF indir?]
// ============================================================================

import { el, formatDate, formatBytes, relativeTime, isRecent } from "./dom.js";
import { ICONS } from "./icons.js";
import { attachPopover } from "./popover.js";
import { avatarEl } from "./avatar.js";
import { socialActions } from "./social.js";
import { t } from "./i18n.js";

/**
 * @param {object} item  API'den gelen yazı (ArticleListItemResponse; item.author nesnesi içerir).
 * @param {object} [opts]
 *   linkHref     : Tıklayınca gidilecek adres. Varsayılan: oku.html?id=...
 *   showStatus   : "Taslak / Yayında" rozeti (varsayılan false).
 *   showAuthor   : Alt satırda yazar (avatar + ad). Varsayılan true.
 *   showDownload : "PDF indir" bağlantısı. Varsayılan true.
 *   showInfo     : ⓘ bilgi baloncuğu. Varsayılan true.
 *   onDelete     : Verilirse sağ üstte çöp kutusu; tıklanınca onDelete(item).
 */
export function articleCard(item, opts = {}) {
  const {
    linkHref = `oku.html?id=${item.id}`,
    showStatus = false,
    showAuthor = true,
    showDownload = true,
    showInfo = true,
    showSocial = true,
    editHref = null,     // verilirse köşede kalem (düzenle) ikonu çıkar
    onDelete = null,     // verilirse köşede çöp kutusu çıkar
  } = opts;

  const author = item.author || {};
  const published = item.status === "Published";
  const dateToShow = item.publishedAtUtc || item.updatedAtUtc;
  const recent = published && isRecent(item.publishedAtUtc);

  // --- Başlık + köşe eylemleri (ⓘ / çöp) ---
  const titleLink = el("a", { href: linkHref, class: "article-card__titlelink" }, [item.title]);
  const corner = [];

  if (showInfo) {
    const infoBtn = el("button", {
      class: "icon-btn icon-btn--info",
      type: "button",
      title: t("card.infoTitle"),
      "aria-label": t("card.infoAria"),
      onclick: (e) => { e.preventDefault(); e.stopPropagation(); },
    }, [el("span", { html: ICONS.info })]);
    attachPopover(infoBtn, () => buildInfoContent(item));
    corner.push(infoBtn);
  }
  if (editHref) {
    corner.push(el("a", {
      class: "icon-btn",
      href: editHref,
      title: t("card.edit"),
      "aria-label": t("card.edit"),
      onclick: (e) => e.stopPropagation(),
    }, [el("span", { html: ICONS.pencil })]));
  }
  if (onDelete) {
    corner.push(el("button", {
      class: "icon-btn icon-btn--danger",
      type: "button",
      title: t("card.delete"),
      "aria-label": t("card.delete"),
      onclick: (e) => { e.preventDefault(); e.stopPropagation(); onDelete(item); },
    }, [el("span", { html: ICONS.trash })]));
  }

  const head = el("div", { class: "article-card__head" }, [
    el("h3", { class: "article-card__title" }, [titleLink]),
    corner.length ? el("div", { class: "article-card__corner" }, corner) : null,
  ]);

  // --- Özet (varsa; CSS 2 satıra kırpar) ---
  const summary = item.summary
    ? el("p", { class: "article-card__summary" }, [item.summary])
    : null;

  // --- Alt satır ---
  const left = [];
  if (showStatus) {
    left.push(el("span", { class: `badge ${published ? "badge--published" : "badge--draft"}` },
      [published ? t("common.published") : t("common.draft")]));
  }
  if (showAuthor && author.displayName) {
    left.push(authorChip(author));
    left.push(el("span", { class: "article-card__dot" }, ["·"]));
  }
  if (dateToShow) {
    left.push(el("time", { datetime: dateToShow, title: formatDate(dateToShow) }, [relativeTime(dateToShow)]));
  }

  const right = [];
  if (recent) {
    right.push(el("span", { class: "badge badge--new" }, [t("card.new")]));
  }
  // PDF'i olmayan taslakta indirme bağlantısı gösterme.
  if (showDownload && item.fileSizeBytes > 0) {
    right.push(el("a", {
      class: "article-card__dl",
      href: `/api/articles/${item.id}/download`,
      title: t("card.downloadPdf"),
      onclick: (e) => e.stopPropagation(),
    }, [el("span", { html: ICONS.download }), "PDF"]));
  }

  const foot = el("div", { class: "article-card__foot" }, [
    el("div", { class: "article-card__identity" }, left),
    right.length ? el("div", { class: "article-card__foot-actions" }, right) : null,
  ]);

  // --- Sosyal satır: beğen / kaydet / paylaş / yorum ---
  const social = showSocial
    ? el("div", { class: "article-card__social" }, [
        socialActions(item, { compact: true, stopClicks: true, showComment: true }),
      ])
    : null;

  // --- Kart ---
  const card = el("li", { class: "article-card" }, [head, summary, foot, social].filter(Boolean));
  card.addEventListener("click", (e) => {
    if (e.target.closest("a, button")) return;   // iç bağlantı/butona karışma
    window.location.href = linkHref;
  });
  return card;
}

/** Alt satırdaki tıklanabilir "avatar + ad" — profile götürür. */
function authorChip(author) {
  return el("a", {
    class: "article-card__author",
    href: `profil.html?id=${author.id}`,
    onclick: (e) => e.stopPropagation(),
  }, [
    avatarEl(author, 22),
    el("span", {}, [author.displayName]),
  ]);
}

// ---------------------------------------------------------------------------
// ⓘ baloncuğu içeriği
// ---------------------------------------------------------------------------
function buildInfoContent(item) {
  const author = item.author || {};

  // 1) Özet (yoksa bilgi ver)
  const summaryNode = item.summary
    ? el("p", { class: "mb-0" }, [item.summary])
    : el("p", { class: "mb-0 muted", style: "font-style:italic" }, [t("card.noSummary")]);

  // 2) Yazar bloğu — tıklanınca profile
  const authorBlock = el("a", { class: "popover__author", href: `profil.html?id=${author.id}` }, [
    avatarEl(author, 40),
    el("div", {}, [
      el("div", { class: "popover__author-name" }, [author.displayName || "—"]),
      author.bio ? el("div", { class: "popover__author-bio" }, [author.bio]) : null,
    ].filter(Boolean)),
  ]);

  // 3) İstatistikler
  const stats = el("div", {}, [
    row(t("card.rowPublish"), item.publishedAtUtc ? formatDate(item.publishedAtUtc) : t("card.stillDraft")),
    row(t("card.rowSize"), formatBytes(item.fileSizeBytes)),
    row(t("card.rowViews"), String(item.viewCount ?? 0)),
    row(t("card.rowDownloads"), String(item.downloadCount ?? 0)),
  ]);

  return el("div", {}, [
    el("h4", { class: "popover__title" }, [item.title]),
    summaryNode,
    el("div", { class: "popover__sep" }),
    authorBlock,
    el("div", { class: "popover__sep" }),
    stats,
  ]);
}

function row(label, value) {
  return el("div", { class: "popover__row" }, [el("span", {}, [label]), el("span", {}, [value])]);
}
