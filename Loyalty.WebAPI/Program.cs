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
            "https://qrwithmenu.com")
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

                return Task.CompletedTask;
            }
        };
    });

// === Authorization Policies ===
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdminOrModerator", policy =>
        policy.RequireRole("Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator"));
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
    foreach (var roleName in new[] { "Super Admin", "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator", "Customer", "Sales Manager" })
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

// === Static files ===
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".cer"] = "application/pkix-cert";
contentTypes.Mappings[".crt"] = "application/x-x509-ca-cert";
contentTypes.Mappings[".der"] = "application/pkix-cert";
contentTypes.Mappings[".pem"] = "application/x-pem-file";
contentTypes.Mappings[".p12"] = "application/x-pkcs12";
contentTypes.Mappings[".pfx"] = "application/x-pkcs12";
contentTypes.Mappings[".pkpass"] = "application/vnd.apple.pkpass";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes
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

// Response Compression — must come EARLY (before UseRouting/MapControllers).
// Brotli/Gzip daxil olan request-in `Accept-Encoding` header-inə uyğun olur.
app.UseResponseCompression();

// Response Caching middleware - must be BEFORE UseSession
app.UseResponseCaching();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// 🔹 Minimal hostingda ayrı `UseRouting/UseEndpoints` ehtiyac yoxdur
app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");

app.Run();
