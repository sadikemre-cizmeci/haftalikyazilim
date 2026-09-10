// ============================================================================
// pages/profilim.js — "YAZILARIM"
// ----------------------------------------------------------------------------
// • Giriş şart
// • Kendi yazıları (taslak dahil) — "Yazılar" sayfasındaki gibi PDF kutucukları
// • Her kartın sağ üstünde: ⓘ bilgi, kalem (düzenle), çöp (sil)
// • Karta / başlığa tıklayınca OKUMA sayfası açılır
// • Sağ altta yüzen "+" → yeni yazı
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { qs, qsa, el, render, setYear, toast } from "../dom.js";
import { ICONS } from "../icons.js";
import { requireAuth } from "../session.js";
import { articleCard } from "../article-card.js";
import { revealOnScroll } from "../motion.js";
import { t } from "../i18n.js";

let allItems = [];
let activeStatus = "all";   // "all" | "Published" | "Draft"

async function init() {
  const user = await requireAuth();
  if (!user) return;

  renderNav("profilim");
  setYear();
  qs("#new-article-fab").hidden = false;

  // Süzgeç sekmeleri
  qsa("#status-tabs button").forEach((b) => {
    b.addEventListener("click", () => {
      if (b.dataset.status === activeStatus) return;
      activeStatus = b.dataset.status;
      qsa("#status-tabs button").forEach((x) => x.classList.toggle("is-active", x === b));
      renderList();
    });
  });

  await load();
}

async function load() {
  const statusEl = qs("#my-list-status");
  statusEl.hidden = false;
  statusEl.textContent = t("common.loading");
  render(qs("#my-article-list"));

  try {
    allItems = await api.get("/api/articles/mine");
  } catch (err) {
    statusEl.textContent = t("profilim.loadFail");
    console.error(err);
    return;
  }

  // Hiç yazı yok → davet ekranı, sekmeler gizli
  if (allItems.length === 0) {
    qs("#status-tabs").hidden = true;
    render(statusEl, el("div", { class: "invite" }, [
      el("div", { class: "invite__title" }, [t("profilim.emptyTitle")]),
      el("p", { class: "muted measure", style: "margin-inline:auto" }, [t("profilim.emptyBody")]),
      el("a", { class: "btn btn--primary", href: "yazi-duzenle.html" }, [
        el("span", { html: ICONS.plus }), t("profilim.emptyCta"),
      ]),
    ]));
    statusEl.hidden = false;
    return;
  }

  qs("#status-tabs").hidden = false;
  renderList();
}

/** Aktif sekmeye göre kartları çiz. */
function renderList() {
  const listEl = qs("#my-article-list");
  const statusEl = qs("#my-list-status");
  render(listEl);

  const items = activeStatus === "all"
    ? allItems
    : allItems.filter((a) => a.status === activeStatus);

  if (items.length === 0) {
    statusEl.hidden = false;
    statusEl.textContent = activeStatus === "Draft"
      ? t("profilim.emptyDraft")
      : t("profilim.emptyPublished");
    return;
  }

  statusEl.hidden = true;
  const cards = items.map((item) =>
    articleCard(item, {
      linkHref: `oku.html?id=${item.id}`,
      editHref: `yazi-duzenle.html?id=${item.id}`,
      onDelete: confirmDelete,
      showStatus: activeStatus === "all",   // sekme zaten durumu söylüyorsa rozet gereksiz
      showAuthor: false,
    }),
  );
  cards.forEach((c) => listEl.append(c));
  revealOnScroll(cards);
}

async function confirmDelete(item) {
  const ok = window.confirm(t("profilim.confirmDelete", { title: item.title }));
  if (!ok) return;

  try {
    await api.del(`/api/articles/${item.id}`);
    toast(t("profilim.deleted"), "success");
    allItems = allItems.filter((a) => a.id !== item.id);
    if (allItems.length === 0) { await load(); } else { renderList(); }
  } catch (err) {
    toast(err instanceof ApiError ? err.message : t("common.deleteFailed"), "error");
  }
}

init();
