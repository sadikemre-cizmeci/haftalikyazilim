using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Data;

/// <summary>
/// Uygulama ilk kez ayağa kalktığında, veritabanında HİÇ kullanıcı yoksa
/// bir "Admin" hesabı oluşturur. Böylece siteye ilk kez girip yazı ekleyebilirsin.
///
/// Admin bilgileri appsettings.json → "AdminSeed" bölümünden okunur.
///
/// Bu kod yalnızca <c>appsettings.json → Database:SeedOnStartup = true</c> iken çalışır
/// (bkz. Program.cs). Veritabanı henüz kurulmadığı için varsayılan olarak KAPALIDIR.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IOptions<AdminSeedOptions> adminOptions,
        ILogger logger,
        CancellationToken ct = default)
    {
        // Zaten kullanıcı varsa hiçbir şey yapma (bu kod her açılışta güvenle çalışabilir).
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var opts = adminOptions.Value;

        var admin = new User
        {
            Email = opts.Email.Trim().ToLowerInvariant(),
            Username = string.IsNullOrWhiteSpace(opts.Username) ? "admin" : opts.Username.Trim().ToLowerInvariant(),
            DisplayName = opts.DisplayName.Trim(),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        // Şifreyi hash'le. PasswordHasher, ASP.NET Core Identity'nin çekirdek
        // parçasıdır; biz tüm Identity sistemini kurmadan sadece bunu kullanıyoruz.
        var hasher = new PasswordHasher<User>();
        admin.PasswordHash = hasher.HashPassword(admin, opts.Password);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("İlk admin hesabı oluşturuldu: {Email}", admin.Email);
    }
}
