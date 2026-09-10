using HaftalikYazilim.Web.Contracts;

namespace HaftalikYazilim.Web.Services;

/// <summary>Beğeni / yıldız (kaydet) / yorum işlemleri. Hepsi giriş ister.</summary>
public interface ISocialService
{
    /// <summary>Yazının beğenisini aç-kapa. Dönen: yeni durum + yeni toplam.</summary>
    Task<ToggleResponse> ToggleLikeAsync(int articleId, CancellationToken ct);

    /// <summary>Yazının yıldızını (okuma listesi) aç-kapa.</summary>
    Task<ToggleResponse> ToggleBookmarkAsync(int articleId, CancellationToken ct);

    /// <summary>Bir yazının yorumları, eskiden yeniye.</summary>
    Task<IReadOnlyList<CommentResponse>> GetCommentsAsync(int articleId, CancellationToken ct);

    /// <summary>Yazıya yorum ekler.</summary>
    Task<CommentResponse> AddCommentAsync(int articleId, CreateCommentRequest request, CancellationToken ct);

    /// <summary>Yorumu siler (sahibi veya Admin).</summary>
    Task DeleteCommentAsync(int commentId, CancellationToken ct);
}
