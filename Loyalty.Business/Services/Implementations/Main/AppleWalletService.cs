using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data.Entity.ModelConfiguration.Configuration;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using File = System.IO.File;

public sealed class AppleWalletService : IAppleWalletService
{
    private readonly IApplePassBuilder _builder;
    private readonly ILoyaltyCardRepository _loyaltyCardRepository;
    private readonly IApplePassSigner _signer;
    private readonly IAppEnvironment _env;
    private readonly IUnitOfWork _uow;
    private readonly IPasskitPushService _passkitPush;
    private readonly IWalletCredentialProvider _credProvider;
    private readonly IBranchRepository? _branchRepo;
    private readonly ILogger<AppleWalletService> _logger;

    public AppleWalletService(
        IApplePassBuilder builder,
        IApplePassSigner signer,
        IAppEnvironment env,
        IUnitOfWork uow,
        IPasskitPushService passkitPush,
        IWalletCredentialProvider credProvider,
        ILoyaltyCardRepository loyaltyCardRepository,
        ILogger<AppleWalletService> logger,
        IBranchRepository? branchRepo = null)
    {
        _builder = builder;
        _signer = signer;
        _env = env;
        _uow = uow;
        _passkitPush = passkitPush;
        _credProvider = credProvider;
        _loyaltyCardRepository = loyaltyCardRepository;
        _logger = logger;
        _branchRepo = branchRepo;
    }

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _passLocks = new();

