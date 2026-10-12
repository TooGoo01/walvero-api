using Hangfire;
using Loyalty.Business.Exceptions;
using Loyalty.Business.Hubs;
using Loyalty.Business.Services;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.Infrastructure;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Loyalty.Infrastructure.Utilities.Security.Jwt;
using Loyalty.WebAPI.Tenancy;
using LoyaltyWalletApp.WEBUI.Tenancy;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.Runtime.InteropServices;

// Tesseract NuGet (5.2.0) literal lib adı axtarır (`libleptonica-1.82.0.so`, `libtesseract50.so`).
// Linux App Service apt-get tesseract-ocr fərqli SONAME quraşdırır (`liblept.so.5`, `libtesseract.so.5`).
// Boot zamanı symlink yaradırıq — startup command-dan asılı olmayaq.
// Linux deyilsə (local Windows dev) bu blok keçilir.
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    EnsureTesseractSymlinks();
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot"
});

static void EnsureTesseractSymlinks()
{
    // Tesseract NuGet (5.2.0) `InteropDotNet.LibraryLoader` sistem path-larını (/usr/lib və LD_LIBRARY_PATH)
    // İGNORE edir. Yalnız Tesseract.dll yanındakı `x64/` (və ya `x86/`) qovluğunda axtarır.
    // Ona görə apt-installed `.so` fayllarına symlink-ləri appBase/x64/ içində yaradırıq.
    var x64Dir = Path.Combine(AppContext.BaseDirectory, "x64");
    try
    {
        System.IO.Directory.CreateDirectory(x64Dir);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Tesseract] cannot create {x64Dir}: {ex.Message}");
        return;
    }

    var pairs = new (string source, string link)[]
    {
        ("/usr/lib/x86_64-linux-gnu/liblept.so.5", Path.Combine(x64Dir, "libleptonica-1.82.0.so")),
        ("/usr/lib/x86_64-linux-gnu/libtesseract.so.5", Path.Combine(x64Dir, "libtesseract50.so")),
        // Tesseract NuGet `dlopen("libdl")` çağırır — Debian 12-də versiyasız `libdl.so` yox, yalnız `.so.2`
        ("/lib/x86_64-linux-gnu/libdl.so.2", Path.Combine(x64Dir, "libdl.so")),
        ("/lib/x86_64-linux-gnu/libdl.so.2", Path.Combine(AppContext.BaseDirectory, "libdl.so")),
    };

    foreach (var (source, link) in pairs)
    {
        try
        {
            if (System.IO.File.Exists(link)) continue;
            if (!System.IO.File.Exists(source))
            {
                Console.WriteLine($"[Tesseract] source missing: {source} — apt tesseract-ocr quraşdırılmayıb?");
                continue;
            }

            var psi = new System.Diagnostics.ProcessStartInfo("ln", $"-sf {source} {link}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
            Console.WriteLine($"[Tesseract] symlink {(System.IO.File.Exists(link) ? "OK" : "FAIL")}: {link} -> {source}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tesseract] symlink error for {link}: {ex.Message}");
        }
    }
}

// === Controllers & CORS & SignalR ===
// EF navigation cycles (e.g. Tenant.Admins.Tenant…) cause JsonException at depth 32.
// IgnoreCycles writes null when a back-reference would recurse — safer global default.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// === Request body size limit (250MB for video uploads) ===
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 250_000_000; // 250 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 250_000_000; // 250 MB
});

// === Response Caching ===
builder.Services.AddResponseCaching();

