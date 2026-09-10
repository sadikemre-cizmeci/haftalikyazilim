using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Sosyal etkileşim: beğeni, yıldız (kaydet), yorum. Hepsi giriş ister
/// (okuma sayfası sayaçları zaten yazı yanıtı içinde geliyor).
/// </summary>
[ApiController]
public class SocialController : ControllerBase
{
    private readonly ISocialService _social;

    public SocialController(ISocialService social)
    {
        _social = social;
    }

    /// <summary>Yazının beğenisini aç/kapa.</summary>
    [HttpPost("api/articles/{id:int}/like")]
    [Authorize]
    public async Task<ActionResult<ToggleResponse>> ToggleLike(int id, CancellationToken ct)
        => Ok(await _social.ToggleLikeAsync(id, ct));

    /// <summary>Yazının yıldızını (okuma listesi) aç/kapa.</summary>
    [HttpPost("api/articles/{id:int}/bookmark")]
    [Authorize]
    public async Task<ActionResult<ToggleResponse>> ToggleBookmark(int id, CancellationToken ct)
        => Ok(await _social.ToggleBookmarkAsync(id, ct));

    /// <summary>Bir yazının yorumları (herkese açık).</summary>
    [HttpGet("api/articles/{id:int}/comments")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> GetComments(int id, CancellationToken ct)
        => Ok(await _social.GetCommentsAsync(id, ct));

    /// <summary>Yazıya yorum ekle.</summary>
    [HttpPost("api/articles/{id:int}/comments")]
    [Authorize]
    public async Task<ActionResult<CommentResponse>> AddComment(
        int id, [FromBody] CreateCommentRequest request, CancellationToken ct)
        => Ok(await _social.AddCommentAsync(id, request, ct));

    /// <summary>Yorumu sil (sahibi veya Admin).</summary>
    [HttpDelete("api/comments/{commentId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int commentId, CancellationToken ct)
    {
        await _social.DeleteCommentAsync(commentId, ct);
        return NoContent();
    }
}
