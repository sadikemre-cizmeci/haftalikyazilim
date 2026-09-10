using System.Globalization;
using System.Text;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Başlıktan URL-dostu "slug" üretir.
/// Örn: "Haftalık Yazılım #1: Cache Nedir?" → "haftalik-yazilim-1-cache-nedir"
///
/// Türkçe karakterleri bilinçli olarak ASCII karşılıklarına çevirir
/// (ç→c, ş→s, ı→i, ğ→g, ö→o, ü→u). Diğer aksanlı latin harfleri de sadeleştirir.
/// </summary>
public static class SlugHelper
{
    // Türkçe harfleri elle eşliyoruz; Unicode "normalize" tek başına ı/İ'yi
    // istediğimiz gibi çözmüyor.
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ç'] = 'c', ['Ç'] = 'c',
        ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ı'] = 'i', ['İ'] = 'i',
        ['ö'] = 'o', ['Ö'] = 'o',
        ['ş'] = 's', ['Ş'] = 's',
        ['ü'] = 'u', ['Ü'] = 'u',
    };

    public static string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "yazi";
        }

        var sb = new StringBuilder(input.Length);

        foreach (var ch in input.Trim())
        {
            if (TurkishMap.TryGetValue(ch, out var mapped))
            {
                sb.Append(mapped);
            }
            else if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                sb.Append('-');
            }
            // kalan noktalama işaretleri tamamen atılır
        }

        // Kalan aksanlı latin harfleri (é, ñ, ...) ASCII'ye indir.
        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(ch);
            }
        }

        var result = ascii.ToString().Normalize(NormalizationForm.FormC);

        // Art arda '-' işaretlerini teke indir, baş/son '-' işaretlerini kırp.
        while (result.Contains("--"))
        {
            result = result.Replace("--", "-");
        }
        result = result.Trim('-');

        return result.Length == 0 ? "yazi" : result;
    }
}
