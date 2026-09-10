// ============================================================================
// footer.js — PAYLAŞILAN ALT BİLGİ (footer) + DİL SEÇİMİ (TR / EN)
// ----------------------------------------------------------------------------
// Her sayfada şu boş kap bulunur:   <footer id="site-footer" class="site-footer"></footer>
// renderFooter() onu doldurur (renderNav gibi). renderNav bittiğinde çağrılır,
// böylece ayrı ayrı her sayfa JS'ine eklemeye gerek kalmaz.
//
// Sağ tarafta "TR / EN" düğmeleri: tıklayınca dili değiştirir ve sayfayı yeniler
// (i18n.setLang → location.reload). Yenileme sayesinde JS ile üretilen her metin
// de (kartlar, bildirimler, "3 saat önce" vb.) yeni dille yeniden çizilir.
// ============================================================================

import { el, qs } from "./dom.js";
import { getLang, setLang, t } from "./i18n.js";

/** Alt bilgiyi çizer. #site-footer yoksa sessizce çıkar. */
export function renderFooter() {
  const foot = qs("#site-footer");
  if (!foot) return;

  const year = new Date().getFullYear();

  const copy = el("span", {}, [`© ${year} HaftalıkYazılım`]);

  const langSwitch = el(
    "div",
    { class: "lang-switch", role: "group", "aria-label": t("foot.langLabel") },
    ["tr", "en"].map((code) => {
      const active = getLang() === code;
      const btn = el(
        "button",
        {
          type: "button",
          "data-lang": code,
          "aria-pressed": active ? "true" : "false",
        },
        [code.toUpperCase()],
      );
      if (!active) btn.addEventListener("click", () => setLang(code));
      return btn;
    }),
  );

  foot.replaceChildren(el("div", { class: "site-footer__inner" }, [copy, langSwitch]));
}
