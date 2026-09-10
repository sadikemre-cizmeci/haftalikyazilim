using HaftalikYazilim.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Site geneli küçük bilgiler. Şimdilik sadece anasayfadaki iletişim kutusu için.
/// İletişim bilgini değiştirmek için HTML'e dokunma; <c>appsettings.json → "Site"</c>
/// bölümünü düzenle, burası onu döner.
/// </summary>
[ApiController]
[Route("api/site")]
public class SiteController : ControllerBase
{
    private readonly SiteOptions _site;

    public SiteController(IOptions<SiteOptions> site)
    {
        _site = site.Value;
    }

    /// <summary>Anasayfanın kullandığı iletişim bilgileri. Boş alanlar frontend'de gizlenir.</summary>
    [HttpGet("info")]
    [AllowAnonymous]
    public ActionResult<SiteInfoResponse> GetInfo()
    {
        return Ok(new SiteInfoResponse
        {
            ContactEmail = _site.ContactEmail,
            ContactPhone = _site.ContactPhone,
        });
    }

    /// <summary>Anasayfa iletişim kutusu için basit veri taşıyıcı.</summary>
    public record SiteInfoResponse
    {
        public string ContactEmail { get; init; } = string.Empty;
        public string ContactPhone { get; init; } = string.Empty;
    }
}