// === Response Compression (Brotli + Gzip) ===
// JSON cavabları 3-5x kiçilir (məs. GetTenants ~100KB → ~20KB).
// Mobile network-də çox böyük effekt — bandwidth + latency hər ikisi azalır.
// Brotli daha sıxılmış olur amma yalnız HTTPS-də işləyir; Gzip fallback.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "text/json" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o =>
{
    // Fastest = az CPU, yenə də ~70% sıxılma. Optimal default-dur.
    o.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(o =>
{
    o.Level = CompressionLevel.Fastest;
});

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", policy => policy
        .WithOrigins(
            "http://10.0.2.2:5025",
            "http://localhost:3000",
            "http://localhost:7285",
            "https://www.qrwithmenu.com",
            "https://walvero-next-d0fydybpcxe8d2d6.westeurope-01.azurewebsites.net",
            "https://walvero.com",
            "https://www.walvero.com",
            "https://qrwithmenu.com",
            "https://menu.hollywood.az")
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod()));

// === JWT Authentication ===
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions")
    .Get<Loyalty.Infrastructure.Utilities.Security.Jwt.TokenOptions>();

builder.Services
    .AddAuthentication(auth =>
    {
        auth.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        auth.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(option =>
    {
        option.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // 🔹 SignalR üçün query-string access_token dəstəyi
        option.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/loyalty"))
                {
                    context.Token = accessToken;
                }

                // 🔹 Hangfire dashboard: brauzer Bearer header göndərmir.
                // İlk açılış /hangfire?access_token=<JWT> → HttpOnly cookie yazılır,
                // dashboard-un sonrakı səhifə/AJAX sorğuları cookie-dən oxunur.
                if (path.StartsWithSegments("/hangfire"))
                {
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                        context.Response.Cookies.Append("hf_token", accessToken.ToString(), new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                            Path = "/hangfire",
                            MaxAge = TimeSpan.FromHours(1)
                        });
                    }
                    else if (context.Request.Cookies.TryGetValue("hf_token", out var hfToken) &&
                             !string.IsNullOrEmpty(hfToken))
                    {
                        context.Token = hfToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

// === Authorization Policies ===
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdminOrModerator", policy =>
        policy.RequireRole("Super Admin", "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator"));
    options.AddPolicy("SuperAdminOnly", policy =>
       policy.RequireRole("Super Admin"));

    options.AddPolicy("TenantAdminOnly", policy =>
        policy.RequireRole("Tenant Admin"));

    options.AddPolicy("TenantModeratorOnly", policy =>
        policy.RequireRole("Moderator"));

    // Branch-scoped admin (ops + branch statistics; BranchId required on user).
    options.AddPolicy("BranchAdminOnly", policy =>
        policy.RequireRole("Branch Admin"));

    // Branch-scoped moderator (ops only; BranchId required on user).
    options.AddPolicy("BranchModeratorOnly", policy =>
        policy.RequireRole("Branch Moderator"));

    // Branch Admin or any higher-level admin (used for branch settings / approve flagged tx)
    options.AddPolicy("BranchAdminOrAbove", policy =>
        policy.RequireRole("Branch Admin", "Tenant Admin", "Super Admin"));

    // Tenant Admin və ya Super Admin — transactions admin actions (approve/reject/reverse).
    // Branch Admin xaric.
    options.AddPolicy("TenantAdminOrSuperAdmin", policy =>
        policy.RequireRole("Tenant Admin", "Super Admin"));

    // Menu Admin — yalnız menyu idarəsi (menu-app); loyalty əməliyyatlarına çıxışı yoxdur.
    options.AddPolicy("TenantAdminOrMenuAdmin", policy =>
        policy.RequireRole("Tenant Admin", "Menu Admin"));

    options.AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"));

    options.AddPolicy("CustomerOrTenantAdmin", policy =>
        policy.RequireRole("Customer", "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator"));

    options.AddPolicy("SalesManagerOrAbove", policy =>
        policy.RequireRole("Sales Manager", "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator", "Super Admin"));

    // FullProfileOnly policy - requires non-guest user with completed profile
    options.AddPolicy("FullProfileOnly", policy =>
    {
        policy.RequireRole("Customer");
        policy.RequireAssertion(context =>
        {
            var isGuestClaim = context.User.FindFirst("is_guest")?.Value;
            var isProfileCompletedClaim = context.User.FindFirst("is_profile_completed")?.Value;
            
            if (bool.TryParse(isGuestClaim, out bool isGuest) && isGuest)
                return false;
            
            if (bool.TryParse(isProfileCompletedClaim, out bool isProfileCompleted) && !isProfileCompleted)
                return false;
            
            return true;
        });
    });
});

// === Caching ===
builder.Services.AddMemoryCache();

// === Session ===
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(conf =>
{
    conf.IdleTimeout = TimeSpan.FromMinutes(60);
    conf.Cookie.HttpOnly = false;
    conf.Cookie.IsEssential = true;
    conf.Cookie.SameSite = SameSiteMode.None;
    conf.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// === DbContext ===
builder.Services.AddDbContext<LoyaltyWalletDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LoyaltyDbConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// === Hangfire (async OCR jobs) ===
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("LoyaltyDbConnection"),
        new Hangfire.SqlServer.SqlServerStorageOptions
        {
            SchemaName = "Hangfire",
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(2),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
// Kart dizayneri flag-ları (bax CardDesignFeatureFlags): real Google class PATCH yalnız açıq konfiqlə.
Loyalty.Business.Helpers.CardDesignFeatureFlags.GoogleClassUpdateEnabled =
    builder.Configuration.GetValue("CardDesign:GoogleClassUpdateEnabled", false);

// Hangfire:ServerEnabled=false → bu instansiya job İCRA ETMİR (yalnız enqueue edir).
// Lokal/dev instansiya prod DB-yə qoşulanda prod job-larını (wallet push, OCR) götürməsin deyə.
if (builder.Configuration.GetValue("Hangfire:ServerEnabled", true))
{
    builder.Services.AddHangfireServer(opts =>
    {
        opts.WorkerCount = 2; // OCR CPU-bound — kicik tutmaq
        opts.Queues = new[] { "ocr", "default" };
    });
}

// HttpClientFactory
builder.Services.AddHttpClient();

// === Identity ===
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(config =>
{
    config.Stores.MaxLengthForKeys = 128;
    config.Password.RequireDigit = false;
    config.Password.RequireLowercase = false;
    config.Password.RequiredLength = 4;
    config.Password.RequiredUniqueChars = 0;
    config.Password.RequireNonAlphanumeric = false;
    config.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<LoyaltyWalletDbContext>()
.AddDefaultTokenProviders();

// === Tenancy & AppEnvironment ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
builder.Services.AddSingleton<IAppEnvironment, WebAppEnvironment>();

// Wallet webhook auth filter
builder.Services.AddScoped<Loyalty.WebAPI.Filters.WalletWebhookAuthFilter>();

// === Business / DataAccess / Infrastructure ===
builder.Services.AddScoped<IGoogleWalletServiceFactory, GoogleWalletServiceFactory>();
builder.Services.AddDataAccessServices();
builder.Services.AddBusinessServices(builder.Configuration);
builder.Services.AddInfrastructureServices();

// === Swagger & Serilog ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Loyalty.WebAPI", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "please insert token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference= new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

//Log.Logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(builder.Configuration)
//    .Enrich.FromLogContext()
//    .CreateLogger();

builder.Services.AddApplicationInsightsTelemetry();

builder.Host.UseSerilog((ctx, services, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .ReadFrom.Services(services)
      .Enrich.FromLogContext()
      .WriteTo.Console();
});

var app = builder.Build();

// === Seed roles (idempotent) ===
// "Branch Admin" — branch-scoped admin rolu; mövcud olmasa yarat.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (var roleName in new[] { "Super Admin", "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator", "Customer", "Sales Manager", "Menu Admin" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }
}

// === Forwarded Headers (MUST be before UseHttpsRedirection for Azure/reverse proxy) ===
// This is critical for Apple PassKit Web Service to work correctly in Azure
// Without this, requests from Apple servers behind load balancer may get 301/405 errors
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost,
    // Azure App Service uses these IP ranges - trust them
    // For production, you may want to restrict this further
    RequireHeaderSymmetry = false,
    ForwardLimit = null // Allow all forwarded headers
});

// === Wallet signing secrets must never be served ===
// Əvvəlki versiya Apple .p12 / Google SA JSON nüsxələrini wwwroot/passes/downloads-a yazırdı (public statik qovluq).
// İndi sirlər temp-də saxlanılır (WalletSecretFetcher); köhnə nüsxələri sil və yolu həmişə 404 et.
try
{
    var legacySecretsDir = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "passes", "downloads");
    if (Directory.Exists(legacySecretsDir))
    {
        foreach (var f in Directory.EnumerateFiles(legacySecretsDir))
        {
            try { System.IO.File.Delete(f); } catch { /* növbəti startup-da */ }
        }
    }
}
catch { /* startup heç vaxt buna görə dayanmamalıdır */ }

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    if (path.StartsWithSegments("/passes/downloads", StringComparison.OrdinalIgnoreCase)
        || path.Value?.EndsWith(".p12", StringComparison.OrdinalIgnoreCase) == true
        || path.Value?.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) == true)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

// === Static files ===
// .p12/.pfx/.pem MIME xəritəsi qəsdən YOXDUR — imza açarları statik serv olunmamalıdır.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".cer"] = "application/pkix-cert";
contentTypes.Mappings[".crt"] = "application/x-x509-ca-cert";
contentTypes.Mappings[".der"] = "application/pkix-cert";
contentTypes.Mappings.Remove(".p12");
contentTypes.Mappings.Remove(".pfx");
contentTypes.Mappings.Remove(".pem");
contentTypes.Mappings[".pkpass"] = "application/vnd.apple.pkpass";
// Diaqnostika: .pkpass endirilməsi + brauzer UA. In-app brauzer / Chrome iOS faylı endirir,
// amma Wallet-ə ötürmür → Register gəlmir; UA-dan səbəb görünür ("pkpass download" traces-də).
var passDownloadLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PassDownload");
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = ctx =>
    {
        if (!ctx.File.Name.EndsWith(".pkpass", StringComparison.OrdinalIgnoreCase)) return;
        var req = ctx.Context.Request;
        passDownloadLogger.LogInformation(
            "pkpass download: {Path} UserAgent={UserAgent} Referer={Referer}",
            req.Path.Value, req.Headers.UserAgent.ToString(), req.Headers.Referer.ToString());
    }
});

