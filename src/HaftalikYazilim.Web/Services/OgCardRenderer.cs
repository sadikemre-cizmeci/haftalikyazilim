using HaftalikYazilim.Web.Options;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Link önizleme kartı (og:image) üretir: 1200×630 PNG.
/// Kağıt zemin üzerine kırmızı fırça halkası + yazı başlığı + yazar adı + "HaftalıkYazılım".
/// PDF render ETMEZ — sadece metin ve basit şekil. Sonuç diske önbelleklenir
/// (yazı güncellendikçe anahtar değişir).
/// </summary>
public class OgCardRenderer
{
    private const int Width = 1200;
    private const int Height = 630;

    private readonly string _cacheDir;
    private readonly string _fontPath;
    private readonly ILogger<OgCardRenderer> _logger;

    // Marka renkleri (theme.css ile aynı).
    private static readonly SKColor Paper = SKColor.Parse("#faf7f0");
    private static readonly SKColor Ink = SKColor.Parse("#2b2724");
    private static readonly SKColor Muted = SKColor.Parse("#6b6259");
    private static readonly SKColor Border = SKColor.Parse("#e6ded0");
    private static readonly SKColor BrandRed = SKColor.Parse("#d92d2d");

    private SKTypeface? _typeface;

    public OgCardRenderer(
        IOptions<OgOptions> options,
        IHostEnvironment env,
        ILogger<OgCardRenderer> logger)
    {
        _logger = logger;

        var opts = options.Value;
        _cacheDir = Path.IsPathRooted(opts.CacheDir)
            ? opts.CacheDir
            : Path.Combine(env.ContentRootPath, opts.CacheDir);
        Directory.CreateDirectory(_cacheDir);

        _fontPath = Path.IsPathRooted(opts.FontPath)
            ? opts.FontPath
            : Path.Combine(env.ContentRootPath, opts.FontPath);
    }

    /// <summary>
    /// Yazı için kart PNG'sini döner. <paramref name="version"/> yazı her
    /// güncellendiğinde değişmeli (ör. UpdatedAtUtc.Ticks) — böylece önbellek tazelenir.
    /// </summary>
    public async Task<byte[]> GetArticleCardAsync(int articleId, long version, string title, string authorName, CancellationToken ct = default)
    {
        var cacheFile = Path.Combine(_cacheDir, $"article-{articleId}-{version}.png");

        if (File.Exists(cacheFile))
        {
            return await File.ReadAllBytesAsync(cacheFile, ct);
        }

        var png = Render(title, authorName);

        try
        {
            // Aynı yazının eski sürüm kartlarını temizle (article-{id}-*).
            foreach (var stale in Directory.EnumerateFiles(_cacheDir, $"article-{articleId}-*.png"))
            {
                if (!string.Equals(stale, cacheFile, StringComparison.Ordinal))
                {
                    File.Delete(stale);
                }
            }
            await File.WriteAllBytesAsync(cacheFile, png, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "og kart önbelleğe yazılamadı: {File}", cacheFile);
        }

        return png;
    }

    /// <summary>Anasayfa kartı (sabit). Diske "home.png" olarak önbelleklenir.</summary>
    public async Task<byte[]> GetHomeCardAsync(CancellationToken ct = default)
    {
        var cacheFile = Path.Combine(_cacheDir, "home.png");
        if (File.Exists(cacheFile))
        {
            return await File.ReadAllBytesAsync(cacheFile, ct);
        }

        var png = Render(
            "Yazılımla ilgili teknik yazıların paylaşıldığı açık bir arşiv",
            authorName: "",
            titleSize: 60);

        try { await File.WriteAllBytesAsync(cacheFile, png, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "anasayfa og kartı yazılamadı"); }

        return png;
    }

    // ------------------------------------------------------------------

