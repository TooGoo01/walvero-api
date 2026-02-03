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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot"
});

// === Controllers & CORS & SignalR ===
builder.Services.AddControllers();

// === Response Caching ===
builder.Services.AddResponseCaching();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", policy => policy
        .WithOrigins(
            "http://10.0.2.2:5025",
            "http://localhost:7285",
            "https://10.0.2.2:7285",
            "https://localhost:7285")
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
            ClockSkew = TimeSpan.Zero
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
        policy.RequireRole("Tenant Admin", "Moderator"));

    options.AddPolicy("TenantAdminOnly", policy =>
        policy.RequireRole("Tenant Admin"));

    options.AddPolicy("TenantModeratorOnly", policy =>
        policy.RequireRole("Moderator"));

    options.AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"));

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("LoyaltyDbConnection")));

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
      .Enrich.FromLogContext();
});

var app = builder.Build();

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
    app.UseHttpsRedirection();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseExceptionHandler(builder => builder.Run(DefaultExceptionHandler.HandleExceptionAsync));
}

app.UseCors("AllowAll");

// Response Caching middleware - must be BEFORE UseSession
app.UseResponseCaching();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// 🔹 Minimal hostingda ayrı `UseRouting/UseEndpoints` ehtiyac yoxdur
app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");

app.Run();
