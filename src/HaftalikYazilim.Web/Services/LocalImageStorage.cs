using HaftalikYazilim.Web.Options;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Profil fotoğraflarını yerel diske yazar. Klasör: <c>Storage:AvatarRoot</c>
/// (varsayılan <c>App_Data/avatars</c>).
///
/// ÖNEMLİ: Burada görsel İŞLENMEZ (küçültme/kırpma yok). Tarayıcı fotoğrafı
/// zaten 512px kareye küçültüp JPEG'e çevirip gönderir. Sunucu yalnızca:
///   • dosya imzasından türü belirler (jpg / png / webp)
///   • <c>user-&lt;id&gt;.&lt;uzantı&gt;</c> adıyla diske yazar (üzerine yazar)
/// Böylece sunucuda görsel kütüphanesi gerekmez.
/// </summary>
public class LocalImageStorage : IImageStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(
        IOptions<StorageOptions> options,
        IHostEnvironment env,
        ILogger<LocalImageStorage> logger)
    {
        _logger = logger;

        var configured = options.Value.AvatarRoot;
        _rootPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAvatarAsync(IFormFile file, int userId, CancellationToken ct = default)
    {
        var ext = SniffExtension(file);
        var fileName = $"user-{userId}{ext}";
        var fullPath = Path.Combine(_rootPath, fileName);

        // Aynı kullanıcı daha önce farklı uzantıyla yüklediyse eskisini temizle.
        foreach (var old in Directory.EnumerateFiles(_rootPath, $"user-{userId}.*"))
        {
            if (!string.Equals(old, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(old);
            }
        }

        // Önce .tmp'ye yaz, sonra taşı → yarıda kalırsa eski foto bozulmasın.
        var tempPath = fullPath + ".tmp";
        await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(target, ct);
        }
        File.Move(tempPath, fullPath, overwrite: true);

        return fileName;
    }

    public Stream? OpenRead(string fileName)
    {
        var fullPath = ResolveSafePath(fileName);
        return File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public void Delete(string fileName) => TryDelete(ResolveSafePath(fileName));

    // ------------------------------------------------------------------

    /// <summary>İlk baytlara bakıp uzantı seçer (.jpg / .png / .webp). Bilinmezse .jpg.</summary>
    private static string SniffExtension(IFormFile file)
    {
        Span<byte> head = stackalloc byte[16];
        using var s = file.OpenReadStream();
        var read = s.ReadAtLeast(head, 16, throwOnEndOfStream: false);
        if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return ".jpg";
        if (read >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) return ".png";
        if (read >= 12 && head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
            && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P') return ".webp";
        return ".jpg";
    }

    private string ResolveSafePath(string fileName)
        => Path.Combine(_rootPath, Path.GetFileName(fileName));

    private void TryDelete(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profil fotoğrafı silinemedi: {Path}", fullPath);
        }
    }
}
