// ============================================================================
// config.js — sitenin genel ayarları. Tek yerden yönetilir.
// ============================================================================

export const CONFIG = {
  // API adresi.
  // Frontend ve backend AYNI site üzerinden sunulduğu için boş bırakmak yeterli;
  // istekler "/api/..." şeklinde gider. İleride frontend'i AYRI bir yere taşırsan
  // (ör. ayrı bir statik hosting) buraya "https://api.haftalikyazilim.com" gibi
  // tam adres yaz.
  apiBaseUrl: "",

  // "Yazılar" sayfasında bir sayfada en fazla kaç yazı gösterilsin.
  // (Altta sayfa numaraları çıkar.)
  pageSize: 20,

  // Site adı — birkaç yerde (sekme başlığı vb.) kullanılır.
  siteName: "HaftalıkYazılım",
};
