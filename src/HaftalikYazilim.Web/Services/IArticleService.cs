using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Domain;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Yazılarla ilgili TÜM iş mantığı burada. Controller'lar sadece HTTP ile ilgilenir,
/// karar/kural buraya taşınır. Böylece ileride başka bir arayüz (ör. bir arka plan
/// görevi, bir CLI) aynı mantığı yeniden kullanabilir.
///
/// Yetki kontrolleri (kimin neyi görebileceği/değiştirebileceği) bu servisin
/// içinde <see cref="ICurrentUser"/> üzerinden yapılır.
/// </summary>
public interface IArticleService
{
    /// <summary>
    /// Herkese açık liste: yalnızca "Yayında" olan yazılar. Filtre + arama + sayfalama.
    /// <paramref name="followingOnly"/> = true ise sadece giriş yapan kişinin takip
    /// ettiği yazarların yazıları döner (giriş yoksa etkisiz).
    /// </summary>
    Task<PagedResult<ArticleListItemResponse>> GetPublishedAsync(
        int? authorId, string? search, bool followingOnly, int page, int pageSize, CancellationToken ct);

    /// <summary>Giriş yapan kullanıcının KENDİ yazıları (taslak dahil), en son güncellenen üstte.</summary>
    Task<IReadOnlyList<ArticleListItemResponse>> GetMineAsync(CancellationToken ct);

    /// <summary>Giriş yapan kullanıcının yıldızladığı (kaydettiği) yazılar, en son yıldızlanan üstte.</summary>
    Task<IReadOnlyList<ArticleListItemResponse>> GetBookmarkedAsync(CancellationToken ct);

    /// <summary>Giriş yapan kullanıcının beğendiği yazılar, en son beğenilen üstte.</summary>
    Task<IReadOnlyList<ArticleListItemResponse>> GetLikedAsync(CancellationToken ct);

    /// <summary>Admin listesi: taslak + yayında (+ istenirse silinmişler). Filtre + arama + sayfalama.</summary>
    Task<PagedResult<ArticleListItemResponse>> GetForAdminAsync(
        int? authorId, string? search, bool includeDeleted, int page, int pageSize, CancellationToken ct);

    /// <summary>Tek yazının detayı. Taslağı yalnızca sahibi veya Admin görebilir; aksi halde 404.</summary>
    Task<ArticleDetailResponse> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Yeni yazı oluşturur (durum: Taslak). PDF zorunludur.</summary>
    Task<ArticleDetailResponse> CreateAsync(SaveArticleRequest request, CancellationToken ct);

    /// <summary>Var olan yazıyı günceller. PDF verilmezse mevcut PDF korunur. Sahibi veya Admin.</summary>
    Task<ArticleDetailResponse> UpdateAsync(int id, SaveArticleRequest request, CancellationToken ct);

    /// <summary>Yayınla / yayından kaldır. Sahibi veya Admin.</summary>
    Task SetStatusAsync(int id, ArticleStatus status, CancellationToken ct);

    /// <summary>Yumuşak silme (çöp kutusu). Kayıt kalır, listelerde görünmez. Sahibi veya Admin.</summary>
    Task DeleteAsync(int id, CancellationToken ct);

    /// <summary>
    /// PDF akışını döner (görüntüleme veya indirme için). Taslak PDF'ini yalnızca
    /// sahibi/Admin alabilir. Sayaç + zaman damgalı olay burada kaydedilir.
    /// </summary>
    Task<PdfPayload> OpenPdfAsync(int id, bool asDownload, CancellationToken ct);

    /// <summary>Bir "paylaşıldı" olayı kaydeder (yönetim istatistikleri için).</summary>
    Task RecordShareAsync(int id, CancellationToken ct);

    /// <summary>"Yazılar" sayfasındaki yazar filtresi için: yayınlanmış yazısı olan yazarlar.</summary>
    Task<IReadOnlyList<AuthorResponse>> GetAuthorsAsync(CancellationToken ct);
}

/// <summary>
/// <see cref="IArticleService.OpenPdfAsync"/> sonucu. Controller bunu bir dosya
/// yanıtına çevirir. <see cref="Content"/> akışını controller kapatır.
/// </summary>
public sealed record PdfPayload(Stream Content, string ContentType, string FileName);
