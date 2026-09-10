namespace HaftalikYazilim.Web.Contracts;

/// <summary>Yönetim istatistik tablosunun bir satırı (bir kullanıcı + bir sayı).</summary>
public record StatRow
{
    public int UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int Value { get; init; }
}

/// <summary>
/// Yönetim istatistikleri: seçilen gün aralığında, farklı ölçütlere göre ilk 10 kişi.
/// Her ölçüt bir kullanıcının TÜM yazılarındaki toplamı üzerinden hesaplanır.
/// </summary>
public record AdminStatsResponse
{
    public int Days { get; init; }
    public IReadOnlyList<StatRow> MostViewed { get; init; } = [];
    public IReadOnlyList<StatRow> MostDownloaded { get; init; } = [];
    public IReadOnlyList<StatRow> MostLiked { get; init; } = [];
    public IReadOnlyList<StatRow> MostBookmarked { get; init; } = [];
    public IReadOnlyList<StatRow> MostShared { get; init; } = [];
}

/// <summary>
/// Yönetim panelindeki "Kullanıcılar" listesinin bir satırı.
/// (Şifre / hash gibi hassas alanlar kesinlikle YOK.)
/// </summary>
public record AdminUserResponse
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;   // "Admin" | "Author"
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Bu kullanıcının (silinmemiş) yazı sayısı.</summary>
    public int ArticleCount { get; init; }
}
