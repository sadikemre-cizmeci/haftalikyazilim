// ============================================================================
// pages/profil-duzenle.js — PROFİL DÜZENLEME
// ----------------------------------------------------------------------------
// • Kullanıcı adı, görünen ad, kısa tanıtım (bio), uzun tanıtım (about)
// • Profil fotoğrafı: tarayıcıda en uzun kenarı ~640px'e küçültülüp JPEG'e
//   çevrilir (KIRPILMADAN — en-boy oranı korunur), öyle yüklenir. Daire içinde
//   görünüm  <img object-fit:cover>  + yakınlaştır / yatay / dikey kaydırıcıları
//   ile ayarlanır (CSS; yeniden yükleme gerekmez).
// ============================================================================

import { renderNav } from "../nav.js";
import { api, ApiError } from "../api.js";
import { qs, el, render, setYear } from "../dom.js";
import { requireAuth, clearSessionCache } from "../session.js";
import { initialsOf, colorFor, avatarImgStyle } from "../avatar.js";
import { t } from "../i18n.js";

const AVATAR_MAX = 640;

const els = {};
let currentAvatarUrl = null;   // sunucudaki mevcut foto
let pendingBlob = null;        // henüz kaydedilmemiş yeni foto (küçültülmüş)
let pendingObjectUrl = null;   // preview için geçici URL
let removeRequested = false;

async function init() {
  const user = await requireAuth();
  if (!user) return;

  renderNav("profil-duzenle");
  setYear();

  for (const id of [
    "profile-form", "profile-error", "profile-submit",
    "email", "username", "display-name", "bio", "about",
    "avatar-preview", "avatar-file", "avatar-remove", "avatar-adjust",
    "adj-zoom", "adj-x", "adj-y",
  ]) {
    els[id] = qs("#" + id);
  }

  // Kullanıcı adını yazarken sadeleştir.
  els["username"].addEventListener("input", () => {
    const clean = els["username"].value.toLowerCase().replace(/[^a-z0-9._]/g, "");
    if (clean !== els["username"].value) els["username"].value = clean;
  });

  const profile = await api.get("/api/profile/me");
  fill(profile);

  els["avatar-file"].addEventListener("change", onPickFile);
  els["avatar-remove"].addEventListener("click", onRemoveAvatar);
  for (const s of ["adj-zoom", "adj-x", "adj-y"]) {
    els[s].addEventListener("input", updatePreview);
  }
  els["profile-form"].addEventListener("submit", onSubmit);
}

function fill(p) {
  els["email"].value = p.email || "";
  els["username"].value = p.username || "";
  els["display-name"].value = p.displayName || "";
  els["bio"].value = p.bio || "";
  els["about"].value = p.about || "";
  els["adj-zoom"].value = p.avatarZoom ?? 1;
  els["adj-x"].value = p.avatarPosX ?? 50;
  els["adj-y"].value = p.avatarPosY ?? 50;
  currentAvatarUrl = p.avatarUrl || null;
  removeRequested = false;
  pendingBlob = null;
  updatePreview();
}

/** Önizleme dairesini güncel foto + kaydırıcı değerleriyle çiz. */
function updatePreview() {
  const preview = els["avatar-preview"];
  const settings = {
    avatarZoom: Number(els["adj-zoom"].value),
    avatarPosX: Number(els["adj-x"].value),
    avatarPosY: Number(els["adj-y"].value),
  };

  const src = pendingObjectUrl || (removeRequested ? null : currentAvatarUrl);

  if (src) {
    preview.className = "avatar avatar--lg avatar--img";
    preview.style.background = "";
    render(preview, el("img", { src, alt: "", style: avatarImgStyle(settings) }));
    els["avatar-adjust"].hidden = false;
    els["avatar-remove"].hidden = false;
  } else {
    // Foto yok → baş harf dairesi
    const name = els["display-name"].value;
    preview.className = "avatar avatar--lg avatar--initials";
    preview.style.background = colorFor(name);
    preview.textContent = initialsOf(name);
    els["avatar-adjust"].hidden = true;
    els["avatar-remove"].hidden = true;
  }
}

