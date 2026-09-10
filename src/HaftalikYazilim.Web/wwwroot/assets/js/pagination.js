// ============================================================================
// pagination.js — sayfa numarası çubuğu
// ----------------------------------------------------------------------------
//   «  1 … 4 [5] 6 … 12  »
// Çok sayfa olsa bile ~7 düğme gösterir, arasını "…" ile atlar.
// ============================================================================

import { el, render } from "./dom.js";

/**
 * @param {HTMLElement} container   Çubuğun çizileceği yer.
 * @param {object} state            { page, totalPages }
 * @param {(page:number)=>void} onGo Sayfa seçilince çağrılır.
 */
export function renderPagination(container, { page, totalPages }, onGo) {
  if (!container) return;

  if (!totalPages || totalPages <= 1) {
    render(container); // tek sayfa → çubuk yok
    return;
  }

  const nums = pageNumbers(page, totalPages);

  const btn = (label, target, { disabled = false, active = false, dots = false } = {}) => {
    if (dots) return el("span", { class: "pager__dots" }, ["…"]);
    const b = el("button", {
      class: `pager__btn${active ? " is-active" : ""}`,
      type: "button",
      disabled: disabled || active,
      "aria-current": active ? "page" : null,
    }, [label]);
    if (!disabled && !active) b.addEventListener("click", () => onGo(target));
    return b;
  };

  render(
    container,
    btn("‹", page - 1, { disabled: page <= 1 }),
    ...nums.map((n) => (n === "…" ? btn("…", 0, { dots: true }) : btn(String(n), n, { active: n === page }))),
    btn("›", page + 1, { disabled: page >= totalPages }),
  );
}

/** [1, "…", 4, 5, 6, "…", 12] gibi bir dizi üretir. */
function pageNumbers(current, total) {
  const span = 1; // geçerli sayfanın iki yanında kaç komşu
  const pages = new Set([1, total, current]);
  for (let i = 1; i <= span; i++) {
    pages.add(current - i);
    pages.add(current + i);
  }

  const sorted = [...pages].filter((p) => p >= 1 && p <= total).sort((a, b) => a - b);

  const out = [];
  let prev = 0;
  for (const p of sorted) {
    if (p - prev > 1) out.push("…");
    out.push(p);
    prev = p;
  }
  return out;
}
