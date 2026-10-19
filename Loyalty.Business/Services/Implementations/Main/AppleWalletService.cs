using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
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
    private readonly ILoyaltyProgramRepository? _programRepo;
    private readonly ITenantBrandingRepository? _brandingRepo;
    private readonly ITenantSuspensionGuard? _suspensionGuard;
    private readonly ITimeZoneService? _timeZoneService;
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
        IBranchRepository? branchRepo = null,
        ILoyaltyProgramRepository? programRepo = null,
        ITenantBrandingRepository? brandingRepo = null,
        ITenantSuspensionGuard? suspensionGuard = null,
        ITimeZoneService? timeZoneService = null)
    {
        _timeZoneService = timeZoneService;
        _builder = builder;
        _signer = signer;
        _env = env;
        _uow = uow;
        _passkitPush = passkitPush;
        _credProvider = credProvider;
        _loyaltyCardRepository = loyaltyCardRepository;
        _logger = logger;
        _branchRepo = branchRepo;
        _programRepo = programRepo;
        _brandingRepo = brandingRepo;
        _suspensionGuard = suspensionGuard;
    }

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _passLocks = new();

    public Task<ServiceResult> GeneratePassUrlByCardIdAsync(
        int loyaltyCardId,
        AppleWalletRuntimeCreds creds,
        CardTemplateSnapshot snapshot,
        string customerFullName,
        string barcodeValue,
        CancellationToken ct = default,
        int lotteryTickets = 0,
        string? lotteryWinPrize = null,
        DateTime? lotteryDrawDate = null)
        => GeneratePassUrlByCardIdAsync(loyaltyCardId, creds, snapshot, customerFullName,
            barcodeValue, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate, extraBackFields: null);

    public async Task<ServiceResult> GeneratePassUrlByCardIdAsync(
        int loyaltyCardId,
        AppleWalletRuntimeCreds creds,
        CardTemplateSnapshot snapshot,
        string customerFullName,
        string barcodeValue,
        CancellationToken ct,
        int lotteryTickets,
        string? lotteryWinPrize,
        DateTime? lotteryDrawDate,
        IEnumerable<PassField>? extraBackFields)
    {
        var card = await _loyaltyCardRepository.GetByIdAsync(loyaltyCardId, ct);
        if (card is null) return new ServiceResult(false, "Card not found.");

        var tenantId = card.TenantId;

        // Ödəniş suspensiyası — display-only mask. Bütün Apple pass build-ləri
        // (redeem, enrollment, notification, batch refresh) bu metoddan keçdiyi üçün
        // mask bir yerdə tətbiq olunur. DB dəyərləri dəyişmir — restore = flag off.
        decimal displayPoints = card.CurrentPoints;
        int displaySteps = card.CurrentSteps;
        int displayCycles = card.CompletedCycles;
        decimal lifetimePoints = card.LifetimePoints;
        DateTime? expiresAt = card.ExpiresAt;
        if (_suspensionGuard != null)
        {
            var susp = await _suspensionGuard.GetAsync(tenantId, ct);
            if (susp.IsSuspended)
            {
                customerFullName = TenantSuspensionMask.EffectiveMessage(susp.Message);
                displayPoints = 0;
                displaySteps = 0;
                displayCycles = 0;
                lotteryTickets = 0;
                lotteryWinPrize = null;
                lotteryDrawDate = null;
                lifetimePoints = 0;
                expiresAt = null;
            }
        }
        // Faza 2: bitmə tarixi yalnız sahə görünəndə hesablanır (əlavə DB sorğusu)
        string? pointsExpiryText = null;
        if (expiresAt.HasValue && CardFieldCatalog.IsVisible(snapshot.Design.EffectiveFields(snapshot.TemplateType), CardFieldCatalog.PointsExpiry))
            pointsExpiryText = await CardFieldCatalog.FormatExpiryAsync(_timeZoneService, tenantId, expiresAt, ct);

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
            try
            {
                var req = BuildCardPassRequest(snapshot, creds, serial, customerFullName, barcodeValue,
                    displayPoints, displaySteps, displayCycles, lotteryTickets, lotteryWinPrize, lotteryDrawDate, extraBackFields,
                    lifetimePoints, pointsExpiryText);

                _logger.LogInformation(
                    "Apple pass colors — BG: {Bg}, FG: {Fg}, Label: {Lbl} (TenantId={TenantId}, CardId={CardId})",
                    req.BackgroundColor, req.ForegroundColor, req.LabelColor, tenantId, card.Id);

                // Inject branch locations for proximity triggers
                if (_branchRepo != null)
                {
                    try
                    {
                        var branches = await _branchRepo.GetByTenantAsync(tenantId, ct);
                        // Yaxınlıq cütləri: hər filial öz siyahısından təsadüfi mətn; pass-da başlıq (organizationName)
                        // tək olduğu üçün əsas filialın (yoxdursa növbətinin) seçilmiş başlığı götürülür.
                        // Əsas filial önə — Apple yalnız ilk 10 lokasiyanı istifadə edir.
                        string? geoTitle = null;
                        var branchLocs = branches?
                            .Where(b => b.IsActive && b.Latitude.HasValue && b.Longitude.HasValue)
                            .OrderByDescending(b => b.IsMainBranch)
                            .ThenBy(b => b.Id)
                            .Select(b =>
                            {
                                var pick = BranchGeoTexts.Pick(b.GeoTextsJson);
                                geoTitle ??= pick?.Title;
                                return new PassLocation
                                {
                                    Latitude = (double)b.Latitude!.Value,
                                    Longitude = (double)b.Longitude!.Value,
                                    RelevantText = pick?.Text ?? b.Name
                                };
                            })
                            .ToList();

                        if (branchLocs?.Count > 0)
                            req.Locations = branchLocs;
                        if (!string.IsNullOrWhiteSpace(geoTitle))
                            req.OrganizationName = geoTitle;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load branch locations for tenant {TenantId}", tenantId);
                    }
                }

                // Bağlanmış kart (ambassador keçidi / silinmə) → pass voided
                req.Voided = !card.IsActive;

                await BuildAndSignAsync(req, creds, workDir, card.Id, ct);

                // 4) pkpass (TMP → MOVE atomik)
                var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}.pkpass";
                var finalPath = Path.Combine(outDir, fileName);
                await CreatePkpassSafelyAsync(workDir, finalPath, ct);

                // 4b) pkpass faylını yoxla
                if (!File.Exists(finalPath))
                {
                    _logger.LogError("[APPLE] pkpass file was NOT created at {Path}", finalPath);
                    return new ServiceResult(false, "pkpass file was not created.");
                }
                var pkpassSize = new FileInfo(finalPath).Length;
                _logger.LogInformation("[APPLE] pkpass verified: size={Size} bytes, path={Path}", pkpassSize, finalPath);
                if (pkpassSize > 30 * 1024 * 1024)
                {
                    _logger.LogError("[APPLE] pkpass too large: {Size} bytes (max 30MB)", pkpassSize);
                    return new ServiceResult(false, $"pkpass file too large: {pkpassSize} bytes.");
                }
                if (pkpassSize < 100)
                {
                    _logger.LogError("[APPLE] pkpass empty or corrupt: {Size} bytes", pkpassSize);
                    return new ServiceResult(false, "pkpass file is empty or corrupt.");
                }

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
                _logger.LogError(ex, "[APPLE] Pass generation FAILED. TenantId={TenantId}, CardId={CardId}, ExType={ExType}, Message={Msg}",
                    tenantId, card.Id, ex.GetType().Name, ex.Message);
                return new ServiceResult(false, $"Apple pass generation failed: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(workDir, true); }
                catch (Exception cleanEx) { _logger.LogWarning("[APPLE] Cleanup failed: {Msg}", cleanEx.Message); }
            }
        }
        finally
        {
            sem.Release();
            _passLocks.TryRemove(card.Id, out _);
        }
    }
    /// <summary>
    /// pass.json sahələri + şəkil/rəng/barkod → BuildPassRequest (DB-siz, pure). Faza 0.3: preview də eyni koddan keçir.
    /// Faza 2: sahələr CardFieldCatalog-dan (admin yuva/sıra/etiket seçimi; default = bugünkü görünüş), barkod formatı Design.Barcode.
    /// </summary>
    private BuildPassRequest BuildCardPassRequest(
        CardTemplateSnapshot snapshot, AppleWalletRuntimeCreds creds, string serial, string customerFullName, string barcodeValue,
        decimal displayPoints, int displaySteps, int displayCycles,
        int lotteryTickets, string? lotteryWinPrize, DateTime? lotteryDrawDate, IEnumerable<PassField>? extraBackFields,
        decimal lifetimePoints = 0, string? pointsExpiryText = null)
    {
                var design = snapshot.Design ?? new CardDesignSettings();
                bool isProgressBased = snapshot.TemplateType == CardTemplateType.ProgressBased;

                string? stripImageUrl = snapshot.StripImageUrl;
                List<PassField> headerFields = new();
                List<PassField> primaryFields = new();
                List<PassField> secondaryFields = new();
                List<PassField> auxiliaryFields = new();
                List<PassField> backFields = new();
                List<PassField> backSlotFields = new();

                // Sahələr: dəyərlər + admin yuvaları (limitlər Effective-də tətbiq olunub)
                var values = CardFieldCatalog.BuildValues(snapshot, customerFullName, barcodeValue,
                    displayPoints, displaySteps, displayCycles, lifetimePoints, pointsExpiryText, CardPlatform.Apple);
                foreach (var f in design.EffectiveFields(snapshot.TemplateType))
                {
                    if (f.Slot == CardFieldSlot.Hidden) continue;
                    if (!values.TryGetValue(f.Id, out var v) || string.IsNullOrWhiteSpace(v.Value)) continue;
                    var pf = new PassField(CardFieldCatalog.AppleKey(f.Id, snapshot.TemplateType), v.Value,
                        CardFieldCatalog.EffectiveLabel(f, v, snapshot, displayPoints));
                    switch (f.Slot)
                    {
                        case CardFieldSlot.Header: headerFields.Add(pf); break;
                        case CardFieldSlot.Primary: primaryFields.Add(pf); break;
                        case CardFieldSlot.Secondary: secondaryFields.Add(pf); break;
                        case CardFieldSlot.Auxiliary: auxiliaryFields.Add(pf); break;
                        case CardFieldSlot.Back: backSlotFields.Add(pf); break;
                    }
                }

                if (isProgressBased)
                {
                    // Progress icons grid: strip = cari addım ikonu (strip → hero fallback)
                    stripImageUrl = PickProgressIconBySteps(snapshot.IconUrls, displaySteps, snapshot.TotalSteps);
                    if (string.IsNullOrWhiteSpace(stripImageUrl)) stripImageUrl = snapshot.StripImageUrl;
                    if (string.IsNullOrWhiteSpace(stripImageUrl)) stripImageUrl = snapshot.HeroImageUrl;
                }

                if (extraBackFields != null)
                {
                    foreach (var extra in extraBackFields)
                        backFields.Add(extra);
                }
                backFields.AddRange(backSlotFields);

                // Arxa üz — kart dizayneri mətnləri (about/website; "-" = gizli)
                if (design.ShowAbout)
                    backFields.Add(new PassField("about", design.EffectiveAboutText, design.EffectiveAboutTitle));
                if (design.ShowWebsite)
                    backFields.Add(new PassField("website", design.EffectiveWebsiteUrl, design.EffectiveWebsiteLabel));

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
                    OrganizationName = string.IsNullOrWhiteSpace(design.Title) ? (snapshot.ProgramName ?? "Walvero") : design.Title!,
                    Description = design.EffectiveDescription,
                    LogoText = design.LogoText ?? "",
                    BackgroundColor = snapshot.BackgroundColor,
                    BarcodeMessage = barcodeValue,
                    Barcode = barcodeValue,
                    CustomerName = customerFullName,
                    BarcodeFormat = CardFieldCatalog.AppleBarcodeFormat(design.Barcode),
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
                return req;
    }

    /// <summary>pass.json + şəkillər (builder), manifest.json (SHA-1), signature — workDir-də. pkpass paketləmə çağıranındır.</summary>
    private async Task BuildAndSignAsync(BuildPassRequest req, AppleWalletRuntimeCreds creds, string workDir, int logCardId, CancellationToken ct)
    {
                await _builder.BuildAsync(req, workDir, ct);

                // DEBUG: pass.json content-ini log-a yaz
                var passJsonPath = Path.Combine(workDir, "pass.json");
                if (File.Exists(passJsonPath))
                {
                    var passJsonContent = await File.ReadAllTextAsync(passJsonPath, ct);
                    _logger.LogInformation("Apple pass.json content: {PassJson} (CardId={CardId})", passJsonContent, logCardId);
                }

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
    }

    /// <summary>
    /// Publish-siz TEST pass: real sertifikatla imzalanmış .pkpass bytes qaytarır — DB yazılmır, APNs/webService yoxdur
    /// (cihaz qeydiyyatı olmur), serial PREVIEW-… (real kartlarla toqquşmur). Admin AirDrop/mail ilə iPhone-a atıb real görünüşü yoxlayır.
    /// </summary>
    public async Task<ServiceResult> GeneratePreviewPassAsync(
        int tenantId, int loyaltyProgramId, AppleWalletRuntimeCreds creds, CardTemplateSnapshot snapshot,
        string customerFullName, string barcodeValue, decimal points, int steps, int cycles, CancellationToken ct = default,
        decimal lifetimePoints = 0, string? pointsExpiryText = null)
    {
        if (!HasValidCreds(creds, out var credsReason))
            return new ServiceResult(false, "Apple credentials not found: " + credsReason);

        var serial = $"PREVIEW-{loyaltyProgramId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var workDir = Path.Combine(Path.GetTempPath(), $"pkpass-preview-{loyaltyProgramId}-{Guid.NewGuid():N}");
        var finalPath = Path.Combine(Path.GetTempPath(), $"preview-{Guid.NewGuid():N}.pkpass");
        Directory.CreateDirectory(workDir);
        try
        {
            var req = BuildCardPassRequest(snapshot, creds, serial, customerFullName, barcodeValue, points, steps, cycles, 0, null, null, null,
                lifetimePoints, pointsExpiryText);
            // Önizləmə: cihaz qeydiyyatı/push olmasın — webServiceURL/authenticationToken göndərilmir
            req.WebServiceURL = null;
            req.AuthenticationToken = null;

            await BuildAndSignAsync(req, creds, workDir, 0, ct);
            await CreatePkpassSafelyAsync(workDir, finalPath, ct);
            var bytes = await File.ReadAllBytesAsync(finalPath, ct);
            _logger.LogInformation("[APPLE] preview pass built. TenantId={TenantId}, ProgramId={ProgramId}, Size={Size}", tenantId, loyaltyProgramId, bytes.Length);
            return new ServiceResult(true, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[APPLE] preview pass FAILED. TenantId={TenantId}, ProgramId={ProgramId}", tenantId, loyaltyProgramId);
            return new ServiceResult(false, $"Apple preview failed: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
            try { if (File.Exists(finalPath)) File.Delete(finalPath); } catch { }
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

    public async Task<ServiceResult> PushNotificationToCardAsync(
        int loyaltyCardId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        if (_programRepo == null || _brandingRepo == null)
            return new ServiceResult(false, "AppleWalletService: program/branding repository not configured for notifications.");

        // GetById (not GetByIdAsync) Includes Customer — needed for FirstName/LastName.
        var card = await _loyaltyCardRepository.GetById(loyaltyCardId, ct);
        if (card is null) return new ServiceResult(false, "Card not found.");

        if (!card.IsIOS || string.IsNullOrWhiteSpace(card.AppleSerial))
            return new ServiceResult(false, "Card is not on Apple Wallet (no AppleSerial / IsIOS=false).");

        var creds = await _credProvider.GetAppleAsync(card.TenantId, ct);
        if (!HasValidCreds(creds, out var credsReason))
        {
            _logger.LogWarning("Apple credentials missing for tenant {TenantId}: {Reason}", card.TenantId, credsReason);
            return new ServiceResult(false, $"Apple credentials missing: {credsReason}");
        }

        var program = await _programRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
        if (program?.CardTemplate is null)
            return new ServiceResult(false, "Program or card template missing.");

        var branding = await _brandingRepo.GetByTenantIdAsync(card.TenantId);

        string baseUrl = "https://appapi.walvero.com";
        string ToAbs(string? dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
            if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
            var rel = "/" + dbPath.TrimStart('/', '\\');
            return baseUrl + rel;
        }
        var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

        // Unique value forces iOS to detect a field change → render changeMessage on lock screen.
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var notifValue = $"[{stamp}] {title}\n{body}";
        var notifField = new PassField("notification", notifValue, "Bildiriş", "%@");

        var customerFullName = $"{card.Customer?.FirstName} {card.Customer?.LastName}".Trim();
        if (string.IsNullOrEmpty(customerFullName)) customerFullName = $"Card #{card.Id}";

        return await GeneratePassUrlByCardIdAsync(
            loyaltyCardId: card.Id,
            creds: creds,
            snapshot: snapshot,
            customerFullName: customerFullName,
            barcodeValue: card.BarcodeValue ?? card.Id.ToString(),
            ct: ct,
            lotteryTickets: 0,
            lotteryWinPrize: null,
            lotteryDrawDate: null,
            extraBackFields: new[] { notifField });
    }

    public async Task<ServiceResult> RedeemByCardIdAsync(
        int loyaltyCardId, CardTemplateSnapshot tpl, AppleWalletRuntimeCreds creds,
        decimal currentPoints, int currentSteps, int pointsDelta,
        CancellationToken ct = default,
        int lotteryTickets = 0,
        string? lotteryWinPrize = null,
        DateTime? lotteryDrawDate = null)
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

        File.Move(tmpPath, finalPath, overwrite: true);
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
