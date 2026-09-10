// ============================================================================
// session.js — "kim giriş yapmış?" durumunu yönetir
// ----------------------------------------------------------------------------
// getCurrentUser() sunucuya BİR KEZ sorar, sonucu hafızada tutar. Böylece her
// sayfa açılışında navbar + sayfa mantığı aynı bilgiyi tekrar tekrar istemez.
// ============================================================================

import { api } from "./api.js";

// _cache durumları:
//   undefined → henüz sorulmadı
//   null      → sorduk, giriş YOK (anonim)
//   {...}     → giriş yapan kullanıcı bilgisi
let _cache;
let _inflight; // aynı anda birden fazla istek atmayı önler

/**
 * Giriş yapan kullanıcıyı döndürür (yoksa null).
 * @param {{force?: boolean}} opts  force=true → önbelleği yok say, yeniden sor.
 */
export async function getCurrentUser({ force = false } = {}) {
  if (!force && _cache !== undefined) {
    return _cache;
  }
  if (_inflight) {
    return _inflight;
  }

  _inflight = api
    .get("/api/auth/me")
    .then((user) => {
      _cache = user ?? null; // 204 → null döner
      return _cache;
    })
    .catch(() => {
      _cache = null;
      return _cache;
    })
    .finally(() => {
      _inflight = undefined;
    });

  return _inflight;
}

/**
 * Kullanıcı adı VEYA e-posta + şifre ile giriş.
 * Başarılıysa kullanıcı bilgisini döndürür ve önbelleğe alır.
 */
export async function login(identifier, password) {
  const user = await api.post("/api/auth/login", { identifier, password });
  _cache = user;
  return user;
}

/** Yeni üye kaydı → başarılıysa otomatik giriş yapılmış olur. */
export async function register(payload) {
  const user = await api.post("/api/auth/register", payload);
  _cache = user;
  return user;
}

/** Oturumu kapatır ve önbelleği temizler. */
export async function logout() {
  try {
    await api.post("/api/auth/logout");
  } finally {
    _cache = null;
  }
}

/**
 * Önbelleği sıfırlar (bir sonraki getCurrentUser() sunucuya yeniden sorar).
 * Profil güncellendikten sonra navbar'daki avatar/ad tazelensin diye kullanılır.
 */
export function clearSessionCache() {
  _cache = undefined;
  _inflight = undefined;
}

/**
 * Sayfa "sadece giriş yapanlara" veya "sadece admin'e" açıksa, en başta bunu çağır.
 * Yetki yoksa kullanıcıyı uygun sayfaya yollar ve false döner.
 *
 *   const user = await requireAuth();          // giriş şart
 *   const user = await requireAuth({ admin: true });  // admin şart
 */
export async function requireAuth({ admin = false } = {}) {
  const user = await getCurrentUser();

  if (!user) {
    const next = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.href = `giris.html?next=${next}`;
    return null;
  }

  if (admin && !user.isAdmin) {
    window.location.href = "index.html";
    return null;
  }

  return user;
}
