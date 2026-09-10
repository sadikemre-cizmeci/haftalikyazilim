using System.Security.Claims;
using HaftalikYazilim.Web.Infrastructure;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// <see cref="ICurrentUser"/> gerçeklemesi. Bilgileri, kimlik doğrulama sırasında
/// çereze yazılan "claim"lerden okur (bkz. <c>AuthController.Login</c>).
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public bool IsAdmin => Principal?.IsInRole(AppRoles.Admin) ?? false;
}
