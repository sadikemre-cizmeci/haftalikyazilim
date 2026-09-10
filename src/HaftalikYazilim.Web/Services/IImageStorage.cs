namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Profil fotoğraflarının saklanması. Bugün: yerel disk (<see cref="LocalImageStorage"/>).
/// Yüklenen görsel küçültülüp (kare, 512×512) JPEG olarak yeniden kodlanır — böylece
/// her yerde küçük ve hızlı yüklenir.
/// </summary>
public interface IImageStorage
{
    /// <summary>Görseli 512×512 kareye kırpıp JPEG olarak kaydeder; dosya adını döner.</summary>
    Task<string> SaveAvatarAsync(IFormFile file, int userId, CancellationToken ct = default);

    /// <summary>Okuma akışı açar; dosya yoksa null.</summary>
    Stream? OpenRead(string fileName);

    /// <summary>Dosyayı siler (yoksa sessiz geçer).</summary>
    void Delete(string fileName);
}
