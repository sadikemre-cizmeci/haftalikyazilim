namespace HaftalikYazilim.Web.Domain;

// ============================================================================
// Sosyal etkileşim varlıkları — hepsi basit ilişki/kayıt tabloları.
// Abartısız: takip, beğeni, yıldız (kaydet), yorum.
// ============================================================================

/// <summary>Bir kullanıcının başka bir kullanıcıyı takip etmesi.</summary>
public class Follow
{
    /// <summary>Takip eden.</summary>
    public int FollowerId { get; set; }
    public User Follower { get; set; } = null!;

    /// <summary>Takip edilen.</summary>
    public int FolloweeId { get; set; }
    public User Followee { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Bir yazının bir kullanıcı tarafından beğenilmesi (kalp).</summary>
public class ArticleLike
{
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Bir yazının bir kullanıcı tarafından yıldızlanması (okuma listesine kaydetme).</summary>
public class ArticleBookmark
{
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Bir yazıya yapılan yorum. Düz liste (iç içe yanıt yok).</summary>
public class Comment
{
    public int Id { get; set; }

    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Yumuşak silme — yorum kaybolmasın, "silinmiş yorum" gösterilebilir.</summary>
    public bool IsDeleted { get; set; }
}
