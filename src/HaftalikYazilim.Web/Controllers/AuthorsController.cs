using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// "Yazılar" sayfasındaki yazar filtresini (açılır liste) beslemek için.
/// Sadece en az bir YAYINLANMIŞ yazısı olan yazarları döner.
/// </summary>
[ApiController]
[Route("api/authors")]
public class AuthorsController : ControllerBase
{
    private readonly IArticleService _articles;

    public AuthorsController(IArticleService articles)
    {
        _articles = articles;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AuthorResponse>>> GetAll(CancellationToken ct)
    {
        var authors = await _articles.GetAuthorsAsync(ct);
        return Ok(authors);
    }
}
