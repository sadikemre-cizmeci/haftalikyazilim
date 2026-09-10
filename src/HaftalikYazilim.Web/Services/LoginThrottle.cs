using HaftalikYazilim.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Services;

/// <summary>
/// Hesap kilidi: aynı kullanıcı adına/e-postasına ÜST ÜSTE yanlış şifre girilince
/// o kimliği artan sürelerle bekletir (kaba kuvvet denemelerini yavaşlatır).
///
/// Durum bellek içinde tutulur (<see cref="IMemoryCache"/>) — ayrı tablo/kolon YOK.
/// • Uygulama yeniden başlarsa sayaçlar sıfırlanır (kabul edilebilir; IP hız sınırı ayrıca var).
/// • Anahtar SADECE kimliktir (IP değil): "3 yanlış = o hesap 1 dk bekler".
///   Not: bu, birinin bir kullanıcı adını bilerek o hesabı kasıtlı kilitlemesine
///   izin verir (hedefli DoS). Küçük bir toplulukta kabul edilebilir; kullanıcı
///   "şifremi unuttum" ile ya da bekleyerek devam edebilir.
/// </summary>
public class LoginThrottle
{
    private readonly IMemoryCache _cache;
    private readonly LoginLockoutOptions _options;
    private readonly TimeProvider _clock;

    public LoginThrottle(
        IMemoryCache cache,
        IOptions<LoginLockoutOptions> options,
        TimeProvider clock)
    {
        _cache = cache;
        _options = options.Value;
        _clock = clock;
    }

    private sealed class Attempt
    {
        public int Fails { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    private static string Key(string identifier) => $"login-lock:{identifier}";

    /// <summary>
    /// Bu kimlik şu an kilitliyse, kalan süreyi döner; kilitli değilse <c>null</c>.
    /// Giriş denemesinden ÖNCE çağır.
    /// </summary>
    public TimeSpan? GetLockRemaining(string identifier)
    {
        if (_cache.TryGetValue(Key(identifier), out Attempt? a)
            && a?.LockedUntil is { } until)
        {
            var remaining = until - _clock.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }
        }
        return null;
    }

    /// <summary>
    /// Yanlış şifre girildiğinde çağır. Sayaç artar; eşiğe ulaşınca kilit koyar.
    /// Kilit uygulandıysa kalan süreyi, uygulanmadıysa <c>null</c> döner.
    /// </summary>
    public TimeSpan? RegisterFailure(string identifier)
    {
        var window = TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes));
        var a = _cache.GetOrCreate(Key(identifier), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Attempt();
        })!;

        a.Fails++;

        TimeSpan? applied = null;
        if (a.Fails >= Math.Max(1, _options.MaxAttempts))
        {
            var minutes = LockMinutesFor(a.Fails);
            a.LockedUntil = _clock.GetUtcNow().AddMinutes(minutes);
            applied = TimeSpan.FromMinutes(minutes);
        }

        // Kilit süresi pencereden uzunsa girişi o kadar yaşat (kilit erken düşmesin).
        var ttl = a.LockedUntil is { } until
            ? Max(window, until - _clock.GetUtcNow() + TimeSpan.FromMinutes(1))
            : window;
        _cache.Set(Key(identifier), a, ttl);

        return applied;
    }

    /// <summary>Başarılı giriş — sayaç ve kilit sıfırlanır.</summary>
    public void RegisterSuccess(string identifier) => _cache.Remove(Key(identifier));

    /// <summary>
    /// Kaçıncı yanlışta kaç dakika beklenecek?
    /// LockMinutes = [1,2,5,10,15] ve MaxAttempts = 3 için:
    ///   3. yanlış → LockMinutes[0]=1, 4. → [1]=2, 5. → [2]=5, 6. → [3]=10, 7+ → [4]=15 (son değer tekrar).
    /// </summary>
    private int LockMinutesFor(int fails)
    {
        var ladder = _options.LockMinutes is { Length: > 0 } l ? l : [1];
        var index = Math.Max(0, fails - Math.Max(1, _options.MaxAttempts));
        return ladder[Math.Min(index, ladder.Length - 1)];
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
