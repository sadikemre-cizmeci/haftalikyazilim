using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Infrastructure;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Yazılarla ilgili tüm HTTP uçları. Gerçek iş <see cref="IArticleService"/> içinde;
/// burada sadece istek/yanıt bağlama ve yetki nitelikleri ([Authorize]) var.
///
/// Yetki özeti:
///  - Liste / detay / PDF görüntüle-indir  → herkes (anonim). Taslaklar hariç.
///  - "Yazılarım"                          → giriş yapan herkes (kendi yazıları)
///  - Oluştur / güncelle / yayınla / sil   → Yazar veya Admin (+ sahiplik kontrolü serviste)
/// </summary>
[ApiController]
[Route("api/articles")]
public class ArticlesController : ControllerBase
{
    // multipart form (PDF yükleme) için istek üst sınırı. Storage:MaxUploadBytes (50 MB)
    // + form alanları + HTTP yükü rahat sığsın diye biraz daha büyük.
    private const long UploadRequestLimitBytes = 56_000_000;

    private readonly IArticleService _articles;

    public ArticlesController(IArticleService articles)
    {
        _articles = articles;
    }

    // ====================================================================
    // OKUMA
    // ====================================================================

    /// <summary>Herkese açık yazı listesi (yalnızca yayında olanlar).</summary>
    /// <param name="authorId">Belirli bir yazara göre filtre (isteğe bağlı).</param>
    /// <param name="search">Başlık / özet / yazar adı içinde arama (isteğe bağlı).</param>
    /// <param name="following">true → sadece takip ettiğin yazarların yazıları (giriş gerekir).</param>
    /// <param name="page">1'den başlar.</param>
    /// <param name="pageSize">Sayfa başına kayıt (en fazla 50).</param>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ArticleListItemResponse>>> GetPublished(
        [FromQuery] int? authorId,
        [FromQuery] string? search,
        [FromQuery] bool following = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _articles.GetPublishedAsync(authorId, search, following, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Giriş yapan kullanıcının kendi yazıları (taslak dahil).</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ArticleListItemResponse>>> GetMine(CancellationToken ct)
    {
        var result = await _articles.GetMineAsync(ct);
        return Ok(result);
    }

    /// <summary>Giriş yapan kullanıcının yıldızladığı (kaydettiği) yazılar.</summary>
    [HttpGet("bookmarked")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ArticleListItemResponse>>> GetBookmarked(CancellationToken ct)
        => Ok(await _articles.GetBookmarkedAsync(ct));

    /// <summary>Giriş yapan kullanıcının beğendiği yazılar.</summary>
    [HttpGet("liked")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ArticleListItemResponse>>> GetLiked(CancellationToken ct)
        => Ok(await _articles.GetLikedAsync(ct));

    /// <summary>Tek yazının detayı.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArticleDetailResponse>> GetById(int id, CancellationToken ct)
    {
        var result = await _articles.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>PDF'i tarayıcıda GÖRÜNTÜLEMEK için (gömülü görüntüleyici bunu kullanır).</summary>
    [HttpGet("{id:int}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFile(int id, CancellationToken ct)
    {
        var pdf = await _articles.OpenPdfAsync(id, asDownload: false, ct);

        // "inline" = tarayıcı indirmek yerine göstermeye çalışsın.
        Response.Headers.ContentDisposition = $"inline; filename=\"{ToAsciiFallback(pdf.FileName)}\"";

        // EnableRangeProcessing: PDF.js dosyanın parçalarını isteyebilsin (hızlı açılış, büyük dosya).
        return new FileStreamResult(pdf.Content, pdf.ContentType) { EnableRangeProcessing = true };
    }

    /// <summary>PDF'i İNDİRMEK için (tarayıcı "farklı kaydet" der).</summary>
    [HttpGet("{id:int}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var pdf = await _articles.OpenPdfAsync(id, asDownload: true, ct);

        // fileDownloadName verilince ASP.NET Core "attachment" başlığını kendi kurar
        // ve Türkçe karakterli adları da doğru kodlar.
        return File(pdf.Content, pdf.ContentType, pdf.FileName, enableRangeProcessing: true);
    }

    /// <summary>"Paylaşıldı" olayını kaydeder (yönetim istatistikleri için). Giriş gerekmez.</summary>
    [HttpPost("{id:int}/share")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordShare(int id, CancellationToken ct)
    {
        await _articles.RecordShareAsync(id, ct);
        return NoContent();
    }

    // ====================================================================
    // YAZMA
    // ====================================================================

    /// <summary>Yeni yazı oluşturur (durum: Taslak). PDF zorunlu. multipart/form-data.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AuthorOrAdmin)]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<ActionResult<ArticleDetailResponse>> Create(
        [FromForm] SaveArticleRequest request,
        CancellationToken ct)
    {
        var created = await _articles.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Var olan yazıyı günceller. PDF alanı boş bırakılırsa mevcut PDF korunur.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.AuthorOrAdmin)]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<ActionResult<ArticleDetailResponse>> Update(
        int id,
        [FromForm] SaveArticleRequest request,
        CancellationToken ct)
    {
        var updated = await _articles.UpdateAsync(id, request, ct);
        return Ok(updated);
    }

    /// <summary>Yazıyı yayınlar.</summary>
    [HttpPost("{id:int}/publish")]
    [Authorize(Roles = AppRoles.AuthorOrAdmin)]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        await _articles.SetStatusAsync(id, ArticleStatus.Published, ct);
        return NoContent();
    }

    /// <summary>Yazıyı yayından kaldırır (taslağa çeker).</summary>
    [HttpPost("{id:int}/unpublish")]
    [Authorize(Roles = AppRoles.AuthorOrAdmin)]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        await _articles.SetStatusAsync(id, ArticleStatus.Draft, ct);
        return NoContent();
    }

    /// <summary>Yazıyı çöp kutusuna atar (yumuşak silme).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.AuthorOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _articles.DeleteAsync(id, ct);
        return NoContent();
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Content-Disposition başlığındaki "filename" (tırnak içi) kısmı için kaba bir
    /// ASCII yedeği üretir. Modern tarayıcılar zaten filename*=UTF-8 kısmını okur;
    /// burada sadece eski istemciler patlamasın diye sadeleştiriyoruz.
    /// </summary>
    private static string ToAsciiFallback(string name)
        => string.Concat(name.Where(c => c is >= (char)32 and < (char)127 && c != '"'));
}