    private byte[] Render(string title, string authorName, float titleSize = 68)
    {
        var typeface = ResolveTypeface();

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(Paper);

        // İnce iç çerçeve
        using (var borderPaint = new SKPaint { Color = Border, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true })
        {
            canvas.DrawRect(new SKRect(24, 24, Width - 24, Height - 24), borderPaint);
        }

        const float marginX = 80f;
        var contentWidth = Width - marginX * 2;

        // Sağ altta SABİT bir şerit ayrılır: marka (halka + "HaftalıkYazılım") her
        // zaman orada, aynı boyutta durur. Başlık + yazar bloğu bunun ÜSTÜNDEKİ
        // alana dikeyde ortalanır — kısa başlıkta da uzun başlıkta da boşluk
        // dengeli dağılır, altta tek bir kocaman boşluk kalmaz.
        const float brandZoneHeight = 100f;
        const float contentTop = 64f;
        var contentBottom = Height - brandZoneHeight;

        // --- Başlık — 1. öncelik: her zaman tam boyutunda, en fazla 3 satır
        // (sığmazsa "…" ile kısalır; bkz. WrapText). ---
        using var titlePaint = new SKPaint
        {
            Color = Ink,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = titleSize,
            FakeBoldText = true,
        };
        var titleLines = WrapText(title, titlePaint, contentWidth, maxLines: 3);
        var titleLineStep = titleSize * 1.24f;
        var titleBlockHeight = titleLines.Count * titleLineStep;

        // --- Yazar adı — 2. öncelik: uzunsa alt satıra kırılır (en fazla 2 satır);
        // başlık bloğuyla birlikte ayrılan alana sığmıyorsa önce küçültülür, o da
        // yetmezse hiç çizilmez. ---
        const float authorGap = 26f;
        var authorLines = new List<string>();
        var authorSize = 46f;
        var authorBlockHeight = 0f;

        if (!string.IsNullOrWhiteSpace(authorName))
        {
            var authorText = $"Yazar : {authorName}";
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var probePaint = new SKPaint { Typeface = typeface, TextSize = authorSize };
                var lines = WrapText(authorText, probePaint, contentWidth, maxLines: 2);
                var blockHeight = authorGap + lines.Count * (authorSize * 1.3f);

                if (contentTop + titleBlockHeight + blockHeight <= contentBottom || attempt == 2)
                {
                    authorLines = lines;
                    authorBlockHeight = blockHeight;
                    break;
                }
                authorSize -= 6f; // sığmadı → küçült, tekrar dene
            }

            // Küçültme de yetmediyse (çok uzun başlık + çok uzun ad) yazarı hiç gösterme.
            if (contentTop + titleBlockHeight + authorBlockHeight > contentBottom)
            {
                authorLines.Clear();
                authorBlockHeight = 0f;
            }
        }

        // --- Başlık + yazar bloğu ayrılan alana dikeyde ortalanır (kısa başlıkta
        // da uzun başlıkta da altta tek, kocaman bir boşluk kalmasın). Her blok
        // kendi "üst" konumundan kendi fontunun Ascent'iyle taban çizgisine
        // çevrilir (fontlar farklı boyutta olduğu için tabanları zincirlemek
        // yerine ayrı ayrı hesaplamak daha güvenli). ---
        var band = contentBottom - contentTop;
        var totalBlockHeight = titleBlockHeight + authorBlockHeight;
        var titleTop = contentTop + Math.Max(0f, (band - totalBlockHeight) / 2f);

        var titleBaseline = titleTop - titlePaint.FontMetrics.Ascent; // Ascent negatif → taban çizgisine çevir
        foreach (var line in titleLines)
        {
            canvas.DrawText(line, marginX, titleBaseline, titlePaint);
            titleBaseline += titleLineStep;
        }

