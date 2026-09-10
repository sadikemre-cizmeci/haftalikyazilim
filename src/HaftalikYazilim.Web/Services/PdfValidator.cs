namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Yüklenen dosyanın gerçekten PDF olduğunu doğrular.
///
/// Sadece uzantıya veya Content-Type'a güvenmek yeterli değildir (ikisi de
/// kolayca yanıltılır). Bu yüzden dosyanın ilk baytlarına da bakarız:
/// her PDF dosyası <c>%PDF-</c> (25 50 44 46 2D) baytlarıyla başlar.
/// </summary>
public static class PdfValidator
{
    // "%PDF-" — UTF-8 bayt dizisi (C# 11 u8 literal).
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    /// <summary>Dosya boş değilse, uzantısı .pdf ise, içerik türü pdf ise ve
    /// baytları "%PDF-" ile başlıyorsa true döner.</summary>
    public static bool LooksLikePdf(IFormFile file)
    {
        if (file.Length == 0)
        {
            return false;
        }

        var hasPdfExtension = Path.GetExtension(file.FileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        var hasPdfContentType =
            file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            file.ContentType.Equals("application/x-pdf", StringComparison.OrdinalIgnoreCase);

        if (!hasPdfExtension || !hasPdfContentType)
        {
            return false;
        }

        // İlk 5 baytı oku ve imzayı karşılaştır.
        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[5];
        var read = stream.ReadAtLeast(header, 5, throwOnEndOfStream: false);
        return read == 5 && header.SequenceEqual(PdfMagic);
    }
}
