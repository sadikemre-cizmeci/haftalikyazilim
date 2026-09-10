using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HaftalikYazilim.Web.Services;

public class SocialService : ISocialService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SocialService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ToggleResponse> ToggleLikeAsync(int articleId, CancellationToken ct)
    {
        var uid = RequireUserId();
        await EnsureArticleAsync(articleId, ct);

        var existing = await _db.ArticleLikes
            .FirstOrDefaultAsync(x => x.ArticleId == articleId && x.UserId == uid, ct);

        if (existing is not null)
        {
            _db.ArticleLikes.Remove(existing);
        }
        else
        {
            _db.ArticleLikes.Add(new ArticleLike { ArticleId = articleId, UserId = uid });
        }
        await _db.SaveChangesAsync(ct);

        var count = await _db.ArticleLikes.CountAsync(x => x.ArticleId == articleId, ct);
        return new ToggleResponse { Active = existing is null, Count = count };
    }

    public async Task<ToggleResponse> ToggleBookmarkAsync(int articleId, CancellationToken ct)
    {
        var uid = RequireUserId();
        await EnsureArticleAsync(articleId, ct);

        var existing = await _db.ArticleBookmarks
            .FirstOrDefaultAsync(x => x.ArticleId == articleId && x.UserId == uid, ct);

        if (existing is not null)
        {
            _db.ArticleBookmarks.Remove(existing);
        }
        else
        {
            _db.ArticleBookmarks.Add(new ArticleBookmark { ArticleId = articleId, UserId = uid });
        }
        await _db.SaveChangesAsync(ct);

        return new ToggleResponse { Active = existing is null, Count = 0 };
    }

    public async Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(int articleId, CancellationToken ct)
    {
        var viewerId = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;

        var comments = await _db.Comments
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.ArticleId == articleId)
            .OrderByDescending(c => c.CreatedAtUtc)   // en yeni yorum üstte
            .ToListAsync(ct);

        return comments.Select(c => ToResponse(c, viewerId, isAdmin)).ToList();
    }

    public async Task<CommentResponse> AddCommentAsync(int articleId, CreateCommentRequest request, CancellationToken ct)
    {
        var uid = RequireUserId();
        await EnsureArticleAsync(articleId, ct);

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            throw new AppValidationException("Yorum boş olamaz.");
        }

        var comment = new Comment
        {
            ArticleId = articleId,
            UserId = uid,
            Body = body,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(comment).Reference(c => c.User).LoadAsync(ct);
        return ToResponse(comment, uid, _currentUser.IsAdmin);
    }

    public async Task DeleteCommentAsync(int commentId, CancellationToken ct)
    {
        var uid = RequireUserId();
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new AppNotFoundException("Yorum bulunamadı.");

        if (comment.UserId != uid && !_currentUser.IsAdmin)
        {
            throw new AppForbiddenException("Bu yorumu silme yetkin yok.");
        }

        comment.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------

    private int RequireUserId()
        => _currentUser.UserId ?? throw new AppForbiddenException("Bu işlem için giriş yapmalısın.");

    private async Task EnsureArticleAsync(int articleId, CancellationToken ct)
    {
        // Query filter → silinmiş yazılar zaten gelmez.
        if (!await _db.Articles.AnyAsync(a => a.Id == articleId, ct))
        {
            throw new AppNotFoundException("Yazı bulunamadı.");
        }
    }

    private static CommentResponse ToResponse(Comment c, int? viewerId, bool isAdmin) => new()
    {
        Id = c.Id,
        Author = new AuthorSummary
        {
            Id = c.UserId,
            DisplayName = c.User?.DisplayName ?? string.Empty,
            AvatarUrl = c.User is null ? null : AvatarUrl.For(c.User),
            Bio = c.User?.Bio,
            AvatarZoom = c.User?.AvatarZoom ?? 1.0,
            AvatarPosX = c.User?.AvatarPosX ?? 50,
            AvatarPosY = c.User?.AvatarPosY ?? 50,
        },
        Body = c.Body,
        CreatedAtUtc = c.CreatedAtUtc,
        CanDelete = isAdmin || (viewerId is not null && viewerId == c.UserId),
    };
}
