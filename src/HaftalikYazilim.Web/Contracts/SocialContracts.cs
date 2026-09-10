using System.ComponentModel.DataAnnotations;

namespace HaftalikYazilim.Web.Contracts;

/// <summary>
/// Bir yazının sosyal durumu — kartta ve okuma sayfasında gösterilir.
/// <c>ViewerLiked</c>/<c>ViewerBookmarked</c> giriş yoksa daima false.
/// </summary>
public record ArticleSocialResponse
{
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public bool ViewerLiked { get; init; }
    public bool ViewerBookmarked { get; init; }
}

/// <summary>Bir yorum satırı.</summary>
public record CommentResponse
{
    public int Id { get; init; }
    public AuthorSummary Author { get; init; } = new();
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Giriş yapan kişi bu yorumu silebilir mi? (Sahibi ya da Admin.)</summary>
    public bool CanDelete { get; init; }
}

/// <summary>Yeni yorum.</summary>
public record CreateCommentRequest
{
    [Required(ErrorMessage = "Yorum boş olamaz.")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Yorum en fazla 4000 karakter olabilir.")]
    public string Body { get; init; } = string.Empty;
}

/// <summary>Like / bookmark aç-kapa sonrası dönen güncel durum.</summary>
public record ToggleResponse
{
    public bool Active { get; init; }   // artık beğenili / yıldızlı mı?
    public int Count { get; init; }     // yeni toplam (like için; bookmark'ta 0)
}