// .well-known (App Links / Universal Links): apple-app-site-association has no
// file extension, so the default static file middleware returns 404 for it.
// Scoped to /.well-known to avoid enabling unknown file types globally.
// NOTE: Google/Apple fetch these from the LINK domain root (https://walvero.com),
// not from appapi — if walvero.com is served elsewhere, the same files must be
// published there too.
// CreateDirectory: PhysicalFileProvider throws at startup if the folder is
// missing (e.g. a deploy pipeline dropped the hidden dot-folder) — the app
// must not die over verification files.
var wellKnownPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", ".well-known");
Directory.CreateDirectory(wellKnownPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wellKnownPath),
    RequestPath = "/.well-known",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/json"
});

// === Middleware pipeline ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // UseHttpsRedirection MUST come after UseForwardedHeaders
    // so it can read X-Forwarded-Proto header correctly
    // BUT: Apple PassKit Web Service sends HTTP requests, so we need to bypass HTTPS redirect for /v1/* paths
    // UseWhen allows conditional middleware execution - skip HTTPS redirect for Apple PassKit endpoints
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/v1"), branch =>
    {
        branch.UseHttpsRedirection();
    });
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseExceptionHandler(builder => builder.Run(DefaultExceptionHandler.HandleExceptionAsync));
}

app.UseCors("AllowAll");

