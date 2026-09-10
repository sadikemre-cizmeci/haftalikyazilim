using System.Security.Claims;
using HaftalikYazilim.Web.Contracts;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Options;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HaftalikYazilim.Web.Controllers;

/// <summary>
/// Giriş / çıkış / "ben kimim" / şifremi unuttum.
/// Kimlik doğrulama ÇEREZ tabanlıdır (HttpOnly cookie).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly LoginThrottle _loginThrottle;
    private readonly PasswordResetOptions _resetOptions;
    private readonly SiteOptions _site;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        ICurrentUser currentUser,
        LoginThrottle loginThrottle,
        IOptions<PasswordResetOptions> resetOptions,
        IOptions<SiteOptions> site,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _loginThrottle = loginThrottle;
        _resetOptions = resetOptions.Value;
        _site = site.Value;
        _logger = logger;
    }

    /// <summary>
    /// Kullanıcı adı VEYA e-posta + şifre ile giriş. Başarılıysa oturum çerezi yazılır.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login([FromBody] LoginRequest request)
    {
        var id = request.Identifier.Trim().ToLowerInvariant();

        // 1) Bu kimlik üst üste yanlış şifre yüzünden kilitli mi? → daha DB'ye bile gitme.
        if (_loginThrottle.GetLockRemaining(id) is { } locked)
        {
            return LockedResponse(locked);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == id || u.Username == id);

        // "Kullanıcı yok" ile "şifre yanlış" ayırt ETTİRİLMEZ. Yanlış deneme SAYILIR
        // (kimliği bilinen ama var olmayan bir hesabın denenmesi de kaba kuvvettir).
        if (user is null)
        {
            return FailedLogin(id);
        }

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return FailedLogin(id);
        }

        if (!user.IsActive)
        {
            return Problem(
                title: "Giriş başarısız", detail: "Bu hesap devre dışı.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        // Başarılı giriş → yanlış deneme sayacı sıfırlanır.
        _loginThrottle.RegisterSuccess(id);

        await SignInAsync(user);
        _logger.LogInformation("Giriş yapıldı: {Email}", user.Email);
        return Ok(ToResponse(user));
    }

    /// <summary>Yanlış giriş: sayacı artır, eşiğe ulaşıldıysa kilit mesajı, yoksa genel 401.</summary>
    private ActionResult<CurrentUserResponse> FailedLogin(string identifier)
    {
        var lockApplied = _loginThrottle.RegisterFailure(identifier);
        if (lockApplied is { } duration)
        {
            _logger.LogWarning("Hesap kilitlendi (üst üste yanlış şifre): {Id} → {Minutes} dk", identifier, duration.TotalMinutes);
            return LockedResponse(duration);
        }

        return Problem(
            title: "Giriş başarısız", detail: "Kullanıcı adı/e-posta veya şifre hatalı.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>429 + kalan süre (hem gövdede hem "Retry-After" başlığında).</summary>
    private ActionResult<CurrentUserResponse> LockedResponse(TimeSpan remaining)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString();

        var human = seconds >= 60
            ? $"{Math.Ceiling(seconds / 60.0):0} dakika"
            : $"{seconds} saniye";

        return Problem(
            title: "Çok fazla hatalı deneme",
            detail: $"Güvenlik için bu hesap geçici olarak kilitlendi. Yaklaşık {human} sonra tekrar dene " +
                    "ya da \"Şifremi unuttum\" ile yeni şifre al.",
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    /// <summary>
    /// Üye kaydı. Herkes hesap açıp yazar (Author) olabilir. Başarılıysa doğrudan giriş yapılır.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserResponse>> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var username = UsernameHelper.Normalize(request.Username);

        if (!UsernameHelper.IsValid(username))
        {
            return Problem(title: "Geçersiz istek",
                detail: "Kullanıcı adı 3–30 karakter olmalı ve yalnızca harf/rakam/nokta/alt çizgi içerebilir.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (UsernameHelper.IsReserved(username))
        {
            return Problem(title: "Kayıt yapılamadı",
                detail: "Bu kullanıcı adı kullanılamaz. Lütfen başka bir ad seç.",
                statusCode: StatusCodes.Status409Conflict);
        }
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Problem(title: "Kayıt yapılamadı", detail: "Bu e-posta zaten kullanılıyor.",
                statusCode: StatusCodes.Status409Conflict);
        }
        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            return Problem(title: "Kayıt yapılamadı", detail: "Bu kullanıcı adı zaten alınmış.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Email = email,
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            Role = UserRole.Author,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInAsync(user);
        _logger.LogInformation("Yeni üye kaydı: {Username} ({Email})", user.Username, user.Email);
        return Ok(ToResponse(user));
    }

    /// <summary>Oturumu kapatır (çerezi siler).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Şu an giriş yapan kullanıcı. Giriş yoksa 204.</summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not int id)
        {
            return NoContent();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null || !user.IsActive)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        return Ok(ToResponse(user));
    }

    /// <summary>
    /// "Şifremi unuttum". E-POSTA YOKTUR.
    ///  - Kullanıcı varsa: yeni rastgele şifre üretilir, hesaba yazılır ve
    ///    doğrudan bu yanıtta DÖNER (ekranda gösterilir).
    ///  - Kullanıcı yoksa: 404 (arayüz "hesap yok, yönetici açar" der).
    ///  - Aynı kullanıcı <c>PasswordReset:CooldownDays</c> gün içinde 1 kez isteyebilir.
    ///
    /// NOT (güvenlik): Yeni şifre ekranda gösterildiği için, bir yazarın e-postasını
    /// bilen biri onun şifresini sıfırlayıp erişebilir. Tek yazarlı bir sitede kabul
    /// edilebilir; yazar sayısı artınca e-posta tabanlı sıfırlamaya geçmek yerinde olur.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var id = request.Identifier.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == id || u.Username == id);

        if (user is null)
        {
            var contact = string.IsNullOrWhiteSpace(_site.ContactEmail)
                ? "site yöneticisiyle iletişime geç."
                : $"site yöneticisiyle iletişime geç: {_site.ContactEmail}";
            return Problem(
                title: "Hesap bulunamadı",
                detail: $"Bu kullanıcı adı/e-posta ile kayıtlı bir hesap yok. Yazar hesabı için {contact}",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Bekleme süresi kontrolü.
        var cooldown = TimeSpan.FromDays(_resetOptions.CooldownDays);
        if (user.LastPasswordResetAtUtc is DateTime last && DateTime.UtcNow - last < cooldown)
        {
            var nextAllowed = last + cooldown;
            var daysLeft = Math.Max(1, (int)Math.Ceiling((nextAllowed - DateTime.UtcNow).TotalDays));
            return Problem(
                title: "Çok sık istek",
                detail: $"Son {_resetOptions.CooldownDays} gün içinde zaten yeni şifre aldın. " +
                        $"Yaklaşık {daysLeft} gün sonra tekrar deneyebilirsin.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var newPassword = PasswordGenerator.Generate(_resetOptions.NewPasswordLength);
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.LastPasswordResetAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Şifre sıfırlandı (yeni şifre üretildi): {Email}", user.Email);

        return Ok(new ForgotPasswordResponse
        {
            NewPassword = newPassword,
            Message = "Yeni şifren aşağıda. Bir yere kaydet, giriş yap ve profilinden değiştir.",
        });
    }

    // ------------------------------------------------------------------

    private async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
    }

    private static CurrentUserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString(),
        IsAdmin = user.Role == UserRole.Admin,
        AvatarUrl = AvatarUrl.For(user),
    };
}
