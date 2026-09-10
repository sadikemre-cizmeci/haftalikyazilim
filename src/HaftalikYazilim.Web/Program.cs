using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HaftalikYazilim.Web.Data;
using HaftalikYazilim.Web.Domain;
using HaftalikYazilim.Web.Infrastructure;
using HaftalikYazilim.Web.Options;
using HaftalikYazilim.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// ============================================================================
//  HaftalıkYazılım — uygulama başlangıcı (composition root)
// ----------------------------------------------------------------------------
//  Bu dosya "her şeyin birbirine bağlandığı" yerdir. Bilerek TEK dosyada ve
//  bölüm bölüm tuttum ki neyin neden açık olduğunu bir bakışta görebilesin.
//  Sıra:
//    1) Yapılandırma (appsettings.json → Options nesneleri)
//    2) Veritabanı (EF Core / PostgreSQL)
//    3) Kimlik doğrulama (çerez) + yetkilendirme
//    4) Uygulama servisleri (DI)
//    5) Controller'lar + JSON ayarları
//    6) Hata yönetimi (ProblemDetails)
//    7) OpenAPI (yalnızca geliştirmede)
//    8) Rate limit (giriş denemeleri)
//    --- uygulama kuruluyor ---
//    9) HTTP pipeline (middleware sırası ÖNEMLİDİR)
//   10) (İsteğe bağlı) veritabanını hazırla + admin tohumla
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1) YAPILANDIRMA — appsettings.json bölümlerini tip güvenli nesnelere bağla
// ---------------------------------------------------------------------------
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<AdminSeedOptions>(
    builder.Configuration.GetSection(AdminSeedOptions.SectionName));
builder.Services.Configure<SiteOptions>(
    builder.Configuration.GetSection(SiteOptions.SectionName));
builder.Services.Configure<PasswordResetOptions>(
    builder.Configuration.GetSection(PasswordResetOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<LoginLockoutOptions>(
    builder.Configuration.GetSection(LoginLockoutOptions.SectionName));
builder.Services.Configure<OgOptions>(
    builder.Configuration.GetSection(OgOptions.SectionName));

// ---------------------------------------------------------------------------
// 2) VERİTABANI — EF Core + PostgreSQL (Npgsql)
// ---------------------------------------------------------------------------
// Bağlantı dizesi: appsettings.json → ConnectionStrings:Default
//   Örn: "Host=localhost;Port=5432;Database=haftalikyazilim;Username=...;Password=..."
// Gerçek ortamda şifreyi appsettings'e yazma; ortam değişkeni ver:
//   ConnectionStrings__Default="Host=...;Database=...;Username=...;Password=..."
//
// Migration almak/uygulamak:
//   dotnet ef migrations add <Ad> --project src/HaftalikYazilim.Web
//   dotnet ef database update      --project src/HaftalikYazilim.Web
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Default tanımlı değil. appsettings.json veya ortam değişkeni ile ver.");

    options.UseNpgsql(connectionString);

    // Geliştirmede SQL loglarını ve ayrıntılı hataları görmek işini kolaylaştırır.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// ---------------------------------------------------------------------------
// 3) KİMLİK DOĞRULAMA (çerez) + YETKİLENDİRME
// ---------------------------------------------------------------------------
// Neden çerez? Frontend saf HTML/JS. Çerez tabanlı oturumda tarayıcı çerezi
// otomatik gönderir; JS tarafında token saklama/yenileme derdi olmaz.

// DataProtection anahtarları: oturum çerezi bununla şifrelenir. KALICI bir klasöre
// yazılmazsa uygulama her yeniden başladığında (ve her deploy'da) HERKES çıkış yapar.
// Klasör: appsettings → "DataProtection:KeyDir" (varsayılan App_Data/keys).
var keyDir = builder.Configuration["DataProtection:KeyDir"] ?? "App_Data/keys";
if (!Path.IsPathRooted(keyDir))
{
    keyDir = Path.Combine(builder.Environment.ContentRootPath, keyDir);
}
Directory.CreateDirectory(keyDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDir))
    .SetApplicationName("HaftalikYazilim");

// Ters vekil (nginx / Apache) arkasında çalışırken gerçek şema (https) ve istemci
// IP'si X-Forwarded-* başlıklarından okunur. Bu olmadan UseHttpsRedirection sonsuz
// yönlendirmeye girer ve rate limiter herkesi tek IP (vekil) sanır.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Varsayılan olarak loopback (127.0.0.1/8, ::1) zaten güvenilir vekil sayılır;
    // nginx aynı makinede olduğu için ek ayara gerek yok. Farklı bir vekil IP'si
    // (ör. ayrı yük dengeleyici) varsa: options.KnownProxies.Add(IPAddress.Parse("..."));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "hy_auth";
        options.Cookie.HttpOnly = true;                       // JS çereze erişemez (XSS koruması)
        options.Cookie.SameSite = SameSiteMode.Lax;           // temel CSRF koruması
        // Canlıda (Production) çerez YALNIZCA HTTPS üzerinden gitsin; geliştirmede http yeterli.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;                     // kullanıldıkça süre uzar

        // Burası API — yetkisiz istekte HTML login sayfasına YÖNLENDİRME yapma;
        // düz 401 / 403 dön ki frontend durumu anlayıp kendi yönlendirmesini yapsın.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// "İsteği yapan kim?" bilgisine servislerden ulaşabilmek için gerekli.
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// 4) UYGULAMA SERVİSLERİ (Dependency Injection)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ISocialService, SocialService>();

// Dosyalar diskte tutulur. Başka depoya (S3 vb.) geçersen SADECE bu satırları değiştir.
builder.Services.AddSingleton<IPdfStorage, LocalPdfStorage>();
builder.Services.AddSingleton<IImageStorage, LocalImageStorage>();

// Şifre hash'leme (Identity'nin sadece bu parçasını kullanıyoruz). Durumsuz → singleton.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Hesap kilidi (üst üste yanlış şifre) — sayaç bellek içinde tutulur.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LoginThrottle>();

// Link önizleme kartı üreteci (og:image). SkiaSharp; durumsuz → singleton.
builder.Services.AddSingleton<OgCardRenderer>();

// ---------------------------------------------------------------------------
// 5) CONTROLLER'LAR + JSON
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(json =>
    {
        // enum'ları sayı yerine METİN olarak gönder/al ("Published", "Admin" ...).
        json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ---------------------------------------------------------------------------
// 6) HATA YÖNETİMİ — tüm hatalar tek biçimde (RFC 7807 ProblemDetails) döner
// ---------------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------------------------------------------------------------------------
// 7) OpenAPI — API'yi elle denemek için makine-okunur belge (yalnızca geliştirme)
//    Çalışınca:  GET http://localhost:5099/openapi/v1.json
//    Görsel arayüz istersen README'de "Scalar / Swagger UI" notuna bak.
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// 8) RATE LIMIT / EŞZAMANLILIK — sunucuyu flood'a ve kaba kuvvete karşı koru
//    (ASP.NET Core'un yerleşik özelliği; ek paket yok.) Ayarlar: "RateLimit" bölümü.
//    NOT: Statik dosyalar (wwwroot) bu middleware'den ÖNCE sunulur → CSS/JS/görsel
//         sınırlanmaz; yalnızca /api/* ve SPA fallback bu limitlere tabidir.
// ---------------------------------------------------------------------------
var rateLimit = builder.Configuration
    .GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // GENEL: aynı IP'den AYNI ANDA işlenecek istek sayısını sınırla (eşzamanlılık).
    // Normal bir sayfa açılışı birkaç paralel istek atar; bu değerler ona rahat yeter,
    // tek bir istemcinin yüzlerce paralel bağlantı açmasını ise engeller.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetConcurrencyLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen",
            factory: _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = Math.Max(1, rateLimit.PerIpConcurrent),
                QueueLimit = Math.Max(0, rateLimit.PerIpQueue),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            }));

    // "login" politikası: aynı IP'den dakikada en fazla N giriş/kayıt/şifre-sıfırlama denemesi.
    // (Hesap bazlı kilit ayrıca LoginThrottle ile AuthController içinde uygulanır.)
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = Math.Max(1, rateLimit.LoginPerMinute),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// ===========================================================================
// 9) HTTP PIPELINE — middleware SIRASI önemlidir, yukarıdan aşağıya çalışır
// ===========================================================================

