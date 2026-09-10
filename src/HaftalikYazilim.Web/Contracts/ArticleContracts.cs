using System.ComponentModel.DataAnnotations;

namespace HaftalikYazilim.Web.Contracts;

/// <summary>Bir yazının yazarına dair, kartta ve ⓘ baloncuğunda gösterilecek özet.</summary>
public record AuthorSummary
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Profil fotoğrafı adresi. Fotoğraf yoksa boş — arayüz baş harf dairesi çizer.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>Kısa tanıtım (özet bilgi). Boş olabilir.</summary>
    public string? Bio { get; init; }

    // Fotoğrafın dairede gösterim ayarları (kullanıcı belirler).
    public double AvatarZoom { get; init; } = 1.0;
    public double AvatarPosX { get; init; } = 50;
    public double AvatarPosY { get; init; } = 50;
}

/// <summary>
/// "Yazılar" listesindeki TEK bir satır. PDF içeriği burada YOK.
/// Hem herkese açık liste hem "Yazılarım" hem Admin listesi bunu kullanır.
/// </summary>
public record ArticleListItemResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>Boş olabilir (özet zorunlu değil).</summary>
    public string? Summary { get; init; }

    public string Slug { get; init; } = string.Empty;

    /// <summary>"Draft" | "Published"</summary>
    public string Status { get; init; } = string.Empty;

    public AuthorSummary Author { get; init; } = new();

    public DateTime? PublishedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    public long FileSizeBytes { get; init; }
    public int ViewCount { get; init; }
    public int DownloadCount { get; init; }

    /// <summary>Beğeni / yorum sayısı + giriş yapan kişinin durumu.</summary>
    public ArticleSocialResponse Social { get; init; } = new();
}

/// <summary>Tek bir yazının detay bilgisi (okuma sayfası ve düzenleme formu için).</summary>
public record ArticleDetailResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    public AuthorSummary Author { get; init; } = new();

    public DateTime? PublishedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    public string OriginalFileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public int ViewCount { get; init; }
    public int DownloadCount { get; init; }

    public string FileUrl { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;

    public ArticleSocialResponse Social { get; init; } = new();
}

/// <summary>
/// Yeni yazı oluşturma / güncelleme formundan gelen veri (multipart/form-data).
///
/// - <see cref="Title"/> zorunlu.
/// - <see cref="Summary"/> İSTEĞE BAĞLI, alt sınır yok; yalnızca üst sınır (500).
/// - <see cref="Pdf"/> yeni yazıda zorunlu, güncellemede isteğe bağlı.
/// </summary>
public class SaveArticleRequest
{
    // Başlık TASLAK için isteğe bağlı (sonra doldurulur). "Yayınla" için serviste
    // ayrıca zorunlu tutulur. Nullable → boş/gönderilmemiş başlık 400 vermez.
    [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string? Title { get; set; }

    [StringLength(500, ErrorMessage = "Özet en fazla 500 karakter olabilir.")]
    public string? Summary { get; set; }

    /// <summary>Yalnızca PDF (uzantı + içerik türü + dosya imzası kontrol edilir).</summary>
    public IFormFile? Pdf { get; set; }

    /// <summary>
    /// "Draft" veya "Published". Verilmezse: oluştururken Taslak, güncellemede durum
    /// aynı kalır. Formdaki "Taslak olarak kaydet" / "Yayınla" düğmeleri bunu gönderir.
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>Filtre menüsü / geçmiş uyumluluk için yazar listesi öğesi.</summary>
public record AuthorResponse
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int ArticleCount { get; init; }
}
