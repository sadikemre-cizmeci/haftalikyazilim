// ============================================================================
// avatar.js — profil fotoğrafı dairesi
// ----------------------------------------------------------------------------
// Fotoğraf varsa: yuvarlak kap içinde bir <img>. Kullanıcının zoom / konum
//   ayarları CSS ile uygulanır:
//     img { object-fit: cover; object-position: X% Y%; transform: scale(zoom); }
//   → fotoğraf çarpılmadan daireye oturur; kaydırıcılarla hangi kısmın
//     görüneceği ayarlanır (yeniden yükleme gerekmez).
// Fotoğraf yoksa: baş harflerden renkli bir daire.
// ============================================================================

import { el } from "./dom.js";

const PALETTE = [
  "#1f6f5c", "#c1653f", "#3f6fb0", "#8a5a9e", "#a9701f", "#4f8f6a", "#b0555a",
];

export function initialsOf(name) {
  const parts = (name || "?").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function colorFor(name) {
  let h = 0;
  for (const ch of name || "") h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return PALETTE[h % PALETTE.length];
}

/** img'e uygulanacak stil metni (zoom + odak noktası). Hem burada hem editör önizlemesinde kullanılır. */
export function avatarImgStyle({ avatarZoom = 1, avatarPosX = 50, avatarPosY = 50 } = {}) {
  const z = Math.max(1, Number(avatarZoom) || 1);
  const x = Number(avatarPosX ?? 50);
  const y = Number(avatarPosY ?? 50);
  return `object-position:${x}% ${y}%;transform:scale(${z});transform-origin:${x}% ${y}%`;
}

/**
 * @param {object} person  { displayName, avatarUrl, avatarZoom, avatarPosX, avatarPosY }
 * @param {number} size     piksel (varsayılan 32)
 * @returns {HTMLElement}
 */
export function avatarEl(person, size = 32) {
  const name = person?.displayName || "";
  const style = `width:${size}px;height:${size}px;font-size:${Math.round(size * 0.4)}px`;

  if (person?.avatarUrl) {
    const img = el("img", {
      src: person.avatarUrl,
      alt: "",
      loading: "lazy",
      style: avatarImgStyle(person),
    });
    return el("div", { class: "avatar avatar--img", style, "aria-hidden": "true" }, [img]);
  }

  return el("div", {
    class: "avatar avatar--initials",
    style: `${style};background:${colorFor(name)}`,
    "aria-hidden": "true",
  }, [initialsOf(name)]);
}
