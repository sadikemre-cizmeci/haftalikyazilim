// ============================================================================
// dom.js — küçük DOM yardımcıları
// ----------------------------------------------------------------------------
// Framework YOK. Bunlar sadece sık tekrar eden işleri kısaltır. Hepsi saf JS.
// ============================================================================

import { getLang, t } from "./i18n.js";

/** Tek eleman seç: qs(".card")  →  ilk eşleşen veya null */
export const qs = (selector, root = document) => root.querySelector(selector);

/** Çok eleman seç (gerçek diziye çevrilmiş hâlde): qsa("li.item") */
export const qsa = (selector, root = document) =>
  Array.from(root.querySelectorAll(selector));

/**
 * Element oluşturucu.
 * Örnek:
 *   el("a", { href: "x.html", class: "btn" }, ["Tıkla"])
 *   el("div", { class: "card", dataset: { id: 5 } }, [baslikEl, "metin"])
 *   el("button", { onclick: () => sil() }, ["Sil"])
 *
 * attrs özel anahtarları:
 *   class   → className
 *   dataset → data-* nitelikleri (obje)
 *   html    → innerHTML (dikkat: sadece güvendiğin sabit metinlerde kullan)
 *   onXxx   → addEventListener("xxx", fn)
 */
export function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);

  for (const [key, value] of Object.entries(attrs)) {
    if (value === null || value === undefined || value === false) {
      continue;
    }
    if (key === "class") {
      node.className = value;
    } else if (key === "dataset") {
      Object.assign(node.dataset, value);
    } else if (key === "html") {
      node.innerHTML = value;
    } else if (key.startsWith("on") && typeof value === "function") {
      node.addEventListener(key.slice(2).toLowerCase(), value);
    } else {
      node.setAttribute(key, value === true ? "" : String(value));
    }
  }

  for (const child of [].concat(children)) {
    if (child === null || child === undefined || child === false) {
      continue;
    }
    node.append(child.nodeType ? child : document.createTextNode(String(child)));
  }

  return node;
}

/** Bir kabın içeriğini tek seferde yenile. */
export function render(container, ...nodes) {
  container.replaceChildren(...nodes.flat().filter(Boolean));
}

/** ISO tarihi seçili dile göre kısa göster: "6 Eylül 2026" / "September 6, 2026" */
export function formatDate(isoString) {
  if (!isoString) return "";
  const d = new Date(isoString);
  if (Number.isNaN(d.getTime())) return "";
  const locale = getLang() === "en" ? "en-US" : "tr-TR";
  return d.toLocaleDateString(locale, {
    day: "numeric",
    month: "long",
    year: "numeric",
  });
}

/**
 * Tarihi "3 gün önce" / "3 days ago" gibi göreli göster (seçili dile göre).
 * Çok eskiyse tam tarihe düşer. Kartlarda "canlı" bir his verir.
 * Metin kalıpları i18n sözlüğünde: time.* (bkz. i18n.js).
 */
export function relativeTime(isoString) {
  if (!isoString) return "";
  const then = new Date(isoString);
  if (Number.isNaN(then.getTime())) return "";

  const secondsAgo = Math.round((Date.now() - then.getTime()) / 1000);
  const minutes = Math.round(secondsAgo / 60);
  const hours = Math.round(secondsAgo / 3600);
  const days = Math.round(secondsAgo / 86400);

  if (secondsAgo < 45) return t("time.justNow");
  if (minutes < 60) return t("time.minAgo", { n: minutes });
  if (hours < 24) return t("time.hourAgo", { n: hours });
  if (days === 1) return t("time.yesterday");
  if (days < 7) return t("time.dayAgo", { n: days });
  if (days < 30) return t("time.weekAgo", { n: Math.round(days / 7) });
  if (days < 365) return t("time.monthAgo", { n: Math.round(days / 30) });
  return formatDate(isoString);
}

/** Bir tarih "yeni" mi? (varsayılan: son 7 gün / 1 hafta) — "Yeni" rozeti için. */
export function isRecent(isoString, days = 7) {
  if (!isoString) return false;
  const then = new Date(isoString).getTime();
  if (Number.isNaN(then)) return false;
  return Date.now() - then < days * 86400 * 1000;
}

/** Dosya boyutunu okunaklı göster: 1536000 → "1.5 MB" */
export function formatBytes(bytes) {
  if (!bytes || bytes < 0) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  const value = bytes / Math.pow(1024, i);
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

/** URL'deki ?id=... gibi parametreyi oku. */
export function queryParam(name) {
  return new URLSearchParams(window.location.search).get(name);
}

/**
 * Kısa süreli bildirim kutusu (ekranın altında belirir).
 *   toast("Kaydedildi", "success")
 *   toast("Bir hata oluştu", "error")
 */
export function toast(message, type = "info") {
  let host = qs("#toast-host");
  if (!host) {
    host = el("div", { id: "toast-host" });
    document.body.append(host);
  }

  const item = el("div", { class: `toast toast--${type}` }, [message]);
  host.append(item);

  // Küçük gecikme → CSS geçiş animasyonu çalışsın.
  requestAnimationFrame(() => item.classList.add("toast--in"));

  setTimeout(() => {
    item.classList.remove("toast--in");
    setTimeout(() => item.remove(), 300);
  }, 3200);
}

/**
 * Girdi olayını geciktirir (arama kutusu her tuşta değil, durunca istek atsın).
 *   const ara = debounce(() => yukle(), 350);
 */
export function debounce(fn, delayMs = 300) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delayMs);
  };
}

/** <span id="year"> gibi bir yeri bu yıl ile doldur (alt bilgi için). */
export function setYear(selector = "#year") {
  const node = qs(selector);
  if (node) node.textContent = String(new Date().getFullYear());
}
