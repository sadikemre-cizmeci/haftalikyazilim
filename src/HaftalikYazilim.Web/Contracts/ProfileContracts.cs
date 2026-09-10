using System.ComponentModel.DataAnnotations;

namespace HaftalikYazilim.Web.Contracts;

/// <summary>Kullanıcının KENDİ profilini düzenlerken gördüğü bilgi (GET /api/profile/me).</summary>
public record MyProfileResponse
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? About { get; init; }
    public string? AvatarUrl { get; init; }
    public double AvatarZoom { get; init; } = 1.0;
    public double AvatarPosX { get; init; } = 50;
    public double AvatarPosY { get; init; } = 50;
}

/// <summary>
/// Profil düzenleme formundan gelen veri. Bio/About için KARAKTER SINIRI YOK;
/// buradaki büyük sayılar sadece aşırı yüklemeye karşı emniyet payı.
/// </summary>
public record UpdateProfileRequest
{
    [Required(ErrorMessage = "Kullanıcı adı zorunlu.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3–30 karakter olmalı.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Görünen ad zorunlu.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Görünen ad 2–120 karakter olmalı.")]
    public string DisplayName { get; init; } = string.Empty;

    // Kullanıcıya "sınır" olarak gösterilmez; sadece aşırı yüklemeye karşı emniyet.
    [StringLength(4000, ErrorMessage = "Kısa tanıtım çok uzun.")]
    public string? Bio { get; init; }

    [StringLength(40_000, ErrorMessage = "Hakkında metni çok uzun.")]
    public string? About { get; init; }

    [Range(1.0, 3.0)] public double AvatarZoom { get; init; } = 1.0;
    [Range(0, 100)] public double AvatarPosX { get; init; } = 50;
    [Range(0, 100)] public double AvatarPosY { get; init; } = 50;
}

/// <summary>Herkese açık profil (GET /api/profile/{id}).</summary>
public record PublicProfileResponse
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? About { get; init; }
    public string? AvatarUrl { get; init; }
    public double AvatarZoom { get; init; } = 1.0;
    public double AvatarPosX { get; init; } = 50;
    public double AvatarPosY { get; init; } = 50;

    public int PublishedArticleCount { get; init; }
    public int FollowerCount { get; init; }
    public int FollowingCount { get; init; }
    public DateTime MemberSinceUtc { get; init; }

    /// <summary>Giriş yapan kişi bu profili takip ediyor mu? (Anonim / kendisi ise false.)</summary>
    public bool ViewerFollows { get; init; }

    /// <summary>Bu profil giriş yapan kişinin kendisi mi?</summary>
    public bool IsSelf { get; init; }
}
