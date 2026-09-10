namespace HaftalikYazilim.Web.Infrastructure;

// ============================================================================
// Servis katmanı, iş kuralı ihlallerini bu istisnaları FIRLATARAK bildirir.
// Controller'lar try/catch yazmaz; Program.cs'teki GlobalExceptionHandler bunları
// uygun HTTP durum koduna (400 / 403 / 404) çevirir.
//
// Böylece controller kodu sade kalır:
//     var article = await _service.UpdateAsync(id, request, ct);   // fırlatırsa handler yakalar
// ============================================================================

/// <summary>Girdi / iş kuralı hatası → HTTP 400.</summary>
public class AppValidationException : Exception
{
    public AppValidationException(string message) : base(message) { }
}

/// <summary>Yetki yok (giriş var ama bu işlemi yapamaz) → HTTP 403.</summary>
public class AppForbiddenException : Exception
{
    public AppForbiddenException(string message = "Bu işlem için yetkin yok.") : base(message) { }
}

/// <summary>Kayıt bulunamadı → HTTP 404.</summary>
public class AppNotFoundException : Exception
{
    public AppNotFoundException(string message = "Kayıt bulunamadı.") : base(message) { }
}
