namespace HaftalikYazilim.Web.Services;

/// <summary>
/// PDF dosyalarının fiziksel olarak saklanmasından sorumlu.
///
/// Bugün: yerel disk (<see cref="LocalPdfStorage"/>).
/// Yarın: S3 / Azure Blob / MinIO istersen sadece yeni bir sınıf yazıp
/// Program.cs'te tek satırı değiştirirsin; controller ve servis kodu hiç değişmez.
/// </summary>
public interface IPdfStorage
{
    /// <summary>Dosyayı kaydeder ve diskte kullanılan (benzersiz) dosya adını döner.</summary>
    Task<string> SaveAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Okuma için akış (stream) açar. Dosya yoksa <c>null</c> döner.</summary>
    Stream? OpenRead(string storedFileName);

    /// <summary>Dosyayı siler. Dosya yoksa sessizce geçer (hata fırlatmaz).</summary>
    void Delete(string storedFileName);
}
