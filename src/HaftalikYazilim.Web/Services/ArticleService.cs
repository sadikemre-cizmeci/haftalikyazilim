using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Infrastructure;
using HaftalikYazilim.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// <see cref="IArticleService"/> gerçeklemesi. Tüm kurallar burada; ayrıntı için
/// arayüzdeki açıklamalara bak.
/// </summary>
public class ArticleService : IArticleService
{
    // Bir sayfada gösterilebilecek en fazla kayıt (kötüye kullanımı sınırlamak için).
    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;
    private readonly IPdfStorage _storage;
    private readonly ICurrentUser _currentUser;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<ArticleService> _logger;

    public ArticleService(
        AppDbContext db,
        IPdfStorage storage,
        ICurrentUser currentUser,
        IOptions<StorageOptions> storageOptions,
        ILogger<ArticleService> logger)
    {
        _db = db;
        _storage = storage;
        _currentUser = currentUser;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    // ========================================================================
    // OKUMA (LİSTELER)
    // ========================================================================

    public async Task<PagedResult<ArticleListItemResponse>> GetPublishedAsync(
        int? authorId, string? search, bool followingOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Where(a => a.Status == ArticleStatus.Published);

        // "Takip ettiklerim" → sadece giriş yapan kişinin takip ettiği yazarlar.
        if (followingOnly && _currentUser.UserId is int uid)
        {
            var followed = _db.Follows.Where(f => f.FollowerId == uid).Select(f => f.FolloweeId);
            query = query.Where(a => followed.Contains(a.AuthorId));
        }

        query = ApplyAuthorAndSearch(query, authorId, search);
        query = query.OrderByDescending(a => a.PublishedAtUtc);

        return await PaginateAsync(query, page, pageSize, ct);
    }

    public async Task<IReadOnlyList<ArticleListItemResponse>> GetMineAsync(CancellationToken ct)
    {
        var userId = RequireUserId();

        var items = await _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Where(a => a.AuthorId == userId)
            .OrderByDescending(a => a.UpdatedAtUtc)
            .ToListAsync(ct);

        var social = await LoadSocialAsync(items.Select(a => a.Id), ct);
        return items.Select(a => ToListItem(a, social.GetValueOrDefault(a.Id) ?? new())).ToList();
    }

    public async Task<IReadOnlyList<ArticleListItemResponse>> GetBookmarkedAsync(CancellationToken ct)
    {
        var uid = RequireUserId();
        var orderedIds = await _db.ArticleBookmarks
            .AsNoTracking()
            .Where(b => b.UserId == uid)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => b.ArticleId)
            .ToListAsync(ct);
        return await ByOrderedIdsAsync(orderedIds, ct);
    }