/** Dosya seçilince: tarayıcıda küçült (kırpmadan) → JPEG blob → önizle. */
async function onPickFile() {
  const file = els["avatar-file"].files[0];
  if (!file) return;

  if (!/^image\/(jpe?g|png|webp)$/i.test(file.type)) {
    showError(t("profilDuzenle.errImgType"));
    return;
  }

  try {
    pendingBlob = await resizeToJpeg(file, AVATAR_MAX);
  } catch (err) {
    console.error(err);
    showError(t("profilDuzenle.errImgRead"));
    return;
  }

  if (pendingObjectUrl) URL.revokeObjectURL(pendingObjectUrl);
  pendingObjectUrl = URL.createObjectURL(pendingBlob);
  removeRequested = false;

  // Yeni foto → ayarları ortala.
  els["adj-zoom"].value = 1;
  els["adj-x"].value = 50;
  els["adj-y"].value = 50;
  updatePreview();
}

function onRemoveAvatar() {
  pendingBlob = null;
  if (pendingObjectUrl) {
    URL.revokeObjectURL(pendingObjectUrl);
    pendingObjectUrl = null;
  }
  removeRequested = true;
  els["avatar-file"].value = "";
  updatePreview();
}

async function onSubmit(e) {
  e.preventDefault();
  els["profile-error"].hidden = true;

  const username = els["username"].value.trim();
  const displayName = els["display-name"].value.trim();
  if (username.length < 3) {
    showError(t("profilDuzenle.errUsername"));
    return;
  }
  if (displayName.length < 2) {
    showError(t("profilDuzenle.errDisplay"));
    return;
  }

  els["profile-submit"].disabled = true;
  els["profile-submit"].textContent = t("common.saving");

  try {
    // 1) Foto değiştiyse önce onu gönder / kaldır.
    if (pendingBlob) {
      const fd = new FormData();
      fd.append("Image", pendingBlob, "avatar.jpg");
      await api.postForm("/api/profile/me/avatar", fd);
    } else if (removeRequested && currentAvatarUrl) {
      await api.del("/api/profile/me/avatar");
    }

    // 2) Metin alanları + fotoğraf gösterim ayarları.
    await api.put("/api/profile/me", {
      username,
      displayName,
      bio: els["bio"].value,
      about: els["about"].value,
      avatarZoom: Number(els["adj-zoom"].value),
      avatarPosX: Number(els["adj-x"].value),
      avatarPosY: Number(els["adj-y"].value),
    });

    // Navbar'daki avatar/ad tazelensin.
    clearSessionCache();
    const fresh = await api.get("/api/profile/me");
    fill(fresh);
    els["avatar-file"].value = "";
    if (pendingObjectUrl) { URL.revokeObjectURL(pendingObjectUrl); pendingObjectUrl = null; }
    await renderNav("profil-duzenle");

    flashSaved();   // düğmede kısa "kaydedildi" onayı, sonra normale döner
  } catch (err) {
    showError(err instanceof ApiError ? err.message : t("common.saveFailed"));
    restoreSubmit();
  }
}

/**
 * Kaydettikten sonra düğmede bir süre "Değişiklikler kaydedildi" görünür;
 * renkler soluklaşır (btn--ok), üst üste basmayı da engeller. Sonra düğme
 * eski haline döner (metin + renk + tıklanabilir).
 */
function flashSaved() {
  const btn = els["profile-submit"];
  btn.classList.add("btn--ok");
  btn.textContent = t("profilDuzenle.savedBtn");
  setTimeout(restoreSubmit, 1800);
}

/** Kaydet düğmesini normal haline döndür (onay sonrası ya da hata sonrası). */
function restoreSubmit() {
  const btn = els["profile-submit"];
  btn.classList.remove("btn--ok");
  btn.disabled = false;
  btn.textContent = t("common.save");
}

function showError(msg) {
  els["profile-error"].textContent = msg;
  els["profile-error"].hidden = false;
  els["profile-error"].scrollIntoView({ behavior: "smooth", block: "nearest" });
}

/**
 * Görseli <canvas> ile KIRPMADAN küçültür (en uzun kenar `maxEdge`, en-boy oranı
 * korunur) ve JPEG blob döner. Kırpma/oranlama daire içinde CSS ile yapıldığı için
 * burada tüm fotoğraf saklanır → kullanıcı sonradan istediği kısmı gösterebilir.
 */
function resizeToJpeg(file, maxEdge) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const scale = Math.min(1, maxEdge / Math.max(img.width, img.height));
      const w = Math.max(1, Math.round(img.width * scale));
      const h = Math.max(1, Math.round(img.height * scale));

      const canvas = document.createElement("canvas");
      canvas.width = w;
      canvas.height = h;
      canvas.getContext("2d").drawImage(img, 0, 0, w, h);

      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error("toBlob boş"))),
        "image/jpeg",
        0.85,
      );
    };
    img.onerror = () => reject(new Error("resim yüklenemedi"));
    img.src = URL.createObjectURL(file);
  });
}

init();
