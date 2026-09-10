using HaftalikYazilim.Web.Contracts;

namespace HaftalikYazilim.Web.Services;

/// <summary>Kullanıcı profili: kendi profilini düzenleme + herkese açık profil görüntüleme.</summary>
public interface IProfileService
{
    /// <summary>Giriş yapan kullanıcının düzenlenebilir profili.</summary>
    Task<MyProfileResponse> GetMyProfileAsync(CancellationToken ct);

    /// <summary>Görünen ad, kısa/uzun tanıtım ve fotoğraf gösterim ayarlarını günceller.</summary>
    Task<MyProfileResponse> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct);

    /// <summary>Profil fotoğrafını yükler (küçültülür, JPEG'e çevrilir).</summary>
    Task<MyProfileResponse> SetAvatarAsync(IFormFile file, CancellationToken ct);

    /// <summary>Profil fotoğrafını kaldırır.</summary>
    Task<MyProfileResponse> RemoveAvatarAsync(CancellationToken ct);

    /// <summary>Herkese açık profil (id ile). Kullanıcı yoksa/pasifse 404.</summary>
    Task<PublicProfileResponse> GetPublicProfileAsync(int userId, CancellationToken ct);

    /// <summary>Giriş yapan kişi verilen kullanıcıyı takip etmeye başlar (zaten ediyorsa etkisiz).</summary>
    Task FollowAsync(int targetUserId, CancellationToken ct);

    /// <summary>Takibi bırakır (etmiyorsa etkisiz).</summary>
    Task UnfollowAsync(int targetUserId, CancellationToken ct);

    /// <summary>Profil fotoğrafının ham akışı + içerik türü (id ile). Yoksa null.</summary>
    Task<AvatarFilePayload?> OpenAvatarAsync(int userId, CancellationToken ct);
}

/// <summary>Profil fotoğrafı dosya yanıtı. <see cref="Content"/> akışını controller kapatır.</summary>
public sealed record AvatarFilePayload(Stream Content, string ContentType);

/// <summary>Kart/baloncuk için yazar özeti üretmede tekrar kullanılan yardımcılar.</summary>
public static class AvatarUrl
{
    /// <summary>Kullanıcının avatar adresi (yoksa null). UpdatedAt ile sürümlenir → önbellek tazelenir.</summary>
    public static string? For(Domain.User user)
        => user.AvatarFileName is null
            ? null
            : $"/api/profile/{user.Id}/avatar?v={user.UpdatedAtUtc.Ticks}";
}
