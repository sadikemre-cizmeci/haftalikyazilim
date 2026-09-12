// ============================================================================
// i18n.js — arayüz çevirisi (Türkçe / İngilizce)
// ----------------------------------------------------------------------------
// • Framework YOK, derleme adımı YOK. Tek sözlük + küçük bir uygulayıcı.
// • Dil seçimi:  localStorage("hy_lang")  →  yoksa tarayıcı dili (tr değilse en).
// • Dil değişince sayfayı YENİLERİZ (location.reload). Böylece JS ile üretilen
//   tüm metinler (kartlar, bildirimler, "3 saat önce" vb.) yeni dille yeniden çizilir.
//   Ayrı ayrı "canlı değiştir" mantığı kurmaya gerek kalmaz.
//
// Statik HTML'de kullanım:
//   <h1 data-i18n="yazilar.h1">Yazılar</h1>
//   <input data-i18n-attr="placeholder:yazilar.searchPh" ... />
//   <title data-i18n="title.articles">Yazılar — HaftalıkYazılım</title>
//   → applyTranslations() bunları çevirir (sayfa yüklenince otomatik çalışır).
//
// JS içinde kullanım:
//   import { t } from "../i18n.js";
//   el("button", {}, [ t("common.save") ])
//   t("profil.since", { date: "6 Eylül 2026" })   // {date} yerine koyar
//
// Yeni metin eklerken: aşağıdaki DICT.tr ve DICT.en içine AYNI anahtarla ekle.
// ============================================================================

const STORE_KEY = "hy_lang";

/** localStorage ok-yaz güvenli sarmalayıcılar (gizli sekme / kapalı depo patlamasın). */
function readStored() {
  try {
    const v = localStorage.getItem(STORE_KEY);
    return v === "tr" || v === "en" ? v : null;
  } catch {
    return null;
  }
}
function writeStored(lang) {
  try {
    localStorage.setItem(STORE_KEY, lang);
  } catch {
    /* yok say */
  }
}

/** Kayıtlı seçim yoksa tarayıcı diline bak: "tr..." ise Türkçe, değilse İngilizce. */
function detectLang() {
  const saved = readStored();
  if (saved) return saved;
  const nav = (navigator.language || navigator.userLanguage || "tr").toLowerCase();
  return nav.startsWith("tr") ? "tr" : "en";
}

let LANG = detectLang();

/** Şu anki dil kodu ("tr" | "en"). */
export function getLang() {
  return LANG;
}

/** Dili değiştir ve sayfayı yenile. Aynı dilse hiçbir şey yapma. */
export function setLang(next) {
  if (next !== "tr" && next !== "en") return;
  if (next === LANG) return;
  writeStored(next);
  location.reload();
}

/** {ad} kalıplarını params.ad ile doldurur. */
function interpolate(str, params) {
  return str.replace(/\{(\w+)\}/g, (whole, key) =>
    Object.prototype.hasOwnProperty.call(params, key) ? String(params[key]) : whole,
  );
}

/**
 * Anahtarın karşılığını verir. Sırayla: seçili dil → Türkçe → anahtarın kendisi.
 * @param {string} key
 * @param {object} [params]  {ad: değer} — metindeki {ad} yerlerine konur.
 */
export function t(key, params) {
  const table = DICT[LANG] || DICT.tr;
  const raw = table[key] ?? DICT.tr[key] ?? key;
  return params ? interpolate(raw, params) : raw;
}

/**
 * Kapsamdaki (varsayılan: tüm belge) data-i18n işaretli elemanları çevirir.
 *   data-i18n="anahtar"                  → textContent
 *   data-i18n-attr="placeholder:anahtar|title:anahtar2"  → setAttribute
 *   data-i18n-html="anahtar"             → innerHTML (SADECE kendi sabit metinlerimiz)
 */
export function applyTranslations(root = document) {
  root.querySelectorAll("[data-i18n]").forEach((node) => {
    const key = node.getAttribute("data-i18n");
    if (key) node.textContent = t(key);
  });

  root.querySelectorAll("[data-i18n-attr]").forEach((node) => {
    node
      .getAttribute("data-i18n-attr")
      .split("|")
      .forEach((pair) => {
        const [attr, key] = pair.split(":").map((s) => s.trim());
        if (attr && key) node.setAttribute(attr, t(key));
      });
  });

  root.querySelectorAll("[data-i18n-html]").forEach((node) => {
    const key = node.getAttribute("data-i18n-html");
    if (key) node.innerHTML = t(key);
  });
}

// ============================================================================
// SÖZLÜK
// ----------------------------------------------------------------------------
// tr = kaynak (yedek). en = İngilizce. Anahtarlar birebir aynı olmalı.
// ============================================================================

