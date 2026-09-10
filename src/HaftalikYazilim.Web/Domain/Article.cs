namespace HaftalikYazilim.Web.Domain;

/// <summary>
/// Bir "yazı" = başlık + kısa özet + TEK bir PDF dosyası.
///
/// Sitenin tüm içeriği PDF olarak yüklenir; okuyucu PDF'i sitede görüntüler
/// (yakınlaştır/uzaklaştır/sayfa geçişi) ve isterse indirir.
///
/// ÖNEMLİ: PDF'in kendisi veritabanında DEĞİL, diskte tutulur (bkz. <c>LocalPdfStorage</c>).
/// Burada sadece dosyanın "nerede olduğu" ve meta bilgisi tutulur.
/// </summary>
public class Article
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Listede/başlığın altında görünen kısa açıklama. İSTEĞE BAĞLI (boş bırakılabilir).
    /// ⓘ baloncuğunda gösterilir; yoksa "özet bırakılmamış" denir.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Başlıktan türetilen URL-dostu metin. Örn: "haftalik-yazilim-1-cache".
    /// Şu an zorunlu kullanılmıyor; ileride /yazi/haftalik-yazilim-1 gibi
    /// okunaklı adresler için hazır dursun diye var.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    /// <summary>İlk kez "Yayında" durumuna geçtiği an. Taslakken <c>null</c>'dır.</summary>
    public DateTime? PublishedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // ---------------------------------------------------------------------
    // Yazar ilişkisi
    // ---------------------------------------------------------------------
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;   // EF her zaman dolduracak (Include ile)

    // ---------------------------------------------------------------------
    // PDF dosya bilgileri
    // ---------------------------------------------------------------------

    /// <summary>
    /// Diskte tutulan dosyanın adı. Çakışmayı ve güvenlik sorununu önlemek için
    /// rastgele üretilir. Örn: "8f3c1d9e4b7a4f2e9c0d.pdf".
    /// </summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Kullanıcının yüklediği orijinal ad. İndirme sırasında bu ad kullanılır.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    // ---------------------------------------------------------------------
    // Basit istatistik alanları
    // ---------------------------------------------------------------------
    // İLERİDE: Bu sayaçlar ayrı bir "erişim log" tablosuna veya bir analitik
    // servisine (ör. tarih bazlı görüntülenme) taşınabilir. Şimdilik en ucuz
    // yol: kayıtta iki sayaç tutmak.
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }

    // ---------------------------------------------------------------------
    // Yumuşak silme (soft delete)
    // ---------------------------------------------------------------------
    // Çöp kutusu ikonu bu bayrağı true yapar; kayıt veritabanında KALIR ama
    // hiçbir listede görünmez (bkz. AppDbContext global query filter).
    // Böylece "yanlışlıkla sildim" durumunda Admin geri getirebilir.
    // Kalıcı silmeyi (ve diskteki PDF'in temizlenmesini) ileride bir
    // arka plan görevi / kuyruk ile ekleyebilirsin.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
