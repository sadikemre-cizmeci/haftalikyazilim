namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Yüklenen profil fotoğrafının gerçekten desteklenen bir görsel olduğunu doğrular
/// (uzantı + içerik türü + dosya imzası / magic bytes).
/// Kabul: JPEG, PNG, WebP. (Küçültme sonrası hepsi JPEG olarak saklanır.)
/// </summary>
public static class ImageValidator
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();   // WebP: "RIFF"...."WEBP"
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    public static bool LooksLikeImage(IFormFile file)
    {
        if (file.Length == 0)
        {
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return false;
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Span<byte> head = stackalloc byte[16];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(head, 16, throwOnEndOfStream: false);
        if (read < 12)
        {
            return false;
        }

        if (head[..3].SequenceEqual(Jpeg)) return true;
        if (head[..8].SequenceEqual(Png)) return true;
        if (head[..4].SequenceEqual(Riff) && head[8..12].SequenceEqual(Webp)) return true;

        return false;
    }
}