const DICT = {
  tr: {
    // -- Ortak --
    "common.loading": "Yükleniyor…",
    "common.save": "Kaydet",
    "common.saving": "Kaydediliyor…",
    "common.saveFailed": "Kaydedilemedi.",
    "common.deleteFailed": "Silinemedi.",
    "common.actionFailed": "İşlem başarısız.",
    "common.listLoadFail": "Liste yüklenemedi.",
    "common.backToArticles": "Yazılara dön",
    "common.browseArticles": "Yazılara göz at",
    "common.published": "Yayında",
    "common.draft": "Taslak",

    // -- Navbar --
    "nav.home": "Anasayfa",
    "nav.articles": "Yazılar",
    "nav.login": "Giriş",
    "nav.loginTitle": "Giriş yap",
    "nav.menuLabel": "Ana menü",
    "nav.myArticles": "Yazılarım",
    "nav.liked": "Beğendiklerim",
    "nav.saved": "Kaydettiklerim",
    "nav.profile": "Profilim",
    "nav.admin": "Yönetim",
    "nav.logout": "Çıkış",

    // -- Alt bilgi + dil --
    "foot.langLabel": "Dil",

    // -- Göreli zaman --
    "time.justNow": "az önce",
    "time.minAgo": "{n} dk önce",
    "time.hourAgo": "{n} saat önce",
    "time.yesterday": "dün",
    "time.dayAgo": "{n} gün önce",
    "time.weekAgo": "{n} hafta önce",
    "time.monthAgo": "{n} ay önce",

    // -- API hataları --
    "err.network": "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et.",
    "err.http": "İstek başarısız oldu (HTTP {status}).",

    // -- PDF görüntüleyici --
    "pdfv.prevPage": "Önceki sayfa",
    "pdfv.nextPage": "Sonraki sayfa",
    "pdfv.pageNumber": "Sayfa numarası",
    "pdfv.zoomOut": "Uzaklaştır",
    "pdfv.zoomIn": "Yakınlaştır",
    "pdfv.download": "İndir",
    "pdfv.exit": "Çık",
    "pdfv.loading": "PDF yükleniyor…",
    "pdfv.loadFail": "PDF açılamadı. Dosya bulunamadı veya bozuk olabilir.",

    // -- Yazı kartı --
    "card.infoTitle": "Bu yazı hakkında",
    "card.infoAria": "Bu yazı hakkında bilgi",
    "card.edit": "Yazıyı düzenle",
    "card.delete": "Yazıyı sil",
    "card.new": "Yeni",
    "card.downloadPdf": "PDF indir",
    "card.noSummary": "Bu yazı için özet bırakılmamış.",
    "card.rowPublish": "Yayın",
    "card.rowSize": "Boyut",
    "card.rowViews": "Görüntülenme",
    "card.rowDownloads": "İndirme",
    "card.stillDraft": "Henüz taslak",

    // -- Yazı tablosu (yönetim / liste) --
    "table.title": "Başlık",
    "table.author": "Yazar",
    "table.status": "Durum",
    "table.published": "Yayın",
    "table.updated": "Güncelleme",
    "table.size": "Boyut",
    "table.views": "Görüntül.",
    "table.downloads": "İndirme",
    "table.deleteAria": "{title} — sil",

    // -- Sosyal (beğen / kaydet / paylaş / yorum) --
    "social.like": "Beğen",
    "social.saved": "Kaydedildi",
    "social.save": "Kaydet",
    "social.saveTitle": "Okuma listeme kaydet",
    "social.share": "Paylaş",
    "social.shareTitle": "Bağlantıyı paylaş",
    "social.comment": "Yorum",
    "social.commentsTitle": "Yorumlar",
    "social.savedToast": "Okuma listene eklendi.",
    "social.unsavedToast": "Listeden çıkarıldı.",
    "social.linkCopied": "Bağlantı kopyalandı.",
    "social.copyFailed": "Kopyalanamadı: {url}",

    // -- Anasayfa --
    "title.home": "HaftalıkYazılım — Anasayfa",
    "home.h1": "Oku, yaz, takip et.",
    "home.lead":
      "Yazılımla ilgili teknik yazıların paylaşıldığı açık bir arşiv. Herkes hesap açıp kendi yazısını PDF olarak yayımlayabilir; yazıları okuyabilir, beğenebilir, yorumlayabilir, kaydedebilir ve yazarları takip edebilirsin.",
    "home.browse": "Yazılara göz at",
    "home.signup": "Hesap aç",
    "home.contactPrefix": "Öneri veya geri bildirim için:",

    // -- Yazılar --
    "title.articles": "Yazılar — HaftalıkYazılım",
    "yazilar.h1": "Yazılar",
    "yazilar.scopeAll": "Tümü",
    "yazilar.scopeFollowing": "Takip ettiklerim",
    "yazilar.searchPh": "Yazı başlığı veya yazar adı ara…",
    "yazilar.pagerLabel": "Sayfalar",
    "yazilar.loadFail": "Yazılar yüklenemedi. Sayfayı yenilemeyi dene.",
    "yazilar.noResults": "“{q}” için sonuç yok.",
    "yazilar.noFollowing": "Takip ettiğin kişilerin henüz yayınlanmış yazısı yok.",
    "yazilar.empty": "Henüz yayınlanmış bir yazı yok.",
    "yazilar.authorNote": "{name} — yazıları",
    "yazilar.seeAll": "hepsini gör",

    // -- Giriş --
    "title.login": "Giriş — HaftalıkYazılım",
    "giris.h1": "Giriş",
    "giris.lead":
      "Okumak için giriş gerekmez. Yazmak, takip etmek, beğenmek ve yorum yapmak için hesap gerekir.",
    "giris.idLabel": "Kullanıcı adı veya e-posta",
    "giris.pwLabel": "Şifre",
    "giris.forgot": "Şifremi unuttum",
    "giris.submit": "Giriş yap",
    "giris.submitting": "Giriş yapılıyor…",
    "giris.noAccount": "Hesabın yok mu?",
    "giris.registerLink": "Kayıt ol",
    "giris.needBoth": "Kullanıcı adı/e-posta ve şifre gerekli.",
    "giris.tooMany": "Çok fazla deneme yaptın. Bir dakika sonra tekrar dene.",
    "giris.failed": "Giriş yapılamadı.",

    // -- Kayıt --
    "title.register": "Kayıt ol — HaftalıkYazılım",
    "kayit.h1": "Kayıt ol",
    "kayit.lead":
      "Hesap açınca yazı yayımlayabilir, yazarları takip edebilir, beğenip yorum yapabilirsin.",
    "kayit.displayLabel": "Görünen ad",
    "kayit.displayHint": "Yazılarında ve profilinde görünür.",
    "kayit.usernameLabel": "Kullanıcı adı",
    "kayit.usernameHint":
      "3–30 karakter; harf, rakam, nokta, alt çizgi. Giriş bununla da yapılır.",
    "kayit.emailLabel": "E-posta",
    "kayit.pwLabel": "Şifre",
    "kayit.pwHint": "En az 8 karakter.",
    "kayit.submit": "Hesap aç",
    "kayit.submitting": "Oluşturuluyor…",
    "kayit.haveAccount": "Zaten hesabın var mı?",
    "kayit.loginLink": "Giriş yap",
    "kayit.errDisplay": "Görünen ad en az 2 karakter olmalı.",
    "kayit.errUsername": "Kullanıcı adı en az 3 karakter olmalı.",
    "kayit.errEmail": "Geçerli bir e-posta gir.",
    "kayit.errPw": "Şifre en az 8 karakter olmalı.",
    "kayit.failed": "Kayıt yapılamadı.",

    // -- Şifremi unuttum --
    "title.reset": "Şifremi sıfırla — HaftalıkYazılım",
    "sifre.h1": "Şifremi sıfırla",
    "sifre.idLabel": "Kullanıcı adı veya e-posta",
    "sifre.submit": "Şifremi sıfırla",
    "sifre.submitting": "Sıfırlanıyor…",
    "sifre.backToLogin": "← Girişe dön",
    "sifre.copy": "Kopyala",
    "sifre.resultHint": "Bu şifre bir daha gösterilmez. Kaydet, giriş yap, profilinden değiştir.",
    "sifre.loginBtn": "Giriş yap",
    "sifre.needId": "Kullanıcı adı veya e-posta gir.",
    "sifre.copied": "Kopyalandı.",
    "sifre.copyManual": "Kopyalanamadı — elle seç.",
    "sifre.failed": "İşlem başarısız.",

    // -- Herkese açık profil --
    "title.profile": "Profil — HaftalıkYazılım",
    "profil.articlesTitle": "Yazıları",
    "profil.notSpecified": "Profil belirtilmedi.",
    "profil.notFound": "Profil bulunamadı",
    "profil.loadFail": "Profil yüklenemedi",
    "profil.statArticles": "yazı",
    "profil.statFollowers": "takipçi",
    "profil.statFollowing": "takip",
    "profil.since": "{date}’den beri",
    "profil.followForLogin": "Takip için giriş",
    "profil.following": "Takipte",
    "profil.follow": "Takip et",
    "profil.noArticlesYet": "Henüz yayınlanmış yazısı yok.",
    "profil.articlesLoadFail": "Yazılar yüklenemedi.",

    // -- Yazılarım --
    "title.myArticles": "Yazılarım — HaftalıkYazılım",
    "profilim.h1": "Yazılarım",
    "profilim.sub": "Taslak ve yayında olan tüm yazıların.",
    "profilim.new": "Yeni yazı",
    "profilim.loadFail": "Yazıların yüklenemedi.",
    "profilim.emptyTitle": "Buradan başlıyor",
    "profilim.emptyBody":
      "İlk yazını bir PDF olarak yükle. Acele yok — hazır olduğunda yayınlarsın.",
    "profilim.emptyCta": "İlk yazını ekle",
    "profilim.confirmDelete": "“{title}” yazısı silinsin mi?\n\n(Yazı çöp kutusuna taşınır.)",
    "profilim.deleted": "Yazı silindi.",
    "profilim.tabAll": "Tümü",
    "profilim.tabPublished": "Yayında",
    "profilim.tabDraft": "Taslak",
    "profilim.emptyPublished": "Henüz yayında yazın yok.",
    "profilim.emptyDraft": "Taslağın yok.",

    // -- Beğendiklerim --
    "title.liked": "Beğendiklerim — HaftalıkYazılım",
    "begendiklerim.h1": "Beğendiklerim",
    "begendiklerim.sub": "Beğendiğin yazılar — en yeniden eskiye.",
    "begendiklerim.emptyTitle": "Henüz beğendiğin yazı yok",
    "begendiklerim.emptyBody": "Bir yazının altındaki kalbe dokunarak beğenebilirsin.",

    // -- Kaydettiklerim --
    "title.saved": "Kaydettiklerim — HaftalıkYazılım",
    "kaydettiklerim.h1": "Kaydettiklerim",
    "kaydettiklerim.sub": "Yıldızladığın yazılar — sonra dönmek için.",
    "kaydettiklerim.emptyTitle": "Henüz kaydettiğin yazı yok",
    "kaydettiklerim.emptyBody": "Bir yazının altındaki yıldıza dokunarak buraya ekleyebilirsin.",

    // -- Yeni yazı / düzenle --
    "title.editArticle": "Yazı düzenle — HaftalıkYazılım",
    "yaziDuzenle.backToMine": "← Yazılarım",
    "yaziDuzenle.titleLabel": "Başlık",
    "yaziDuzenle.summaryLabel": "Özet",
    "yaziDuzenle.optional": "(isteğe bağlı)",
    "yaziDuzenle.summaryPh":
      "Yazının kısa özeti (ⓘ baloncuğunda görünür). Liste için '- ', kalın için **böyle**.",
    "yaziDuzenle.summaryHint": "En fazla 500 karakter.",
    "yaziDuzenle.pdfLabel": "PDF dosyası",
    "yaziDuzenle.pdfHintNew": "Zorunlu. Yalnızca PDF, en fazla 50 MB.",
    "yaziDuzenle.headingNew": "Yeni yazı",
    "yaziDuzenle.headingEdit": "Yazıyı düzenle",
    "yaziDuzenle.saveDraft": "Taslak olarak kaydet",
    "yaziDuzenle.publishBtn": "Yayınla",
    "yaziDuzenle.savingBtn": "Kaydediliyor…",
    "yaziDuzenle.uploading": "Yükleniyor… %{pct}",
    "yaziDuzenle.processing": "İşleniyor…",
    "yaziDuzenle.pdfPick": "PDF seç",
    "yaziDuzenle.pdfReplace": "Değiştir",
    "yaziDuzenle.pdfView": "Görüntüle",
    "yaziDuzenle.pdfRemove": "Seçili dosyayı kaldır",
    "yaziDuzenle.notFound": "Yazı bulunamadı ya da görüntüleme yetkin yok.",
    "yaziDuzenle.unpublished": "Yayından kaldırıldı.",
    "yaziDuzenle.published": "Yayınlandı.",
    "yaziDuzenle.errPdfType": "Dosya PDF olmalı.",
    "yaziDuzenle.errPdfSize": "PDF en fazla 50 MB olabilir.",
    "yaziDuzenle.titleReqPublish": "Yayınlamak için bir başlık gerekli.",
    "yaziDuzenle.pdfReqPublish": "Yayınlamak için bir PDF eklemelisin.",
    "yaziDuzenle.summaryTooLong": "Özet 500 karakteri aştı (şu an {count}).",
    "yaziDuzenle.draftSavedMsg": "Taslak olarak kaydedildi",
    "yaziDuzenle.publishedMsg": "Yazın yayınlandı.",
    "yaziDuzenle.goToMyArticles": "Yazılarıma git",
    "yaziDuzenle.keepEditing": "Düzenlemeye devam et",
    "yaziDuzenle.savedChanges": "Değişiklikler kaydedildi.",
    "yaziDuzenle.createdDraft": "Yazı taslak olarak kaydedildi.",

    // -- Okuma sayfası --
    "title.read": "Yazı — HaftalıkYazılım",
    "oku.commentsHeading": "Yorumlar",
    "oku.noId": "Yazı belirtilmedi.",
    "oku.notFound": "Yazı bulunamadı ya da yayında değil.",
    "oku.loadFail": "Yazı yüklenemedi.",
    "oku.loginToComment1": "Giriş yap",
    "oku.loginToComment2": " veya ",
    "oku.loginToComment3": "kayıt ol",
    "oku.loginToComment4": " — yorum yapabilmek için.",
    "oku.commentsLoadFail": "Yorumlar yüklenemedi.",
    "oku.firstComment": "İlk yorumu sen yaz.",
    "oku.commentPh": "Yorumun…",
    "oku.send": "Gönder",
    "oku.commentAddFail": "Yorum eklenemedi.",
    "oku.deleteComment": "Yorumu sil",
    "oku.confirmDeleteComment": "Yorum silinsin mi?",
    "oku.deleteCommentShort": "sil",

    // -- Yönetim --
    "title.admin": "Yönetim — HaftalıkYazılım",
    "yonetim.h1": "Yönetim",
    "yonetim.sub": "İstatistikler, kullanıcılar ve tüm yazılar. (Salt-okunur)",
    "yonetim.statsTitle": "İstatistik — ilk 10",
    "yonetim.today": "Bugün",
    "yonetim.week": "Bir hafta",
    "yonetim.month": "Bir ay",
    "yonetim.usersTitle": "Kullanıcılar",
    "yonetim.allArticles": "Tüm yazılar",
    "yonetim.includeDeleted": "Silinmişleri de göster",
    "yonetim.statViewed": "En çok görüntülenen",
    "yonetim.statDownloaded": "En çok indirilen",
    "yonetim.statLiked": "En çok beğeni alan",
    "yonetim.statBookmarked": "En çok kaydedilen",
    "yonetim.statShared": "En çok paylaşılan",
    "yonetim.unitViews": "görüntülenme",
    "yonetim.unitDownloads": "indirme",
    "yonetim.unitLikes": "beğeni",
    "yonetim.unitSaves": "kaydetme",
    "yonetim.unitShares": "paylaşım",
    "yonetim.noData": "Bu aralıkta veri yok.",
    "yonetim.statsLoadFail": "İstatistik yüklenemedi.",
    "yonetim.usersLoadFail": "Kullanıcılar yüklenemedi.",
    "yonetim.articlesLoadFail": "Yazılar yüklenemedi.",
    "yonetim.noRecords": "Kayıt yok.",
    "yonetim.totalRecords": "Toplam {n} kayıt.",
    "yonetim.colName": "Ad",
    "yonetim.colEmail": "E-posta",
    "yonetim.colRole": "Rol",
    "yonetim.colStatus": "Durum",
    "yonetim.colJoined": "Katılım",
    "yonetim.colArticles": "Yazı",
    "yonetim.roleAdmin": "Yönetici",
    "yonetim.roleAuthor": "Yazar",
    "yonetim.active": "Aktif",
    "yonetim.inactive": "Pasif",

    // -- Profil düzenle --
    "title.editProfile": "Profilim — HaftalıkYazılım",
    "profilDuzenle.h1": "Profilim",
    "profilDuzenle.backToMine": "← Yazılarım",
    "profilDuzenle.photoLabel": "Profil fotoğrafı",
    "profilDuzenle.pickPhoto": "Fotoğraf seç",
    "profilDuzenle.removePhoto": "Kaldır",
    "profilDuzenle.zoom": "Yakınlaştır",
    "profilDuzenle.horizontal": "Yatay",
    "profilDuzenle.vertical": "Dikey",
    "profilDuzenle.emailLabel": "E-posta",
    "profilDuzenle.usernameLabel": "Kullanıcı adı",
    "profilDuzenle.displayLabel": "Görünen ad",
    "profilDuzenle.bioLabel": "Kısa tanıtım",
    "profilDuzenle.bioPh": "Bir-iki cümle. ⓘ baloncuğunda ve profil başlığında görünür.",
    "profilDuzenle.aboutLabel": "Hakkında",
    "profilDuzenle.aboutPh": "Profil sayfanda görünecek tam metin. Boş satır = paragraf.",
    "profilDuzenle.savedBtn": "Değişiklikler kaydedildi",
    "profilDuzenle.errUsername": "Kullanıcı adı en az 3 karakter olmalı.",
    "profilDuzenle.errDisplay": "Görünen ad en az 2 karakter olmalı.",
    "profilDuzenle.errImgType": "Yalnızca JPG, PNG veya WebP seçebilirsin.",
    "profilDuzenle.errImgRead": "Fotoğraf okunamadı. Başka bir dosya dene.",
  },

  en: {
    // -- Common --
    "common.loading": "Loading…",
    "common.save": "Save",
    "common.saving": "Saving…",
    "common.saveFailed": "Couldn’t save.",
    "common.deleteFailed": "Couldn’t delete.",
    "common.actionFailed": "Something went wrong.",
    "common.listLoadFail": "Couldn’t load the list.",
    "common.backToArticles": "Back to articles",
    "common.browseArticles": "Browse articles",
    "common.published": "Published",
    "common.draft": "Draft",

    // -- Navbar --
    "nav.home": "Home",
    "nav.articles": "Articles",
    "nav.login": "Sign in",
    "nav.loginTitle": "Sign in",
    "nav.menuLabel": "Main menu",
    "nav.myArticles": "My Articles",
    "nav.liked": "Liked",
    "nav.saved": "Saved",
    "nav.profile": "My Profile",
    "nav.admin": "Admin",
    "nav.logout": "Sign out",

    // -- Footer + language --
    "foot.langLabel": "Language",

    // -- Relative time --
    "time.justNow": "just now",
    "time.minAgo": "{n} min ago",
    "time.hourAgo": "{n} h ago",
    "time.yesterday": "yesterday",
    "time.dayAgo": "{n} days ago",
    "time.weekAgo": "{n} wk ago",
    "time.monthAgo": "{n} mo ago",

    // -- API errors --
    "err.network": "Couldn’t reach the server. Check your connection.",
    "err.http": "Request failed (HTTP {status}).",

    // -- PDF viewer --
    "pdfv.prevPage": "Previous page",
    "pdfv.nextPage": "Next page",
    "pdfv.pageNumber": "Page number",
    "pdfv.zoomOut": "Zoom out",
    "pdfv.zoomIn": "Zoom in",
    "pdfv.download": "Download",
    "pdfv.exit": "Exit",
    "pdfv.loading": "Loading PDF…",
    "pdfv.loadFail": "Couldn’t open the PDF. The file may be missing or corrupt.",

    // -- Article card --
    "card.infoTitle": "About this article",
    "card.infoAria": "Information about this article",
    "card.edit": "Edit article",
    "card.delete": "Delete article",
    "card.new": "New",
    "card.downloadPdf": "Download PDF",
    "card.noSummary": "No summary was left for this article.",
    "card.rowPublish": "Published",
    "card.rowSize": "Size",
    "card.rowViews": "Views",
    "card.rowDownloads": "Downloads",
    "card.stillDraft": "Still a draft",

    // -- Article table --
    "table.title": "Title",
    "table.author": "Author",
    "table.status": "Status",
    "table.published": "Published",
    "table.updated": "Updated",
    "table.size": "Size",
    "table.views": "Views",
    "table.downloads": "Downloads",
    "table.deleteAria": "{title} — delete",

    // -- Social --
    "social.like": "Like",
    "social.saved": "Saved",
    "social.save": "Save",
    "social.saveTitle": "Save to my reading list",
    "social.share": "Share",
    "social.shareTitle": "Share link",
    "social.comment": "Comment",
    "social.commentsTitle": "Comments",
    "social.savedToast": "Added to your reading list.",
    "social.unsavedToast": "Removed from your list.",
    "social.linkCopied": "Link copied.",
    "social.copyFailed": "Couldn’t copy: {url}",

    // -- Home --
    "title.home": "HaftalıkYazılım — Home",
    "home.h1": "Read, write, follow.",
    "home.lead":
      "An open archive of technical writing about software. Anyone can create an account and publish their own article as a PDF; you can read articles, like them, comment, save them, and follow authors.",
    "home.browse": "Browse articles",
    "home.signup": "Create account",
    "home.contactPrefix": "For suggestions or feedback:",

    // -- Articles --
    "title.articles": "Articles — HaftalıkYazılım",
    "yazilar.h1": "Articles",
    "yazilar.scopeAll": "All",
    "yazilar.scopeFollowing": "Following",
    "yazilar.searchPh": "Search by article title or author…",
    "yazilar.pagerLabel": "Pages",
    "yazilar.loadFail": "Couldn’t load articles. Try refreshing the page.",
    "yazilar.noResults": "No results for “{q}”.",
    "yazilar.noFollowing": "People you follow haven’t published anything yet.",
    "yazilar.empty": "No articles published yet.",
    "yazilar.authorNote": "{name} — articles",
    "yazilar.seeAll": "see all",

    // -- Sign in --
    "title.login": "Sign in — HaftalıkYazılım",
    "giris.h1": "Sign in",
    "giris.lead":
      "You don’t need an account to read. Writing, following, liking and commenting need one.",
    "giris.idLabel": "Username or email",
    "giris.pwLabel": "Password",
    "giris.forgot": "Forgot password",
    "giris.submit": "Sign in",
    "giris.submitting": "Signing in…",
    "giris.noAccount": "No account?",
    "giris.registerLink": "Register",
    "giris.needBoth": "Username/email and password are required.",
    "giris.tooMany": "Too many attempts. Try again in a minute.",
    "giris.failed": "Couldn’t sign in.",

    // -- Register --
    "title.register": "Register — HaftalıkYazılım",
    "kayit.h1": "Register",
    "kayit.lead":
      "With an account you can publish articles, follow authors, and like and comment.",
    "kayit.displayLabel": "Display name",
    "kayit.displayHint": "Shown on your articles and profile.",
    "kayit.usernameLabel": "Username",
    "kayit.usernameHint":
      "3–30 characters; letters, digits, dot, underscore. Also used to sign in.",
    "kayit.emailLabel": "Email",
    "kayit.pwLabel": "Password",
    "kayit.pwHint": "At least 8 characters.",
    "kayit.submit": "Create account",
    "kayit.submitting": "Creating…",
    "kayit.haveAccount": "Already have an account?",
    "kayit.loginLink": "Sign in",
    "kayit.errDisplay": "Display name must be at least 2 characters.",
    "kayit.errUsername": "Username must be at least 3 characters.",
    "kayit.errEmail": "Enter a valid email.",
    "kayit.errPw": "Password must be at least 8 characters.",
    "kayit.failed": "Couldn’t register.",

    // -- Forgot password --
    "title.reset": "Reset password — HaftalıkYazılım",
    "sifre.h1": "Reset my password",
    "sifre.idLabel": "Username or email",
    "sifre.submit": "Reset my password",
    "sifre.submitting": "Resetting…",
    "sifre.backToLogin": "← Back to sign in",
    "sifre.copy": "Copy",
    "sifre.resultHint":
      "This password won’t be shown again. Save it, sign in, then change it from your profile.",
    "sifre.loginBtn": "Sign in",
    "sifre.needId": "Enter your username or email.",
    "sifre.copied": "Copied.",
    "sifre.copyManual": "Couldn’t copy — select it manually.",
    "sifre.failed": "Something went wrong.",

    // -- Public profile --
    "title.profile": "Profile — HaftalıkYazılım",
    "profil.articlesTitle": "Articles",
    "profil.notSpecified": "No profile specified.",
    "profil.notFound": "Profile not found",
    "profil.loadFail": "Couldn’t load profile",
    "profil.statArticles": "articles",
    "profil.statFollowers": "followers",
    "profil.statFollowing": "following",
    "profil.since": "since {date}",
    "profil.followForLogin": "Sign in to follow",
    "profil.following": "Following",
    "profil.follow": "Follow",
    "profil.noArticlesYet": "No published articles yet.",
    "profil.articlesLoadFail": "Couldn’t load articles.",

    // -- My Articles --
    "title.myArticles": "My Articles — HaftalıkYazılım",
    "profilim.h1": "My Articles",
    "profilim.sub": "All your drafts and published articles.",
    "profilim.new": "New article",
    "profilim.loadFail": "Couldn’t load your articles.",
    "profilim.emptyTitle": "It starts here",
    "profilim.emptyBody":
      "Upload your first article as a PDF. No rush — publish it when you’re ready.",
    "profilim.emptyCta": "Add your first article",
    "profilim.confirmDelete": "Delete “{title}”?\n\n(It will be moved to the trash.)",
    "profilim.deleted": "Article deleted.",
    "profilim.tabAll": "All",
    "profilim.tabPublished": "Published",
    "profilim.tabDraft": "Drafts",
    "profilim.emptyPublished": "You have no published articles yet.",
    "profilim.emptyDraft": "You have no drafts.",

    // -- Liked --
    "title.liked": "Liked — HaftalıkYazılım",
    "begendiklerim.h1": "Liked",
    "begendiklerim.sub": "Articles you liked — newest first.",
    "begendiklerim.emptyTitle": "You haven’t liked anything yet",
    "begendiklerim.emptyBody": "Tap the heart under an article to like it.",

    // -- Saved --
    "title.saved": "Saved — HaftalıkYazılım",
    "kaydettiklerim.h1": "Saved",
    "kaydettiklerim.sub": "Articles you starred — to come back to later.",
    "kaydettiklerim.emptyTitle": "You haven’t saved anything yet",
    "kaydettiklerim.emptyBody": "Tap the star under an article to add it here.",

    // -- New / edit article --
    "title.editArticle": "Edit article — HaftalıkYazılım",
    "yaziDuzenle.backToMine": "← My Articles",
    "yaziDuzenle.titleLabel": "Title",
    "yaziDuzenle.summaryLabel": "Summary",
    "yaziDuzenle.optional": "(optional)",
    "yaziDuzenle.summaryPh":
      "Short summary (shown in the ⓘ popover). Use '- ' for a list, **like this** for bold.",
    "yaziDuzenle.summaryHint": "Up to 500 characters.",
    "yaziDuzenle.pdfLabel": "PDF file",
    "yaziDuzenle.pdfHintNew": "Required. PDF only, max 50 MB.",
    "yaziDuzenle.headingNew": "New article",
    "yaziDuzenle.headingEdit": "Edit article",
    "yaziDuzenle.saveDraft": "Save as draft",
    "yaziDuzenle.publishBtn": "Publish",
    "yaziDuzenle.savingBtn": "Saving…",
    "yaziDuzenle.uploading": "Uploading… {pct}%",
    "yaziDuzenle.processing": "Processing…",
    "yaziDuzenle.pdfPick": "Choose PDF",
    "yaziDuzenle.pdfReplace": "Replace",
    "yaziDuzenle.pdfView": "View",
    "yaziDuzenle.pdfRemove": "Remove selected file",
    "yaziDuzenle.notFound": "Article not found or you don’t have access.",
    "yaziDuzenle.unpublished": "Unpublished.",
    "yaziDuzenle.published": "Published.",
    "yaziDuzenle.errPdfType": "The file must be a PDF.",
    "yaziDuzenle.errPdfSize": "The PDF can be at most 50 MB.",
    "yaziDuzenle.titleReqPublish": "A title is required to publish.",
    "yaziDuzenle.pdfReqPublish": "You must add a PDF to publish.",
    "yaziDuzenle.summaryTooLong": "Summary exceeds 500 characters ({count} now).",
    "yaziDuzenle.draftSavedMsg": "Saved as draft",
    "yaziDuzenle.publishedMsg": "Your article is published.",
    "yaziDuzenle.goToMyArticles": "Go to My Articles",
    "yaziDuzenle.keepEditing": "Keep editing",
    "yaziDuzenle.savedChanges": "Changes saved.",
    "yaziDuzenle.createdDraft": "Article saved as a draft.",

    // -- Reader --
    "title.read": "Article — HaftalıkYazılım",
    "oku.commentsHeading": "Comments",
    "oku.noId": "No article specified.",
    "oku.notFound": "Article not found or not published.",
    "oku.loadFail": "Couldn’t load the article.",
    "oku.loginToComment1": "Sign in",
    "oku.loginToComment2": " or ",
    "oku.loginToComment3": "register",
    "oku.loginToComment4": " — to comment.",
    "oku.commentsLoadFail": "Couldn’t load comments.",
    "oku.firstComment": "Be the first to comment.",
    "oku.commentPh": "Your comment…",
    "oku.send": "Send",
    "oku.commentAddFail": "Couldn’t post comment.",
    "oku.deleteComment": "Delete comment",
    "oku.confirmDeleteComment": "Delete this comment?",
    "oku.deleteCommentShort": "delete",

    // -- Admin --
    "title.admin": "Admin — HaftalıkYazılım",
    "yonetim.h1": "Admin",
    "yonetim.sub": "Stats, users and all articles. (Read-only)",
    "yonetim.statsTitle": "Stats — top 10",
    "yonetim.today": "Today",
    "yonetim.week": "One week",
    "yonetim.month": "One month",
    "yonetim.usersTitle": "Users",
    "yonetim.allArticles": "All articles",
    "yonetim.includeDeleted": "Include deleted",
    "yonetim.statViewed": "Most viewed",
    "yonetim.statDownloaded": "Most downloaded",
    "yonetim.statLiked": "Most liked",
    "yonetim.statBookmarked": "Most saved",
    "yonetim.statShared": "Most shared",
    "yonetim.unitViews": "views",
    "yonetim.unitDownloads": "downloads",
    "yonetim.unitLikes": "likes",
    "yonetim.unitSaves": "saves",
    "yonetim.unitShares": "shares",
    "yonetim.noData": "No data for this range.",
    "yonetim.statsLoadFail": "Couldn’t load stats.",
    "yonetim.usersLoadFail": "Couldn’t load users.",
    "yonetim.articlesLoadFail": "Couldn’t load articles.",
    "yonetim.noRecords": "No records.",
    "yonetim.totalRecords": "{n} records total.",
    "yonetim.colName": "Name",
    "yonetim.colEmail": "Email",
    "yonetim.colRole": "Role",
    "yonetim.colStatus": "Status",
    "yonetim.colJoined": "Joined",
    "yonetim.colArticles": "Articles",
    "yonetim.roleAdmin": "Admin",
    "yonetim.roleAuthor": "Author",
    "yonetim.active": "Active",
    "yonetim.inactive": "Inactive",

    // -- Edit profile --
    "title.editProfile": "My Profile — HaftalıkYazılım",
    "profilDuzenle.h1": "My Profile",
    "profilDuzenle.backToMine": "← My Articles",
    "profilDuzenle.photoLabel": "Profile photo",
    "profilDuzenle.pickPhoto": "Choose photo",
    "profilDuzenle.removePhoto": "Remove",
    "profilDuzenle.zoom": "Zoom",
    "profilDuzenle.horizontal": "Horizontal",
    "profilDuzenle.vertical": "Vertical",
    "profilDuzenle.emailLabel": "Email",
    "profilDuzenle.usernameLabel": "Username",
    "profilDuzenle.displayLabel": "Display name",
    "profilDuzenle.bioLabel": "Short bio",
    "profilDuzenle.bioPh": "A sentence or two. Shown in the ⓘ popover and at the top of your profile.",
    "profilDuzenle.aboutLabel": "About",
    "profilDuzenle.aboutPh": "The full text shown on your profile page. Blank line = new paragraph.",
    "profilDuzenle.savedBtn": "Changes saved",
    "profilDuzenle.errUsername": "Username must be at least 3 characters.",
    "profilDuzenle.errDisplay": "Display name must be at least 2 characters.",
    "profilDuzenle.errImgType": "You can only pick JPG, PNG or WebP.",
    "profilDuzenle.errImgRead": "Couldn’t read the photo. Try another file.",
  },
};

// ============================================================================
// ÇALIŞTIR — sözlük tanımlandıktan SONRA (const DICT "temporal dead zone").
// ============================================================================

// Kök <html lang> her zaman güncel dili göstersin (erişilebilirlik + tarayıcı).
document.documentElement.lang = LANG;

// Sayfa hazır olunca statik metinleri (data-i18n) çevir.
if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", () => applyTranslations());
} else {
  applyTranslations();
}
