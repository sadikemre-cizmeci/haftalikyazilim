// ============================================================================
// motion.js — hareket (animasyon) yardımcıları
// ----------------------------------------------------------------------------
// Sakin bir arayüz için: az, kısa, anlamlı hareket. Ve kullanıcı "hareketi azalt"
// dediyse HİÇ hareket etme.
// ============================================================================

/** Kullanıcı işletim sisteminde "hareketi azalt" seçtiyse true döner. */
export function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/**
 * Verilen elemanları "kaydırınca belir" (fade + hafif yukarı) yapar.
 * - Her eleman görünür alana girince .is-in eklenir (CSS geçişi devreye girer).
 * - Kademeli (stagger) gecikme: sıradaki eleman biraz daha geç belirir.
 * - Bir kez olur; sonra gözlemci bırakılır (performans).
 * - reduced-motion ise: hemen görünür yap, animasyon yok.
 *
 * Kullanım (bkz. pages/yazilar.js):
 *   import { revealOnScroll } from "../motion.js";
 *   revealOnScroll(document.querySelectorAll(".article-card"));
 */
export function revealOnScroll(elements) {
  const list = Array.from(elements);
  if (list.length === 0) return;

  // Hareket istenmiyorsa ya da IntersectionObserver yoksa: direkt göster.
  if (prefersReducedMotion() || !("IntersectionObserver" in window)) {
    list.forEach((el) => el.classList.add("is-in"));
    return;
  }

  const observer = new IntersectionObserver(
    (entries, obs) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        const el = entry.target;
        // Aynı anda görünen kartlar arasında küçük kademe gecikmesi.
        const index = Number(el.dataset.revealIndex || 0);
        el.style.transitionDelay = `${Math.min(index, 6) * 40}ms`;
        el.classList.add("is-in");
        obs.unobserve(el);
      });
    },
    { rootMargin: "0px 0px -10% 0px", threshold: 0.05 },
  );

  list.forEach((el, i) => {
    el.classList.add("reveal");
    el.dataset.revealIndex = String(i);
    observer.observe(el);
  });
}
