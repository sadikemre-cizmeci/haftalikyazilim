namespace HaftalikYazilim.Web.Infrastructure;

/// <summary>
/// Rol adları tek yerde. Controller'larda <c>[Authorize(Roles = AppRoles.Admin)]</c>
/// şeklinde kullanılır. Değerler <see cref="Domain.UserRole"/> enum adlarıyla
/// AYNI olmak zorunda (kimlik doğrulama sırasında claim'e enum'un adı yazılıyor).
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Author = "Author";

    /// <summary>Yazı oluşturabilen/yönetebilen roller.</summary>
    public const string AuthorOrAdmin = Author + "," + Admin;
}
