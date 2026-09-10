// ============================================================================
// pages/yazilar.js — "YAZILAR" sayfası
// ----------------------------------------------------------------------------
// • Yayınlanmış yazılar, sayfada en fazla CONFIG.pageSize (20) tane
// • Altta sayfa numaraları
// • Tek arama kutusu: başlıkta VE yazar adında arar (sunucuda trigram indeksli
//   ILIKE → DB'yi yormaz; en az 2 karakter gerekli)
//
// Durum URL'ye yazılır: yazilar.html?q=cache&page=2  (paylaşılabilir / geri tuşu)
// Bir yazarın prof­il sayfasından "yazıları" linkiyle gelinirse ?authorId=3 de
// desteklenir (üstte küçük bir "şu yazar" bilgisi + "hepsi" çıkışı gösterilir).
// ============================================================================

import { renderNav } from "../nav.js";
import { api } from "../api.js";
import { CONFIG } from "../config.js";
import { qs, qsa, el, render, setYear, debounce, queryParam } from "../dom.js";
import { articleCard } from "../article-card.js";
import { revealOnScroll } from "../motion.js";
import { renderPagination } from "../pagination.js";
import { getCurrentUser } from "../session.js";
import { t } from "../i18n.js";

const state = {
  q: (queryParam("q") || "").trim(),
  authorId: queryParam("authorId") ? Number(queryParam("authorId")) : null,
  scope: queryParam("scope") === "following" ? "following" : "all",
  page: Math.max(1, Number(queryParam("page")) || 1),
  totalPages: 1,
};

let listEl, statusEl, pagerEl, searchInput, authorNoteEl;

async function init() {
  renderNav("yazilar");
  setYear();

  listEl = qs("#article-list");
  statusEl = qs("#list-status");
  pagerEl = qs("#pager");
  searchInput = qs("#search-input");
  authorNoteEl = qs("#author-note");

  searchInput.value = state.q;

  searchInput.addEventListener("input", debounce(() => {
    state.q = searchInput.value.trim();
    state.page = 1;
    syncUrl();
    load();
  }, 350));

  // Tümü / Takip ettiklerim — giriş varsa göster.
  const user = await getCurrentUser();
  if (user) {
    const toggle = qs("#scope-toggle");
    toggle.hidden = false;
    qsa("button", toggle).forEach((b) => {
      b.classList.toggle("is-active", b.dataset.scope === state.scope);
      b.addEventListener("click", () => {
        if (state.scope === b.dataset.scope) return;
        state.scope = b.dataset.scope;
        state.page = 1;
        qsa("button", toggle).forEach((x) => x.classList.toggle("is-active", x === b));
        syncUrl();
        load();
      });
    });
  }

  await maybeShowAuthorNote();
  await load();
}

/** ?authorId varsa üstte "şu yazarın yazıları · hepsini gör" bilgisi göster. */
async function maybeShowAuthorNote() {
  if (!state.authorId) {
    authorNoteEl.hidden = true;
    return;
  }
  try {
    const p = await api.get(`/api/profile/${state.authorId}`);
    render(authorNoteEl, [
      el("span", {}, [t("yazilar.authorNote", { name: p.displayName })]),
      el("a", { href: "yazilar.html" }, [t("yazilar.seeAll")]),
    ]);
    authorNoteEl.hidden = false;
  } catch {
    authorNoteEl.hidden = true;
  }
}

async function load() {
  render(listEl);
  render(pagerEl);
  statusEl.hidden = false;
  statusEl.textContent = t("common.loading");

  const params = new URLSearchParams({
    page: String(state.page),
    pageSize: String(CONFIG.pageSize),
  });
  if (state.q.length >= 2) params.set("search", state.q);
  if (state.authorId) params.set("authorId", String(state.authorId));
  if (state.scope === "following") params.set("following", "true");

  let result;
  try {
    result = await api.get(`/api/articles?${params.toString()}`);
  } catch (err) {
    statusEl.textContent = t("yazilar.loadFail");
    console.error(err);
    return;
  }

  state.totalPages = result.totalPages;

  // İstenen sayfa boşsa (ör. elle ?page=99) 1. sayfaya düş.
  if (result.items.length === 0 && state.page > 1) {
    state.page = 1;
    syncUrl();
    return load();
  }

  if (result.items.length === 0) {
    statusEl.textContent = state.q
      ? t("yazilar.noResults", { q: state.q })
      : state.scope === "following"
        ? t("yazilar.noFollowing")
        : t("yazilar.empty");
    return;
  }

  statusEl.hidden = true;
  const cards = result.items.map((item) => articleCard(item, { showAuthor: true }));
  cards.forEach((c) => listEl.append(c));
  revealOnScroll(cards);

  renderPagination(pagerEl, state, (page) => {
    state.page = page;
    syncUrl();
    load();
    // Liste başına kaydır (sayfa değişince tepeden başla).
    listEl.scrollIntoView({ behavior: "smooth", block: "start" });
  });
}

function syncUrl() {
  const params = new URLSearchParams();
  if (state.q) params.set("q", state.q);
  if (state.authorId) params.set("authorId", String(state.authorId));
  if (state.scope === "following") params.set("scope", "following");
  if (state.page > 1) params.set("page", String(state.page));
  const query = params.toString();
  history.replaceState(null, "", query ? `yazilar.html?${query}` : "yazilar.html");
}

init();
