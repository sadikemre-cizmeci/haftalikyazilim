// ============================================================================
// api.js — backend ile TÜM konuşma buradan geçer
// ----------------------------------------------------------------------------
// Tek yerde: JSON çevirme, hata yakalama, oturum çerezini gönderme.
// Sayfalar doğrudan fetch çağırmaz; hep bu "api" nesnesini kullanır.
// ============================================================================

import { CONFIG } from "./config.js";
import { t } from "./i18n.js";

/** API'den dönen hatalar bu tiple fırlatılır (status + sunucu mesajı). */
export class ApiError extends Error {
  constructor(message, status, payload) {
    super(message);
    this.name = "ApiError";
    this.status = status;      // HTTP kodu (401, 404, 400 ...)
    this.payload = payload;    // sunucunun döndürdüğü ham gövde (varsa)
  }
}

async function request(method, path, { body, isForm = false } = {}) {
  const options = {
    method,
    headers: {},
    // Oturum çerezinin gönderilmesi için gerekli (aynı origin'de de açıkça belirtiyoruz).
    credentials: "include",
  };

  if (body !== undefined) {
    if (isForm) {
      // FormData → Content-Type'ı (boundary ile birlikte) tarayıcı kendi ayarlar.
      options.body = body;
    } else {
      options.headers["Content-Type"] = "application/json";
      options.body = JSON.stringify(body);
    }
  }

  let response;
  try {
    response = await fetch(`${CONFIG.apiBaseUrl}${path}`, options);
  } catch {
    // Ağ hatası / sunucu kapalı
    throw new ApiError(t("err.network"), 0, null);
  }

  // 204 No Content → dönecek gövde yok.
  if (response.status === 204) {
    return null;
  }

  // NOT: hata yanıtları "application/problem+json" ile gelir → "json" kelimesine bak.
  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("json")
    ? await response.json().catch(() => null)
    : await response.text().catch(() => null);

  if (!response.ok) {
    // ASP.NET Core ProblemDetails gövdesi genelde { title, detail, errors } taşır.
    const message =
      (payload && (payload.detail || payload.title)) ||
      (typeof payload === "string" && payload) ||
      t("err.http", { status: response.status });
    throw new ApiError(message, response.status, payload);
  }

  return payload;
}

/**
 * multipart/form-data yükleme + GERÇEK ilerleme yüzdesi.
 * fetch() yükleme ilerlemesi vermez; bunun için XMLHttpRequest gerekir.
 * @param {"POST"|"PUT"} method
 * @param {string} path
 * @param {FormData} formData
 * @param {(fraction:number)=>void} [onProgress]  0..1 arası (yükleme bitince 1).
 * @returns {Promise<any>}  JSON gövde ya da null.
 */
function upload(method, path, formData, onProgress) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(method, `${CONFIG.apiBaseUrl}${path}`);
    xhr.withCredentials = true; // oturum çerezi

    if (xhr.upload && typeof onProgress === "function") {
      xhr.upload.addEventListener("progress", (e) => {
        if (e.lengthComputable) onProgress(e.loaded / e.total);
      });
    }

    const fail = () => reject(new ApiError(t("err.network"), 0, null));
    xhr.addEventListener("error", fail);
    xhr.addEventListener("timeout", fail);
    xhr.addEventListener("abort", fail);

    xhr.addEventListener("load", () => {
      const contentType = xhr.getResponseHeader("content-type") || "";
      let payload = null;
      if (xhr.responseText) {
        payload = contentType.includes("json")
          ? (() => { try { return JSON.parse(xhr.responseText); } catch { return null; } })()
          : xhr.responseText;
      }

      if (xhr.status >= 200 && xhr.status < 300) {
        if (typeof onProgress === "function") onProgress(1);
        resolve(payload);
        return;
      }

      const message =
        (payload && (payload.detail || payload.title)) ||
        (typeof payload === "string" && payload) ||
        t("err.http", { status: xhr.status });
      reject(new ApiError(message, xhr.status, payload));
    });

    xhr.send(formData);
  });
}

export const api = {
  get: (path) => request("GET", path),
  post: (path, body) => request("POST", path, { body }),
  put: (path, body) => request("PUT", path, { body }),
  del: (path) => request("DELETE", path),

  // multipart/form-data (PDF yükleme). body bir FormData olmalı.
  postForm: (path, formData) => request("POST", path, { body: formData, isForm: true }),
  putForm: (path, formData) => request("PUT", path, { body: formData, isForm: true }),

  // Yükleme ilerlemesi gereken yerler için (PDF yükleme formu).
  uploadPost: (path, formData, onProgress) => upload("POST", path, formData, onProgress),
  uploadPut: (path, formData, onProgress) => upload("PUT", path, formData, onProgress),
};
