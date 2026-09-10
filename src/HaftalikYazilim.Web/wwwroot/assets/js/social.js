// ============================================================================
// social.js — beğeni / yıldız (kaydet) / paylaş düğmeleri
// ----------------------------------------------------------------------------
// Hem yazı kartında hem okuma sayfasında kullanılır. İşlemler giriş ister;
// anonim kullanıcı düğmeye basınca giriş sayfasına yönlenir.
// ============================================================================

import { el, toast } from "./dom.js";
import { ICONS } from "./icons.js";
import { api, ApiError } from "./api.js";
import { getCurrentUser } from "./session.js";
import { t } from "./i18n.js";

/**
 * @param {object} article  { id, social: { likeCount, commentCount, viewerLiked, viewerBookmarked } }
 * @param {object} [opts]
 *   compact       : küçük (kartta). Varsayılan true.
 *   stopClicks    : tıklamalarda e.stopPropagation() (kart içindeyken). Varsayılan false.
 *   showComment   : yorum sayısını göster (okuma sayfasına götüren link). Varsayılan true.
 * @returns {HTMLElement}
 */
export function socialActions(article, opts = {}) {
  const { compact = true, stopClicks = false, showComment = true } = opts;
  const s = article.social || {};

  let liked = !!s.viewerLiked;
  let likeCount = Number(s.likeCount || 0);
  let bookmarked = !!s.viewerBookmarked;

  // --- Beğeni ---
  const likeBtn = actionBtn({
    compact,
    label: () => (likeCount > 0 ? String(likeCount) : t("social.like")),
    icon: () => (liked ? ICONS.heartFilled : ICONS.heart),
    active: () => liked,
    activeClass: "is-liked",
    title: t("social.like"),
    onClick: async () => {
      if (!(await ensureLogin())) return;
      try {
        const r = await api.post(`/api/articles/${article.id}/like`);
        liked = r.active;
        likeCount = r.count;
        sync(likeBtn, { label: likeCount > 0 ? String(likeCount) : t("social.like"), icon: liked ? ICONS.heartFilled : ICONS.heart, active: liked });
      } catch (err) {
        toast(err instanceof ApiError ? err.message : t("common.actionFailed"), "error");
      }
    },
  });

  // --- Yıldız (kaydet) ---
  const starBtn = actionBtn({
    compact,
    label: () => (bookmarked ? t("social.saved") : t("social.save")),
    icon: () => (bookmarked ? ICONS.starFilled : ICONS.star),
    active: () => bookmarked,
    activeClass: "is-starred",
    title: t("social.saveTitle"),
    onClick: async () => {
      if (!(await ensureLogin())) return;
      try {
        const r = await api.post(`/api/articles/${article.id}/bookmark`);
        bookmarked = r.active;
        sync(starBtn, { label: bookmarked ? t("social.saved") : t("social.save"), icon: bookmarked ? ICONS.starFilled : ICONS.star, active: bookmarked });
        toast(bookmarked ? t("social.savedToast") : t("social.unsavedToast"), "success");
      } catch (err) {
        toast(err instanceof ApiError ? err.message : t("common.actionFailed"), "error");
      }
    },
  });

  const children = [likeBtn, starBtn, shareButton(article, { compact })];

  if (showComment) {
    const c = Number(s.commentCount || 0);
    children.push(el("a", {
      class: `social-btn${compact ? " social-btn--sm" : ""}`,
      href: `oku.html?id=${article.id}#yorumlar`,
      title: t("social.commentsTitle"),
    }, [el("span", { html: ICONS.comment }), el("span", {}, [c > 0 ? String(c) : t("social.comment")])]));
  }

  const wrap = el("div", { class: `social-actions${compact ? " social-actions--sm" : ""}` }, children);

  if (stopClicks) {
    wrap.addEventListener("click", (e) => e.stopPropagation());
  }
  return wrap;
}

/**
 * Tek başına "Paylaş" düğmesi (okuma sayfası + önizleme çubuğunda tekil kullanım için).
 * navigator.share varsa onu, yoksa panoya kopyalar; "paylaşıldı" olayını da kaydeder.
 */
export function shareButton(article, { compact = true } = {}) {
  return actionBtn({
    compact,
    label: () => t("social.share"),
    icon: () => ICONS.share,
    title: t("social.shareTitle"),
    onClick: async () => {
      const url = `${location.origin}/oku.html?id=${article.id}`;
      const title = article.title || "HaftalıkYazılım";

      // "Paylaşıldı" olayını sessizce kaydet (yönetim istatistikleri için).
      api.post(`/api/articles/${article.id}/share`).catch(() => {});

      if (navigator.share) {
        try { await navigator.share({ title, url }); } catch { /* kullanıcı vazgeçti */ }
        return;
      }
      try {
        await navigator.clipboard.writeText(url);
        toast(t("social.linkCopied"), "success");
      } catch {
        toast(t("social.copyFailed", { url }), "error");
      }
    },
  });
}

/** Giriş yoksa giriş sayfasına yönlendir; varsa true döner. */
async function ensureLogin() {
  const user = await getCurrentUser();
  if (user) return true;
  const next = encodeURIComponent(location.pathname + location.search);
  location.href = `giris.html?next=${next}`;
  return false;
}

function actionBtn({ compact, label, icon, active, activeClass, title, onClick }) {
  const btn = el("button", {
    class: `social-btn${compact ? " social-btn--sm" : ""}${active && active() ? " " + activeClass : ""}`,
    type: "button",
    title,
  }, [
    el("span", { class: "social-btn__icon", html: icon() }),
    el("span", { class: "social-btn__label" }, [label()]),
  ]);
  btn._activeClass = activeClass;
  btn.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    onClick();
  });
  return btn;
}

function sync(btn, { label, icon, active }) {
  btn.querySelector(".social-btn__icon").innerHTML = icon;
  btn.querySelector(".social-btn__label").textContent = label;
  if (btn._activeClass) btn.classList.toggle(btn._activeClass, !!active);
}
