using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Walletobjects.v1;
using Google.Apis.Walletobjects.v1.Data;
using Loyalty.Business.Dtos.Post.Main;
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
    // Placeholder fallback (public, HTTPS, 3:1 ~ 1032x336/1860x620 və s.)
    private const string FALLBACK_BANNER_URL = "https://yourcdn.example.com/fallback-1032x336.png";

    public GoogleWalletService(GoogleWalletRuntimeConfig cfg, ILogger<GoogleWalletService>? log = null, ILoyaltyCardRepository? cardRepo = null, Loyalty.DataAccess.Repositories.Abstractions.Main.IBranchRepository? branchRepo = null)
    {
        _log = log;
        _cardRepo = cardRepo;
        _branchRepo = branchRepo;
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
        !string.IsNullOrWhiteSpace(tpl.TenantCompanyName) ? tpl.TenantCompanyName!
        : !string.IsNullOrWhiteSpace(tpl.ProgramName) ? tpl.ProgramName
        : tpl.TemplateName;

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

        LoyaltyClass BuildDesired()
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
                Id = ClassId,
                ProgramName = tpl.TemplateName,        // "Pubzade"
                IssuerName = programName,
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

                ClassTemplateInfo = BuildClassTemplateInfo(tpl)
            };

            return cls;
        }

        if (existing is null)
        {
            var toCreate = BuildDesired();
            toCreate.ReviewStatus = "UNDER_REVIEW"; // initial submit only — Console must approve before push fires for real users.
            await _svc.Loyaltyclass.Insert(toCreate).ExecuteAsync(ct);
            _log?.LogInformation("Google Wallet class CREATED: {ClassId} (review status UNDER_REVIEW — approve in Google Pay Console for production push)", ClassId);
        }
        else
        {
            // Do NOT touch ReviewStatus on update — would knock an APPROVED class back to UNDER_REVIEW
            // and silently block push notifications for production users.
            var toUpdate = BuildDesired();
            await _svc.Loyaltyclass.Update(toUpdate, ClassId).ExecuteAsync(ct);
            _log?.LogInformation("Google Wallet class UPDATED: {ClassId}", ClassId);
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
         System.DateTime? lotteryDrawDate = null)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping CreateOrGetObject. CardId={CardId}", loyaltyCardId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
            await EnsureClassAsync(tpl, programName: ResolveIssuerName(tpl), ct);

            var objectId = ObjectIdForCard(_issuerId, tpl.TenantId, tpl.LoyaltyProgramId, loyaltyCardId);

            bool objectExists = false;
            try
            {
                var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);
                objectExists = true;

                // Object exists but balance may be stale - update it via RedeemByCardIdAsync
                _log?.LogInformation("GW object already exists, updating balance. ObjectId={ObjectId}, currentPoints={CurrentPoints}", objectId, currentPoints);
                return await RedeemByCardIdAsync(loyaltyCardId, tpl, currentPoints, currentSteps, 0, accountName, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // create new
            }

            var state = BuildDisplayState(tpl, currentPoints, currentSteps);
            var barcode = string.IsNullOrWhiteSpace(barcodeValue) ? loyaltyCardId.ToString() : barcodeValue;

            // ProgressBased vs TierBased/PointsBased logic
            bool isProgressBased = tpl.TemplateType == CardTemplateType.ProgressBased;

            string? bannerUrl = null;
            List<TextModuleData> textModules = new();
            List<ImageModuleData> imageModules = new();

            if (isProgressBased)
            {
                // ProgressBased Google Wallet Layout:
                // 1. Large "Reward Card" title
                // 2. Free reward label + big number (use CompletedCycles from card)
                // 3. Barcode (centered)
                // 4. Reward description (centered)
                // 5. Progress icons grid at bottom (as image module)

                // Get CompletedCycles from card if available, otherwise calculate from steps
                int completedCycles = 0;
                if (_cardRepo != null)
                {
                    try
                    {
                        var card = await _cardRepo.GetByIdAsync(loyaltyCardId, ct);
                        if (card != null)
                        {
                            completedCycles = card.CompletedCycles;
                        }
                        else
                        {
                            completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                        }
                    }
                    catch
                    {
                        completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                }
                else
                {
                    // Fallback: calculate from steps
                    completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                }

                string freeDrinkValue = completedCycles == 0 ? "0" : completedCycles.ToString();

                // Get FreeRewardLabel from template (default: "bonus" instead of "Pulsuz içki")
                string freeRewardLabel = string.IsNullOrWhiteSpace(tpl.FreeRewardLabel)
                    ? "bonus"
                    : tpl.FreeRewardLabel;

                // 1) Large "Reward Card" title
                textModules.Add(new TextModuleData { Id = "rewardCard", Header = "", Body = accountName });

                // 2) Free reward label + big number (use FreeRewardLabel from template)
                textModules.Add(new TextModuleData { Id = "pulsuzIcki", Header = freeRewardLabel, Body = freeDrinkValue });

                // 3) Reward description (centered, below barcode)
                if (!string.IsNullOrWhiteSpace(tpl.RewardDescription))
                {
                    textModules.Add(new TextModuleData { Id = "rewardDesc", Header = "", Body = tpl.RewardDescription });
                }

                // 4) Progress icons grid at bottom (as main image)
                // Use progress icon for the grid visual
                bannerUrl = PickProgressIconBySteps(tpl.IconUrls, currentSteps, tpl.TotalSteps);
                if (!IsHttps(bannerUrl) && IsHttps(tpl.HeroImageUrl)) bannerUrl = tpl.HeroImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.StripImageUrl)) bannerUrl = tpl.StripImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
                if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;
            }
            else
            {
                // TierBased/PointsBased: Keep existing logic
                var percent = GetCashbackPercent(currentPoints, tpl);
                var percentCash = GetCashbackPercentCash(currentPoints, tpl);

                string cashback;
                string cashbackText;
                if (tpl.PayMentType)
                {
                    cashback = $"Cashback:{percent}%";
                    cashbackText = $" Nağd ödənişdə {percentCash}%";
                }
                else
                {
                    cashback = "Cashback";
                    cashbackText = $"{percent}%";
                }

                // Banner URL fallback (hero -> tier icon -> logo -> placeholder)
                bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
                if (!IsHttps(bannerUrl) && IsHttps(state.CurrentIconUrl)) bannerUrl = state.CurrentIconUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
                if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;

                textModules.Add(new TextModuleData { Id = "customer", Header = "Customer", Body = accountName ?? $"Card #{loyaltyCardId}" });
                var currency = string.IsNullOrWhiteSpace(tpl.Currency) ? "AZN" : tpl.Currency;
                textModules.Add(new TextModuleData { Id = "balance", Header = "BALANCE", Body = currentPoints.ToString() + " " + currency });
                textModules.Add(new TextModuleData { Id = "cashback", Header = cashback, Body = cashbackText });
            }

            _log?.LogInformation("GW Create: bannerUrl={Url} hero={Hero} icon={Icon} logo={Logo} isProgress={IsProgress}",
                bannerUrl, tpl.HeroImageUrl, state.CurrentIconUrl, tpl.GoogleLogoUrl, isProgressBased);
            textModules.Add(new TextModuleData
            {
                Id = "about",
                Header = "Walvero Loyallıq",
                Body = "Loyallıq modulu müştəri bağlılığını artırmaq və təkrar alış-verişləri təşviq etmək üçün hazırlanmışdır..."
            });

            textModules.Add(new TextModuleData
            {
                Id = "features",
                Header = "Əsas üstünlüklər",
                Body =
                    "• Fərdiləşdirilmiş mükafatlar\n" +
                    "• Avtomatlaşdırılmış izləmə\n" +
                    "• Müştəri iştirakının artırılması\n" +
                    "• Strateji analizlər"
            });

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
                ClassId = ClassId,
                State = "ACTIVE",
                AccountId = loyaltyCardId.ToString(),
                AccountName = accountName ?? string.Empty,

                // Only set LoyaltyPoints for non-ProgressBased
                LoyaltyPoints = isProgressBased ? null : new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = (int)Math.Round(currentPoints) },
                    Label = string.IsNullOrWhiteSpace(tpl.Currency) ? "Points" : tpl.Currency
                },
                SecondaryLoyaltyPoints = secondaryLoyaltyPoints,

                TextModulesData = textModules,
                LinksModuleData = new LinksModuleData
                {
                    Uris = new List<Google.Apis.Walletobjects.v1.Data.Uri>
        {
            new Google.Apis.Walletobjects.v1.Data.Uri
            {
                Id = "website",
                UriValue = "https://www.walvero.com",
                Description = "Learn More"
            }
        }
                },

                Barcode = new Barcode { Type = "CODE_128", Value = barcode, AlternateText = barcode },

                // Image modules: banner/grid for ProgressBased, banner for TierBased
                ImageModulesData = new List<ImageModuleData>
                {
                    new ImageModuleData
                    {
                        MainImage = new Image
                        {
                            SourceUri = new ImageUri { Uri = bannerUrl, Description = isProgressBased ? "Progress Icons Grid" : "Banner" },
                            ContentDescription = new LocalizedString
                            {
                                DefaultValue = new TranslatedString { Language = "en-US", Value = "\u200B" }
                            }
                        }
                    }
                },
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
            var created = await _svc.Loyaltyobject.Insert(obj).ExecuteAsync(ct);
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

    public async Task<ServiceResult> AddMessageToObjectAsync(string objectId, string title, string body, CancellationToken ct = default)
    {
        if (!_isInitialized || _svc == null)
        {
            _log?.LogWarning("Google Wallet service not initialized. Skipping AddMessage. ObjectId={ObjectId}", objectId);
            return new ServiceResult(false, "Google Wallet service not initialized");
        }

        try
        {
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
                    MessageType = "TEXT",
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
       System.DateTime? lotteryDrawDate = null)
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

            var existingBalance = existing?.LoyaltyPoints?.Balance?.Int__;
            _log?.LogWarning("GW PATCH: CardId={CardId}, existingGoogleBalance={ExistBal}, newBalance={NewBal}, delta={Delta}, isProgress={IsProgress}, objectId={ObjId}",
                loyaltyCardId, existingBalance, nextPoints, delta, isProgressBased, objectId);

            var textList = existing.TextModulesData?.ToList() ?? new List<TextModuleData>();

            string? bannerUrl = null;

            if (isProgressBased)
            {
                // ProgressBased Google Wallet Layout (same as Create):
                // 1. Large "Reward Card" title
                // 2. Free reward label + big number (use CompletedCycles from card)
                // 3. Reward description
                // 4. Progress icons grid at bottom

                // Get CompletedCycles from card if available, otherwise calculate from steps
                int completedCycles = 0;
                if (_cardRepo != null)
                {
                    try
                    {
                        var card = await _cardRepo.GetById(loyaltyCardId, ct);
                        if (card != null)
                        {
                            completedCycles = card.CompletedCycles;
                        }
                        else
                        {
                            completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                        }
                    }
                    catch
                    {
                        completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                    }
                }
                else
                {
                    // Fallback: calculate from steps
                    completedCycles = CalculateFreeDrinkCount(currentSteps, tpl.TotalSteps);
                }
                string freeDrinkValue = completedCycles == 0 ? "0" : completedCycles.ToString();

                // Get FreeRewardLabel from template (default: "bonus" instead of "Pulsuz içki")
                string freeRewardLabel = string.IsNullOrWhiteSpace(tpl.FreeRewardLabel)
                    ? "bonus"
                    : tpl.FreeRewardLabel;

                // 1) Customer name (was: hard-coded "Reward Card" placeholder).
                var rewardCard = textList.FirstOrDefault(t => t.Id == "rewardCard");
                if (rewardCard == null)
                {
                    rewardCard = new TextModuleData { Id = "rewardCard" };
                    textList.Add(rewardCard);
                }
                rewardCard.Header = "";
                rewardCard.Body = accountName;

                // 2) Free reward label + big number (use FreeRewardLabel from template)
                var pulsuzIcki = textList.FirstOrDefault(t => t.Id == "pulsuzIcki");
                if (pulsuzIcki == null)
                {
                    pulsuzIcki = new TextModuleData { Id = "pulsuzIcki" };
                    textList.Add(pulsuzIcki);
                }
                pulsuzIcki.Header = freeRewardLabel;
                pulsuzIcki.Body = freeDrinkValue;

                // 3) Reward description
                if (!string.IsNullOrWhiteSpace(tpl.RewardDescription))
                {
                    var rewardDesc = textList.FirstOrDefault(t => t.Id == "rewardDesc");
                    if (rewardDesc == null)
                    {
                        rewardDesc = new TextModuleData { Id = "rewardDesc" };
                        textList.Add(rewardDesc);
                    }
                    rewardDesc.Header = "";
                    rewardDesc.Body = tpl.RewardDescription;
                }

                // 4) Progress icons grid at bottom
                bannerUrl = PickProgressIconBySteps(tpl.IconUrls, currentSteps, tpl.TotalSteps);
                if (!IsHttps(bannerUrl) && IsHttps(tpl.HeroImageUrl)) bannerUrl = tpl.HeroImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.StripImageUrl)) bannerUrl = tpl.StripImageUrl;
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
                if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;
            }
            else
            {
                // TierBased/PointsBased: Keep existing logic
                var percent = GetCashbackPercent(currentPoints, tpl);
                var percentCash = GetCashbackPercentCash(currentPoints, tpl);

                string cashback;
                string cashbackText;
                if (tpl.PayMentType)
                {
                    cashback = $"Cashback:{percent}%";
                    cashbackText = $" Nağd ödənişdə {percentCash}%";
                }
                else
                {
                    cashback = "Cashback";
                    cashbackText = $"{percent}%";
                }

                // customer — always update so renamed customers reflect on the pass.
                var customer = textList.FirstOrDefault(t => t.Id == "customer");
                if (customer == null)
                {
                    customer = new TextModuleData { Id = "customer" };
                    textList.Add(customer);
                }
                customer.Header = "Customer";
                customer.Body = accountName;

                // balance
                var balance = textList.FirstOrDefault(t => t.Id == "balance");
                if (balance == null)
                {
                    balance = new TextModuleData { Id = "balance" };
                    textList.Add(balance);
                }
                balance.Header = "BALANCE";
                var currency = string.IsNullOrWhiteSpace(tpl.Currency) ? "AZN" : tpl.Currency;
                balance.Body = nextPoints.ToString() + " " + currency;

                // cashback
                var cashbackId = textList.FirstOrDefault(t => t.Id == "cashback");
                if (cashbackId == null)
                {
                    cashbackId = new TextModuleData { Id = "cashback" };
                    textList.Add(cashbackId);
                }
                cashbackId.Header = cashback;
                cashbackId.Body = cashbackText;

                // FIX B: Do NOT use PickTierIconByPoints for ProgressBased (already handled above)
                // Redeem zamanı da banner fallback (hero -> tier icon -> logo -> placeholder)
                bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
                if (!IsHttps(bannerUrl))
                {
                    var fallbackIcon = PickTierIconByPoints(tpl, nextPoints, out _);
                    if (IsHttps(fallbackIcon)) bannerUrl = fallbackIcon;
                }
                if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
                if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;
            }

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

            var imageModules = new List<ImageModuleData>
            {
                new ImageModuleData
                {
                    MainImage = new Image
                    {
                        SourceUri = new ImageUri { Uri = bannerUrl, Description = isProgressBased ? "Progress Icons Grid" : "Banner" },
                        ContentDescription = new LocalizedString
                        {
                            DefaultValue = new TranslatedString { Language = "en-US", Value = "\u200B" }
                        }
                    }
                }
            };

            var patch = new LoyaltyObject
            {
                // Only set LoyaltyPoints for non-ProgressBased
                // FIX: Label must be "Points" to match class definition, otherwise Google API silently ignores PATCH
                LoyaltyPoints = isProgressBased ? null : new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = (int)Math.Round(nextPoints) },
                    Label = string.IsNullOrWhiteSpace(tpl.Currency) ? "Points" : tpl.Currency
                },
                SecondaryLoyaltyPoints = secondaryLoyaltyPoints,
                TextModulesData = textList,
                ImageModulesData = imageModules,
                // Account fields shown in collapsed/list view of Google Wallet.
                AccountId = loyaltyCardId.ToString(),
                AccountName = accountName
            };

            var patched = await _svc.Loyaltyobject.Patch(patch, objectId)
                .ExecuteAsync(ct);

            _log?.LogInformation("Google Wallet object PATCHED (redeem): {ObjectId} (banner={Banner}) isProgress={IsProgress} lotteryTickets={LotteryTickets}",
                objectId, bannerUrl, isProgressBased, lotteryTickets);

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
            if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;

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

                ImageModulesData = new List<ImageModuleData>
                {
                    new ImageModuleData
                    {
                        MainImage = new Image
                        {
                            SourceUri = new ImageUri { Uri = bannerUrl, Description = "Banner" },
                            ContentDescription = new LocalizedString
                            {
                                DefaultValue = new TranslatedString { Language = "en-US", Value = "\u200B" }
                            }
                        }
                    }
                },
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

            var loyaltyObject = new JObject
            {
                ["id"] = objectId,
                ["classId"] = ClassId,
                ["state"] = "ACTIVE",
                ["accountId"] = loyaltyCardId.ToString(),
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
            return new ServiceResult(true, url);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "GenerateSaveLinkByCardIdAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }

    // =================== HELPERS ===================

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


    private static ClassTemplateInfo BuildClassTemplateInfo(CardTemplateSnapshot tpl)
    {
        bool isProgressBased = tpl.TemplateType == CardTemplateType.ProgressBased;

        if (isProgressBased)
        {
            // ProgressBased Google Wallet Layout:
            // 1. Logo + program name (handled by ProgramLogo and IssuerName in class)
            // 2. Large "Reward Card" title
            // 3. "Pulsuz içki" label + big number (left side)
            // 4. Barcode (centered)
            // 5. Reward description (centered)
            // 6. Progress icons grid at bottom
            return new ClassTemplateInfo
            {
                CardTemplateOverride = new CardTemplateOverride
                {
                    CardRowTemplateInfos = new List<CardRowTemplateInfo>
                    {
                        // 1) Large "Reward Card" title
                        new CardRowTemplateInfo
                        {
                            OneItem = new CardRowOneItem
                            {
                                Item = new TemplateItem
                                {
                                    FirstValue = new FieldSelector
                                    {
                                        Fields = new List<FieldReference>
                                        {
                                            new FieldReference { FieldPath = "object.textModulesData['rewardCard']" }
                                        }
                                    }
                                }
                            }
                        },
                        // 2) "Pulsuz içki" label + big number (left side)
                        new CardRowTemplateInfo
                        {
                            OneItem = new CardRowOneItem
                            {
                                Item = new TemplateItem
                                {
                                    FirstValue = new FieldSelector
                                    {
                                        Fields = new List<FieldReference>
                                        {
                                            new FieldReference { FieldPath = "object.textModulesData['pulsuzIcki']" }
                                        }
                                    }
                                }
                            }
                        },
                        // 3) Barcode (centered)
                        new CardRowTemplateInfo
                        {
                            OneItem = new CardRowOneItem
                            {
                                Item = new TemplateItem
                                {
                                    FirstValue = new FieldSelector
                                    {
                                        Fields = new List<FieldReference>
                                        {
                                            new FieldReference { FieldPath = "object.barcode" }
                                        }
                                    }
                                }
                            }
                        },
                        // 4) Reward description (centered)
                        new CardRowTemplateInfo
                        {
                            OneItem = new CardRowOneItem
                            {
                                Item = new TemplateItem
                                {
                                    FirstValue = new FieldSelector
                                    {
                                        Fields = new List<FieldReference>
                                        {
                                            new FieldReference { FieldPath = "object.textModulesData['rewardDesc']" }
                        }
                    }
                                }
                            }
                        },
                        // 5) Progress icons grid at bottom
                        new CardRowTemplateInfo
                        {
                            OneItem = new CardRowOneItem
                            {
                                Item = new TemplateItem
                                {
                                    FirstValue = new FieldSelector
                                    {
                                        Fields = new List<FieldReference>
                                        {
                                            new FieldReference { FieldPath = "object.imageModulesData[0].mainImage" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                ListTemplateOverride = new ListTemplateOverride
                {
                    FirstRowOption = new FirstRowOption
                    {
                        FieldOption = new FieldSelector
                        {
                            Fields = new List<FieldReference>
                            {
                                new FieldReference { FieldPath = "object.textModulesData['rewardCard']" }
                            }
                        }
                    }
                }
            };
        }
        else
        {
            // TierBased/PointsBased: Keep existing template
            return new ClassTemplateInfo
            {
                CardTemplateOverride = new CardTemplateOverride
                {
                    CardRowTemplateInfos = new List<CardRowTemplateInfo>
                        {
                            // 1) Banner — OBYEKT-dən oxunur
                            new CardRowTemplateInfo
                            {
                                OneItem = new CardRowOneItem
                                {
                                    Item = new TemplateItem
                                    {
                                        FirstValue = new FieldSelector
                                        {
                                            Fields = new List<FieldReference>
                                            {
                                                new FieldReference { FieldPath = "object.imageModulesData[0].mainImage" }
                                            }
                                        }
                                    }
                                }
                            },
                            // 2) customer + balance
                            new CardRowTemplateInfo
                            {
                                TwoItems = new CardRowTwoItems
                                {
                                    StartItem = new TemplateItem
                                    {
                                        FirstValue = new FieldSelector
                                        {
                                            Fields = new List<FieldReference>
                                            {
                                                new FieldReference { FieldPath = "object.textModulesData['customer']" }
                                            }
                                        }
                                    },
                                    EndItem = new TemplateItem
                                    {
                                        FirstValue = new FieldSelector
                                        {
                                            Fields = new List<FieldReference>
                                            {
                                                new FieldReference { FieldPath = "object.textModulesData['balance']" }
                                            }
                                        }
                                    }
                                }
                            },
                            // 3) cashback
                            new CardRowTemplateInfo
                            {
                                TwoItems = new CardRowTwoItems
                                {
                                    StartItem = new TemplateItem
                                    {
                                        FirstValue = new FieldSelector
                                        {
                                            Fields = new List<FieldReference>
                                            {
                                                new FieldReference { FieldPath = "object.textModulesData['cashback']" }
                                            }
                                        }
                                    }
                                }
                            },
                            // 4) barcode
                            new CardRowTemplateInfo
                            {
                                OneItem = new CardRowOneItem
                                {
                                    Item = new TemplateItem
                                    {
                                        FirstValue = new FieldSelector
                                        {
                                            Fields = new List<FieldReference>
                                            {
                                                new FieldReference { FieldPath = "object.barcode" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                },
                ListTemplateOverride = new ListTemplateOverride
                {
                    FirstRowOption = new FirstRowOption
                    {
                        FieldOption = new FieldSelector
                        {
                            Fields = new List<FieldReference>
                                {
                                    new FieldReference { FieldPath = "object.textModulesData['customer']" }
                                }
                        }
                    }
                }
            };
        }
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
        var bronzeTh = snapshot.BronzeThreshold ?? 0;
        var silverTh = snapshot.SilverThreshold ?? int.MaxValue;
        var goldTh = snapshot.GoldThreshold ?? int.MaxValue;
        var platTh = snapshot.PlatinumThreshold ?? int.MaxValue;

        var bronzePct = snapshot.BronzePercent ?? 0;
        var silverPct = snapshot.SilverPercent ?? bronzePct;
        var goldPct = snapshot.GoldPercent ?? silverPct;
        var platPct = snapshot.PlatinumPercent ?? goldPct;

        if (currentPoints < bronzeTh)
            return bronzePct;
        else if (currentPoints >= bronzeTh && currentPoints < silverTh)
            return bronzePct;
        else if (currentPoints >= silverTh && currentPoints < goldTh)
            return silverPct;
        else if (currentPoints >= goldTh && currentPoints < platTh)
            return goldPct;
        else
            return platPct;
    }
    private static int GetCashbackPercentCash(decimal currentPoints, CardTemplateSnapshot snapshot)
    {
        var bronzeTh = snapshot.BronzeThreshold ?? 0;
        var silverTh = snapshot.SilverThreshold ?? int.MaxValue;
        var goldTh = snapshot.GoldThreshold ?? int.MaxValue;
        var platTh = snapshot.PlatinumThreshold ?? int.MaxValue;

        var bronzePct = snapshot.BronzePercentCash ?? 0;
        var silverPct = snapshot.SilverPercentCash ?? bronzePct;
        var goldPct = snapshot.GoldPercentCash ?? silverPct;
        var platPct = snapshot.PlatinumPercentCash ?? goldPct;

        if (currentPoints < bronzeTh)
            return bronzePct;
        else if (currentPoints >= bronzeTh && currentPoints < silverTh)
            return bronzePct;
        else if (currentPoints >= silverTh && currentPoints < goldTh)
            return silverPct;
        else if (currentPoints >= goldTh && currentPoints < platTh)
            return goldPct;
        else
            return platPct;
    }

    public Task<ServiceResult> RedeemAsync(string scannedCode, int pointsDelta = 1, CancellationToken ct = default)
        => Task.FromResult(new ServiceResult(false, "Use RedeemByCardIdAsync with snapshot."));
}
