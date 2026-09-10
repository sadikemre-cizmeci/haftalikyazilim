using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Kullanıcı profili uçları.
///  - <c>/me</c> uçları: giriş yapan kullanıcının KENDİ profili (düzenleme).
///  - <c>/{id}</c> uçları: herkese açık profil (görüntüleme, ⓘ baloncuğu, profil sayfası).
/// </summary>
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    // Profil fotoğrafı yüklemesi için istek üst sınırı.
    private const long AvatarRequestLimitBytes = 10_000_000;

    private readonly IProfileService _profiles;

    public ProfileController(IProfileService profiles)
    {
        _profiles = profiles;
    }

    /// <summary>Giriş yapan kullanıcının düzenlenebilir profili.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MyProfileResponse>> GetMine(CancellationToken ct)
        => Ok(await _profiles.GetMyProfileAsync(ct));

    /// <summary>Görünen ad, kısa/uzun tanıtım ve fotoğraf gösterim ayarlarını günceller.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<MyProfileResponse>> UpdateMine(
        [FromBody] UpdateProfileRequest request, CancellationToken ct)
        => Ok(await _profiles.UpdateMyProfileAsync(request, ct));

    /// <summary>Profil fotoğrafını yükler (multipart; alan adı: <c>Image</c>).</summary>
    [HttpPost("me/avatar")]
    [Authorize]
    [RequestSizeLimit(AvatarRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarRequestLimitBytes)]
    public async Task<ActionResult<MyProfileResponse>> UploadAvatar(
        [FromForm] AvatarUploadForm form, CancellationToken ct)
    {
        if (form.Image is null)
        {
            return Problem(title: "Eksik dosya", detail: "Bir fotoğraf seç.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        return Ok(await _profiles.SetAvatarAsync(form.Image, ct));
    }

    /// <summary>Profil fotoğrafını kaldırır.</summary>
    [HttpDelete("me/avatar")]
    [Authorize]
    public async Task<ActionResult<MyProfileResponse>> RemoveAvatar(CancellationToken ct)
        => Ok(await _profiles.RemoveAvatarAsync(ct));

    /// <summary>Herkese açık profil.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicProfileResponse>> GetPublic(int id, CancellationToken ct)
        => Ok(await _profiles.GetPublicProfileAsync(id, ct));

    /// <summary>Bu kullanıcıyı takip etmeye başla.</summary>
    [HttpPost("{id:int}/follow")]
    [Authorize]
    public async Task<IActionResult> Follow(int id, CancellationToken ct)
    {
        await _profiles.FollowAsync(id, ct);
        return NoContent();
    }

    /// <summary>Bu kullanıcıyı takibi bırak.</summary>
    [HttpDelete("{id:int}/follow")]
    [Authorize]
    public async Task<IActionResult> Unfollow(int id, CancellationToken ct)
    {
        await _profiles.UnfollowAsync(id, ct);
        return NoContent();
    }

    /// <summary>Profil fotoğrafının kendisi. Yoksa 404 → arayüz baş harf dairesi çizer.</summary>
    [HttpGet("{id:int}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(int id, CancellationToken ct)
    {
        var payload = await _profiles.OpenAvatarAsync(id, ct);
        if (payload is null)
        {
            return NotFound();
        }

        // Adres UpdatedAt ile sürümlendiği için uzun süre önbelleklenebilir.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(payload.Content, payload.ContentType);
    }

    /// <summary>Fotoğraf yükleme formunun bağlanması için.</summary>
    public class AvatarUploadForm
    {
        public IFormFile? Image { get; set; }
    }
}
