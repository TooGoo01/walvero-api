using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Walletobjects.v1;
using Google.Apis.Walletobjects.v1.Data;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Uri = System.Uri;

public sealed class GoogleWalletService : IGoogleWalletService
{
    private readonly WalletobjectsService? _svc;
    private readonly string _issuerId;
    private readonly string _classSuffix;
    private readonly string _serviceAccountEmail;
    private readonly string _privateKeyPem;
    private readonly string? _privateKeyId;
    private readonly string[] _origins;
    private readonly string _applicationName;
    private readonly ILogger<GoogleWalletService>? _log;
    private readonly ILoyaltyCardRepository? _cardRepo;
    private readonly Loyalty.DataAccess.Repositories.Abstractions.Main.IBranchRepository? _branchRepo;
    private readonly bool _isInitialized;
    private readonly ITenantSuspensionGuard? _suspensionGuard;
    private readonly ITimeZoneService? _timeZoneService;
    // Banner yoxdursa image module göndərilmir (əvvəlki placeholder domain mövcud deyildi → qırıq şəkil)

    public GoogleWalletService(GoogleWalletRuntimeConfig cfg, ILogger<GoogleWalletService>? log = null, ILoyaltyCardRepository? cardRepo = null,
        Loyalty.DataAccess.Repositories.Abstractions.Main.IBranchRepository? branchRepo = null,
        ITenantSuspensionGuard? suspensionGuard = null, ITimeZoneService? timeZoneService = null)
    {
        _log = log;
        _cardRepo = cardRepo;
        _branchRepo = branchRepo;
        _suspensionGuard = suspensionGuard;
        _timeZoneService = timeZoneService;
        _isInitialized = false;

        try
        {
            if (cfg is null)
            {
                _log?.LogWarning("GoogleWalletRuntimeConfig is null. Service will not initialize.");
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.IssuerId) || string.IsNullOrWhiteSpace(cfg.ClassSuffix) || string.IsNullOrWhiteSpace(cfg.ServiceAccountJsonPath))
            {
                _log?.LogWarning("Google Wallet config incomplete. IssuerId={IssuerId}, ClassSuffix={ClassSuffix}, JsonPath={JsonPath}",
                    cfg.IssuerId, cfg.ClassSuffix, cfg.ServiceAccountJsonPath);
                return;
            }

            _issuerId = cfg.IssuerId;
            _classSuffix = cfg.ClassSuffix;
            _origins = cfg.Origins ?? Array.Empty<string>();
            _applicationName = string.IsNullOrWhiteSpace(cfg.ApplicationName) ? "Walvero Loyalty" : cfg.ApplicationName;

            // Blob storage URL or local file
            string json;
            if (cfg.ServiceAccountJsonPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient();
                json = http.GetStringAsync(cfg.ServiceAccountJsonPath).GetAwaiter().GetResult();
            }
            else
            {
                if (!File.Exists(cfg.ServiceAccountJsonPath))
                {
                    _log?.LogWarning("Service account JSON not found: {Path}", cfg.ServiceAccountJsonPath);
                    return;
                }
                json = File.ReadAllText(cfg.ServiceAccountJsonPath);
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            _serviceAccountEmail = root.GetProperty("client_email").GetString()!;
            _privateKeyPem = root.GetProperty("private_key").GetString()!;
            _privateKeyId = root.TryGetProperty("private_key_id", out var kidEl) ? kidEl.GetString() : null;

            var credential = GoogleCredential.FromJson(json)
                .CreateScoped(WalletobjectsService.Scope.WalletObjectIssuer);

            _svc = new WalletobjectsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });

