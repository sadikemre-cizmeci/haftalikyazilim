using System.ComponentModel.DataAnnotations;

namespace HaftalikYazilim.Web.Contracts;

/// <summary>Giriş formundan gelen veri. <see cref="Identifier"/> e-posta VEYA kullanıcı adı olabilir.</summary>
public record LoginRequest
{
    [Required(ErrorMessage = "Kullanıcı adı veya e-posta zorunlu.")]
    public string Identifier { get; init; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunlu.")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Üye kaydı formundan gelen veri. Herkes hesap açıp yazar olabilir.
/// </summary>
public record RegisterRequest
{
    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kullanıcı adı zorunlu.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3–30 karakter olmalı.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Görünen ad zorunlu.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Görünen ad 2–120 karakter olmalı.")]
    public string DisplayName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunlu.")]
    [StringLength(200, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>"Şu an kim giriş yapmış?" cevabı (GET /api/auth/me). Giriş yoksa uç 204 döner.</summary>
public record CurrentUserResponse
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;   // "Admin" | "Author"
    public bool IsAdmin { get; init; }
    public string? AvatarUrl { get; init; }
}

/// <summary>"Şifremi unuttum" formundan gelen veri. Kullanıcı adı VEYA e-posta.</summary>
public record ForgotPasswordRequest
{
    [Required(ErrorMessage = "Kullanıcı adı veya e-posta zorunlu.")]
    public string Identifier { get; init; } = string.Empty;
}

/// <summary>
/// "Şifremi unuttum" cevabı. E-POSTA YOKTUR: yeni şifre doğrudan bu yanıtta
/// döner ve ekranda gösterilir.
/// </summary>
public record ForgotPasswordResponse
{
    public string NewPassword { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
