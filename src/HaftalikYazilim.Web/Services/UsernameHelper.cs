using System.Globalization;
using System.Text;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Kullanıcı adı normalize + doğrulama.
/// Kural: 3–30 karakter, küçük harf, yalnızca a-z 0-9 . _ ; nokta/alt çizgi
/// baş/sonda olamaz. Türkçe/aksanlı harfler ASCII'ye indirgenir (ş→s, ı→i ...).
/// </summary>
public static class UsernameHelper
{
    private static readonly Dictionary<char, char> Map = new()
    {
        ['ç'] = 'c', ['ğ'] = 'g', ['ı'] = 'i', ['İ'] = 'i', ['ö'] = 'o', ['ş'] = 's', ['ü'] = 'u',
    };

    /// <summary>
    /// Kimse alamasın: taklit riski taşıyan ("admin", "haftalikyazilim" …) ya da
    /// ileride bir adres/rota olabilecek ("api", "giris" …) adlar. Hepsi Normalize
    /// çıktısı biçiminde (küçük harf, ASCII, noktasız). TAM eşleşme engellenir;
    /// "admin_emre" gibi adlar serbesttir.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "administrator", "root", "sistem", "system", "yonetim", "yonetici",
        "moderator", "mod", "destek", "support", "yardim", "help", "iletisim", "contact",
        "hesap", "account", "kayit", "register", "giris", "login", "cikis", "logout",
        "sifre", "password", "api", "www", "mail", "email", "eposta", "posta",
        "haftalikyazilim", "haftalik", "editor", "ekip", "team", "staff", "resmi",
        "official", "anonim", "anonymous", "null", "undefined", "profil", "profile",
        "ayarlar", "settings", "yazi", "yazilar", "oku", "yonetimpaneli",
    };

    /// <summary>Girdiyi kurallara uygun bir kullanıcı adına çevirir.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var raw in input.Trim().ToLowerInvariant())
        {
            var ch = Map.TryGetValue(raw, out var m) ? m : raw;
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_')
            {
                sb.Append(ch);
            }
        }

        // Aksanlı latin harfleri (é, ñ ...) sadeleştir.
        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(ch);
            }
        }

        var result = ascii.ToString().Trim('.', '_');
        while (result.Contains("..") || result.Contains("__"))
        {
            result = result.Replace("..", ".").Replace("__", "_");
        }

        return result.Length > 30 ? result[..30] : result;
    }

    /// <summary>Kurallara uyuyor mu? (Normalize sonrası çağır.)</summary>
    public static bool IsValid(string username)
        => username.Length is >= 3 and <= 30;

    /// <summary>Bu ad rezerve mi? (Normalize sonrası çağır.)</summary>
    public static bool IsReserved(string username)
        => Reserved.Contains(username);
}
