using HaftalikYazilim.Web.Options;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// PDF'leri yerel diske kaydeder. Klasör: <c>appsettings.json → Storage:Root</c>
/// (varsayılan: <c>App_Data/uploads</c>).
/// </summary>
public class LocalPdfStorage : IPdfStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalPdfStorage> _logger;

    public LocalPdfStorage(
        IOptions<StorageOptions> options,
        IHostEnvironment env,
        ILogger<LocalPdfStorage> logger)
    {
        _logger = logger;

        // Göreli yol verildiyse uygulamanın kök klasörüne (ContentRoot) göre çöz.
        var configured = options.Value.Root;
        _rootPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);

        // Klasör yoksa oluştur (ilk çalıştırmada).
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        // Dosya adını BİZ üretiyoruz. Nedenleri:
        //  1) İki yazı aynı ada sahip PDF yüklerse çakışmasın.
        //  2) Kullanıcının verdiği ad "../../etc/passwd" gibi bir dizin gezinme
        //     (path traversal) saldırısı olabilir; hiç kullanmıyoruz.
        var storedName = $"{Guid.NewGuid():N}.pdf";
        var fullPath = Path.Combine(_rootPath, storedName);

        await using var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var source = file.OpenReadStream();
        await source.CopyToAsync(target, ct);

        return storedName;
    }

    public Stream? OpenRead(string storedFileName)
    {
        var fullPath = ResolveSafePath(storedFileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        // Aynı dosyayı birden çok okuyucu aynı anda indirebilsin diye FileShare.Read.
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Delete(string storedFileName)
    {
        try
        {
            var fullPath = ResolveSafePath(storedFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            // Dosya silinemese bile uygulama akışı bozulmasın; sadece uyarı logla.
            _logger.LogWarning(ex, "PDF diskten silinemedi: {File}", storedFileName);
        }
    }

    /// <summary>
    /// Verilen adı SADECE dosya adına indirger (klasör parçalarını atar) ve
    /// kök klasörle birleştirir. Böylece kök klasör dışına çıkılamaz.
    /// </summary>
    private string ResolveSafePath(string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        return Path.Combine(_rootPath, safeName);
    }
}
