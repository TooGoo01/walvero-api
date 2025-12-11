using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Walletobjects.v1;
using Google.Apis.Walletobjects.v1.Data;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Uri = System.Uri;

public sealed class GoogleWalletService : IGoogleWalletService
{
    private readonly WalletobjectsService _svc;
    private readonly string _issuerId;
    private readonly string _classSuffix;
    private readonly string _serviceAccountEmail;
    private readonly string _privateKeyPem;
    private readonly string? _privateKeyId;
    private readonly string[] _origins;
    private readonly string _applicationName;
    private readonly ILogger<GoogleWalletService>? _log;

    // Placeholder fallback (public, HTTPS, 3:1 ~ 1032x336/1860x620 və s.)
    private const string FALLBACK_BANNER_URL = "https://yourcdn.example.com/fallback-1032x336.png";

    public GoogleWalletService(GoogleWalletRuntimeConfig cfg, ILogger<GoogleWalletService>? log = null)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));
        if (string.IsNullOrWhiteSpace(cfg.IssuerId)) throw new ArgumentException("IssuerId required");
        if (string.IsNullOrWhiteSpace(cfg.ClassSuffix)) throw new ArgumentException("ClassSuffix required");
        if (string.IsNullOrWhiteSpace(cfg.ServiceAccountJsonPath)) throw new ArgumentException("ServiceAccountJsonPath required");
        if (!File.Exists(cfg.ServiceAccountJsonPath)) throw new FileNotFoundException("Service account JSON not found", cfg.ServiceAccountJsonPath);

        _issuerId = cfg.IssuerId;
        _classSuffix = cfg.ClassSuffix;
        _origins = cfg.Origins ?? Array.Empty<string>();
        _applicationName = string.IsNullOrWhiteSpace(cfg.ApplicationName) ? "Walvero Loyalty" : cfg.ApplicationName;
        _log = log;

        var json = File.ReadAllText(cfg.ServiceAccountJsonPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        _serviceAccountEmail = root.GetProperty("client_email").GetString()!;
        _privateKeyPem = root.GetProperty("private_key").GetString()!;
        _privateKeyId = root.TryGetProperty("private_key_id", out var kidEl) ? kidEl.GetString() : null;

        var credential = GoogleCredential.FromFile(cfg.ServiceAccountJsonPath)
            .CreateScoped(WalletobjectsService.Scope.WalletObjectIssuer);

        _svc = new WalletobjectsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }

    private string ClassId => $"{_issuerId}.{_classSuffix}";
    private static string ObjectIdForCard(string issuerId, int loyaltyCardId) => $"{issuerId}.lc-{loyaltyCardId}";

    // =================== 1) CLASS ===================
    public async Task EnsureClassAsync(CardTemplateSnapshot tpl, string programName, CancellationToken ct = default)
    {
        LoyaltyClass? existing = null;
        try
        {
            existing = await _svc.Loyaltyclass.Get(ClassId).ExecuteAsync(ct);
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

                ClassTemplateInfo = new ClassTemplateInfo
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
                }
            };

            return cls;
        }

        if (existing is null)
        {
            var toCreate = BuildDesired();
            toCreate.ReviewStatus = "UNDER_REVIEW"; // yalnız INSERT-də
            await _svc.Loyaltyclass.Insert(toCreate).ExecuteAsync(ct);
            _log?.LogInformation("Google Wallet class CREATED: {ClassId}", ClassId);
        }
        else
        {
            var toUpdate = BuildDesired();
            toUpdate.ReviewStatus = "UNDER_REVIEW";
            // UPDATE-də ReviewStatus göndərmirik
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
        int currentPoints = 0,
        int currentSteps = 0,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureClassAsync(tpl, programName: tpl.ProgramName, ct);

            var objectId = ObjectIdForCard(_issuerId, loyaltyCardId);

            try
            {
                var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);
                return new ServiceResult(true, existing);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // create
            }

            var state = BuildDisplayState(tpl, currentPoints, currentSteps);
            var barcode = string.IsNullOrWhiteSpace(barcodeValue) ? loyaltyCardId.ToString() : barcodeValue;

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
            // Banner URL fallback (hero -> icon -> logo -> placeholder)
            string? bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
            if (!IsHttps(bannerUrl) && IsHttps(state.CurrentIconUrl)) bannerUrl = state.CurrentIconUrl;
            if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;

            _log?.LogInformation("GW Create: bannerUrl={Url} hero={Hero} icon={Icon} logo={Logo}",
                bannerUrl, tpl.HeroImageUrl, state.CurrentIconUrl, tpl.GoogleLogoUrl);

            var obj = new LoyaltyObject
            {
                Id = objectId,
                ClassId = ClassId,
                State = "ACTIVE",
                AccountId = null,
                AccountName = "",

                LoyaltyPoints = new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = currentPoints },
                    Label = "Points"
                },

                TextModulesData = new List<TextModuleData>
                {
                    new TextModuleData { Id = "customer", Header = "Customer", Body = accountName ?? $"Card #{loyaltyCardId}" },
                    new TextModuleData { Id = "balance",  Header = "BALANCE",  Body = currentPoints.ToString()+" "+"AZN" },
                    new TextModuleData { Id = "cashback", Header = cashback, Body = cashbackText }
                },

                Barcode = new Barcode { Type = "CODE_128", Value = barcode, AlternateText = barcode },

                // Banneri obyektin imageModulesData[0]-a yaz (həmişə)
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

            var created = await _svc.Loyaltyobject.Insert(obj).ExecuteAsync(ct);
            return new ServiceResult(true, created);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "CreateOrGetObjectByCardIdAsync failed");
            return new ServiceResult(false, ex.Message);
        }
    }

    // =================== 3) REDEEM / UPDATE ===================
    public async Task<ServiceResult> RedeemByCardIdAsync(
        int loyaltyCardId,
        CardTemplateSnapshot tpl,
        int currentPoints,
        int currentSteps,
        int delta,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureClassAsync(tpl, programName: tpl.ProgramName, ct);

            var objectId = $"{_issuerId}.lc-{loyaltyCardId}";
            var existing = await _svc.Loyaltyobject.Get(objectId).ExecuteAsync(ct);

            var nextPoints = currentPoints;  // və ya currentPoints + delta;
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

            var textList = existing.TextModulesData?.ToList() ?? new List<TextModuleData>();

            // customer
            var customer = textList.FirstOrDefault(t => t.Id == "customer");
            if (customer == null)
            {
                customer = new TextModuleData { Id = "customer", Header = "Customer", Body = existing.AccountName ?? $"Card #{loyaltyCardId}" };
                textList.Add(customer);
            }

            // balance
            var balance = textList.FirstOrDefault(t => t.Id == "balance");
            if (balance == null)
            {
                balance = new TextModuleData { Id = "balance" };
                textList.Add(balance);
            }
            balance.Header = "BALANCE";
            balance.Body = nextPoints.ToString() + " " + "AZN";

            // cashback
            var cashbackId = textList.FirstOrDefault(t => t.Id == "cashback");
            if (cashbackId == null)
            {
                cashbackId = new TextModuleData { Id = "cashback" };
                textList.Add(cashbackId);
            }
            cashbackId.Header = cashback;
            cashbackId.Body = cashbackText;

            // Redeem zamanı da banner fallback (hero -> tier icon -> logo -> placeholder)
            string? bannerUrl = IsHttps(tpl.HeroImageUrl) ? tpl.HeroImageUrl : null;
            if (!IsHttps(bannerUrl))
            {
                var fallbackIcon = PickTierIconByPoints(tpl, nextPoints, out _);
                if (IsHttps(fallbackIcon)) bannerUrl = fallbackIcon;
            }
            if (!IsHttps(bannerUrl) && IsHttps(tpl.GoogleLogoUrl)) bannerUrl = tpl.GoogleLogoUrl;
            if (!IsHttps(bannerUrl)) bannerUrl = FALLBACK_BANNER_URL;

            var imageModules = new List<ImageModuleData>
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
            };

            var patch = new LoyaltyObject
            {
                LoyaltyPoints = new LoyaltyPoints
                {
                    Balance = new LoyaltyPointsBalance { Int__ = nextPoints },
                    Label = "Points"
                },
                TextModulesData = textList,
                ImageModulesData = imageModules
            };

            var patched = await _svc.Loyaltyobject.Patch(patch, objectId)
                //.UpdateMask("loyaltyPoints,textModulesData,imageModulesData") // SDK dəstəkləyirsə aktivləşdir
                .ExecuteAsync(ct);

            _log?.LogInformation("Google Wallet object PATCHED (redeem): {ObjectId} (banner={Banner})", objectId, bannerUrl);

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

    // =================== 4) SAVE LINK ===================
    public async Task<ServiceResult> GenerateSaveLinkByCardIdAsync(int loyaltyCardId, CancellationToken ct = default)
    {
        try
        {
            var objectId = ObjectIdForCard(_issuerId, loyaltyCardId);

            var loyaltyObject = new JObject
            {
                ["id"] = objectId,
                ["classId"] = ClassId,
                ["state"] = "ACTIVE",
                ["accountId"] = loyaltyCardId.ToString(),
                ["accountName"] = $"Card #{loyaltyCardId}"
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

    private static LoyaltyDisplayState BuildDisplayState(CardTemplateSnapshot tpl, int currentPoints, int currentSteps)
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

    private static string? PickTierIconByPoints(CardTemplateSnapshot tpl, int pts, out string tier)
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

    private static int GetCashbackPercent(int currentPoints, CardTemplateSnapshot snapshot)
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
        else if (currentPoints < silverTh)
            return silverPct;
        else if (currentPoints < goldTh)
            return goldPct;
        else if (currentPoints < platTh)
            return platPct;
        else
            return platPct;
    }
    private static int GetCashbackPercentCash(int currentPoints, CardTemplateSnapshot snapshot)
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
        else if (currentPoints < silverTh)
            return silverPct;
        else if (currentPoints < goldTh)
            return goldPct;
        else if (currentPoints < platTh)
            return platPct;
        else
            return platPct;
    }

    public Task<ServiceResult> RedeemAsync(string scannedCode, int pointsDelta = 1, CancellationToken ct = default)
        => Task.FromResult(new ServiceResult(false, "Use RedeemByCardIdAsync with snapshot."));
}
