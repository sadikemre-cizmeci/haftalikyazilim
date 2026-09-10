namespace HaftalikYazilim.Web.Services;

/// <summary>
/// "İsteği yapan kişi kim?" bilgisini controller ve servislerde kolayca almak için.
/// HttpContext'e her yerde tek tek bakmak yerine bunu enjekte ederiz;
/// hem okuması kolay hem test edilebilir.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Giriş yapılmış mı?</summary>
    bool IsAuthenticated { get; }

    /// <summary>Giriş yapan kullanıcının veritabanı Id'si (giriş yoksa null).</summary>
    int? UserId { get; }

    /// <summary>Giriş yapan kullanıcının e-postası (giriş yoksa null).</summary>
    string? Email { get; }

    /// <summary>Giriş yapan kullanıcı Admin mi?</summary>
    bool IsAdmin { get; }
}
