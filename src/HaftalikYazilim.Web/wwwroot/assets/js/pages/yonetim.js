// ============================================================================
// pages/yonetim.js — YÖNETİM (admin) paneli
// ----------------------------------------------------------------------------
// • İstatistik: gün aralığına göre (bugün / bir hafta / bir ay) 5 ölçütte ilk 10
// • Kullanıcılar tablosu
// • Tüm yazılar tablosu (moderasyon; silinmişler dahil edilebilir)
// ============================================================================

import { renderNav } from "../nav.js";
import { api } from "../api.js";
import { qs, qsa, el, render, setYear, formatDate } from "../dom.js";
import { requireAuth } from "../session.js";
import { avatarEl } from "../avatar.js";
import { articleTable } from "../article-table.js";
import { t } from "../i18n.js";

async function init() {
  const user = await requireAuth({ admin: true });
  if (!user) return;

  renderNav("yonetim");
  setYear();

  // Gün aralığı düğmeleri
  const range = qs("#stats-range");
  qsa("button", range).forEach((b) => {
    b.addEventListener("click", () => {
      qsa("button", range).forEach((x) => x.classList.toggle("is-active", x === b));
      loadStats(Number(b.dataset.days));
    });
  });

  qs("#include-deleted").addEventListener("change", loadArticles);

  await Promise.all([loadStats(7), loadUsers(), loadArticles()]);
}

// --------------------------------------------------------------------
// İstatistik
// --------------------------------------------------------------------
async function loadStats(days) {
  const grid = qs("#stats-grid");
  render(grid, el("p", { class: "muted" }, [t("common.loading")]));

  let stats;
  try {
    stats = await api.get(`/api/admin/stats?days=${days}`);
  } catch (err) {
    render(grid, el("p", { class: "alert alert--error" }, [t("yonetim.statsLoadFail")]));
    console.error(err);
    return;
  }

  render(
    grid,
    leaderboard(t("yonetim.statViewed"), stats.mostViewed, t("yonetim.unitViews")),
    leaderboard(t("yonetim.statDownloaded"), stats.mostDownloaded, t("yonetim.unitDownloads")),
    leaderboard(t("yonetim.statLiked"), stats.mostLiked, t("yonetim.unitLikes")),
    leaderboard(t("yonetim.statBookmarked"), stats.mostBookmarked, t("yonetim.unitSaves")),
    leaderboard(t("yonetim.statShared"), stats.mostShared, t("yonetim.unitShares")),
  );
}

function leaderboard(title, rows, unit) {
  const body = rows.length === 0
    ? el("p", { class: "muted text-sm mb-0" }, [t("yonetim.noData")])
    : el("ol", { class: "leaderboard__list" }, rows.map((r, i) =>
        el("li", { class: "leaderboard__row" }, [
          el("span", { class: "leaderboard__rank" }, [`${i + 1}`]),
          avatarEl(r, 26),
          el("a", { class: "leaderboard__name", href: `profil.html?id=${r.userId}`, title: `@${r.username}` }, [r.displayName]),
          el("span", { class: "leaderboard__value" }, [`${r.value} ${unit}`]),
        ]),
      ));

  return el("div", { class: "card leaderboard" }, [
    el("h3", { class: "leaderboard__title" }, [title]),
    body,
  ]);
}

// --------------------------------------------------------------------
// Kullanıcılar
// --------------------------------------------------------------------
async function loadUsers() {
  const wrap = qs("#admin-users");
  render(wrap, el("p", { class: "muted" }, [t("common.loading")]));

  let users;
  try {
    users = await api.get("/api/admin/users");
  } catch (err) {
    render(wrap, el("p", { class: "alert alert--error" }, [t("yonetim.usersLoadFail")]));
    console.error(err);
    return;
  }

  const rows = users.map((u) =>
    el("tr", {}, [
      el("td", {}, [el("a", { href: `profil.html?id=${u.id}` }, [u.displayName])]),
      el("td", {}, [u.email]),
      el("td", {}, [u.role === "Admin" ? t("yonetim.roleAdmin") : t("yonetim.roleAuthor")]),
      el("td", {}, [u.isActive ? t("yonetim.active") : t("yonetim.inactive")]),
      el("td", {}, [formatDate(u.createdAtUtc)]),
      el("td", {}, [String(u.articleCount)]),
    ]),
  );

  const headers = [
    t("yonetim.colName"), t("yonetim.colEmail"), t("yonetim.colRole"),
    t("yonetim.colStatus"), t("yonetim.colJoined"), t("yonetim.colArticles"),
  ];

  render(wrap, el("div", { class: "table-wrap" }, [
    el("table", { class: "table" }, [
      el("thead", {}, [el("tr", {}, headers.map((h) => el("th", {}, [h])))]),
      el("tbody", {}, rows),
    ]),
  ]));
}

// --------------------------------------------------------------------
// Tüm yazılar (moderasyon)
// --------------------------------------------------------------------
async function loadArticles() {
  const wrap = qs("#admin-articles");
  const includeDeleted = qs("#include-deleted").checked;
  render(wrap, el("p", { class: "muted" }, [t("common.loading")]));

  let result;
  try {
    result = await api.get(`/api/admin/articles?includeDeleted=${includeDeleted}&pageSize=100`);
  } catch (err) {
    render(wrap, el("p", { class: "alert alert--error" }, [t("yonetim.articlesLoadFail")]));
    console.error(err);
    return;
  }

  if (result.items.length === 0) {
    render(wrap, el("p", { class: "muted" }, [t("yonetim.noRecords")]));
    return;
  }

  render(
    wrap,
    articleTable(result.items, { showAuthor: true, rowHref: (a) => `oku.html?id=${a.id}` }),
    el("p", { class: "muted text-sm" }, [t("yonetim.totalRecords", { n: result.totalCount })]),
  );
}

init();
