using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Sosyal medya link önizlemesi için üretilen görseller (og:image).
/// Etiketlerin kendisi <see cref="Infrastructure.LinkPreviewMiddleware"/> içinde eklenir.
/// </summary>
[ApiController]
public class ShareController : ControllerBase
{
    private readonly IArticleService _articles;
    private readonly OgCardRenderer _cards;

    public ShareController(IArticleService articles, OgCardRenderer cards)
    {
        _articles = articles;
        _cards = cards;
    }

    /// <summary>Bir yazının 1200×630 önizleme kartı (PNG). Herkese açık.</summary>
    [HttpGet("/og/article/{id:int}.png")]
    [AllowAnonymous]
    public async Task<IActionResult> ArticleCard(int id, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(id, ct);   // yoksa AppNotFoundException → 404

        var png = await _cards.GetArticleCardAsync(
            article.Id,
            version: article.UpdatedAtUtc.Ticks,
            title: article.Title,
            authorName: article.Author?.DisplayName ?? "",
            ct);

        // Kart botları tekrar tekrar çeker; uzun süre önbelleklensin (sürüm ?v= ile değişir).
        Response.Headers.CacheControl = "public, max-age=604800, immutable";
        return File(png, "image/png");
    }

    /// <summary>Anasayfa önizleme kartı (PNG). Herkese açık.</summary>
    [HttpGet("/og/home.png")]
    [AllowAnonymous]
    public async Task<IActionResult> HomeCard(CancellationToken ct)
    {
        var png = await _cards.GetHomeCardAsync(ct);
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(png, "image/png");
    }
}
