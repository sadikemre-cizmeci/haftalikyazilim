namespace HaftalikYazilim.Web.Options;

// ============================================================================
// appsettings.json bölümlerini C# nesnelerine bağlamak için "Options" sınıfları.
// Bağlama: Program.cs → builder.Services.Configure<...>(...)
// ============================================================================

/// <summary>Yüklenen dosyaların (PDF + profil fotoğrafı) yeri ve kuralları. Bölüm: "Storage".</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>PDF'lerin diskte tutulacağı klasör (uygulama köküne göre).</summary>
    public string Root { get; set; } = "App_Data/uploads";

    /// <summary>Profil fotoğraflarının tutulacağı klasör.</summary>
    public string AvatarRoot { get; set; } = "App_Data/avatars";

    /// <summary>İzin verilen en büyük PDF (byte). Varsayılan 50 MB — 30+ sayfalık PDF sığar.</summary>
    public long MaxUploadBytes { get; set; } = 52_428_800;

    /// <summary>
    /// İzin verilen en büyük profil fotoğrafı (byte). Tarayıcı zaten 512px'e küçültüp
    /// gönderir (~50–150 KB); bu sınır sadece doğrudan API çağrılarına karşı emniyet.
    /// </summary>
    public long MaxAvatarBytes { get; set; } = 3_145_728;
}

/// <summary>
/// İlk açılışta oluşturulacak admin. Bölüm: "AdminSeed".
/// GERÇEK ORTAMDA şifreyi user-secrets / ortam değişkeni ile ver: AdminSeed__Password=...
/// </summary>
public class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string Email { get; set; } = "admin@haftalikyazilim.local";
    public string Username { get; set; } = "admin";
    public string DisplayName { get; set; } = "Site Yöneticisi";
    public string Password { get; set; } = "DegistirBeniAdmin!2026";
}

/// <summary>Anasayfadaki iletişim bilgisi + site kimliği. Bölüm: "Site".</summary>
public class SiteOptions
{
    public const string SectionName = "Site";

    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    /// <summary>
    /// Sitenin herkese açık tam adresi, ör. "https://haftalikyazilim.com" (sonda / YOK).
    /// Link önizlemelerinde (og:url / og:image) MUTLAK adres gerektiği için kullanılır.
    /// Boş bırakılırsa gelen isteğin şeması+host'undan türetilir (yerel geliştirmede yeterli).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>Link önizleme kartı (og:image) üretimi. Bölüm: "Og".</summary>
public class OgOptions
{
    public const string SectionName = "Og";

    /// <summary>
    /// Kart metinlerinde kullanılacak .ttf font yolu (uygulama köküne göre veya mutlak).
    /// Boşsa sistem varsayılan fontu denenir. Linux sunucuda sistem fontu yoksa
    /// buraya bir .ttf koy (ör. DejaVuSans / Inter) — Türkçe karakter içeren bir font seç.
    /// </summary>
    public string FontPath { get; set; } = "App_Data/fonts/card.ttf";

    /// <summary>Üretilen kart PNG'lerinin önbelleğe yazılacağı klasör (uygulama köküne göre).</summary>
    public string CacheDir { get; set; } = "App_Data/og-cache";
}

/// <summary>"Şifremi unuttum" davranışı. Bölüm: "PasswordReset".</summary>
public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Bir kullanıcı bu kadar gün içinde en fazla 1 kez yeni şifre isteyebilir.</summary>
    public int CooldownDays { get; set; } = 7;

    /// <summary>Üretilen yeni şifrenin uzunluğu (karakter).</summary>
    public int NewPasswordLength { get; set; } = 14;
}

/// <summary>
/// İstek hızı / eşzamanlılık sınırları. Bölüm: "RateLimit".
/// ASP.NET Core'un yerleşik rate limiter'ı ile Program.cs'te uygulanır.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Aynı IP'den AYNI ANDA işlenecek en fazla istek sayısı (eşzamanlılık).</summary>
    public int PerIpConcurrent { get; set; } = 25;

    /// <summary>Eşzamanlılık dolunca kuyrukta bekleyebilecek en fazla istek. Aşılırsa 429.</summary>
    public int PerIpQueue { get; set; } = 25;

    /// <summary>Aynı IP'den dakikada en fazla kaç giriş/kayıt/şifre-sıfırlama denemesi.</summary>
    public int LoginPerMinute { get; set; } = 10;
}

/// <summary>
/// Hesap kilidi: aynı kullanıcı adına üst üste yanlış şifre girilince o hesabı
/// bir süre bekletir. Bölüm: "LoginLockout".
///
/// Sayaç bellek içinde (IMemoryCache) tutulur — ayrı bir tablo/kolon yok. Uygulama
/// yeniden başlarsa sayaçlar sıfırlanır (kabul edilebilir; IP hız sınırı yine devrede).
/// </summary>
public class LoginLockoutOptions
{
    public const string SectionName = "LoginLockout";

    /// <summary>Kilit devreye girmeden önce izin verilen üst üste yanlış deneme.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Yanlış deneme sayacının "unutulma" süresi (dakika). Bu süre boyunca doğru giriş olmazsa sayaç sıfırlanır.</summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// Artan bekleme süreleri (dakika). MaxAttempts'e ulaşınca ilk değer,
    /// sonraki her yanlışta bir sonraki değer uygulanır; liste bitince son değer tekrarlanır.
    /// Örn. [1,2,5,10,15] → 3. yanlış: 1 dk, 4.: 2 dk, 5.: 5 dk, 6.: 10 dk, 7+: 15 dk.
    /// </summary>
    public int[] LockMinutes { get; set; } = [1, 2, 5, 10, 15];
}
