namespace HaftalikYazilim.Web.Contracts;

/// <summary>
/// Sayfalı liste sonucu. Frontend "Daha fazla" / sayfa numaraları düğmelerini
/// buna göre gösterir.
/// </summary>
/// <typeparam name="T">Liste elemanının tipi (ör. <see cref="ArticleListItemResponse"/>).</typeparam>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>1'den başlar.</summary>
    public int Page { get; init; }

    public int PageSize { get; init; }

    /// <summary>Filtreye uyan TOPLAM kayıt sayısı (bu sayfadaki değil).</summary>
    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;

    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