    public async Task<IReadOnlyList<ArticleListItemResponse>> GetLikedAsync(CancellationToken ct)
    {
        var uid = RequireUserId();
        var orderedIds = await _db.ArticleLikes
            .AsNoTracking()
            .Where(l => l.UserId == uid)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => l.ArticleId)
            .ToListAsync(ct);
        return await ByOrderedIdsAsync(orderedIds, ct);
    }

    /// <summary>Verilen id sırasını koruyarak yayında olan yazıları (+ sosyal durum) getirir.</summary>
    private async Task<IReadOnlyList<ArticleListItemResponse>> ByOrderedIdsAsync(
        List<int> orderedIds, CancellationToken ct)
    {
        if (orderedIds.Count == 0)
        {
            return Array.Empty<ArticleListItemResponse>();
        }

        var articles = await _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .Where(a => orderedIds.Contains(a.Id) && a.Status == ArticleStatus.Published)
            .ToListAsync(ct);

        var rank = orderedIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        articles = articles.OrderBy(a => rank[a.Id]).ToList();

        var social = await LoadSocialAsync(orderedIds, ct);
        return articles.Select(a => ToListItem(a, social.GetValueOrDefault(a.Id) ?? new())).ToList();
    }

    public async Task<PagedResult<ArticleListItemResponse>> GetForAdminAsync(
        int? authorId, string? search, bool includeDeleted, int page, int pageSize, CancellationToken ct)
    {
        // Admin isterse silinmişleri de görebilir → global query filter'ı devre dışı bırak.
        var baseQuery = includeDeleted
            ? _db.Articles.IgnoreQueryFilters()
            : _db.Articles;

        var query = baseQuery
            .AsNoTracking()
            .Include(a => a.Author)
            .AsQueryable();

        query = ApplyAuthorAndSearch(query, authorId, search);
        query = query.OrderByDescending(a => a.UpdatedAtUtc);

        return await PaginateAsync(query, page, pageSize, ct);
    }

    public async Task<ArticleDetailResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var article = await _db.Articles
            .AsNoTracking()
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (article is null)
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        // Taslağı yalnızca sahibi veya Admin görebilir. Başkasına "yok" deriz
        // (varlığını bile sızdırmamak için 404).
        if (article.Status == ArticleStatus.Draft && !CanModify(article))
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        var social = await LoadSocialAsync(new[] { article.Id }, ct);
        return ToDetail(article, social.GetValueOrDefault(article.Id) ?? new());
    }

    public async Task<IReadOnlyList<AuthorResponse>> GetAuthorsAsync(CancellationToken ct)
    {
        return await _db.Articles
            .AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published)
            .GroupBy(a => new { a.AuthorId, a.Author.DisplayName })
            .Select(g => new AuthorResponse
            {
                Id = g.Key.AuthorId,
                DisplayName = g.Key.DisplayName,
                ArticleCount = g.Count()
            })
            .OrderBy(a => a.DisplayName)
            .ToListAsync(ct);
    }

    // ========================================================================
    // YAZMA (OLUŞTUR / GÜNCELLE / DURUM / SİL)
    // ========================================================================

    public async Task<ArticleDetailResponse> CreateAsync(SaveArticleRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();

        var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new AppForbiddenException("Hesabın aktif değil.");

        // PDF: TASLAK için isteğe bağlı (sonradan eklenebilir), YAYINLAMAK için zorunlu.
        var storedName = "";
        var originalName = "";
        long fileSize = 0;
        if (request.Pdf is not null)
        {
            ValidatePdf(request.Pdf);
            storedName = await _storage.SaveAsync(request.Pdf, ct);
            originalName = CleanFileName(request.Pdf.FileName);
            fileSize = request.Pdf.Length;
        }
        else if (WantsPublish(request.Status))
        {
            throw new AppValidationException("Yayınlamak için bir PDF dosyası eklemelisin.");
        }

        var title = (request.Title ?? "").Trim();
        if (WantsPublish(request.Status) && title.Length == 0)
        {
            throw new AppValidationException("Yayınlamak için bir başlık gerekli.");
        }

        var article = new Article
        {
            Title = title,
            Summary = NormalizeSummary(request.Summary),
            Slug = await MakeUniqueSlugAsync(title, excludeId: null, ct),
            Status = ArticleStatus.Draft,
            Author = author,
            AuthorId = author.Id,
            StoredFileName = storedName,
            OriginalFileName = originalName,
            FileSizeBytes = fileSize,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        // "Yayınla" ile oluşturulduysa doğrudan yayına al.
        ApplyRequestedStatus(article, request.Status);

        _db.Articles.Add(article);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Yeni yazı oluşturuldu: #{Id} '{Title}' (yazar {AuthorId})",
            article.Id, article.Title, article.AuthorId);

        return ToDetail(article, new ArticleSocialResponse());
    }

    public async Task<ArticleDetailResponse> UpdateAsync(int id, SaveArticleRequest request, CancellationToken ct)
    {
        var article = await LoadModifiableAsync(id, ct);

        var newTitle = (request.Title ?? "").Trim();
        var titleChanged = !string.Equals(article.Title, newTitle, StringComparison.Ordinal);

        article.Title = newTitle;
        article.Summary = NormalizeSummary(request.Summary);
        if (titleChanged && newTitle.Length > 0)
        {
            article.Slug = await MakeUniqueSlugAsync(newTitle, excludeId: article.Id, ct);
        }

        // PDF verildiyse değiştir; verilmediyse dokunma.
        string? oldStoredName = null;
        if (request.Pdf is not null)
        {
            ValidatePdf(request.Pdf);
            oldStoredName = article.StoredFileName;

            article.StoredFileName = await _storage.SaveAsync(request.Pdf, ct);
            article.OriginalFileName = CleanFileName(request.Pdf.FileName);
            article.FileSizeBytes = request.Pdf.Length;
        }

        // Yayınlanacaksa başlık + PDF şart.
        var willPublish = WantsPublish(request.Status)
            || (string.IsNullOrWhiteSpace(request.Status) && article.Status == ArticleStatus.Published);
        if (willPublish && newTitle.Length == 0)
        {
            throw new AppValidationException("Yayınlamak için bir başlık gerekli.");
        }
        if (willPublish && string.IsNullOrEmpty(article.StoredFileName))
        {
            throw new AppValidationException("Yayınlamak için bir PDF dosyası eklemelisin.");
        }

        // "Taslak olarak kaydet" / "Yayınla" düğmesi durum değişikliği de istemiş olabilir.
        ApplyRequestedStatus(article, request.Status);

        article.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Yeni PDF başarıyla kaydedildiyse ESKİ dosyayı şimdi sil (önce DB, sonra disk).
        if (oldStoredName is not null)
        {
            _storage.Delete(oldStoredName);
        }

        var social = await LoadSocialAsync(new[] { article.Id }, ct);
        return ToDetail(article, social.GetValueOrDefault(article.Id) ?? new());
    }

    /// <summary>İstek "Yayınla" mı istiyor? (form düğmesi Status="Published" gönderir)</summary>
    private static bool WantsPublish(string? requested)
        => string.Equals(requested?.Trim(), "Published", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Formdan gelen "Draft"/"Published" değerini yazıya uygular. Boş/tanınmayan
    /// değer → dokunma. İlk kez yayınlanıyorsa yayın tarihini damgalar.
    /// </summary>
    private static void ApplyRequestedStatus(Article article, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return;
        if (!Enum.TryParse<ArticleStatus>(requested.Trim(), ignoreCase: true, out var status)) return;

        article.Status = status;
        if (status == ArticleStatus.Published && article.PublishedAtUtc is null)
        {
            article.PublishedAtUtc = DateTime.UtcNow;
        }
    }

    public async Task SetStatusAsync(int id, ArticleStatus status, CancellationToken ct)
    {
        var article = await LoadModifiableAsync(id, ct);

        if (status == ArticleStatus.Published && string.IsNullOrEmpty(article.StoredFileName))
        {
            throw new AppValidationException("Yayınlamak için bir PDF dosyası eklemelisin.");
        }

        article.Status = status;

        // İlk kez yayınlanıyorsa yayın tarihini şimdi damgala.
        if (status == ArticleStatus.Published && article.PublishedAtUtc is null)
        {
            article.PublishedAtUtc = DateTime.UtcNow;
        }

        article.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var article = await LoadModifiableAsync(id, ct);

        // Yumuşak silme: kayıt kalır, bayrak set edilir. Diskteki PDF şimdilik durur;
        // ileride "30 günden eski silinmişleri temizle" gibi bir görevle kaldırılabilir.
        article.IsDeleted = true;
        article.DeletedAtUtc = DateTime.UtcNow;
        article.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Yazı çöpe atıldı: #{Id}", article.Id);
    }

    // ========================================================================
    // PDF AKIŞI (GÖRÜNTÜLE / İNDİR)
    // ========================================================================

    public async Task<PdfPayload> OpenPdfAsync(int id, bool asDownload, CancellationToken ct)
    {
        // Not: global query filter sayesinde silinmiş yazılar zaten gelmez.
        var article = await _db.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (article is null)
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        if (article.Status == ArticleStatus.Draft && !CanModify(article))
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        var stream = _storage.OpenRead(article.StoredFileName);
        if (stream is null)
        {
            _logger.LogWarning("Yazı #{Id} için PDF diskte bulunamadı: {File}", id, article.StoredFileName);
            throw new AppNotFoundException("PDF dosyası bulunamadı.");
        }

        // 1) Kartlarda gösterilen hızlı sayaç (+1, kaydı yüklemeden).
        if (asDownload)
        {
            await _db.Articles.Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.DownloadCount, a => a.DownloadCount + 1), ct);
        }
        else
        {
            await _db.Articles.Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), ct);
        }

        // 2) Zaman damgalı olay (yönetim istatistikleri "son X gün" için).
        _db.ArticleEvents.Add(new ArticleEvent
        {
            ArticleId = id,
            Kind = asDownload ? ArticleEventKind.Download : ArticleEventKind.View,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        return new PdfPayload(stream, "application/pdf", article.OriginalFileName);
    }

    public async Task RecordShareAsync(int id, CancellationToken ct)
    {
        if (!await _db.Articles.AnyAsync(a => a.Id == id, ct))
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        _db.ArticleEvents.Add(new ArticleEvent
        {
            ArticleId = id,
            Kind = ArticleEventKind.Share,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    // ========================================================================
    // YARDIMCI METODLAR (private)
    // ========================================================================

    /// <summary>Giriş yapan kullanıcının Id'sini döner; yoksa 403 fırlatır.</summary>
    private int RequireUserId()
        => _currentUser.UserId ?? throw new AppForbiddenException("Bu işlem için giriş yapmalısın.");

    /// <summary>Bu kullanıcı verilen yazıyı değiştirebilir mi? (Sahibi ya da Admin.)</summary>
    private bool CanModify(Article article)
        => _currentUser.IsAdmin || _currentUser.UserId == article.AuthorId;

    /// <summary>
    /// Yazıyı DEĞİŞTİRMEK üzere (tracking açık) yükler. Yoksa 404, yetki yoksa 403 fırlatır.
    /// </summary>
    private async Task<Article> LoadModifiableAsync(int id, CancellationToken ct)
    {
        var article = await _db.Articles
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (article is null)
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }

        if (!CanModify(article))
        {
            throw new AppForbiddenException("Bu yazı üzerinde işlem yapma yetkin yok.");
        }

        return article;
    }

    private void ValidatePdf(IFormFile file)
    {
        if (file.Length > _storageOptions.MaxUploadBytes)
        {
            var mb = _storageOptions.MaxUploadBytes / (1024 * 1024);
            throw new AppValidationException($"Dosya çok büyük. En fazla {mb} MB olabilir.");
        }

        if (!PdfValidator.LooksLikePdf(file))
        {
            throw new AppValidationException("Yalnızca geçerli bir PDF dosyası yükleyebilirsin.");
        }
    }

    private static IQueryable<Article> ApplyAuthorAndSearch(IQueryable<Article> query, int? authorId, string? search)
    {
        if (authorId is int id)
        {
            query = query.Where(a => a.AuthorId == id);
        }

        var term = search?.Trim();
        // En az 2 karakter — tek harfte her şeyi taramanın anlamı yok.
        if (!string.IsNullOrEmpty(term) && term.Length >= 2)
        {
            var pattern = $"%{Escape(term)}%";
            // ILIKE = büyük/küçük harf duyarsız "içinde geçiyor mu".
            // AppDbContext'teki pg_trgm GIN indeksleri sayesinde bu sorgu
            // büyük tabloda bile tarama yapmaz → DB'yi yormaz.
            query = query.Where(a =>
                EF.Functions.ILike(a.Title, pattern) ||
                (a.Summary != null && EF.Functions.ILike(a.Summary, pattern)) ||
                EF.Functions.ILike(a.Author.DisplayName, pattern));
        }

        return query;
    }

    /// <summary>LIKE/ILIKE desenindeki özel karakterleri (% _ \) kaçışlar.</summary>
    private static string Escape(string term)
        => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private async Task<PagedResult<ArticleListItemResponse>> PaginateAsync(
        IQueryable<Article> query, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 1 : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

        var total = await query.CountAsync(ct);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var social = await LoadSocialAsync(rows.Select(r => r.Id), ct);

        return new PagedResult<ArticleListItemResponse>
        {
            Items = rows.Select(a => ToListItem(a, social.GetValueOrDefault(a.Id) ?? new())).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    /// <summary>
    /// Verilen yazı id'leri için beğeni/yorum sayıları + giriş yapan kişinin
    /// beğeni/yıldız durumu. Liste için TOPLAM 2–4 sorgu (N+1 yok).
    /// </summary>
    private async Task<Dictionary<int, ArticleSocialResponse>> LoadSocialAsync(
        IEnumerable<int> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<int, ArticleSocialResponse>();
        }

        var likeCounts = await _db.ArticleLikes
            .Where(l => idList.Contains(l.ArticleId))
            .GroupBy(l => l.ArticleId)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.C, ct);

        var commentCounts = await _db.Comments
            .Where(c => idList.Contains(c.ArticleId))
            .GroupBy(c => c.ArticleId)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.C, ct);

        var myLikes = new HashSet<int>();
        var myBookmarks = new HashSet<int>();
        if (_currentUser.UserId is int uid)
        {
            myLikes = (await _db.ArticleLikes
                .Where(l => l.UserId == uid && idList.Contains(l.ArticleId))
                .Select(l => l.ArticleId).ToListAsync(ct)).ToHashSet();

            myBookmarks = (await _db.ArticleBookmarks
                .Where(b => b.UserId == uid && idList.Contains(b.ArticleId))
                .Select(b => b.ArticleId).ToListAsync(ct)).ToHashSet();
        }

        return idList.ToDictionary(id => id, id => new ArticleSocialResponse
        {
            LikeCount = likeCounts.GetValueOrDefault(id),
            CommentCount = commentCounts.GetValueOrDefault(id),
            ViewerLiked = myLikes.Contains(id),
            ViewerBookmarked = myBookmarks.Contains(id),
        });
    }

    /// <summary>
    /// Başlıktan slug üretir ve veritabanında çakışıyorsa sonuna -2, -3 ... ekler.
    /// </summary>
    private async Task<string> MakeUniqueSlugAsync(string title, int? excludeId, CancellationToken ct)
    {
        var baseSlug = SlugHelper.Generate(title);
        var candidate = baseSlug;
        var suffix = 2;

        // excludeId null ise 0 kullan: gerçek bir Id ile asla çakışmaz, yani
        // "kendisi hariç" kontrolü güncelleme dışındaki durumlarda etkisiz kalır.
        var excluded = excludeId ?? 0;

        // Silinmişler de dahil (IgnoreQueryFilters) — aynı slug'ı tekrar üretmeyelim.
        while (await _db.Articles.IgnoreQueryFilters()
                   .AnyAsync(a => a.Slug == candidate && a.Id != excluded, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>Boş/whitespace özeti null'a çevirir, doluyu kırpar.</summary>
    private static string? NormalizeSummary(string? summary)
        => string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

    /// <summary>Yüklenen dosya adını güvenli hale getirir (yol parçalarını atar, kısaltır).</summary>
    private static string CleanFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "yazi.pdf";
        }
        return name.Length > 200 ? name[^200..] : name;
    }

    // ---- Domain → DTO dönüşümleri ----

    private static AuthorSummary ToAuthor(Article a) => new()
    {
        Id = a.AuthorId,
        DisplayName = a.Author?.DisplayName ?? string.Empty,
        AvatarUrl = a.Author is null ? null : AvatarUrl.For(a.Author),
        Bio = a.Author?.Bio,
        AvatarZoom = a.Author?.AvatarZoom ?? 1.0,
        AvatarPosX = a.Author?.AvatarPosX ?? 50,
        AvatarPosY = a.Author?.AvatarPosY ?? 50,
    };

    private static ArticleListItemResponse ToListItem(Article a, ArticleSocialResponse social) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Summary = a.Summary,
        Slug = a.Slug,
        Status = a.Status.ToString(),
        Author = ToAuthor(a),
        PublishedAtUtc = a.PublishedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
        FileSizeBytes = a.FileSizeBytes,
        ViewCount = a.ViewCount,
        DownloadCount = a.DownloadCount,
        Social = social,
    };

    private static ArticleDetailResponse ToDetail(Article a, ArticleSocialResponse social) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Summary = a.Summary,
        Slug = a.Slug,
        Status = a.Status.ToString(),
        Author = ToAuthor(a),
        PublishedAtUtc = a.PublishedAtUtc,
        CreatedAtUtc = a.CreatedAtUtc,
        UpdatedAtUtc = a.UpdatedAtUtc,
        OriginalFileName = a.OriginalFileName,
        FileSizeBytes = a.FileSizeBytes,
        ViewCount = a.ViewCount,
        DownloadCount = a.DownloadCount,
        // Frontend bu adresleri doğrudan kullanır (gömülü görüntüleyici / indirme linki).
        FileUrl = $"/api/articles/{a.Id}/file",
        DownloadUrl = $"/api/articles/{a.Id}/download",
        Social = social,
    };
}