// Accept-Language → AppMessages.CurrentLanguage (az/en/ru, default az).
// AsyncLocal olduğu üçün bütün downstream async axına daşınır — servislər DI-siz oxuyur.
app.Use((context, next) =>
{
    var header = context.Request.Headers.AcceptLanguage.ToString();
    var lang = "az";
    if (!string.IsNullOrWhiteSpace(header))
    {
        var first = header.Split(',')[0].Trim();
        if (first.Length >= 2) lang = first.Substring(0, 2).ToLowerInvariant();
    }
    Loyalty.Business.Resources.AppMessages.CurrentLanguage = lang;
    return next(context);
});

// Response Compression — must come EARLY (before UseRouting/MapControllers).
// Brotli/Gzip daxil olan request-in `Accept-Encoding` header-inə uyğun olur.
app.UseResponseCompression();

// Response Caching middleware - must be BEFORE UseSession
app.UseResponseCaching();

// BlobUrlRewriteMiddleware: rewrites blob.core.windows.net → CDN host in JSON responses.
// MUST come BEFORE UseResponseCompression in the response pipeline (i.e. registered AFTER, since
// middleware runs in reverse on response path) so we modify the body before it gets gzipped.
// Behavior is no-op (single type check) when `Cdn:BaseUrl` is empty.
app.UseMiddleware<Loyalty.WebAPI.Middleware.BlobUrlRewriteMiddleware>();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard — yalnız Tenant Admin / Super Admin
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new Loyalty.WebAPI.Hangfire.TenantAdminDashboardFilter() }
});

