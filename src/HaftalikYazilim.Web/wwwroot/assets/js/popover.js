// ============================================================================
// popover.js — ⓘ bilgi baloncuğu (tek, paylaşılan)
// ----------------------------------------------------------------------------
// Aynı anda EN FAZLA bir baloncuk açık olur. Bir tetikleyiciye (ör. ⓘ butonu)
// bağlanır; içeriği ihtiyaç anında üretilir (progressive disclosure).
//
// Açılma:  fareyle üstüne gelince • klavyeyle odaklanınca • tık/dokunma
// Kapanma: fare hem tetikleyiciden hem baloncuktan çıkınca • dışarı tıklama •
//          Esc • sayfa kaydırma
//
// Konum:   tercihen tetikleyicinin ÜSTÜNDE; yer yoksa altına döner; yanlardan
//          ekrana taşarsa içeri çekilir.
//
// Kullanım (bkz. article-card.js):
//   import { attachPopover } from "./popover.js";
//   attachPopover(infoButton, () => buildInfoContent(article));
// ============================================================================

let host; // paylaşılan baloncuk DOM'u
let currentTrigger = null;
let hideTimer = null;

function ensureHost() {
  if (host) return host;
  host = document.createElement("div");
  host.className = "popover";
  host.setAttribute("role", "tooltip");
  host.hidden = true;

  // Fare baloncuğun içindeyken kapanmayı iptal et.
  host.addEventListener("mouseenter", () => clearTimeout(hideTimer));
  host.addEventListener("mouseleave", scheduleHide);

  document.body.append(host);
  return host;
}

function open(trigger, buildContent) {
  clearTimeout(hideTimer);
  const el = ensureHost();

  // İçeriği (yeniden) doldur.
  el.replaceChildren(buildContent());
  el.hidden = false;
  currentTrigger = trigger;
  trigger.setAttribute("aria-expanded", "true");

  position(el, trigger);
}

function hide() {
  if (!host || host.hidden) return;
  host.hidden = true;
  host.replaceChildren();
  if (currentTrigger) {
    currentTrigger.setAttribute("aria-expanded", "false");
    currentTrigger = null;
  }
}

function scheduleHide() {
  clearTimeout(hideTimer);
  hideTimer = setTimeout(hide, 120); // fareyi tetikleyici→baloncuk taşıyacak kadar kısa süre
}

/** Baloncuğu tetikleyiciye göre konumla (önce üst, olmazsa alt; yanları kırp). */
function position(el, trigger) {
  // Ölçüm için görünür ama şeffaf bırak.
  el.style.left = "0px";
  el.style.top = "0px";
  const pad = 8;
  const rect = trigger.getBoundingClientRect();
  const pop = el.getBoundingClientRect();

  // Dikey: üstte yer var mı?
  let top = rect.top - pop.height - pad;
  if (top < pad) {
    top = rect.bottom + pad; // altına al
  }

  // Yatay: tetikleyicinin ortasına hizala, sonra ekran içine çek.
  let left = rect.left + rect.width / 2 - pop.width / 2;
  const maxLeft = window.innerWidth - pop.width - pad;
  left = Math.max(pad, Math.min(left, maxLeft));

  el.style.left = `${Math.round(left)}px`;
  el.style.top = `${Math.round(top)}px`;
}

/**
 * Bir tetikleyiciye baloncuk davranışı bağlar.
 * @param {HTMLElement} trigger        ⓘ butonu gibi bir eleman
 * @param {() => Node} buildContent    Baloncuğun içeriğini üreten fonksiyon
 */
export function attachPopover(trigger, buildContent) {
  trigger.setAttribute("aria-haspopup", "true");
  trigger.setAttribute("aria-expanded", "false");

  const show = () => open(trigger, buildContent);
  const isOpenForThis = () => currentTrigger === trigger && host && !host.hidden;

  // Fare
  trigger.addEventListener("mouseenter", show);
  trigger.addEventListener("mouseleave", scheduleHide);

  // Klavye odağı
  trigger.addEventListener("focus", show);
  trigger.addEventListener("blur", scheduleHide);

  // Tık / dokunma → aç-kapa (dokunmatikte hover olmadığı için şart)
  trigger.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (isOpenForThis()) hide();
    else show();
  });
}

// --- Global kapanma olayları (bir kez bağlanır) ---
document.addEventListener("keydown", (e) => {
  if (e.key === "Escape") hide();
});
document.addEventListener("click", (e) => {
  if (!host || host.hidden) return;
  if (host.contains(e.target)) return;
  if (currentTrigger && currentTrigger.contains(e.target)) return;
  hide();
});
window.addEventListener("scroll", hide, { passive: true });
window.addEventListener("resize", hide);
