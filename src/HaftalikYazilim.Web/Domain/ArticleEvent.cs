namespace HaftalikYazilim.Web.Domain;

/// <summary>
/// Bir yazıya dair zaman damgalı olay — analitik/istatistik için.
/// Kartlardaki hızlı sayaçlar (ViewCount/DownloadCount) ayrıca tutulur;
/// bu tablo "son 7 günde en çok görüntülenen" gibi ZAMAN FİLTRELİ sorgular için.
///
/// <see cref="Kind"/>: "view" | "download" | "share".
/// </summary>
public class ArticleEvent
{
    public long Id { get; set; }

    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public string Kind { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Olay türleri — sihirli metin dağılmasın diye.</summary>
public static class ArticleEventKind
{
    public const string View = "view";
    public const string Download = "download";
    public const string Share = "share";
}
