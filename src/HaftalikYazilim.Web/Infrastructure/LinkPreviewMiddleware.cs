using System.Text;
using HaftalikYazilim.Web.Options;
using HaftalikYazilim.Web.Services;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Infrastructure;

/// <summary>
/// Sosyal medyada (LinkedIn / WhatsApp / X / Slack …) paylaşılan linkler "zengin
/// kart" olarak görünsün diye <c>&lt;head&gt;</c>'e Open Graph / Twitter Card
/// etiketlerini SUNUCUDA ekler. Bu botlar JavaScript çalıştırmadığı için etiketler
/// istemci tarafında (oku.js) değil, burada üretilmeli.
///
/// Yakaladığı yollar:
///   GET /                → anasayfa kartı
///   GET /index.html      → anasayfa kartı
///   GET /oku.html?id=N   → o yazının kartı (başlık + özet + yazar + og:image)
///
/// İnsanlar da aynı sayfayı alır; fazladan etiketler görünmez, PDF yine yüklenir.
/// UseStaticFiles'tan ÖNCE çalışır (yoksa dosya olduğu gibi sunulurdu).
/// </summary>
public class LinkPreviewMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LinkPreviewMiddleware> _logger;

    // oku.html / index.html şablonları bir kez okunup tutulur.
    private string? _readTemplate;
    private string? _homeTemplate;

    public LinkPreviewMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<LinkPreviewMiddleware> logger)
    {
        _next = next;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        var site = context.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value;
        var baseUrl = ResolveBaseUrl(site, context.Request);

        try
        {
            if (path is "/" or "/index.html")
            {
                await WriteHtmlAsync(context, InjectHome(await HomeTemplateAsync(), baseUrl));
                return;
            }

            if (path == "/oku.html" && int.TryParse(context.Request.Query["id"], out var id) && id > 0)
            {
                var articles = context.RequestServices.GetRequiredService<IArticleService>();
                var article = await articles.GetByIdAsync(id, context.RequestAborted);

                // Sadece YAYINDA olan yazılar için zengin kart üret (taslak sızmasın).
                if (!string.Equals(article.Status, "Published", StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }

                await WriteHtmlAsync(context, InjectArticle(await ReadTemplateAsync(), baseUrl, article));
                return;
            }
        }
        catch (Exception ex)
        {
            // Yazı yok / okunamadı / şablon yok → sıradan akışa bırak (etiketler eklenmez).
            _logger.LogDebug(ex, "Link önizleme etiketleri eklenemedi: {Path}", path);
            await _next(context);
            return;
        }

        await _next(context);
    }

    // ------------------------------------------------------------------

    private async Task<string> ReadTemplateAsync()
        => _readTemplate ??= await File.ReadAllTextAsync(Path.Combine(_env.WebRootPath, "oku.html"));

    private async Task<string> HomeTemplateAsync()
        => _homeTemplate ??= await File.ReadAllTextAsync(Path.Combine(_env.WebRootPath, "index.html"));

    private static string ResolveBaseUrl(SiteOptions site, HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return site.BaseUrl.TrimEnd('/');
        }
        return $"{request.Scheme}://{request.Host}";
    }

    private static string InjectArticle(string template, string baseUrl, Contracts.ArticleDetailResponse a)
    {
        var title = string.IsNullOrWhiteSpace(a.Title) ? "Yazı" : a.Title.Trim();
        var author = a.Author?.DisplayName?.Trim() ?? "";
        var desc = !string.IsNullOrWhiteSpace(a.Summary)
            ? a.Summary!.Trim()
            : (author.Length > 0 ? $"{author} · HaftalıkYazılım" : "HaftalıkYazılım");

        var pageUrl = $"{baseUrl}/oku.html?id={a.Id}";
        var imageUrl = $"{baseUrl}/og/article/{a.Id}.png?v={a.UpdatedAtUtc.Ticks}";

        var tags = BuildTags(new()
        {
            ["og:type"] = "article",
            ["og:site_name"] = "HaftalıkYazılım",
            ["og:title"] = title,
            ["og:description"] = desc,
            ["og:url"] = pageUrl,
            ["og:image"] = imageUrl,
            ["og:image:width"] = "1200",
            ["og:image:height"] = "630",
            ["og:locale"] = "tr_TR",
            ["article:author"] = author,
            ["twitter:card"] = "summary_large_image",
            ["twitter:title"] = title,
            ["twitter:description"] = desc,
            ["twitter:image"] = imageUrl,
        });

        return template.Replace("<!--OG-->", tags);
    }

    private static string InjectHome(string template, string baseUrl)
    {
        const string title = "HaftalıkYazılım";
        const string desc = "Yazılımla ilgili teknik yazıların paylaşıldığı açık bir arşiv. Herkes hesap açıp kendi yazısını PDF olarak yayımlayabilir.";
        var imageUrl = $"{baseUrl}/og/home.png";

        var tags = BuildTags(new()
        {
            ["og:type"] = "website",
            ["og:site_name"] = "HaftalıkYazılım",
            ["og:title"] = title,
            ["og:description"] = desc,
            ["og:url"] = $"{baseUrl}/",
            ["og:image"] = imageUrl,
            ["og:locale"] = "tr_TR",
            ["twitter:card"] = "summary_large_image",
            ["twitter:title"] = title,
            ["twitter:description"] = desc,
            ["twitter:image"] = imageUrl,
        });

        return template.Replace("<!--OG-->", tags);
    }

    /// <summary>Sözlükten güvenli &lt;meta&gt; satırları üretir. Sayfa UTF-8 olduğu için
    /// Türkçe karakterler olduğu gibi bırakılır; sadece nitelik değerini kıracak
    /// karakterler (&amp; &lt; &gt; ") kaçırılır.</summary>
    private static string BuildTags(Dictionary<string, string> pairs)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var safe = value
                .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;")
                .Replace('\n', ' ').Replace('\r', ' ');
            // twitter:* için "name", og:*/article:* için "property" beklenir.
            var attr = key.StartsWith("twitter:", StringComparison.Ordinal) ? "name" : "property";
            sb.Append("  <meta ").Append(attr).Append("=\"").Append(key).Append("\" content=\"").Append(safe).Append("\" />\n");
        }
        return sb.ToString();
    }

    private static async Task WriteHtmlAsync(HttpContext context, string html)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        // Bot ile insan aynı sayfayı alıyor; kısa süre önbelleklensin (kart botları tekrar tekrar çeker).
        context.Response.Headers.CacheControl = "public, max-age=300";
        await context.Response.WriteAsync(html, Encoding.UTF8, context.RequestAborted);
    }
}