    public async Task<ServiceResult> GeneratePassUrlByCardIdAsync(
        int loyaltyCardId,
        AppleWalletRuntimeCreds creds,               // istəsən bu parametri çıxarıb içəridə credProvider-dən götür
        CardTemplateSnapshot snapshot,
        string customerFullName,
        string barcodeValue,
        CancellationToken ct = default,
        int lotteryTickets = 0,
        string? lotteryWinPrize = null,
        DateTime? lotteryDrawDate = null)
    {
        var card = await _loyaltyCardRepository.GetByIdAsync(loyaltyCardId, ct);
        if (card is null) return new ServiceResult(false, "Card not found.");

        var tenantId = card.TenantId;

        if (!HasValidCreds(creds, out var credsReason))
        {
            _logger.LogWarning("Apple credentials missing/invalid. TenantId={TenantId}. {Reason}", tenantId, credsReason);
            return new ServiceResult(false, "Apple credentials not found.");
        }

        var outDir = Path.Combine(_env.WebRootPath, "passes", tenantId.ToString(), card.LoyaltyProgramId.ToString());
        Directory.CreateDirectory(outDir);

        var sem = _passLocks.GetOrAdd(card.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            var serial = card.AppleSerial;
            if (string.IsNullOrWhiteSpace(serial))
            {
                serial = $"AL-{card.Id:D8}";
                card.AppleSerial = serial;
            }

            var workDir = Path.Combine(Path.GetTempPath(), $"pkpass-{card.Id}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDir);
            int percent = 0;
            int percentCash = 0;
            if (card.CurrentPoints < snapshot.BronzeThreshold)
            {
                percent = snapshot.BronzePercent.Value; // Bronze tier
                percentCash = snapshot.BronzePercentCash.Value; // Bronze tier
            }

            else if (card.CurrentPoints >= snapshot.BronzeThreshold && card.CurrentPoints < snapshot.SilverThreshold)
            {
                percent = snapshot.BronzePercent.Value;
                percentCash = snapshot.BronzePercentCash.Value;
            }

            else if (card.CurrentPoints >= snapshot.SilverThreshold && card.CurrentPoints < snapshot.GoldThreshold)
            {
                percent = snapshot.SilverPercent.Value;
                percentCash = snapshot.SilverPercentCash.Value;
            }

            // FIX C: Use GoldThreshold instead of GoldPercent
            else if (card.CurrentPoints >= snapshot.GoldThreshold && card.CurrentPoints < snapshot.PlatinumThreshold)
            {
                percent = snapshot.GoldPercent.Value;
                percentCash = snapshot.GoldPercentCash.Value;
            }
            else
            {
                percent = snapshot.PlatinumPercent.Value;
                percentCash = snapshot.PlatinumPercentCash.Value;
            }

            try
            {
                string cashback;
                string cashbackText;
                if (snapshot.PayMentType)
                {
                    cashback = $"Cashback:{percent}%";
                    cashbackText = $" Nağd ödənişdə {percentCash}%";
                }
                else
                {
                    cashback = "Cashback";
                    cashbackText = $"{percent}%";

                }

                // ProgressBased vs TierBased/PointsBased logic
                bool isProgressBased = snapshot.TemplateType == CardTemplateType.ProgressBased;

                string? stripImageUrl = snapshot.StripImageUrl;
                List<PassField> headerFields = new();
                List<PassField> primaryFields = new();
                List<PassField> secondaryFields = new();
                List<PassField> auxiliaryFields = new();
                List<PassField> backFields = new();

                if (isProgressBased)
                {
                    // ProgressBased Apple Wallet Layout:
                    // Top: reward description (uppercase rule text) - use headerFields for top rule text
                    // Middle: progress icons grid (strip image)
                    // Lower-middle: "Reward Card" (left) and "Pulsuz içki: {N}" (right) - use secondaryFields for side-by-side
                    // Bottom: barcode (existing)

                    // Use CompletedCycles from card (available reward count)
                    int completedCycles = card.CompletedCycles;
                    string freeDrinkValue = completedCycles == 0 ? "0" : completedCycles.ToString();

                    // Get FreeRewardLabel from template (default: "bonus" instead of "Pulsuz içki")
                    string freeRewardLabel = string.IsNullOrWhiteSpace(snapshot.FreeRewardLabel)
                        ? "bonus"
                        : snapshot.FreeRewardLabel;

                    string freeDrinkText = completedCycles == 0 ? "0" : $"{completedCycles}";

                    // Top: Reward description as uppercase rule text (headerFields for top placement)
                    if (!string.IsNullOrWhiteSpace(snapshot.RewardDescription))
                    {
                        string rewardDescUpper = snapshot.RewardDescription.ToUpperInvariant();
                        primaryFields.Add(new PassField("rule", rewardDescUpper, null)); // No label for rule text
                    }

                    // Lower-middle: "Reward Card" (left) and "{FreeRewardLabel}: {N}" (right) - side-by-side
                    secondaryFields.Add(new PassField("rewardCard", customerFullName, null));
                    secondaryFields.Add(new PassField("pulsuzIcki", $"{freeRewardLabel}: {freeDrinkText}", null));

                    // Progress icons grid: Use strip image showing the grid
                    // Try to get a composite grid image, or use the current step icon
                    stripImageUrl = PickProgressIconBySteps(snapshot.IconUrls, card.CurrentSteps, snapshot.TotalSteps);
                    if (string.IsNullOrWhiteSpace(stripImageUrl)) stripImageUrl = snapshot.StripImageUrl;
                    if (string.IsNullOrWhiteSpace(stripImageUrl)) stripImageUrl = snapshot.HeroImageUrl;
                    // If we have multiple icons, we might need a composite, but for now use the current icon
                }
                else
                {
                    var currency = string.IsNullOrWhiteSpace(snapshot.Currency) ? "AZN" : snapshot.Currency;
                    // TierBased/PointsBased: Keep existing logic
                    headerFields.Add(new PassField("balance", card.CurrentPoints.ToString() + " " + currency, "Balance"));
                    auxiliaryFields.Add(new PassField("cashback", cashbackText, cashback));
                    secondaryFields.Add(new PassField("name", customerFullName, "Customer"));
                }
                backFields.Add(new PassField(
    "about",
    "POS ilə inteqrasiya olunan sadiqlik proqramı. Müştəri bağlılığını artırın və təkrar alışları təşviq edin.",
    "Walvero Loyallıq"
));

                backFields.Add(new PassField(
                    "features",
                    "• Fərdiləşdirilmiş mükafatlar\n• Avtomatlaşdırılmış izləmə (POS)\n• Hədəfli kampaniyalar\n• Analitika və insight-lar",
                    "Əsas imkanlar"
                ));

                backFields.Add(new PassField(
                    "website",
                    "https://www.walvero.com",
                    "Veb-sayt"
                ));

                // Lottery fields (on the front — auxiliaryFields)
                if (snapshot.IsLotteryEnabled && lotteryTickets > 0)
                {
                    auxiliaryFields.Add(new PassField("lottery", lotteryTickets.ToString(), "Lotereya şansları"));
                }
                if (snapshot.IsLotteryEnabled && !string.IsNullOrWhiteSpace(lotteryWinPrize))
                {
                    auxiliaryFields.Add(new PassField("lotteryWin", lotteryWinPrize, "Lotereya uduşu"));
                }
                if (snapshot.IsLotteryEnabled && lotteryDrawDate.HasValue)
                {
                    auxiliaryFields.Add(new PassField("lotteryDrawDate", lotteryDrawDate.Value.ToString("dd.MM.yyyy HH:mm"), "Çəkiliş tarixi"));
                }

                // 1) pass.json + şəkillər (DB creds → Team/PassType)
                var req = new BuildPassRequest
                {
                    TeamIdentifier = creds.TeamIdentifier,
                    PassTypeIdentifier = creds.PassTypeIdentifier,
                    SerialNumber = serial,
                    OrganizationName = snapshot.ProgramName ?? "Walvero",
                    Description = "Loyalty Card",
                    LogoText = "",
                    BackgroundColor = snapshot.BackgroundColor,
                    BarcodeMessage = barcodeValue,
                    Barcode = barcodeValue,
                    CustomerName = customerFullName,
                    BarcodeFormat = "PKBarcodeFormatCode128",
                    PrimaryFields = primaryFields,
                    SecondaryFields = secondaryFields,
                    HeaderFields = headerFields,
                    AuxiliaryFields = auxiliaryFields,
                    BackFields = backFields,
                    LogoUrl = snapshot.LogoUrl,
                    HeroImageUrl = snapshot.HeroImageUrl,
                    WebServiceURL = creds.WebServiceUrl,          // varsa
                    AuthenticationToken = creds.AuthToken,       // varsa
                    StripImageUrl = stripImageUrl,
                    ForegroundColor = snapshot.ForegroundColor,
                    LabelColor = snapshot.LabelColor,
                };

                // Inject branch locations for proximity triggers
                if (_branchRepo != null)
                {
                    try
                    {
                        var branches = await _branchRepo.GetByTenantAsync(tenantId, ct);
                        var branchLocs = branches?
                            .Where(b => b.IsActive && b.Latitude.HasValue && b.Longitude.HasValue)
                            .Select(b => new PassLocation
                            {
                                Latitude = b.Latitude!.Value,
                                Longitude = b.Longitude!.Value,
                                RelevantText = b.Name
                            })
                            .ToList();

                        if (branchLocs?.Count > 0)
                            req.Locations = branchLocs;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load branch locations for tenant {TenantId}", tenantId);
                    }
                }

                await _builder.BuildAsync(req, workDir, ct);

                // 2) manifest.json (SHA-1)
                var files = Directory.GetFiles(workDir)
                                     .Where(p => Path.GetFileName(p) != "manifest.json" && Path.GetFileName(p) != "signature")
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .ToList();

                var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var f in files)
                {
                    var sha1 = SHA1.HashData(await File.ReadAllBytesAsync(f, ct));
                    manifest[Path.GetFileName(f)] = Convert.ToHexString(sha1).ToLowerInvariant();
                }
                var manifestPath = Path.Combine(workDir, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), ct);

                // 3) signature (tam DB creds ilə)
                var signatureBytes = _signer.Sign(
                    await File.ReadAllBytesAsync(manifestPath, ct),
                    creds.CertP12Path, creds.CertPassword, creds.WwdrCertPath);
                await File.WriteAllBytesAsync(Path.Combine(workDir, "signature"), signatureBytes, ct);

                // 4) pkpass (TMP → MOVE atomik)
                var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}.pkpass";
                var finalPath = Path.Combine(outDir, fileName);
                await CreatePkpassSafelyAsync(workDir, finalPath, ct);

                // 5) latest link
                var publicUrl = $"/passes/{tenantId}/{card.LoyaltyProgramId}/{fileName}";

                // 6) DB update
                card.AppleSerial = serial;
                card.AppleAuthToken ??= creds.AuthToken; // varsa saxla
                card.ApplePassUrl = publicUrl;
                _uow.Repository<LoyaltyCard>().Update(card);
                _uow.Commit();

                // 7) push (WebService varsa) — qeydiyyatlı cihazlara xəbər ver
                _logger.LogInformation("[APPLE-PUSH] WebServiceUrl={WebServiceUrl}, PTI={PTI}, Serial={Serial}",
                    creds.WebServiceUrl ?? "(null)", creds.PassTypeIdentifier, serial);
                if (!string.IsNullOrWhiteSpace(creds.WebServiceUrl))
                {
                    _logger.LogInformation("[APPLE-PUSH] Calling NotifyUpdatedAsync...");
                    await _passkitPush.NotifyUpdatedAsync(tenantId, creds.PassTypeIdentifier, serial, fileName, ct);
                    _logger.LogInformation("[APPLE-PUSH] NotifyUpdatedAsync completed.");
                }
                else
                {
                    _logger.LogWarning("[APPLE-PUSH] SKIPPED - WebServiceUrl is empty/null!");
                }

                return new ServiceResult(true, publicUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Apple pass generation failed. TenantId={TenantId}, CardId={CardId}", tenantId, card.Id);
                return new ServiceResult(false, "Apple pass generation failed.");
            }
            finally
            {
                try { Directory.Delete(workDir, true); } catch { }
            }
        }
        finally
        {
            sem.Release();
            _passLocks.TryRemove(card.Id, out _);
        }
    }
    private int GetCashbackPercent(int points, int bronze, int silver, int gold, int platinium)
    {
        // Points sayına görə cashback faizini təyin edin
        if (points < 50)
            return 5; // Bronze tier
        else if (points >= 50 && points < 100)
            return 10; // Silver tier
        else if (points >= 100 && points < 150)
            return 15; // Gold tier
        else
            return 20; // Platinum tier
    }

    public async Task<ServiceResult> RedeemByCardIdAsync(
        int loyaltyCardId, CardTemplateSnapshot tpl, AppleWalletRuntimeCreds creds, int currentPoints, int currentSteps, int pointsDelta, CancellationToken ct = default,
        int lotteryTickets = 0, string? lotteryWinPrize = null, DateTime? lotteryDrawDate = null)
    {
        // Creds-i DB-dən burda da çəkə bilərsən (tenantId → _credProvider.GetAppleAsync)
        var card = await _loyaltyCardRepository.GetById(loyaltyCardId, ct);
        if (card is null) return new(false, "Card not found.");


        // Serial sabit qalsın (Wallet pass-ı serial ilə tanıyır)
        if (string.IsNullOrWhiteSpace(card.AppleSerial))
            card.AppleSerial = $"AL-{card.Id:D8}";
        card.AppleAuthToken ??= creds.AuthToken;

        // Build + sign + zip → latest link qaytar (yuxarıdakı GeneratePassUrlByCardIdAsync
        // ilə eyni, sadəcə Serial = card.AppleSerial istifadə et və SecondaryFields balansa uyğun)
        // Qısa olsun deyə istifadəçidə həmin metodu çağır:
        return await GeneratePassUrlByCardIdAsync(loyaltyCardId, creds, tpl,
            $"{card.Customer?.FirstName} {card.Customer?.LastName}".Trim(),
            card.BarcodeValue ?? card.Id.ToString(), ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate);
    }

    private static async Task<string> CreatePkpassSafelyAsync(string workingDir, string finalPath, CancellationToken ct)
    {
        var outDir = Path.GetDirectoryName(finalPath)!;
        Directory.CreateDirectory(outDir);

        var tmpPath = Path.Combine(outDir, Path.GetRandomFileName() + ".pkpass");
        using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in Directory.EnumerateFiles(workingDir))
            {
                var name = Path.GetFileName(file);
                zip.CreateEntryFromFile(file, name, CompressionLevel.NoCompression);
            }
        }

        File.Move(tmpPath, finalPath);
        return finalPath;
    }

    private static string RandomToken(int len)
    {
        var bytes = RandomNumberGenerator.GetBytes(len);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Selects progress icon based on currentSteps and totalSteps.
    /// Rules:
    /// - If currentSteps == 0 => show 0th icon
    /// - Calculate: remainder = currentSteps % totalSteps
    /// - If currentSteps > 0 and remainder == 0 => iconIndex = totalSteps (completion icon if exists)
    /// - Else iconIndex = remainder
    /// - Clamp to [0 .. icons.Count-1]
    /// </summary>
    private static string? PickProgressIconBySteps(List<string> icons, int currentSteps, int totalSteps)
    {
        if (icons == null || icons.Count == 0) return null;
        if (totalSteps <= 0) totalSteps = 1;

        int iconIndex;
        if (currentSteps == 0)
        {
            iconIndex = 0;
        }
        else
        {
            int remainder = currentSteps % totalSteps;
            if (remainder == 0)
            {
                // Completion icon (totalSteps-th icon if exists, else last icon)
                iconIndex = totalSteps < icons.Count ? totalSteps : icons.Count - 1;
            }
            else
            {
                iconIndex = remainder;
            }
        }

        // Clamp to valid range
        iconIndex = Math.Clamp(iconIndex, 0, icons.Count - 1);
        var url = icons[iconIndex]?.Trim();
        return !string.IsNullOrWhiteSpace(url) ? url : null;
    }

    /// <summary>
    /// Calculates free drink count: floor(currentSteps / totalSteps)
    /// </summary>
    private static int CalculateFreeDrinkCount(int currentSteps, int totalSteps)
    {
        if (totalSteps <= 0) return 0;
        return currentSteps / totalSteps; // Integer division = floor
    }

    private static bool HasValidCreds(AppleWalletRuntimeCreds creds, out string reason)
    {
        if (string.IsNullOrWhiteSpace(creds.TeamIdentifier))
        {
            reason = "TeamIdentifier is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(creds.PassTypeIdentifier))
        {
            reason = "PassTypeIdentifier is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(creds.CertP12Path))
        {
            reason = "CertP12Path is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(creds.CertPassword))
        {
            reason = "CertPassword is missing.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(creds.WwdrCertPath))
        {
            reason = "WwdrCertPath is missing.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
