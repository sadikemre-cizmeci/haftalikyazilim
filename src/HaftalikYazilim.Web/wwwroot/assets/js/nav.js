// ============================================================================
// nav.js — PAYLAŞILAN ÜST MENÜ (navbar)
// ----------------------------------------------------------------------------
// Her HTML sayfasında şu boş kap bulunur:   <header id="site-header"></header>
// Bu dosyadaki renderNav() onu doldurur.
//
// → Menüyü değiştirmek istersen SADECE burayı düzenle; tüm sayfalar etkilenir.
//
// Menü yapısı:
//   [logo + HaftalıkYazılım]  [Anasayfa] [Yazılar] .......... [kullanıcı ▾]
//
// Sağdaki alan:
//   - Giriş YOKSA  →  "Giriş" bağlantısı (giris.html)
//   - Giriş VARSA  →  açılır menü: "Yazılarım", (admin ise) "Yönetim", "Çıkış"
//
// Metinler i18n.js sözlüğünden gelir (t("nav....")). renderNav ayrıca alt bilgiyi
// (renderFooter) ve statik metin çevirisini (applyTranslations) tetikler; böylece
// her sayfa JS'ine tek tek eklememize gerek kalmaz.
// ============================================================================

import { el, qs } from "./dom.js";
import { ICONS } from "./icons.js";
import { getCurrentUser, logout } from "./session.js";
import { avatarEl } from "./avatar.js";
import { t, applyTranslations } from "./i18n.js";
import { renderFooter } from "./footer.js";

/**
 * Üst menüyü çizer.
 * @param {"home"|"yazilar"|"profilim"|"yonetim"|null} activePage
 *        Hangi menü öğesinin "aktif" görüneceği. İlgisi olmayan sayfalarda null geç.
 */
export async function renderNav(activePage = null) {
  // Statik metinleri (data-i18n) ve alt bilgiyi her sayfada tek yerden hallet.
  applyTranslations(document);
  renderFooter();

  const header = qs("#site-header");
  if (!header) return;

  const user = await getCurrentUser();

  // --- Sol: logo + isim ---
  // Logo ayrı bir SVG dosyası (assets/img/logo.svg). Fırça darbesi efektleri
  // (filtre, degrade) kendi içinde durduğu için <img> olarak veriyoruz —
  // hem sayfayı şişirmez hem de tarayıcı bir kez indirip önbelleğe alır.
  // alt="" çünkü hemen yanındaki yazı zaten adı söylüyor (ekran okuyucu tekrarlamasın).
  const brand = el("a", { class: "nav__brand", href: "index.html" }, [
    el("img", {
      class: "nav__logo",
      src: "assets/img/logo.svg",
      alt: "",
      width: 26,
      height: 26,
    }),
    el("span", { class: "nav__brandtext" }, ["HaftalıkYazılım"]),
  ]);

  // --- Sol: okuyucu bağlantıları ---
  const links = el("nav", { class: "nav__links", "aria-label": t("nav.menuLabel") }, [
    navLink(t("nav.home"), "index.html", activePage === "home"),
    navLink(t("nav.articles"), "yazilar.html", activePage === "yazilar"),
  ]);

  // --- Sağ: kullanıcı alanı ---
  const right = user
    ? buildUserMenu(user, activePage)
    : el(
        "a",
        { class: "nav__user", href: "giris.html", title: t("nav.loginTitle") },
        [
          el("span", { class: "nav__usericon", "aria-hidden": "true", html: ICONS.user }),
          el("span", { class: "nav__userlabel" }, [t("nav.login")]),
        ],
      );

  const inner = el("div", { class: "nav__inner" }, [
    el("div", { class: "nav__left" }, [brand, links]),
    right,
  ]);

  header.replaceChildren(inner);
}

/** Tek bir menü bağlantısı (aktifse işaretli). */
function navLink(text, href, isActive) {
  return el("a", {
    class: `nav__link${isActive ? " is-active" : ""}`,
    href,
    "aria-current": isActive ? "page" : null,
  }, [text]);
}

/** Giriş yapmış kullanıcı için açılır menü. */
function buildUserMenu(user, activePage) {
  // Kullanıcı düğmesinde profil fotoğrafı (yoksa baş harf dairesi).
  const button = el(
    "button",
    {
      class: "nav__user",
      type: "button",
      "aria-haspopup": "true",
      "aria-expanded": "false",
      title: user.displayName,
    },
    [
      avatarEl(user, 24),
      el("span", { class: "nav__userlabel" }, [user.displayName]),
    ],
  );

  const items = [
    el(
      "a",
      { class: `nav__menuitem${activePage === "profilim" ? " is-active" : ""}`, href: "profilim.html" },
      [t("nav.myArticles")],
    ),
    el(
      "a",
      { class: `nav__menuitem${activePage === "begendiklerim" ? " is-active" : ""}`, href: "begendiklerim.html" },
      [t("nav.liked")],
    ),
    el(
      "a",
      { class: `nav__menuitem${activePage === "kaydettiklerim" ? " is-active" : ""}`, href: "kaydettiklerim.html" },
      [t("nav.saved")],
    ),
    el(
      "a",
      { class: `nav__menuitem${activePage === "profil-duzenle" ? " is-active" : ""}`, href: "profil-duzenle.html" },
      [t("nav.profile")],
    ),
  ];

  // Sadece admin "Yönetim" linkini görür.
  if (user.isAdmin) {
    items.push(
      el(
        "a",
        { class: `nav__menuitem${activePage === "yonetim" ? " is-active" : ""}`, href: "yonetim.html" },
        [t("nav.admin")],
      ),
    );
  }

  const logoutButton = el(
    "button",
    { class: "nav__menuitem nav__menuitem--danger", type: "button" },
    [t("nav.logout")],
  );
  logoutButton.addEventListener("click", async () => {
    await logout();
    window.location.href = "index.html";
  });
  items.push(logoutButton);

  const menu = el("div", { class: "nav__menu", role: "menu", hidden: true }, items);
  const wrap = el("div", { class: "nav__usermenu" }, [button, menu]);

  // Aç / kapa
  button.addEventListener("click", (e) => {
    e.stopPropagation();
    const willOpen = menu.hidden;
    menu.hidden = !willOpen;
    button.setAttribute("aria-expanded", String(willOpen));
  });

  // Menü dışına tıklayınca kapat
  document.addEventListener("click", (e) => {
    if (!wrap.contains(e.target)) {
      menu.hidden = true;
      button.setAttribute("aria-expanded", "false");
    }
  });

  // Esc ile kapat
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") {
      menu.hidden = true;
      button.setAttribute("aria-expanded", "false");
    }
  });

  return wrap;
}
