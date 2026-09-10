namespace HaftalikYazilim.Web.Domain;

/// <summary>
/// Yazı yazabilen kişi (Yazar veya Admin).
/// Okuyucular burada TUTULMAZ — onların hesabı yok.
///
/// GÜVENLİK: Şifreler asla düz metin tutulmaz. <see cref="PasswordHash"/> alanında,
/// ASP.NET Core'un <c>PasswordHasher</c> sınıfıyla üretilmiş hash saklanır
/// (bkz. <c>AuthController</c> ve <c>DbSeeder</c>).
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Giriş için kullanılır. Sistemde benzersizdir (unique index).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Kullanıcı adı. Giriş bununla DA yapılabilir (e-posta veya kullanıcı adı).
    /// Küçük harf, benzersiz. Örn: "sadikemre". Kullanıcı profilinden değiştirebilir.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Yazılarda ve profilde gösterilen ad. Örn: "Sadık Emre".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hash'lenmiş şifre. Düz şifre burada TUTULMAZ.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Yetki seviyesi. Bkz. <see cref="UserRole"/>.</summary>
    public UserRole Role { get; set; } = UserRole.Author;

    /// <summary>
    /// Hesabı geçici olarak kapatmak için. <c>false</c> ise kullanıcı giriş yapamaz
    /// ama yazıları veritabanında durmaya devam eder.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // ---------------------------------------------------------------------
    // PROFİL BİLGİLERİ  (kullanıcı kendi profilini düzenleyebilir)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Kısa tanıtım — "özet bilgi". ⓘ baloncuğunda ve profilde başlık altında görünür.
    /// Karakter sınırı YOK (veritabanında 'text').
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Uzun tanıtım — profil sayfasında tam metin. Karakter sınırı YOK ('text').
    /// </summary>
    public string? About { get; set; }

    /// <summary>
    /// Yüklenen profil fotoğrafının diskteki dosya adı (bkz. LocalImageStorage).
    /// null ise fotoğraf yok → arayüz baş harflerden bir daire üretir.
    /// </summary>
    public string? AvatarFileName { get; set; }

    // Fotoğrafın dairede nasıl gösterileceği (kullanıcı ayarlar; CSS ile uygulanır).
    /// <summary>Yakınlaştırma katsayısı. 1.0 = sığdır, 3.0 = 3 kat.</summary>
    public double AvatarZoom { get; set; } = 1.0;
    /// <summary>Yatay odak noktası (%). 50 = orta.</summary>
    public double AvatarPosX { get; set; } = 50;
    /// <summary>Dikey odak noktası (%). 50 = orta.</summary>
    public double AvatarPosY { get; set; } = 50;

    // ---------------------------------------------------------------------
    // ŞİFRE SIFIRLAMA
    // ---------------------------------------------------------------------

    /// <summary>
    /// "Şifremi unuttum" ile en son ne zaman yeni şifre üretildi.
    /// Kural: bir kullanıcı 7 günde en fazla 1 kez isteyebilir (bkz. AuthController).
    /// </summary>
    public DateTime? LastPasswordResetAtUtc { get; set; }

    /// <summary>
    /// Bu kullanıcıya ait yazılar. EF Core "navigation property" — veritabanında
    /// ayrı bir sütun değildir, ilişkiyi temsil eder.
    /// </summary>
    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
