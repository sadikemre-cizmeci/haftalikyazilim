using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Infrastructure;
using HaftalikYazilim.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IImageStorage _images;
    private readonly StorageOptions _storage;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        AppDbContext db,
        ICurrentUser currentUser,
        IImageStorage images,
        IOptions<StorageOptions> storage,
        ILogger<ProfileService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _images = images;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<MyProfileResponse> GetMyProfileAsync(CancellationToken ct)
    {
        var user = await LoadMeAsync(ct);
        return ToMine(user);
    }

    public async Task<MyProfileResponse> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await LoadMeAsync(ct);

        // Kullanıcı adı: normalize et, kurallara uy, benzersiz olsun.
        var username = UsernameHelper.Normalize(request.Username);
        if (!UsernameHelper.IsValid(username))
        {
            throw new AppValidationException("Kullanıcı adı 3–30 karakter olmalı ve yalnızca harf/rakam/nokta/alt çizgi içerebilir.");
        }
        // Rezerve ad kontrolü YALNIZCA ad değişiyorsa: hâlihazırda böyle bir adı olan
        // (ör. seed'den gelen "admin") kullanıcı profilini kaydedebilsin.
        if (username != user.Username && UsernameHelper.IsReserved(username))
        {
            throw new AppValidationException("Bu kullanıcı adı kullanılamaz. Lütfen başka bir ad seç.");
        }
        if (username != user.Username &&
            await _db.Users.AnyAsync(u => u.Username == username && u.Id != user.Id, ct))
        {
            throw new AppValidationException("Bu kullanıcı adı zaten alınmış.");
        }
        user.Username = username;

        user.DisplayName = request.DisplayName.Trim();
        user.Bio = Clean(request.Bio);
        user.About = Clean(request.About);
        user.AvatarZoom = Math.Clamp(request.AvatarZoom, 1.0, 3.0);
        user.AvatarPosX = Math.Clamp(request.AvatarPosX, 0, 100);
        user.AvatarPosY = Math.Clamp(request.AvatarPosY, 0, 100);
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToMine(user);
    }

    public async Task<MyProfileResponse> SetAvatarAsync(IFormFile file, CancellationToken ct)
    {
        var user = await LoadMeAsync(ct);

        if (file.Length > _storage.MaxAvatarBytes)
        {
            var mb = _storage.MaxAvatarBytes / (1024 * 1024);
            throw new AppValidationException($"Fotoğraf çok büyük. En fazla {mb} MB olabilir.");
        }
        if (!ImageValidator.LooksLikeImage(file))
        {
            throw new AppValidationException("Yalnızca JPG, PNG veya WebP fotoğraf yükleyebilirsin.");
        }

        user.AvatarFileName = await _images.SaveAvatarAsync(file, user.Id, ct);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Profil fotoğrafı güncellendi: kullanıcı {UserId}", user.Id);
        return ToMine(user);
    }

    public async Task<MyProfileResponse> RemoveAvatarAsync(CancellationToken ct)
    {
        var user = await LoadMeAsync(ct);

        if (user.AvatarFileName is not null)
        {
            _images.Delete(user.AvatarFileName);
            user.AvatarFileName = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return ToMine(user);
    }

    public async Task<PublicProfileResponse> GetPublicProfileAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new AppNotFoundException("Profil bulunamadı.");

        var count = await _db.Articles
            .AsNoTracking()
            .CountAsync(a => a.AuthorId == userId && a.Status == ArticleStatus.Published, ct);

        var followerCount = await _db.Follows.CountAsync(f => f.FolloweeId == userId, ct);
        var followingCount = await _db.Follows.CountAsync(f => f.FollowerId == userId, ct);

        var viewerId = _currentUser.UserId;
        var viewerFollows = viewerId is int vid && vid != userId
            && await _db.Follows.AnyAsync(f => f.FollowerId == vid && f.FolloweeId == userId, ct);

        return new PublicProfileResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            About = user.About,
            AvatarUrl = AvatarUrl.For(user),
            AvatarZoom = user.AvatarZoom,
            AvatarPosX = user.AvatarPosX,
            AvatarPosY = user.AvatarPosY,
            PublishedArticleCount = count,
            FollowerCount = followerCount,
            FollowingCount = followingCount,
            MemberSinceUtc = user.CreatedAtUtc,
            ViewerFollows = viewerFollows,
            IsSelf = viewerId == userId,
        };
    }

    public async Task FollowAsync(int targetUserId, CancellationToken ct)
    {
        var me = _currentUser.UserId
            ?? throw new AppForbiddenException("Takip için giriş yapmalısın.");
        if (me == targetUserId)
        {
            throw new AppValidationException("Kendini takip edemezsin.");
        }
        if (!await _db.Users.AnyAsync(u => u.Id == targetUserId && u.IsActive, ct))
        {
            throw new AppNotFoundException("Kullanıcı bulunamadı.");
        }

        var exists = await _db.Follows
            .AnyAsync(f => f.FollowerId == me && f.FolloweeId == targetUserId, ct);
        if (!exists)
        {
            _db.Follows.Add(new Follow { FollowerId = me, FolloweeId = targetUserId });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task UnfollowAsync(int targetUserId, CancellationToken ct)
    {
        var me = _currentUser.UserId
            ?? throw new AppForbiddenException("Bu işlem için giriş yapmalısın.");

        var existing = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == me && f.FolloweeId == targetUserId, ct);
        if (existing is not null)
        {
            _db.Follows.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<AvatarFilePayload?> OpenAvatarAsync(int userId, CancellationToken ct)
    {
        var name = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AvatarFileName)
            .FirstOrDefaultAsync(ct);

        if (name is null)
        {
            return null;
        }

        var stream = _images.OpenRead(name);
        if (stream is null)
        {
            return null;
        }

        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return new AvatarFilePayload(stream, contentType);
    }

    // ------------------------------------------------------------------

    private async Task<User> LoadMeAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId
            ?? throw new AppForbiddenException("Bu işlem için giriş yapmalısın.");

        return await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new AppNotFoundException("Hesap bulunamadı.");
    }

    private static string? Clean(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static MyProfileResponse ToMine(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Username = u.Username,
        DisplayName = u.DisplayName,
        Bio = u.Bio,
        About = u.About,
        AvatarUrl = AvatarUrl.For(u),
        AvatarZoom = u.AvatarZoom,
        AvatarPosX = u.AvatarPosX,
        AvatarPosY = u.AvatarPosY,
    };
}
