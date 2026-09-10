// ============================================================================
// article-table.js — yazıları TABLO olarak gösteren ortak bileşen
// ----------------------------------------------------------------------------
// Hem "Yazılarım" hem "Yönetim" ekranı bunu kullanır (yönetimde Yazar sütunu da var).
// ============================================================================

import { el, formatDate, formatBytes } from "./dom.js";
import { ICONS } from "./icons.js";
import { t } from "./i18n.js";

/**
 * @param {Array} items
 * @param {object} [opts]
 *   showAuthor : "Yazar" sütununu göster (yönetim ekranı). Varsayılan false.
 *   rowHref    : (item) => string — satıra/başlığa tıklayınca gidilecek adres.
 *   onDelete   : (item) => void — verilirse son sütunda çöp kutusu çıkar.
 * @returns {HTMLElement} kaydırılabilir tablo sarmalayıcı
 */
export function articleTable(items, opts = {}) {
  const { showAuthor = false, rowHref = null, onDelete = null } = opts;

  const headers = [
    t("table.title"),
    showAuthor ? t("table.author") : null,
    t("table.status"), t("table.published"), t("table.updated"),
    t("table.size"), t("table.views"), t("table.downloads"),
    onDelete ? "" : null,
  ].filter((h) => h !== null);

  const rows = items.map((a) => {
    const published = a.status === "Published";
    const cells = [];

    // Başlık (link)
    cells.push(el("td", {}, [
      rowHref
        ? el("a", { href: rowHref(a) }, [a.title])
        : el("span", {}, [a.title]),
    ]));

    if (showAuthor) {
      cells.push(el("td", {}, [a.author?.displayName || "—"]));
    }

    cells.push(el("td", {}, [
      el("span", { class: `badge ${published ? "badge--published" : "badge--draft"}` },
        [published ? t("common.published") : t("common.draft")]),
    ]));
    cells.push(el("td", {}, [a.publishedAtUtc ? formatDate(a.publishedAtUtc) : "—"]));
    cells.push(el("td", {}, [formatDate(a.updatedAtUtc)]));
    cells.push(el("td", {}, [formatBytes(a.fileSizeBytes)]));
    cells.push(el("td", {}, [String(a.viewCount)]));
    cells.push(el("td", {}, [String(a.downloadCount)]));

    if (onDelete) {
      const del = el("button", {
        class: "icon-btn icon-btn--danger",
        type: "button",
        title: t("card.delete"),
        "aria-label": t("table.deleteAria", { title: a.title }),
        onclick: (e) => { e.preventDefault(); e.stopPropagation(); onDelete(a); },
      }, [el("span", { html: ICONS.trash })]);
      cells.push(el("td", { class: "table__actions" }, [del]));
    }

    const tr = el("tr", rowHref ? { class: "table__row--link" } : {}, cells);
    if (rowHref) {
      tr.addEventListener("click", (e) => {
        if (e.target.closest("a, button")) return;
        window.location.href = rowHref(a);
      });
    }
    return tr;
  });

  return el("div", { class: "table-wrap" }, [
    el("table", { class: "table" }, [
      el("thead", {}, [el("tr", {}, headers.map((h) => el("th", {}, [h])))]),
      el("tbody", {}, rows),
    ]),
  ]);
}