        if (authorLines.Count > 0)
        {
            using var authorPaint = new SKPaint
            {
                Color = Muted,
                IsAntialias = true,
                Typeface = typeface,
                TextSize = authorSize,
            };
            var authorTop = titleTop + titleBlockHeight + authorGap;
            var authorBaseline = authorTop - authorPaint.FontMetrics.Ascent;
            var authorLineStep = authorSize * 1.3f;
            foreach (var line in authorLines)
            {
                canvas.DrawText(line, marginX, authorBaseline, authorPaint);
                authorBaseline += authorLineStep;
            }
        }

        // --- Sağ alt köşe: SABİT marka (halka + "HaftalıkYazılım") — 3. öncelik,
        // ama zaten kendi ayrılmış şeridinde durduğu için başlık/yazar ne kadar
        // uzun olursa olsun yeri/boyu değişmez. ---
        const string brandText = "HaftalıkYazılım";
        const float brandTextSize = 42f;
        const float brandRingRadius = 19f;
        const float brandGap = 14f;
        const float rightMargin = 76f;
        const float brandBaselineFromBottom = 58f;

        using (var brandPaint = new SKPaint
        {
            Color = Muted,
            IsAntialias = true,
            Typeface = typeface,
            TextSize = brandTextSize,
            FakeBoldText = true,
        })
        {
            var brandBaseline = Height - brandBaselineFromBottom;
            var textWidth = brandPaint.MeasureText(brandText);
            var textX = Width - rightMargin - textWidth;
            canvas.DrawText(brandText, textX, brandBaseline, brandPaint);

            var ringCx = textX - brandGap - brandRingRadius;
            var ringCy = brandBaseline - brandTextSize * 0.35f;
            DrawBrandRing(canvas, cx: ringCx, cy: ringCy, radius: brandRingRadius, strokeWidth: 8);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>Üstte açık kırmızı halka (ensō) — logonun sade hâli.</summary>
    private static void DrawBrandRing(SKCanvas canvas, float cx, float cy, float radius, float strokeWidth)
    {
        using var paint = new SKPaint
        {
            Color = BrandRed,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };
        using var path = new SKPath();
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        // -120° konumundan başlayıp 300° tara (üstte ~60°'lik boşluk kalsın).
        path.AddArc(rect, startAngle: -120, sweepAngle: 300);
        canvas.DrawPath(path, paint);
    }

    /// <summary>Metni verilen genişliğe kelime kelime böler; maxLines'ı aşarsa "…" ekler.</summary>
    private static List<string> WrapText(string text, SKPaint paint, float maxWidth, int maxLines)
    {
        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (paint.MeasureText(candidate) <= maxWidth || current.Length == 0)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
                if (lines.Count == maxLines) break;
            }
        }

        if (lines.Count < maxLines && current.Length > 0)
        {
            lines.Add(current);
        }

        // Taşan başlıkta son satırı "…" ile kısalt.
        if (lines.Count == maxLines)
        {
            var consumed = string.Join(' ', lines).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (consumed < words.Length)
            {
                var last = lines[^1];
                while (last.Length > 0 && paint.MeasureText(last + "…") > maxWidth)
                {
                    last = last[..^1].TrimEnd();
                }
                lines[^1] = last + "…";
            }
        }

        return lines.Count > 0 ? lines : new List<string> { string.Empty };
    }

    private SKTypeface ResolveTypeface()
    {
        if (_typeface is not null) return _typeface;

        // 1) Yapılandırılmış .ttf
        if (File.Exists(_fontPath))
        {
            var fromFile = SKTypeface.FromFile(_fontPath);
            if (fromFile is not null)
            {
                return _typeface = fromFile;
            }
        }

        // 2) Türkçe karakter içeren bir sistem fontu
        var match = SKFontManager.Default.MatchCharacter('ş');
        if (match is not null)
        {
            return _typeface = match;
        }

        // 3) Ne bulursa
        _logger.LogWarning("og kart için uygun font bulunamadı; sistem varsayılanı kullanılacak (metin kutu görünebilir).");
        return _typeface = SKTypeface.Default;
    }
}
