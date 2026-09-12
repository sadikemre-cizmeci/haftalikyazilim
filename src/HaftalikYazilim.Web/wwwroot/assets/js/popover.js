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

/**
 * Baloncuğu tetikleyiciye göre konumla: önce daha çok yer olan tarafa (üst ya
 * da alt) yerleştirir; yanlardan taşarsa içeri çeker. İçerik o taraftaki
 * boşluğa sığmayacak kadar uzunsa (ör. uzun özet + madde listesi), baloncuğu
 * EKRAN DIŞINA TAŞIRIP KIRPMAK yerine kendi yüksekliğini o boşlukla sınırlar
 * ve kendi içinde kaydırılabilir yapar (bkz. .popover { overflow-y: auto }).
 */
function position(el, trigger) {
  // Ölçüm için görünür ama şeffaf bırak; önceki açılıştan kalan sınırı sıfırla.
  el.style.maxHeight = "none";
  el.style.left = "0px";
  el.style.top = "0px";
  const pad = 8;
  const rect = trigger.getBoundingClientRect();
  const pop = el.getBoundingClientRect();

  // Dikey: üstte mi altta mı daha çok yer var? Daha ferah olan tarafı seç.
  const spaceAbove = rect.top - pad * 2;
  const spaceBelow = window.innerHeight - rect.bottom - pad * 2;
  const useAbove = spaceAbove >= spaceBelow;
  const maxHeight = Math.max(120, Math.floor(useAbove ? spaceAbove : spaceBelow));

  const top = useAbove
    ? Math.max(pad, rect.top - Math.min(pop.height, maxHeight) - pad)
    : rect.bottom + pad;

  // Yatay: tetikleyicinin ortasına hizala, sonra ekran içine çek.
  let left = rect.left + rect.width / 2 - pop.width / 2;
  const maxLeft = window.innerWidth - pop.width - pad;
  left = Math.max(pad, Math.min(left, maxLeft));

  el.style.left = `${Math.round(left)}px`;
  el.style.top = `${Math.round(top)}px`;
  el.style.maxHeight = `${maxHeight}px`;
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