// 9.0) Ters vekil başlıklarını (X-Forwarded-Proto / -For) EN BAŞTA uygula ki
//      altındaki her middleware isteği "https + gerçek istemci IP'si" olarak görsün.
app.UseForwardedHeaders();

// 9.1) Hata yakalama en dışta olmalı ki alttaki her şeyi sarsın.
app.UseExceptionHandler();

// 9.2) Canlı ortamda HTTPS'e zorla + HSTS. Geliştirmede http ile uğraşmayalım.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// 9.2b) Link önizleme: "/", "/index.html" ve "/oku.html?id=N" isteklerini yakalayıp
//       <head>'e Open Graph / Twitter Card etiketlerini ekler (sosyal medya kartı).
//       Statik dosya sunumundan ÖNCE olmalı.
app.UseMiddleware<LinkPreviewMiddleware>();

// 9.3) Statik site (frontend). wwwroot/index.html "/" adresinde açılır.
//      Frontend'i tümüyle değiştirmek istersen sadece wwwroot içeriğini değiştir.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // HTML/JS/CSS için sürüm damgası (?v=) YOK. Bu yüzden her yeni dağıtımda
        // tarayıcı eski dosyayı göstermesin diye "her kullanımdan önce doğrula"
        // diyoruz: ETag/Last-Modified sayesinde değişmediyse ucuz bir 304 döner,
        // değiştiyse yeni dosya gelir. Görsel/font/pdf.js gibi nadir değişen
        // varlıklar bir hafta önbelleklenir.
        var path = ctx.File.Name;
        var revalidate = path.EndsWith(".html") || path.EndsWith(".js")
            || path.EndsWith(".css") || path.EndsWith(".mjs") || path.EndsWith(".json")
            || path.EndsWith(".svg");
        ctx.Context.Response.Headers.CacheControl = revalidate
            ? "no-cache"
            : "public, max-age=604800";

        if (app.Environment.IsDevelopment())
        {
            // Geliştirmede kesin taze: hiç önbellekleme.
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    },
});

// 9.4) Yönlendirme + rate limit + kimlik + yetki, sonra controller'lar.
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// OpenAPI belgesini yalnızca geliştirmede yayınla.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ===========================================================================
// 10) (İSTEĞE BAĞLI) VERİTABANINI HAZIRLA + İLK ADMİNİ OLUŞTUR
// ---------------------------------------------------------------------------
//  appsettings.json → "Database:SeedOnStartup" = true olduğunda:
//    - bekleyen migration'ları uygular (Database.Migrate)
//    - hiç kullanıcı yoksa bir admin hesabı oluşturur (DbSeeder)
//
//  Veritabanı henüz KURULMADIĞI için bu bayrak şu an false. Migration
//  aldıktan sonra true yapabilir ya da bu bloğu silip yönetimi elle
//  ("dotnet ef database update") üstlenebilirsin.
// ===========================================================================
if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<AppDbContext>();
    var adminOptions = services.GetRequiredService<IOptions<AdminSeedOptions>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, adminOptions, logger);
}

app.Run();
