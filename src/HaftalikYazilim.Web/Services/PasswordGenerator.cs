using System.Security.Cryptography;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// "Şifremi unuttum" akışında kullanıcıya verilecek yeni, rastgele şifreyi üretir.
/// Kriptografik olarak güçlü rastgelelik (RandomNumberGenerator) kullanır.
/// Karışıklık için büyük/küçük harf + rakam + birkaç sembol; karıştırılabilir
/// karakterler (O/0, l/1/I) dışarıda bırakılır.
/// </summary>
public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?-_";
    private const string All = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = 14)
    {
        if (length < 8) length = 8;

        var chars = new char[length];

        // Her gruptan en az bir tane olsun.
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < length; i++)
        {
            chars[i] = Pick(All);
        }

        // Fisher–Yates karıştırma (grup sırası tahmin edilmesin).
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