// Recurring: uğursuz iiko webhook sifarişlərini yenidən emal et.
// Uyğunluq sətir səviyyəsində NextAttemptAt (növə görə backoff) ilə idarə olunur;
// 2 dəqiqəlik dövrə yalnız "vaxtı çatanları" götürür (indeksli sorğu, ucuz).
Hangfire.RecurringJob.AddOrUpdate<Loyalty.Business.Services.Abstractions.Main.IIikoWebhookFailureService>(
    "iiko-webhook-retry",
    s => s.RetryPendingAsync(CancellationToken.None),
    "*/2 * * * *");

// Recurring: son istifadə tarixi keçmiş kart balanslarını sıfırla (ambassador xalı) + iiko wallet chargeoff.
Hangfire.RecurringJob.AddOrUpdate<Loyalty.Business.Services.Abstractions.Main.IPointsExpiryService>(
    "points-expiry",
    s => s.RunAsync(CancellationToken.None),
    "*/5 * * * *");

// Bildirişlər v2 — yalnız job icra edən instansiya qeyd edir (lokal instansiya prod storage-a yeni job tipi yazmasın:
// köhnə versiyalı prod server tanımadığı tipi götürüb xəta verərdi).
if (builder.Configuration.GetValue("Hangfire:ServerEnabled", true))
{
    // Planlı bildirişlər: vaxtı çatanları hər dəqiqə göndər (ScheduledAtUtc indeksli, ucuz sorğu)
    Hangfire.RecurringJob.AddOrUpdate<Loyalty.Business.Services.Abstractions.Main.INotificationJobs>(
        "scheduled-notifications",
        s => s.RunScheduledAsync(null, CancellationToken.None),
        "* * * * *");
    // Ad günü: tenant yerli vaxtı göndərmə saatına çatanda (15 dəq dövr, idempotent — BirthdayNotificationLogs unikal)
    Hangfire.RecurringJob.AddOrUpdate<Loyalty.Business.Services.Abstractions.Main.INotificationJobs>(
        "birthday-notifications",
        s => s.RunBirthdaysAsync(null, false, CancellationToken.None),
        "*/15 * * * *");
    // Bonus istisnaları: qrup/kateqoriya qaydası olan iiko inteqrasiyalarının nomenklatura surətini yenilə
    // (qrupa yeni əlavə olunan məhsul avtomatik bloklansın).
    Hangfire.RecurringJob.AddOrUpdate<Loyalty.Business.Services.Abstractions.Main.IPosNomenclatureJobs>(
        "iiko-nomenclature-refresh",
        s => s.RefreshAllAsync(CancellationToken.None),
        "15 */6 * * *");
}

// 🔹 Minimal hostingda ayrı `UseRouting/UseEndpoints` ehtiyac yoxdur
app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");

app.Run();