            _isInitialized = true;
            _log?.LogInformation("Google Wallet service initialized successfully. IssuerId={IssuerId}", _issuerId);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Failed to initialize Google Wallet service. Service will be disabled.");
            _isInitialized = false;
        }
    }

    private string ClassId => $"{_issuerId}.{_classSuffix}";

    public string GetIssuerId() => _issuerId;

    // Wallet collapsed/list view top line = tenant company name; fall back to program/template name.
    private static string ResolveIssuerName(CardTemplateSnapshot tpl) =>
        !string.IsNullOrWhiteSpace(tpl.Design?.IssuerName) ? tpl.Design!.IssuerName!.Trim()
        : !string.IsNullOrWhiteSpace(tpl.TenantCompanyName) ? tpl.TenantCompanyName!
        : !string.IsNullOrWhiteSpace(tpl.ProgramName) ? tpl.ProgramName
        : tpl.TemplateName;

    // Kart başlığı (class programName): dizayner Title → TemplateName (köhnə davranış) → ProgramName
    private static string ClassProgramName(CardTemplateSnapshot tpl) =>
        !string.IsNullOrWhiteSpace(tpl.Design?.Title) ? tpl.Design!.Title!.Trim()
        : !string.IsNullOrWhiteSpace(tpl.TemplateName) ? tpl.TemplateName
        : tpl.ProgramName;

    private static string NormalizeHexColor(string? v) =>
        string.IsNullOrWhiteSpace(v) ? "#FFFFFF" : (v.StartsWith("#") ? v.Trim() : "#" + v.Trim());

    private static string ClassBackgroundHex(CardTemplateSnapshot tpl) =>
        NormalizeHexColor(tpl.TemplateType == CardTemplateType.TierBased && string.IsNullOrWhiteSpace(tpl.BackgroundColor) ? "#556734" : tpl.BackgroundColor);

    private static LinksModuleData? BuildLinks(CardTemplateSnapshot tpl)
    {
        var d = tpl.Design ?? new CardDesignSettings();
        if (!d.ShowWebsite || !Uri.TryCreate(d.EffectiveWebsiteUrl, UriKind.Absolute, out _)) return null;
        return new LinksModuleData
        {
            Uris = new List<Google.Apis.Walletobjects.v1.Data.Uri>
            {
                new Google.Apis.Walletobjects.v1.Data.Uri { Id = "website", UriValue = d.EffectiveWebsiteUrl, Description = d.EffectiveWebsiteLabel }
            }
        };
    }

    private static ImageModuleData BuildBanner(string url, bool isProgressBased) => new ImageModuleData
    {
        MainImage = new Image
        {
            SourceUri = new ImageUri { Uri = url, Description = isProgressBased ? "Progress Icons Grid" : "Banner" },
            ContentDescription = new LocalizedString { DefaultValue = new TranslatedString { Language = "en-US", Value = "\u200B" } }
        }
    };

    /// about modulu (features ilə birləşib) — upsert/sil
    private static void ApplyAboutModule(List<TextModuleData> textList, CardTemplateSnapshot tpl)
    {
        var d = tpl.Design ?? new CardDesignSettings();
        textList.RemoveAll(t => t.Id == "features");
        if (!d.ShowAbout) { textList.RemoveAll(t => t.Id == "about"); return; }
        var about = textList.FirstOrDefault(t => t.Id == "about");
        if (about == null) { about = new TextModuleData { Id = "about" }; textList.Add(about); }
        about.Header = d.EffectiveAboutTitle;
        about.Body = d.EffectiveAboutText;
    }

    // =================== 1b) CLASS DESIGN UPDATE (admin saxlama / refresh) ===================
    public async Task<ServiceResult> UpdateClassDesignAsync(CardTemplateSnapshot tpl, CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
            return new ServiceResult(false, "Google Wallet service not initialized");

        LoyaltyClass? existing = null;
        try
        {
            existing = await _svc.Loyaltyclass.Get(ClassId).ExecuteAsync(ct);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            existing = null;
        }

        if (existing == null)
        {
            await EnsureClassAsync(tpl, ResolveIssuerName(tpl), ct);
            return new ServiceResult(true, "Google class yaradıldı.");
        }

        // Faza 2: başlıq/issuer/rəng/logo + sətir şablonu (classTemplateInfo) — Faza 0 sınağı: classTemplateInfo PATCH işləyir
        var desired = BuildDesiredClass(tpl, ResolveIssuerName(tpl), ClassId, designLayout: true);
        var programName = desired.ProgramName;
        var issuerName = desired.IssuerName;
        var bg = desired.HexBackgroundColor;
        var logoUri = desired.ProgramLogo?.SourceUri?.Uri;

        var same = string.Equals(existing.ProgramName, programName)
                   && string.Equals(existing.IssuerName, issuerName)
                   && string.Equals(existing.HexBackgroundColor, bg, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(existing.ProgramLogo?.SourceUri?.Uri, logoUri)
                   && string.Equals(TemplateInfoJson(existing.ClassTemplateInfo), TemplateInfoJson(desired.ClassTemplateInfo), StringComparison.Ordinal);
        if (same)
            return new ServiceResult(true, "Google class artıq eynidir.");

        // Faza 0 sınağı (2026-10-01, issuer 3388…118): approved class-a PATCH reviewStatus-suz → 400 "Invalid review status APPROVED.
        // Use UNDER_REVIEW instead."; reviewStatus=UNDER_REVIEW ilə → 200 və class approved qalır (production issuer avtomatik təsdiq).
        var patch = desired;
        patch.ReviewStatus = "UNDER_REVIEW";

        try
        {
            await _svc.Loyaltyclass.Patch(patch, ClassId).ExecuteAsync(ct);
            _log?.LogInformation("[GW-CLASS] design PATCHED. ClassId={ClassId} programName={ProgramName} issuer={Issuer} bg={Bg} logo={Logo} reviewStatus(before)={Review}",
                ClassId, programName, issuerName, bg, logoUri, existing.ReviewStatus);
            return new ServiceResult(true, "Google class yeniləndi.");
        }
        catch (Google.GoogleApiException ex)
        {
            _log?.LogWarning(ex, "[GW-CLASS] design PATCH failed. ClassId={ClassId}", ClassId);
            return new ServiceResult(false, $"Google class PATCH xətası: {ex.Message}");
        }
    }

    // =================== 1) CLASS ===================
    public async Task EnsureClassAsync(CardTemplateSnapshot tpl, string programName, CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping EnsureClass.");
            return;
        }

        LoyaltyClass? existing = null;
        try
        {
            existing = await _svc.Loyaltyclass.Get(ClassId).ExecuteAsync(ct);
            _log?.LogInformation("[GW-CLASS] State check. ClassId={ClassId} reviewStatus={ReviewStatus} (APPROVED required for production push)",
                ClassId, existing?.ReviewStatus ?? "(null)");
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            existing = null;
        }

        LoyaltyClass BuildDesired() => BuildDesiredClass(tpl, programName, ClassId, DesignLayoutEnabled(isPreview: false));

        if (existing is null)
        {
            var toCreate = BuildDesired();
            toCreate.ReviewStatus = "UNDER_REVIEW"; // initial submit only — Console must approve before push fires for real users.
            await _svc.Loyaltyclass.Insert(toCreate).ExecuteAsync(ct);
            _log?.LogInformation("Google Wallet class CREATED: {ClassId} (review status UNDER_REVIEW — approve in Google Pay Console for production push)", ClassId);
        }
        else
        {
            // Class already exists. Do NOT write on the hot path (Redeem / CreateObject):
            //   - sending reviewStatus="APPROVED" → 400 "Invalid review status"
            //   - sending "UNDER_REVIEW" on an approved class → demotes it back into review
            //   - even Patch can echo server-side reviewStatus depending on SDK round-trip
            // Class metadata edits (logo, colors, ProgramName) must go through an explicit
            // admin/re-submit endpoint, not runtime card operations.
            _log?.LogInformation("Google Wallet class exists ({ClassId}) — skipping update during runtime op. ReviewStatus={ReviewStatus}",
                ClassId, existing.ReviewStatus ?? "(null)");
        }
    }

    // =================== 2) OBJECT CREATE / GET ===================
    public async Task<ServiceResult> CreateOrGetObjectByCardIdAsync(
         int loyaltyCardId,
         CardTemplateSnapshot tpl,
         string? accountName = null,
         string? barcodeValue = null,
         decimal currentPoints = 0,
         int currentSteps = 0,
         CancellationToken ct = default,
         int lotteryTickets = 0,
         string? lotteryWinPrize = null,
         System.DateTime? lotteryDrawDate = null,
         int? completedCyclesOverride = null,
         string? objectIdOverride = null,
         string? classIdOverride = null,
         bool updateIfExists = false,
         decimal? lifetimePointsOverride = null,
         string? pointsExpiryOverride = null,
         decimal? discountOverride = null)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping CreateOrGetObject. CardId={CardId}", loyaltyCardId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            await EnsureClassAsync(tpl, programName: ResolveIssuerName(tpl), ct);

            var objectId = objectIdOverride ?? ObjectIdForCard(_issuerId, tpl.TenantId, tpl.LoyaltyProgramId, loyaltyCardId);

            bool objectExists = false;
            try
            {
                var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);
                objectExists = true;

                if (!updateIfExists)
                {
                    // Object exists but balance may be stale - update it via RedeemByCardIdAsync
                    _log?.LogInformation("GW object already exists, updating balance. ObjectId={ObjectId}, currentPoints={CurrentPoints}", objectId, currentPoints);
                    return await RedeemByCardIdAsync(loyaltyCardId, tpl, currentPoints, currentSteps, 0, accountName, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate, completedCyclesOverride);
                }
                // updateIfExists (preview): aşağıda tam obyekt qurulub Update olunur
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // create new
            }

            var state = BuildDisplayState(tpl, currentPoints, currentSteps);
            var barcode = string.IsNullOrWhiteSpace(barcodeValue) ? loyaltyCardId.ToString() : barcodeValue;

            // Faza 2: sahə modulları kataloqdan — admin layout yalnız preview-də və ya CardDesign:GoogleClassUpdateEnabled açıqkən;
            // əks halda default yerləşmə (= bugünkü görünüş) → mövcud class sətirləri ilə uyğun.
            bool isProgressBased = tpl.TemplateType == CardTemplateType.ProgressBased;
            bool isPreview = objectIdOverride != null;
            var (fields, bannerPos, barcodeType, _) = LayoutFor(tpl, DesignLayoutEnabled(isPreview));

            string? bannerUrl = null;
            List<TextModuleData> textModules = new();
            int completedCycles = 0;

            if (isProgressBased)
            {
                // completedCyclesOverride: suspension mask zamanı DB dəyəri əvəzinə 0 ötürülür
                if (completedCyclesOverride.HasValue)
                {
                    completedCycles = completedCyclesOverride.Value;
                }
                else if (_cardRepo != null)
                {
                    try
                    {
                        var card = await _cardRepo.GetByIdAsync(loyaltyCardId, ct);
                        completedCycles = card != null ? card.CompletedCycles : CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                    catch
                    {
                        completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                }
                else
                {
                    completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                }

                // Progress icons grid (banner): cari addım ikonu → hero → strip → logo
                bannerUrl = PickProgressIconBySteps(tpl.IconUrls, currentSteps, tpl.TotalSteps);
                if (!IsHttps(bannerUrl) && IsHttps(tpl.HeroImageUrl)) bannerUrl = tpl.HeroImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.StripImageUrl)) bannerUrl = tpl.StripImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            }
            else
            {
                // Banner URL fallback (hero -> tier icon -> logo); banner yoxdursa null qalır → şəkil göndərilmir
                bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
                if (!IsHttps(bannerUrl) && IsHttps(state.CurrentIconUrl)) bannerUrl = state.CurrentIconUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            }

            var extras = await LoadExtrasAsync(loyaltyCardId, tpl, fields, lifetimePointsOverride, pointsExpiryOverride, discountOverride, ct);
            var values = CardFieldCatalog.BuildValues(tpl, accountName ?? $"Card #{loyaltyCardId}", barcode,
                currentPoints, currentSteps, completedCycles, extras.Lifetime, extras.ExpiryText, CardPlatform.Google, extras.Discount);
            ApplyFieldModules(textModules, tpl, fields, values, currentPoints);
            var imageModules = bannerPos == GoogleBannerPosition.Top && IsHttps(bannerUrl)
                ? new List<ImageModuleData> { BuildBanner(bannerUrl!, isProgressBased) } : null;
            var heroImage = bannerPos == GoogleBannerPosition.Bottom && IsHttps(bannerUrl) ? BuildHero(bannerUrl!) : null;

            _log?.LogInformation("GW Create: bannerUrl={Url} hero={Hero} icon={Icon} logo={Logo} isProgress={IsProgress}",
                bannerUrl, tpl.HeroImageUrl, state.CurrentIconUrl, tpl.GoogleLogoUrl, isProgressBased);
            // about (kart dizayneri; "-" = gizli)
            ApplyAboutModule(textModules, tpl);

            // Lottery: show on card even if tickets=0 (so customer sees the feature exists)
            LoyaltyPoints? secondaryLoyaltyPoints = null;
            if (tpl.IsLotteryEnabled)
            {
                var label = "Lotereya şansları";
                if (lotteryDrawDate.HasValue)
                    label += $" · Çəkiliş: {lotteryDrawDate.Value:dd.MM.yyyy HH:mm}";
                if (!string.IsNullOrWhiteSpace(lotteryWinPrize))
                    label = $"Lotereya uduşu: {lotteryWinPrize}";

                secondaryLoyaltyPoints = new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = lotteryTickets },
                    Label = label
                };

                textModules.Add(new TextModuleData
                {
                    Id = "lottery",
                    Header = label,
                    Body = lotteryTickets.ToString()
                });
            }

            var obj = new LoyaltyObject
            {
                Id = objectId,
                ClassId = classIdOverride ?? ClassId,
                State = "ACTIVE",
                AccountId = objectIdOverride != null ? "preview" : loyaltyCardId.ToString(),
                AccountName = accountName ?? string.Empty,

                // Only set LoyaltyPoints for non-ProgressBased
                LoyaltyPoints = isProgressBased ? null : new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = (int)Math.Round(currentPoints) },
                    Label = CardFieldCatalog.EffectiveUnit(tpl)
                },
                SecondaryLoyaltyPoints = secondaryLoyaltyPoints,

                TextModulesData = textModules,
                LinksModuleData = BuildLinks(tpl),

                Barcode = new Barcode { Type = CardFieldCatalog.GoogleBarcodeType(barcodeType), Value = barcode, AlternateText = barcode },

                // Banner: Top → image modulu (1-ci sətir), Bottom → heroImage (native, altda), None → yox
                ImageModulesData = imageModules,
                HeroImage = heroImage,
            };

            // Inject branch locations for proximity triggers
            if (_branchRepo != null)
            {
                try
                {
                    var branches = await _branchRepo.GetByTenantAsync(tpl.TenantId, ct);
                    var branchLocations = branches?
                        .Where(b => b.IsActive && b.Latitude.HasValue && b.Longitude.HasValue)
                        .Select(b => new LatLongPoint
                        {
                            Latitude = (double)b.Latitude!.Value,
                            Longitude = (double)b.Longitude!.Value
                        })
                        .ToList();

                    if (branchLocations?.Count > 0)
                        obj.Locations = branchLocations;
                }
                catch (Exception ex)
                {
                    _log?.LogWarning(ex, "Failed to load branch locations for Google Wallet");
                }
            }

            _log?.LogInformation("Creating GW object with lottery: IsLotteryEnabled={IsLotteryEnabled}, Tickets={Tickets}", tpl.IsLotteryEnabled, lotteryTickets);
            LoyaltyObject created;
            if (objectExists && updateIfExists)
                created = await _svc.Loyaltyobject.Update(obj, objectId).ExecuteAsync(ct);
            else
                created = await _svc.Loyaltyobject.Insert(obj).ExecuteAsync(ct);
            return new ServiceResult(true, created);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "CreateOrGetObjectByCardIdAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }
    private static string ObjectIdForCard(string issuerId, int tenantId, int programId, int loyaltyCardId) => $"{issuerId}.{tenantId}.{programId}.lc-{loyaltyCardId}";

    public string BuildObjectIdForCard(int tenantId, int programId, int loyaltyCardId)
        => ObjectIdForCard(_issuerId, tenantId, programId, loyaltyCardId);

    public async Task<ServiceResult> SetObjectStateAsync(int tenantId, int programId, int loyaltyCardId, string state, CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
            return new ServiceResult(false, "Google Wallet service not initialized.");

        var objectId = ObjectIdForCard(_issuerId, tenantId, programId, loyaltyCardId);
        try
        {
            await _svc.Loyaltyobject.Patch(new LoyaltyObject { State = state }, objectId).ExecuteAsync(ct);
            _log?.LogInformation("GW object state set. ObjectId={ObjectId}, State={State}", objectId, state);
            return new ServiceResult(true, "OK");
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Kart heç vaxt Google Wallet-ə əlavə olunmayıb — dəyişiləcək obyekt yoxdur
            return new ServiceResult(true, "Object not found (no Google pass).");
        }
    }

    public async Task<ServiceResult> AddMessageToObjectAsync(string objectId, string title, string body, CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping AddMessage. ObjectId={ObjectId}", objectId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            // Log class review state — push only fires for production users when class is APPROVED.
            try
            {
                var cls = await _svc.Loyaltyclass.Get(ClassId).ExecuteAsync(ct);
                _log?.LogWarning("[GW-PUSH-CHECK] ClassId={ClassId} reviewStatus={ReviewStatus} — Google Wallet push will only reach production users when this is APPROVED. UNDER_REVIEW = only Trusted Testers (service account email).",
                    ClassId, cls?.ReviewStatus ?? "(null)");
            }
            catch (Exception clsEx)
            {
                _log?.LogWarning(clsEx, "[GW-PUSH-CHECK] Could not fetch class state for {ClassId}", ClassId);
            }

            // displayInterval = 7 days. Google requires it to trigger a push on Android Wallet —
            // without it some devices treat the message as silent (in-app only).
            var start = System.DateTime.UtcNow;
            var end = start.AddDays(7);
            var req = new AddMessageRequest
            {
                Message = new Message
                {
                    Header = title,
                    Body = body,
                    Id = $"notif-{Guid.NewGuid():N}",
                    MessageType = "TEXT_AND_NOTIFY",
                    DisplayInterval = new TimeInterval
                    {
                        Start = new Google.Apis.Walletobjects.v1.Data.DateTime
                        {
                            Date = start.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        },
                        End = new Google.Apis.Walletobjects.v1.Data.DateTime
                        {
                            Date = end.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        }
                    }
                }
            };
            await _svc.Loyaltyobject.Addmessage(req, objectId).ExecuteAsync(ct);
            _log?.LogInformation("GW addmessage OK. ObjectId={ObjectId}", objectId);

            // PUSH-TRIGGER HACK: addmessage alone is optional-push on Google Wallet — Android only
            // fires a system notification when the user toggled per-pass notifications ON.
            // Forcing a state change (ACTIVE -> INACTIVE -> ACTIVE) is treated by Google as a
            // significant card update and pushes a banner reliably across all devices.
            try
            {
                await _svc.Loyaltyobject.Patch(
                    new LoyaltyObject { State = "INACTIVE" },
                    objectId).ExecuteAsync(ct);
                await Task.Delay(250, ct);
                await _svc.Loyaltyobject.Patch(
                    new LoyaltyObject { State = "ACTIVE" },
                    objectId).ExecuteAsync(ct);
                _log?.LogInformation("GW state-cycle bump OK. ObjectId={ObjectId} (INACTIVE -> ACTIVE)", objectId);
            }
            catch (Exception bumpEx)
            {
                _log?.LogWarning(bumpEx, "GW state-cycle bump failed (message still delivered). ObjectId={ObjectId}", objectId);
            }

            return new ServiceResult(true, "OK");
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            _log?.LogWarning("GW addmessage: object not found {ObjectId}", objectId);
            return new ServiceResult(false, "Object not found");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "AddMessageToObjectAsync failed for {ObjectId}", objectId);
            return new ServiceResult(false, ex.Message);
        }
    }
    // =================== 3) REDEEM / UPDATE ===================
    public async Task<ServiceResult> RedeemByCardIdAsync(
       int loyaltyCardId,
       CardTemplateSnapshot tpl,
       decimal currentPoints,
       int currentSteps,
       int delta,
       string? accountName = null,
       CancellationToken ct = default,
       int lotteryTickets = 0,
       string? lotteryWinPrize = null,
       System.DateTime? lotteryDrawDate = null,
       int? completedCyclesOverride = null)
    {
        _log?.LogInformation("RedeemByCardIdAsync started. CardId={CardId}, IssuerId={IssuerId}, TenantId={TenantId}, ProgramId={ProgramId}, Delta={Delta}, Initialized={Initialized}",
            loyaltyCardId, _issuerId, tpl.TenantId, tpl.LoyaltyProgramId, delta, _isInitialized);

        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping redeem. CardId={CardId}", loyaltyCardId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            await EnsureClassAsync(tpl, programName: ResolveIssuerName(tpl), ct);

            // FIX A: Use ObjectIdForCard instead of hardcoded format
            var objectId = ObjectIdForCard(_issuerId, tpl.TenantId, tpl.LoyaltyProgramId, loyaltyCardId);
            var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);

            // Resolve customer full name: caller-provided wins, else load from card, else AccountName, else "Card #N".
            if (string.IsNullOrWhiteSpace(accountName) && _cardRepo != null)
            {
                try
                {
                    var cardForName = await _cardRepo.GetById(loyaltyCardId, ct);
                    if (cardForName?.Customer != null)
                    {
                        var full = $"{cardForName.Customer.FirstName} {cardForName.Customer.LastName}".Trim();
                        if (!string.IsNullOrWhiteSpace(full)) accountName = full;
                    }
                }
                catch { /* fallback below */ }
            }
            if (string.IsNullOrWhiteSpace(accountName)) accountName = existing.AccountName;
            if (string.IsNullOrWhiteSpace(accountName)) accountName = $"Card #{loyaltyCardId}";

            var nextPoints = currentPoints;
            bool isProgressBased = tpl.TemplateType == CardTemplateType.ProgressBased;
            // Faza 2: admin layout yalnız flag açıqkən (class sətirləri ilə sinxron), əks halda default = bugünkü
            var (fields, bannerPos, barcodeType, _) = LayoutFor(tpl, DesignLayoutEnabled(false));

            var existingBalance = existing?.LoyaltyPoints?.Balance?.Int__;
            _log?.LogWarning("GW PATCH: CardId={CardId}, existingGoogleBalance={ExistBal}, newBalance={NewBal}, delta={Delta}, isProgress={IsProgress}, objectId={ObjId}",
                loyaltyCardId, existingBalance, nextPoints, delta, isProgressBased, objectId);

            var textList = existing.TextModulesData?.ToList() ?? new List<TextModuleData>();

            string? bannerUrl = null;
            int completedCycles = 0;

            if (isProgressBased)
            {
                // completedCyclesOverride: suspension mask zamanı DB dəyəri əvəzinə 0 ötürülür
                if (completedCyclesOverride.HasValue)
                {
                    completedCycles = completedCyclesOverride.Value;
                }
                else if (_cardRepo != null)
                {
                    try
                    {
                        var card = await _cardRepo.GetById(loyaltyCardId, ct);
                        completedCycles = card != null ? card.CompletedCycles : CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                    catch
                    {
                        completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                }
                else
                {
                    completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                }

                // Progress icons grid (banner)
                bannerUrl = PickProgressIconBySteps(tpl.IconUrls, currentSteps, tpl.TotalSteps);
                if (!IsHttps(bannerUrl) && IsHttps(tpl.HeroImageUrl)) bannerUrl = tpl.HeroImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.StripImageUrl)) bannerUrl = tpl.StripImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            }
            else
            {
                // Redeem zamanı da banner fallback (hero -> tier icon -> logo)
                bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
                if (!IsHttps(bannerUrl))
                {
                    var fallbackIcon = PickTierIconByPoints(tpl, nextPoints, out _);
                    if (IsHttps(fallbackIcon)) bannerUrl = fallbackIcon;
                }
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            }

            // Sahə modulları: görünən → upsert (ad dəyişən müştəri də yenilənir), gizli → silinir
            var extras = await LoadExtrasAsync(loyaltyCardId, tpl, fields, null, null, null, ct);
            var values = CardFieldCatalog.BuildValues(tpl, accountName, existing.Barcode?.Value ?? loyaltyCardId.ToString(),
                nextPoints, currentSteps, completedCycles, extras.Lifetime, extras.ExpiryText, CardPlatform.Google, extras.Discount);
            ApplyFieldModules(textList, tpl, fields, values, nextPoints);

            // Lottery: show on card face via SecondaryLoyaltyPoints + TextModule
            LoyaltyPoints? secondaryLoyaltyPoints = null;
            _log?.LogInformation("Lottery check: IsLotteryEnabled={IsLotteryEnabled}, lotteryTickets={Tickets}, lotteryWinPrize={Prize}, lotteryDrawDate={DrawDate}",
                tpl.IsLotteryEnabled, lotteryTickets, lotteryWinPrize, lotteryDrawDate);

            if (tpl.IsLotteryEnabled && lotteryTickets > 0)
            {
                var label = "Lotereya şansları";
                if (lotteryDrawDate.HasValue)
                    label += $" · Çəkiliş: {lotteryDrawDate.Value:dd.MM.yyyy HH:mm}";
                if (!string.IsNullOrWhiteSpace(lotteryWinPrize))
                    label = $"Lotereya uduşu: {lotteryWinPrize}";

                secondaryLoyaltyPoints = new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = lotteryTickets },
                    Label = label
                };

                // Also add as TextModule (always visible regardless of SecondaryLoyaltyPoints rendering)
                var lotteryText = textList.FirstOrDefault(t => t.Id == "lottery");
                if (lotteryText == null)
                {
                    lotteryText = new TextModuleData { Id = "lottery" };
                    textList.Add(lotteryText);
                }
                lotteryText.Header = label;
                lotteryText.Body = lotteryTickets.ToString();
            }
            else
            {
                // Remove lottery text module if no tickets
                textList.RemoveAll(t => t.Id == "lottery");
            }

            // about (kart dizayneri) — mövcud obyektdə də yenilənsin
            ApplyAboutModule(textList, tpl);

            var imageModules = bannerPos == GoogleBannerPosition.Top && IsHttps(bannerUrl)
                ? new List<ImageModuleData> { BuildBanner(bannerUrl!, isProgressBased) }
                : new List<ImageModuleData>(); // boş siyahı → PATCH mövcud image modulunu silir
            var heroImage = bannerPos == GoogleBannerPosition.Bottom && IsHttps(bannerUrl) ? BuildHero(bannerUrl!) : null;

            var patch = new LoyaltyObject
            {
                // Only set LoyaltyPoints for non-ProgressBased
                // FIX: Label must be "Points" to match class definition, otherwise Google API silently ignores PATCH
                LoyaltyPoints = isProgressBased ? null : new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = (int)Math.Round(nextPoints) },
                    Label = CardFieldCatalog.EffectiveUnit(tpl)
                },
                SecondaryLoyaltyPoints = secondaryLoyaltyPoints,
                TextModulesData = textList,
                ImageModulesData = imageModules,
                HeroImage = heroImage,
                LinksModuleData = BuildLinks(tpl),
                // Account fields shown in collapsed/list view of Google Wallet.
                AccountId = loyaltyCardId.ToString(),
                AccountName = accountName
            };

            // Faza 2: barkod tipi (dəyər dəyişmir) — Faza 0 sınağı: obyekt PATCH ilə tip dərhal dəyişir (QR/PDF417/Aztec/Code128)
            // GET tipi camelCase qaytarır ("qrCode", "code128", "pdf417", "aztec") → alt xətt-siz/kiçik hərflə müqayisə
            var desiredBarcodeType = CardFieldCatalog.GoogleBarcodeType(barcodeType);
            static string NormBarcode(string? t) => (t ?? "").Replace("_", "").ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(existing.Barcode?.Value)
                && NormBarcode(existing.Barcode!.Type) != NormBarcode(desiredBarcodeType))
            {
                patch.Barcode = new Barcode { Type = desiredBarcodeType, Value = existing.Barcode.Value, AlternateText = existing.Barcode.AlternateText ?? existing.Barcode.Value };
            }

            LoyaltyObject patched;
            if (existing.HeroImage != null && heroImage == null)
            {
                // PATCH null sahəni silmir → heroImage-i silmək üçün tam Update (PUT) təzə obyektlə (preview/Walvero pass ilə eyni yol)
                var full = new LoyaltyObject
                {
                    Id = objectId,
                    ClassId = existing.ClassId ?? ClassId,
                    State = existing.State ?? "ACTIVE",
                    AccountId = patch.AccountId,
                    AccountName = patch.AccountName,
                    LoyaltyPoints = patch.LoyaltyPoints ?? existing.LoyaltyPoints,
                    SecondaryLoyaltyPoints = patch.SecondaryLoyaltyPoints,
                    TextModulesData = textList,
                    ImageModulesData = imageModules,
                    LinksModuleData = patch.LinksModuleData,
                    Barcode = patch.Barcode ?? existing.Barcode,
                    Locations = existing.Locations,
                    Messages = existing.Messages,
                    HeroImage = null
                };
                patched = await _svc.Loyaltyobject.Update(full, objectId).ExecuteAsync(ct);
                _log?.LogInformation("[GW] hero removed via full Update. ObjectId={ObjectId}", objectId);
            }
            else
            {
                patched = await _svc.Loyaltyobject.Patch(patch, objectId).ExecuteAsync(ct);
            }

            _log?.LogInformation("Google Wallet object PATCHED (redeem): {ObjectId} (banner={Banner}) isProgress={IsProgress} lotteryTickets={LotteryTickets}",
                objectId, bannerUrl, isProgressBased, lotteryTickets);

            // Balans dəyişmə bildirişi (tenant ayarı) — Apple changeMessage-in Google qarşılığı. Best-effort.
            if (tpl.Design?.NotifyOnBalanceChange == true && !isProgressBased
                && existingBalance.HasValue && existingBalance.Value != (int)Math.Round(nextPoints)
                && values.TryGetValue(CardFieldCatalog.Balance, out var balanceValue)
                && !await IsSuspendedAsync(tpl.TenantId, ct))
            {
                await NotifyBalanceChangeAsync(objectId, existing.Messages, tpl, balanceValue.Value, ct);
            }

            return new ServiceResult(true, patched);
        }
        catch (Google.GoogleApiException gex)
        {
            _log?.LogError(gex, "RedeemByCardIdAsync Google error");
            return new ServiceResult(false, gex.Message);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "RedeemByCardIdAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }
    private static int CalculateFreeDrinkCount(int currentSteps, int totalSteps)
    {
        if (totalSteps <= 0) return 0;
        return currentSteps / totalSteps; // Integer division = floor
    }
    // =================== WALVERO PASS METHODS ===================
    public async Task<ServiceResult> CreateOrGetWalveroObjectAsync(
        string objectId,
        CardTemplateSnapshot tpl,
        string accountName,
        string barcodeValue,
        CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping CreateOrGetWalveroObject. ObjectId={ObjectId}", objectId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            await EnsureClassAsync(tpl, "Walvero", ct);

            // Banner URL fallback
            string? bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
            if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            // banner yoxdursa image module göndərilmir

            // Build Walvero object WITHOUT points/balance
            var obj = new LoyaltyObject
            {
                Id = objectId,
                ClassId = ClassId,
                State = "ACTIVE",
                AccountId = null,
                AccountName = accountName,

                // NO LoyaltyPoints - Walvero pass has no balance

                TextModulesData = new List<TextModuleData>
                {
                    new TextModuleData
                    {
                        Id = "customer",
                        Header = "Customer",
                        Body = accountName
                    }
                    // NO balance or cashback modules
                },

                Barcode = new Barcode
                {
                    Type = "CODE_128",
                    Value = barcodeValue,
                    AlternateText = barcodeValue
                },

                ImageModulesData = IsHttps(bannerUrl) ? new List<ImageModuleData> { BuildBanner(bannerUrl!, false) } : null,
            };

            // Check if object already exists
            bool objectExists = false;
            try
            {
                var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);
                objectExists = true;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                objectExists = false;
            }

            if (objectExists)
            {
                // Update existing object with new data
                var updated = await _svc.Loyaltyobject.Update(obj, objectId).ExecuteAsync(ct);
                _log?.LogInformation("Walvero Google Wallet object UPDATED: {ObjectId} (AccountName: {AccountName}, Barcode: {Barcode})",
                    objectId, accountName, barcodeValue);
                return new ServiceResult(true, updated);
            }
            else
            {
                // Create new object
                var created = await _svc.Loyaltyobject.Insert(obj).ExecuteAsync(ct);
                _log?.LogInformation("Walvero Google Wallet object CREATED: {ObjectId} (AccountName: {AccountName}, Barcode: {Barcode})",
                    objectId, accountName, barcodeValue);
                return new ServiceResult(true, created);
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "CreateOrGetWalveroObjectAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }

    public async Task<ServiceResult> GenerateWalveroSaveLinkAsync(
        string objectId,
        CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping GenerateWalveroSaveLink. ObjectId={ObjectId}", objectId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            var loyaltyObject = new JObject
            {
                ["id"] = objectId,
                ["classId"] = ClassId,
                ["state"] = "ACTIVE"
            };

            var claims = new JObject
            {
                ["iss"] = _serviceAccountEmail,
                ["aud"] = "google",
                ["typ"] = "savetowallet",
                ["origins"] = JArray.FromObject(_origins),
                ["payload"] = new JObject
                {
                    ["loyaltyObjects"] = new JArray { loyaltyObject }
                }
            };

            var jwt = SignJwt(claims, _privateKeyPem, _privateKeyId);
            var url = $"https://pay.google.com/gp/v/save/{jwt}";
            return new ServiceResult(true, url);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "GenerateWalveroSaveLinkAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }

    // =================== 4) SAVE LINK ===================
    public async Task<ServiceResult> GenerateSaveLinkByCardIdAsync(int loyaltyCardId, int tenantId, int programId, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping GenerateSaveLink. CardId={CardId}", loyaltyCardId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            var objectId = ObjectIdForCard(_issuerId, tenantId, programId, loyaltyCardId);

            // Fetch card to get current balance
            int currentPoints = 0;
            string currency = "Points";
            string accountName = $"Card #{loyaltyCardId}";
            if (_cardRepo != null)
            {
                var card = await _cardRepo.GetByIdAsync(loyaltyCardId, ct);
                if (card != null)
                {
                    // Google Wallet API "int" balance gözləyir; CurrentPoints decimal-dir,
                    // ona görə fractional dəyəri yuvarlaqlaşdırırıq.
                    currentPoints = (int)Math.Round(card.CurrentPoints);
                    accountName = $"{card.Customer?.FirstName} {card.Customer?.LastName}".Trim();
                    if (string.IsNullOrEmpty(accountName)) accountName = $"Card #{loyaltyCardId}";
                }
            }

            var url = BuildSaveLink(objectId, ClassId, loyaltyCardId.ToString(), accountName, currentPoints, currency);
            return new ServiceResult(true, url);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "GenerateSaveLinkByCardIdAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }

    /// <summary>Class strukturu (preview class da eyni koddan qurulur — classId parametrlə).</summary>
    private LoyaltyClass BuildDesiredClass(CardTemplateSnapshot tpl, string programName, string classId, bool designLayout)
    {
            string NormalizeHex(string? v) =>
                string.IsNullOrWhiteSpace(v) ? "#FFFFFF" :
                (v.StartsWith("#") ? v.Trim() : "#" + v.Trim());

            var bg = NormalizeHex(
                tpl.TemplateType == CardTemplateType.TierBased && string.IsNullOrWhiteSpace(tpl.BackgroundColor)
                    ? "#556734"
                    : tpl.BackgroundColor
            );

            // Class səviyyəsində HeroImage-istifadə ETMİRİK (layout bizdə olsun)
            Image? hero = null;

            var blank = "\u200B";
            var cls = new LoyaltyClass
            {
                Id = classId,
                ProgramName = ClassProgramName(tpl),
                IssuerName = !string.IsNullOrWhiteSpace(tpl.Design?.IssuerName) ? tpl.Design!.IssuerName!.Trim() : programName,
                HexBackgroundColor = bg,
                ProgramLogo = IsHttps(tpl.GoogleLogoUrl)
                    ? new Image
                    {
                        SourceUri = new ImageUri { Uri = tpl.GoogleLogoUrl, Description = "Logo" },
                        ContentDescription = new LocalizedString
                        {
                            DefaultValue = new TranslatedString { Language = "en-US", Value = programName }
                        }
                    }
                    : null,
                //WideProgramLogo = IsHttps(tpl.GoogleLogoUrl)
                //    ? new Image
                //    {
                //        SourceUri = new ImageUri { Uri = tpl.GoogleLogoUrl, Description = "Logo" },
                //        ContentDescription = new LocalizedString
                //        {
                //            DefaultValue = new TranslatedString { Language = "en-US", Value = programName }
                //        }
                //    }
                //    : null,
                HeroImage = hero, // sabit null

                ClassTemplateInfo = BuildClassTemplateInfo(tpl, designLayout)
            };

            return cls;
    }

    /// <summary>"Save to Google Wallet" linki (JWT) — card/preview obyektləri üçün ortaq.</summary>
    private string BuildSaveLink(string objectId, string classId, string accountId, string accountName, int currentPoints, string currency)
    {
        var loyaltyCardId = accountId;
            var loyaltyObject = new JObject
            {
                ["id"] = objectId,
                ["classId"] = classId,
                ["state"] = "ACTIVE",
                ["accountId"] = accountId,
                ["accountName"] = accountName,
                ["loyaltyPoints"] = new JObject
                {
                    ["balance"] = new JObject { ["int"] = currentPoints },
                    ["label"] = currency
                }
            };

            var claims = new JObject
            {
                ["iss"] = _serviceAccountEmail,
                ["aud"] = "google",
                ["typ"] = "savetowallet",
                ["origins"] = JArray.FromObject(_origins),
                ["payload"] = new JObject
                {
                    ["loyaltyObjects"] = new JArray { loyaltyObject }
                }
            };

            var jwt = SignJwt(claims, _privateKeyPem, _privateKeyId);
            var url = $"https://pay.google.com/gp/v/save/{jwt}";
        return url;
    }

    // =================== 5) PREVIEW (publish-siz test) + CLASS STATUS ===================
    /// <summary>
    /// Ayrıca PREVIEW class ({ClassId}-preview, real class-a toxunulmur) + preview obyekt → Save-to-Wallet linki.
    /// Admin Android-də real görünüşü yoxlayır. Hər çağırış class-ı cari dizayna PATCH edir (reviewStatus=UNDER_REVIEW qaydası).
    /// </summary>
    public async Task<ServiceResult> PreviewAsync(
        CardTemplateSnapshot tpl, string previewKey, string accountName, string barcodeValue,
        decimal points, int steps, int cycles, CancellationToken ct = default,
        decimal lifetimePoints = 0, string? pointsExpiryText = null, decimal? discountPercent = null)
    {
        if (!_isInitialized || _svc == null)
            return new ServiceResult(false, "Google Wallet service not initialized");

        var previewClassId = ClassId + "-preview";
        var desired = BuildDesiredClass(tpl, ResolveIssuerName(tpl), previewClassId, designLayout: true);
        desired.ReviewStatus = "UNDER_REVIEW";
        string classMsg;
        try
        {
            LoyaltyClass? existing = null;
            try { existing = await _svc.Loyaltyclass.Get(previewClassId).ExecuteAsync(ct); }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound) { existing = null; }

            if (existing == null)
            {
                var created = await _svc.Loyaltyclass.Insert(desired).ExecuteAsync(ct);
                classMsg = $"preview class yaradıldı ({created.ReviewStatus})";
            }
            else
            {
                var patched = await _svc.Loyaltyclass.Patch(desired, previewClassId).ExecuteAsync(ct);
                classMsg = $"preview class yeniləndi ({patched.ReviewStatus})";
            }
        }
        catch (Google.GoogleApiException ex)
        {
            _log?.LogWarning(ex, "[GW-PREVIEW] class failed. ClassId={ClassId}", previewClassId);
            return new ServiceResult(false, $"Google preview class xətası: {ex.Message}");
        }

        var objectId = $"{_issuerId}.{tpl.TenantId}.{tpl.LoyaltyProgramId}.preview-{previewKey}";
        var objRes = await CreateOrGetObjectByCardIdAsync(0, tpl, accountName, barcodeValue, points, steps, ct, 0, null, null, cycles,
            objectIdOverride: objectId, classIdOverride: previewClassId, updateIfExists: true,
            lifetimePointsOverride: lifetimePoints, pointsExpiryOverride: pointsExpiryText, discountOverride: discountPercent);
        if (!objRes.Success)
            return new ServiceResult(false, $"Google preview obyekt xətası: {objRes.Message}");

        var label = CardFieldCatalog.EffectiveUnit(tpl);
        var url = BuildSaveLink(objectId, previewClassId, "preview", accountName, (int)Math.Round(points), label);
        _log?.LogInformation("[GW-PREVIEW] ok. ClassId={ClassId} ObjectId={ObjectId} {Msg}", previewClassId, objectId, classMsg);
        return new ServiceResult(true, new { url, classId = previewClassId, objectId, classStatus = classMsg }, classMsg);
    }

    /// <summary>Real class-ın vəziyyəti (read-only): reviewStatus, başlıq, issuer, rəng, logo, sətir sayı.</summary>
    public async Task<ServiceResult> GetClassStatusAsync(CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
            return new ServiceResult(false, "Google Wallet service not initialized");
        try
        {
            var cls = await _svc.Loyaltyclass.Get(ClassId).ExecuteAsync(ct);
            var rowInfos = cls.ClassTemplateInfo?.CardTemplateOverride?.CardRowTemplateInfos ?? new List<CardRowTemplateInfo>();
            static string P(TemplateItem? it) => it?.FirstValue?.Fields?.FirstOrDefault()?.FieldPath ?? "?";
            var rows = rowInfos.Select(r =>
                r.OneItem != null ? P(r.OneItem.Item)
                : r.TwoItems != null ? $"{P(r.TwoItems.StartItem)} | {P(r.TwoItems.EndItem)}"
                : r.ThreeItems != null ? $"{P(r.ThreeItems.StartItem)} | {P(r.ThreeItems.MiddleItem)} | {P(r.ThreeItems.EndItem)}"
                : "?").ToList();
            return new ServiceResult(true, new
            {
                exists = true, classId = ClassId, reviewStatus = cls.ReviewStatus, programName = cls.ProgramName, issuerName = cls.IssuerName,
                hexBackgroundColor = cls.HexBackgroundColor, programLogo = cls.ProgramLogo?.SourceUri?.Uri, rowCount = rows.Count, rows
            });
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return new ServiceResult(true, new { exists = false, classId = ClassId });
        }
        catch (Exception ex)
        {
            return new ServiceResult(false, ex.Message);
        }
    }

    // =================== HELPERS ===================

    /// <summary>classTemplateInfo müqayisəsi üçün (null-lar atılır).</summary>
    private static string TemplateInfoJson(ClassTemplateInfo? info) => info == null ? "" :
        Newtonsoft.Json.JsonConvert.SerializeObject(info, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });


    private static bool IsHttps(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        return u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static string SignJwt(JObject payload, string privateKeyPem, string? kid)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.ToCharArray());

        var header = new JObject { ["alg"] = "RS256", ["typ"] = "JWT" };
        if (!string.IsNullOrWhiteSpace(kid)) header["kid"] = kid;

        static string B64(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var h = B64(Encoding.UTF8.GetBytes(header.ToString(Newtonsoft.Json.Formatting.None)));
        var p = B64(Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None)));
        var input = $"{h}.{p}";
        var sig = rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var s = B64(sig);
        return $"{input}.{s}";
    }

    private static LoyaltyDisplayState BuildDisplayState(CardTemplateSnapshot tpl, decimal currentPoints, int currentSteps)
    {
        var s = new LoyaltyDisplayState();

        switch (tpl.TemplateType)
        {
            case CardTemplateType.ProgressBased:
                {
                    int total = Math.Max(1, tpl.TotalSteps);
                    int cur = Math.Clamp(currentSteps, 0, total);
                    s.Current = cur; s.Total = total;
                    s.Header = $"{total} {tpl.StepLabel} = {tpl.RewardDescription}".Trim();
                    s.Body = (tpl.MinPointsToRedeem > 0) ? $"Redeem at {tpl.MinPointsToRedeem} pts" : "Earn points on every purchase";
                    s.CurrentIconUrl = PickProgressIcon(tpl.IconUrls, cur);
                    break;
                }
            case CardTemplateType.PointsBased:
                {
                    s.Current = currentPoints;
                    s.Total = Math.Max(0, tpl.MinPointsToRedeem ?? 0);
                    s.Header = $"Points Balance: {currentPoints}";
                    s.Body = (tpl.MinPointsToRedeem > 0) ? $"Redeem at {tpl.MinPointsToRedeem} pts" : "Earn points on every purchase";
                    s.CurrentIconUrl = PickTierIconByPoints(tpl, currentPoints, out var tierName1);
                    s.TierName = tierName1;
                    break;
                }
            case CardTemplateType.TierBased:
                {
                    s.Current = currentPoints; s.Total = 0;
                    s.CurrentIconUrl = PickTierIconByPoints(tpl, currentPoints, out var tierName2);
                    s.TierName = tierName2;
                    s.Header = $"Tier: {tierName2}";
                    s.Body = $"Points: {currentPoints}";
                    break;
                }
        }

        return s;
    }

    private static string? PickProgressIcon(List<string> icons, int curIndex)
    {
        if (icons == null || icons.Count == 0) return null;
        var i = Math.Clamp(curIndex, 0, icons.Count - 1);
        var url = icons[i]?.Trim();
        return IsHttps(url) ? url : null;
    }


    // ===== Progress-based helpers (Google Wallet) =====
    private readonly struct ProgressStats
    {
        public int CycleSteps { get; }
        public int TotalSteps { get; }
        public int FreeRewardCount { get; }

        public ProgressStats(int cycleSteps, int totalSteps, int freeRewardCount)
        {
            CycleSteps = cycleSteps;
            TotalSteps = totalSteps;
            FreeRewardCount = freeRewardCount;
        }
    }

    private static ProgressStats GetProgressStats(int currentSteps, int totalSteps)
    {
        var total = Math.Max(1, totalSteps);
        var steps = Math.Max(0, currentSteps);

        // Cycle step within [1..total] when steps > 0, otherwise 0
        var cycle = steps % total;
        if (cycle == 0 && steps > 0) cycle = total;

        var freeRewards = steps / total;
        return new ProgressStats(cycle, total, freeRewards);
    }

    // Picks icon based on TOTAL steps progression (supports multiple cycles).
    // Expected icon list convention:
    //   index 0   -> 0 steps
    //   index 1..total -> intermediate (and total steps = "completed") icons
    // If icon list is shorter, we clamp.
    private static string? PickProgressIconBySteps(List<string> icons, int currentSteps, int totalSteps)
    {
        if (icons == null || icons.Count == 0) return null;

        var total = Math.Max(1, totalSteps);
        var steps = Math.Max(0, currentSteps);

        int iconIndex;
        if (steps == 0)
        {
            iconIndex = 0;
        }
        else
        {
            var remainder = steps % total;
            iconIndex = remainder == 0 ? total : remainder;
        }

        iconIndex = Math.Clamp(iconIndex, 0, icons.Count - 1);
        var url = icons[iconIndex]?.Trim();
        return IsHttps(url) ? url : null;
    }

    private static TextModuleData UpsertTextModule(List<TextModuleData> list, string id, string header, string body)
    {
        var item = list.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
        if (item == null)
        {
            item = new TextModuleData { Id = id };
            list.Add(item);
        }

        item.Header = header;
        item.Body = body;
        return item;
    }


    /// <summary>
    /// Class sətirləri (Faza 2, data-driven): CardFieldCatalog.GoogleRows — Header+Primary → 1, Secondary → 2, Auxiliary → 3 (hər biri ≤3 element),
    /// banner Top → image modulu 1-ci sətirdə; barkod native (sətir tutmur); Back/yerləşməyən modullar detallarda. Heç sahə yoxdursa barkod sətri.
    /// designLayout=false → default yerləşmə (flag bağlı, runtime insert).
    /// </summary>
    private static ClassTemplateInfo BuildClassTemplateInfo(CardTemplateSnapshot tpl, bool designLayout)
    {
        var (fields, banner, _, legacy) = LayoutFor(tpl, designLayout);
        // Sahələr toxunulmayıbsa → bugünkü sətirlər (prod class-larla eyni); dəyişilibsə → törəmə qayda
        var rows = legacy ? CardFieldCatalog.LegacyGoogleRows(tpl.TemplateType) : CardFieldCatalog.GoogleRows(fields, banner, out _);

        string PathOf(string id) => id == CardFieldCatalog.GoogleImageRow
            ? "object.imageModulesData[0].mainImage"
            : id == CardFieldCatalog.GoogleBarcodeRow
                ? "object.barcode"
                : $"object.textModulesData['{CardFieldCatalog.GoogleModuleId(id, tpl.TemplateType)}']";
        static TemplateItem ItemOf(string path) => new TemplateItem
        {
            FirstValue = new FieldSelector { Fields = new List<FieldReference> { new FieldReference { FieldPath = path } } }
        };

        var rowInfos = new List<CardRowTemplateInfo>();
        foreach (var r in rows)
        {
            rowInfos.Add(r.Count switch
            {
                1 => new CardRowTemplateInfo { OneItem = new CardRowOneItem { Item = ItemOf(PathOf(r[0])) } },
                2 => new CardRowTemplateInfo { TwoItems = new CardRowTwoItems { StartItem = ItemOf(PathOf(r[0])), EndItem = ItemOf(PathOf(r[1])) } },
                _ => new CardRowTemplateInfo { ThreeItems = new CardRowThreeItems { StartItem = ItemOf(PathOf(r[0])), MiddleItem = ItemOf(PathOf(r[1])), EndItem = ItemOf(PathOf(r[2])) } },
            });
        }
        if (rowInfos.Count == 0)
            rowInfos.Add(new CardRowTemplateInfo { OneItem = new CardRowOneItem { Item = ItemOf("object.barcode") } });

        // Siyahı (collapsed) görünüşünün 1-ci sətri: müştəri adı görünürsə o, yoxsa ilk görünən sahə
        var visible = fields.Where(f => f.Slot != CardFieldSlot.Hidden).ToList();
        var listField = visible.FirstOrDefault(f => f.Id == CardFieldCatalog.CustomerName)
                        ?? visible.FirstOrDefault(f => f.Slot != CardFieldSlot.Back)
                        ?? visible.FirstOrDefault();

        var info = new ClassTemplateInfo
        {
            CardTemplateOverride = new CardTemplateOverride { CardRowTemplateInfos = rowInfos }
        };
        if (listField != null)
            info.ListTemplateOverride = new ListTemplateOverride
            {
                FirstRowOption = new FirstRowOption
                {
                    FieldOption = new FieldSelector { Fields = new List<FieldReference> { new FieldReference { FieldPath = PathOf(listField.Id) } } }
                }
            };
        return info;
    }

    // =================== FAZA 2 — sahə layout köməkçiləri ===================
    /// <summary>Admin layout-u Google-a yalnız preview-də və ya CardDesign:GoogleClassUpdateEnabled açıqkən gedir (class ↔ obyekt sinxron qalsın).</summary>
    private static bool DesignLayoutEnabled(bool isPreview) => isPreview || CardDesignFeatureFlags.GoogleClassUpdateEnabled;

    /// <summary>
    /// Google layout mənbəyi. Legacy = true → bugünkü sətirlər/modullar (image modulu Top): flag bağlıdır VƏ YA admin "Sahələr"-i dəyişməyib
    /// (Fields boş + GoogleBanner null). Barkod tipi flag/preview ilə dizayndan, əks halda Code128.
    /// </summary>
    private static (List<CardFieldSetting> Fields, GoogleBannerPosition Banner, BarcodeType Barcode, bool Legacy) LayoutFor(CardTemplateSnapshot tpl, bool designLayout)
    {
        var d = tpl.Design ?? new CardDesignSettings();
        if (!designLayout)
            return (CardFieldCatalog.DefaultFields(tpl.TemplateType), GoogleBannerPosition.Top, BarcodeType.Code128, true);
        if (CardFieldCatalog.IsLegacyGoogleLayout(d))
            return (CardFieldCatalog.DefaultFields(tpl.TemplateType), GoogleBannerPosition.Top, d.EffectiveBarcode, true);
        return (d.EffectiveFields(tpl.TemplateType), d.EffectiveGoogleBanner(tpl.TemplateType), d.EffectiveBarcode, false);
    }

    /// <summary>Sahə modulları: görünən sahə → upsert (id kataloqdan), gizli/boş → silinir, legacy id-lər silinir.</summary>
    private static void ApplyFieldModules(List<TextModuleData> list, CardTemplateSnapshot tpl, List<CardFieldSetting> fields,
        Dictionary<string, CardFieldValue> values, decimal displayPoints)
    {
        foreach (var id in CardFieldCatalog.AllIds)
        {
            var moduleId = CardFieldCatalog.GoogleModuleId(id, tpl.TemplateType);
            var f = fields.FirstOrDefault(x => x.Id == id);
            if (f == null || f.Slot == CardFieldSlot.Hidden || !values.TryGetValue(id, out var v) || string.IsNullOrWhiteSpace(v.Value))
            {
                list.RemoveAll(t => t.Id == moduleId);
                continue;
            }
            var m = list.FirstOrDefault(t => t.Id == moduleId);
            if (m == null) { m = new TextModuleData { Id = moduleId }; list.Add(m); }
            m.Header = CardFieldCatalog.EffectiveLabel(f, v, tpl, displayPoints) ?? "";
            m.Body = v.Value;
        }
        foreach (var legacy in CardFieldCatalog.LegacyModuleIds)
            list.RemoveAll(t => t.Id == legacy);
    }

    private static Image BuildHero(string url) => new Image
    {
        SourceUri = new ImageUri { Uri = url, Description = "Banner" },
        ContentDescription = new LocalizedString { DefaultValue = new TranslatedString { Language = "en-US", Value = "\u200B" } }
    };

    /// <summary>Ümumi xal / bitmə tarixi / kassa endirimi — yalnız sahə görünəndə DB-dən (suspensiyada maskalanır); preview override ilə.</summary>
    private async Task<(decimal Lifetime, string? ExpiryText, decimal? Discount)> LoadExtrasAsync(int loyaltyCardId, CardTemplateSnapshot tpl, List<CardFieldSetting> fields,
        decimal? lifetimeOverride, string? expiryOverride, decimal? discountOverride, CancellationToken ct)
    {
        var needLifetime = CardFieldCatalog.IsVisible(fields, CardFieldCatalog.LifetimePoints);
        var needExpiry = CardFieldCatalog.IsVisible(fields, CardFieldCatalog.PointsExpiry);
        var needDiscount = CardFieldCatalog.IsVisible(fields, CardFieldCatalog.Discount);
        if (!needLifetime && !needExpiry && !needDiscount) return (0, null, null);
        if (lifetimeOverride.HasValue || expiryOverride != null || discountOverride.HasValue || loyaltyCardId <= 0)
            return (lifetimeOverride ?? 0, expiryOverride, discountOverride);

        decimal lifetime = 0; System.DateTime? expiresAt = null; decimal? discount = null;
        if (_cardRepo != null)
        {
            try
            {
                // GetById Customer-i include edir (endirim faizi müştəridədir)
                var card = await _cardRepo.GetById(loyaltyCardId, ct);
                if (card != null)
                {
                    lifetime = card.LifetimePoints; expiresAt = card.ExpiresAt;
                    if (card.Customer != null)
                        discount = PosDiscount.Percent(card.Customer.PosGuestCategoryId, card.Customer.PosGuestCategoryName, card.Customer.PosDiscountPercent);
                }
            }
            catch (Exception ex) { _log?.LogWarning(ex, "[GW] extras load failed. CardId={CardId}", loyaltyCardId); }
        }
        if (await IsSuspendedAsync(tpl.TenantId, ct)) { lifetime = 0; expiresAt = null; discount = null; }
        var text = needExpiry ? await CardFieldCatalog.FormatExpiryAsync(_timeZoneService, tpl.TenantId, expiresAt, ct) : null;
        return (needLifetime ? lifetime : 0, text, needDiscount ? discount : null);
    }

    private const string BalanceMessagePrefix = "balance-";

    /// <summary>
    /// Balans dəyişmə bildirişi: köhnə balans mesajları silinir (obyekt şişməsin), yenisi TEXT_AND_NOTIFY ilə əlavə olunur.
    /// Google ~3 bildiriş/24 saat/kart limitindən artığı səssiz mesaj kimi qalır. Xəta udulur — PATCH artıq uğurludur.
    /// </summary>
    private async Task NotifyBalanceChangeAsync(string objectId, IList<Message>? existingMessages, CardTemplateSnapshot tpl, string balanceText, CancellationToken ct)
    {
        try
        {
            if (existingMessages?.Any(m => m.Id?.StartsWith(BalanceMessagePrefix) == true) == true)
            {
                var kept = existingMessages.Where(m => m.Id?.StartsWith(BalanceMessagePrefix) != true).ToList();
                await _svc!.Loyaltyobject.Patch(new LoyaltyObject { Messages = kept }, objectId).ExecuteAsync(ct);
            }

            var body = tpl.Design!.EffectiveBalanceChangeText.Replace(CardDesignSettings.ChangeValuePlaceholder, balanceText);
            var title = string.IsNullOrWhiteSpace(tpl.Design.Title) ? (tpl.ProgramName ?? "") : tpl.Design.Title!;
            var start = System.DateTime.UtcNow;
            await _svc!.Loyaltyobject.Addmessage(new AddMessageRequest
            {
                Message = new Message
                {
                    Id = $"{BalanceMessagePrefix}{Guid.NewGuid():N}",
                    Header = title,
                    Body = body,
                    MessageType = "TEXT_AND_NOTIFY",
                    DisplayInterval = new TimeInterval
                    {
                        Start = new Google.Apis.Walletobjects.v1.Data.DateTime { Date = start.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        End = new Google.Apis.Walletobjects.v1.Data.DateTime { Date = start.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
                    }
                }
            }, objectId).ExecuteAsync(ct);
            _log?.LogInformation("[GW] balance change message added. ObjectId={ObjectId}", objectId);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "[GW] balance change message failed. ObjectId={ObjectId}", objectId);
        }
    }

    private async Task<bool> IsSuspendedAsync(int tenantId, CancellationToken ct)
    {
        if (_suspensionGuard == null) return false;
        try { return (await _suspensionGuard.GetAsync(tenantId, ct)).IsSuspended; }
        catch { return false; /* mask best-effort */ }
    }

    private static string? PickTierIconByPoints(CardTemplateSnapshot tpl, decimal pts, out string tier)
    {
        tier = "Bronze";
        string? icon = IsHttps(tpl.BronzeIconUrl) ? tpl.BronzeIconUrl : null;

        if (tpl.SilverThreshold.HasValue && pts >= tpl.SilverThreshold.Value)
        {
            tier = "Silver";
            icon = IsHttps(tpl.SilverIconUrl) ? tpl.SilverIconUrl : icon;
        }

        if (tpl.GoldThreshold.HasValue && pts >= tpl.GoldThreshold.Value)
        {
            tier = "Gold";
            icon = IsHttps(tpl.GoldIconUrl) ? tpl.GoldIconUrl : icon;
        }

        if (tpl.PlatinumThreshold.HasValue && pts >= tpl.PlatinumThreshold.Value)
        {
            tier = "Platinum";
            icon = IsHttps(tpl.PlatinumIconUrl) ? tpl.PlatinumIconUrl : icon;
        }

        return icon;
    }

    private static int GetCashbackPercent(decimal currentPoints, CardTemplateSnapshot snapshot)
    {
        // PointsBased: tier ladder yoxdur — həmişə Bronze%
        if (snapshot.TemplateType == CardTemplateType.PointsBased)
            return snapshot.BronzePercent ?? 0;

        var bronzeTh = snapshot.BronzeThreshold ?? 0;
        var silverTh = snapshot.SilverThreshold ?? int.MaxValue;
        var goldTh = snapshot.GoldThreshold ?? int.MaxValue;
        var platTh = snapshot.PlatinumThreshold ?? int.MaxValue;

        var bronzePct = snapshot.BronzePercent ?? 0;
        var silverPct = snapshot.SilverPercent ?? bronzePct;
        var goldPct = snapshot.GoldPercent ?? silverPct;
        var platPct = snapshot.PlatinumPercent ?? goldPct;

        if (currentPoints < silverTh)
            return bronzePct;
        else if (currentPoints < goldTh)
            return silverPct;
        else if (currentPoints < platTh)
            return goldPct;
        else
            return platPct;
    }
    private static int GetCashbackPercentCash(decimal currentPoints, CardTemplateSnapshot snapshot)
    {
        // PointsBased: tier ladder yoxdur — həmişə Bronze cash%
        if (snapshot.TemplateType == CardTemplateType.PointsBased)
            return snapshot.BronzePercentCash ?? 0;

        var bronzeTh = snapshot.BronzeThreshold ?? 0;
        var silverTh = snapshot.SilverThreshold ?? int.MaxValue;
        var goldTh = snapshot.GoldThreshold ?? int.MaxValue;
        var platTh = snapshot.PlatinumThreshold ?? int.MaxValue;

        var bronzePct = snapshot.BronzePercentCash ?? 0;
        var silverPct = snapshot.SilverPercentCash ?? bronzePct;
        var goldPct = snapshot.GoldPercentCash ?? silverPct;
        var platPct = snapshot.PlatinumPercentCash ?? goldPct;

        if (currentPoints < silverTh)
            return bronzePct;
        else if (currentPoints < goldTh)
            return silverPct;
        else if (currentPoints < platTh)
            return goldPct;
        else
            return platPct;
    }

    public Task<ServiceResult> RedeemAsync(string scannedCode, int pointsDelta = 1, CancellationToken ct = default)
        => Task.FromResult(new ServiceResult(false, "Use RedeemByCardIdAsync with snapshot."));
}
