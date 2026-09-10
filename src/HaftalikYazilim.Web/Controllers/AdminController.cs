using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Infrastructure;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Yönetim (admin) uçları. Bu controller'daki HER uç yalnızca Admin rolüne açıktır.
/// "Her şeyi görebilen admin" ihtiyacını karşılar:
///  - Tüm yazılar (taslak + yayında, istenirse silinmişler dahil)
///  - Tüm kullanıcılar
///
/// İleride buraya kullanıcı ekleme/rol değiştirme, istatistik özetleri vb. eklenebilir.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IArticleService _articles;
    private readonly AppDbContext _db;

    public AdminController(IArticleService articles, AppDbContext db)
    {
        _articles = articles;
        _db = db;
    }

    /// <summary>Tüm yazılar. <paramref name="includeDeleted"/> = true ise çöp kutusundakiler de gelir.</summary>
    [HttpGet("articles")]
    public async Task<ActionResult<PagedResult<ArticleListItemResponse>>> GetAllArticles(
        [FromQuery] int? authorId,
        [FromQuery] string? search,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _articles.GetForAdminAsync(authorId, search, includeDeleted, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Tüm kullanıcılar (yazar + admin).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers(CancellationToken ct)
    {
        // Basit bir okuma; ayrı bir servis açmaya değmeyecek kadar küçük.
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                CreatedAtUtc = u.CreatedAtUtc,
                // Silinmemiş yazı sayısı (global query filter bu alt sorguya da uygulanır).
                ArticleCount = _db.Articles.Count(a => a.AuthorId == u.Id),
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>
    /// İstatistik: son <paramref name="days"/> gün içinde farklı ölçütlere göre ilk 10 kişi.
    /// days: 1 (bugün) | 7 (bir hafta) | 30 (bir ay). Diğer değerler 7'ye çekilir.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsResponse>> GetStats(
        [FromQuery] int days = 7, CancellationToken ct = default)
    {
        days = days is 1 or 7 or 30 ? days : 7;
        var since = DateTime.UtcNow.AddDays(-days);

        return Ok(new AdminStatsResponse
        {
            Days = days,
            MostViewed = await EventTopAsync(Domain.ArticleEventKind.View, since, ct),
            MostDownloaded = await EventTopAsync(Domain.ArticleEventKind.Download, since, ct),
            MostShared = await EventTopAsync(Domain.ArticleEventKind.Share, since, ct),
            MostLiked = await ToRowsAsync(await (
                from l in _db.ArticleLikes
                where l.CreatedAtUtc >= since
                join a in _db.Articles on l.ArticleId equals a.Id
                group l by a.AuthorId into g
                orderby g.Count() descending
                select new TopRow(g.Key, g.Count())).Take(10).ToListAsync(ct), ct),
            MostBookmarked = await ToRowsAsync(await (
                from b in _db.ArticleBookmarks
                where b.CreatedAtUtc >= since
                join a in _db.Articles on b.ArticleId equals a.Id
                group b by a.AuthorId into g
                orderby g.Count() descending
                select new TopRow(g.Key, g.Count())).Take(10).ToListAsync(ct), ct),
        });
    }

    private record TopRow(int AuthorId, int Count);

    private async Task<IReadOnlyList<StatRow>> EventTopAsync(string kind, DateTime since, CancellationToken ct)
    {
        var top = await (
            from e in _db.ArticleEvents
            where e.Kind == kind && e.CreatedAtUtc >= since
            join a in _db.Articles on e.ArticleId equals a.Id
            group e by a.AuthorId into g
            orderby g.Count() descending
            select new TopRow(g.Key, g.Count())).Take(10).ToListAsync(ct);

        return await ToRowsAsync(top, ct);
    }

    private async Task<IReadOnlyList<StatRow>> ToRowsAsync(List<TopRow> top, CancellationToken ct)
    {
        if (top.Count == 0) return [];

        var ids = top.Select(t => t.AuthorId).ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Username, u.AvatarFileName, u.UpdatedAtUtc })
            .ToDictionaryAsync(u => u.Id, ct);

        return top.Select(t =>
        {
            users.TryGetValue(t.AuthorId, out var u);
            return new StatRow
            {
                UserId = t.AuthorId,
                DisplayName = u?.DisplayName ?? "—",
                Username = u?.Username ?? "",
                AvatarUrl = u?.AvatarFileName is null
                    ? null
                    : $"/api/profile/{t.AuthorId}/avatar?v={u.UpdatedAtUtc.Ticks}",
                Value = t.Count,
            };
        }).ToList();
    }
}
