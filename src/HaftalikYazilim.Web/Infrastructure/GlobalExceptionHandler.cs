using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HaftalikYazilim.Web.Infrastructure;

/// <summary>
/// Tüm yakalanmamış istisnaları tek yerden yönetir ve istemciye standart
/// "ProblemDetails" (RFC 7807) JSON'u döner. Program.cs'te şöyle bağlanır:
///
///     builder.Services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;();
///     builder.Services.AddProblemDetails();
///     app.UseExceptionHandler();
///
/// - <see cref="AppValidationException"/>  → 400
/// - <see cref="AppForbiddenException"/>   → 403
/// - <see cref="AppNotFoundException"/>    → 404
/// - diğer her şey                         → 500 (detay loglanır, istemciye sızmaz)
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            AppValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            AppForbiddenException  => (StatusCodes.Status403Forbidden,  "Yetki yok"),
            AppNotFoundException   => (StatusCodes.Status404NotFound,   "Bulunamadı"),
            _                      => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu"),
        };

        // 500'leri tam detayıyla logla; bilinen (beklenen) hataları sadece kısa logla.
        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "İşlenmeyen hata: {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation("İş kuralı hatası ({Status}): {Message}", status, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // 500'de içeriği gizle; bilinen hatalarda mesajı göster.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            }
        });
    }
}
