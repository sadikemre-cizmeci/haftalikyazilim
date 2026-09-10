// ============================================================================
// pages/kaydettiklerim.js — YILDIZLADIĞIM (kaydettiğim) yazılar
// ============================================================================

import { renderNav } from "../nav.js";
import { api } from "../api.js";
import { qs, el, render, setYear } from "../dom.js";
import { requireAuth } from "../session.js";
import { articleCard } from "../article-card.js";
import { revealOnScroll } from "../motion.js";
import { t } from "../i18n.js";

async function init() {
  const user = await requireAuth();
  if (!user) return;

  renderNav("kaydettiklerim");
  setYear();

  const listEl = qs("#article-list");
  const statusEl = qs("#list-status");

  let items;
  try {
    items = await api.get("/api/articles/bookmarked");
  } catch (err) {
    statusEl.textContent = t("common.listLoadFail");
    console.error(err);
    return;
  }

  if (items.length === 0) {
    render(statusEl, el("div", { class: "state" }, [
      el("div", { class: "state__title" }, [t("kaydettiklerim.emptyTitle")]),
      el("p", { class: "muted" }, [t("kaydettiklerim.emptyBody")]),
      el("a", { class: "btn btn--primary", href: "yazilar.html" }, [t("common.browseArticles")]),
    ]));
    statusEl.hidden = false;
    return;
  }

  statusEl.hidden = true;
  const cards = items.map((item) => articleCard(item, { showAuthor: true }));
  cards.forEach((c) => listEl.append(c));
  revealOnScroll(cards);
}

init();
