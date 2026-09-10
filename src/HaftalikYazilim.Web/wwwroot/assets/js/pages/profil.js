// ============================================================================
// pages/profil.js — HERKESE AÇIK PROFİL
// ----------------------------------------------------------------------------
// Adres:  profil.html?id=5
// Avatar + ad + kısa tanıtım + uzun "hakkında" + o kişinin yayınlanmış yazıları.
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { CONFIG } from "../config.js";
import { qs, el, render, setYear, formatDate, queryParam, toast } from "../dom.js";
import { avatarEl } from "../avatar.js";
import { articleCard } from "../article-card.js";
import { revealOnScroll } from "../motion.js";
import { getCurrentUser } from "../session.js";
import { ICONS } from "../icons.js";
import { t } from "../i18n.js";

async function init() {
  renderNav(null);
  setYear();

  const id = Number(queryParam("id"));
  const headEl = qs("#profile-head");

  if (!id) {
    render(headEl, el("p", { class: "state" }, [t("profil.notSpecified")]));
    return;
  }

  let profile;
  try {
    profile = await api.get(`/api/profile/${id}`);
  } catch (err) {
    const notFound = err instanceof ApiError && err.status === 404;
    render(headEl, el("div", { class: "state" }, [
      el("div", { class: "state__title" }, [notFound ? t("profil.notFound") : t("profil.loadFail")]),
      el("a", { class: "btn", href: "yazilar.html" }, [t("common.backToArticles")]),
    ]));
    return;
  }

  document.title = `${profile.displayName} — HaftalıkYazılım`;

  const me = await getCurrentUser();

  // --- Başlık: avatar + ad + @kullanıcıadı + kısa tanıtım + sayılar + takip ---
  render(headEl, el("div", { class: "profile-head" }, [
    avatarEl(profile, 88),
    el("div", { class: "stack", style: "gap: var(--space-2); flex: 1;" }, [
      el("div", { class: "row row--between" }, [
        el("div", {}, [
          el("h1", { class: "mb-0" }, [profile.displayName]),
          el("p", { class: "mb-0 muted text-sm" }, [`@${profile.username}`]),
        ]),
        followButton(profile, me),
      ]),
      profile.bio ? el("p", { class: "mb-0 muted measure" }, [profile.bio]) : null,
      el("p", { class: "mb-0 text-sm profile-stats" }, [
        stat(profile.publishedArticleCount, t("profil.statArticles")),
        stat(profile.followerCount, t("profil.statFollowers")),
        stat(profile.followingCount, t("profil.statFollowing")),
        el("span", { class: "muted" }, [t("profil.since", { date: formatDate(profile.memberSinceUtc) })]),
      ]),
    ].filter(Boolean)),
  ]));

  // --- Hakkında (uzun metin; boş satır = paragraf) ---
  const aboutEl = qs("#profile-about");
  if (profile.about) {
    render(aboutEl, ...paragraphs(profile.about));
  } else {
    aboutEl.hidden = true;
  }

  await loadArticles(id);
}

function stat(n, label) {
  return el("span", { class: "profile-stat" }, [
    el("strong", {}, [String(n)]), " ", label,
  ]);
}

/** Takip et / Takipte düğmesi. Kendi profili veya anonim → düğme yok. */
function followButton(profile, me) {
  if (!me || profile.isSelf) {
    return me ? null : el("a", { class: "btn btn--sm", href: "giris.html" }, [t("profil.followForLogin")]);
  }

  let following = profile.viewerFollows;
  const btn = el("button", { class: "btn btn--sm", type: "button" }, []);

  const paint = () => {
    btn.className = `btn btn--sm${following ? "" : " btn--primary"}`;
    btn.replaceChildren(
      el("span", { html: following ? ICONS.check : ICONS.userPlus }),
      following ? t("profil.following") : t("profil.follow"),
    );
  };
  paint();

  btn.addEventListener("click", async () => {
    btn.disabled = true;
    try {
      if (following) {
        await api.del(`/api/profile/${profile.id}/follow`);
        following = false;
      } else {
        await api.post(`/api/profile/${profile.id}/follow`);
        following = true;
      }
      paint();
    } catch (err) {
      toast(err instanceof ApiError ? err.message : t("common.actionFailed"), "error");
    } finally {
      btn.disabled = false;
    }
  });

  return btn;
}

/** Metni boş satırlara göre <p>'lere böler (HTML enjeksiyonu yok: textContent). */
function paragraphs(text) {
  return text
    .split(/\n{2,}/)
    .map((block) => block.trim())
    .filter(Boolean)
    .map((block) => {
      const p = el("p", {});
      // Tek satır sonlarını <br> yap.
      block.split("\n").forEach((line, i) => {
        if (i) p.append(el("br"));
        p.append(document.createTextNode(line));
      });
      return p;
    });
}

async function loadArticles(authorId) {
  const listEl = qs("#article-list");
  const statusEl = qs("#list-status");
  const titleEl = qs("#articles-title");

  let result;
  try {
    result = await api.get(`/api/articles?authorId=${authorId}&page=1&pageSize=${CONFIG.pageSize}`);
  } catch {
    statusEl.hidden = false;
    statusEl.textContent = t("profil.articlesLoadFail");
    return;
  }

  if (result.items.length === 0) {
    statusEl.hidden = false;
    statusEl.textContent = t("profil.noArticlesYet");
    return;
  }

  titleEl.hidden = false;
  const cards = result.items.map((item) => articleCard(item, { showAuthor: false }));
  cards.forEach((c) => listEl.append(c));
  revealOnScroll(cards);
}

init();
