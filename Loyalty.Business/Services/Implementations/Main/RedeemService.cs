// Business/Services/Implementations/Main/RedeemService.cs
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Realtime;
using Loyalty.Business.Helpers;
using Loyalty.Business.Hubs;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging; // <-- LOGGING
using System.Collections.Generic;   // <-- BeginScope dict
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using WalletPlatform = Loyalty.DataAccess.Enums.WalletPlatform;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class RedeemService : IRedeemService
    {
        private readonly ILoyaltyCardRepository _cardSvc;
        private readonly ILoyaltyProgramRepository _programSvc;
        private readonly ITenantBrandingRepository _brandingSvc;

        private readonly IGoogleWalletServiceFactory _gwFactory;
        private readonly IWalletRouter _walletRouter;
        private readonly ITenantRepository _tenantPolicySvc;
        private readonly IOtpGenerator _otpGenerator;

        private readonly IUnitOfWork uow;
        private readonly ILogger<RedeemService> _logger; // <-- LOGGING
        private readonly IHubContext<LoyaltyHub> _hubContext;
        private readonly INotificationService _notificationService;
        public RedeemService(
            ILoyaltyCardRepository cardSvc,
            ILoyaltyProgramRepository programSvc,
            ITenantBrandingRepository brandingSvc,
            IGoogleWalletServiceFactory gwFactory,
            IWalletRouter walletRouter,
            ITenantRepository tenantPolicySvc,
            IOtpGenerator otpGenerator,
            IUnitOfWork uow,
            ILogger<RedeemService> logger,
            IHubContext<LoyaltyHub> hubContext,
            INotificationService notificationService) // <-- LOGGING
        {
            _cardSvc = cardSvc;
            _programSvc = programSvc;
            _brandingSvc = brandingSvc;
            _gwFactory = gwFactory;

            _walletRouter = walletRouter;
            _tenantPolicySvc = tenantPolicySvc;
            _otpGenerator = otpGenerator;

            this.uow = uow;
            _logger = logger; // <-- LOGGING
            _hubContext = hubContext;
            _notificationService = notificationService;
        }

        // START (Auto / ForcePending / ForceApply)
        public async Task<RedeemResultDto> RedeemAsync(RedeemRequestDto request, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["TenantId"] = request.TenantId,
                ["ProgramId"] = request.LoyaltyProgramId,
                ["Delta"] = request.Delta,
                ["Mode"] = request.Mode.ToString(),
                ["PrefPlatform"] = request.PreferredPlatform.ToString(),
                ["IdemKey"] = request.IdempotencyKey
            });

            try
            {
                _logger.LogInformation("Redeem started. Code={Code}", request.Code);

                if (request.TenantId <= 0 || string.IsNullOrWhiteSpace(request.Code))
                {
                    _logger.LogWarning("Invalid tenant or code. TenantId={TenantId}, CodeEmpty={CodeEmpty}",
                        request.TenantId, string.IsNullOrWhiteSpace(request.Code));
                    return Fail("Tenant və ya kod yanlışdır.");
                }

                // 1) Kart
                var card = await _cardSvc.GetByBarcodeValueAsync(request.TenantId, request.Code.Trim(), ct);
                if (card is null)
                {
                    _logger.LogWarning("Card not found by barcode. Code={Code}", request.Code);
                    return Fail("Kart tapılmadı (barcode uyğun gəlmədi).");
                }
                var appUserName = card.Customer?.applicationUser?.Id;
                // 2) Program + Branding + Policy
                var program = await _programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                if (program is null)
                {
                    _logger.LogWarning("Program not found. ProgramId={ProgramId}", card.LoyaltyProgramId);
                    return Fail("Loyalty program tapılmadı.");
                }

                var branding = await _brandingSvc.GetByTenantIdAsync(request.TenantId, ct);
                var policy = await _tenantPolicySvc.GetByIdAsync(request.TenantId, ct);
                string baseUrl = "https://walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    var rel = "/" + dbPath.TrimStart('/', '\\');
                    return baseUrl + rel;
                }
                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);
                var delta = request.Delta;

                _logger.LogInformation("Snapshot built. TemplateType={TemplateType}, RequireConfirmation={RequireConfirmation}",
                    snapshot.TemplateType, policy.RequireConfirmation);

                // 3) Mode qərarı
                var mustConfirm = snapshot.TemplateType == CardTemplateType.TierBased
                                  && policy.RequireConfirmation;

                if (request.Mode == RedeemMode.ForcePending)
                {
                    mustConfirm = true;
                    _logger.LogInformation("ForcePending enabled -> mustConfirm={MustConfirm}", mustConfirm);
                }

                if (request.Mode == RedeemMode.ForceApply && policy.RequireConfirmation)
                {
                    _logger.LogWarning("ForceApply denied due to tenant policy (confirmation required).");
                    return Fail("Bu tenant üçün Tier redeem təsdiq tələb edir.");
                }

                if (mustConfirm && request.Delta<0)
                {
                    // 3a) OTP ilə PENDING
                    var otp = _otpGenerator.GenerateNumeric(6);
                    var rr = new RedeemRequest
                    {
                        TenantId = request.TenantId,
                        LoyaltyCardId = card.Id,
                        PointsDelta = delta,
                        Status = RedeemStatus.Pending,
                        Code = otp,
                        ExpiresUtc = DateTime.UtcNow.AddMinutes(policy.ConfirmationTtlMinutes),
                        IdempotencyKey = request.IdempotencyKey,
                        OperatorId = request.OperatorId,
                        OrderId = request.OrderId,
                        Note = request.Note
                    };

                    await uow.Repository<RedeemRequest>().AddAsync(rr);
                    uow.Commit();

                    _logger.LogInformation("Pending redeem created. RequestId={RequestId}, ExpiresInMin={TTL}, OtpMasked={Otp}",
                        rr.Id, policy.ConfirmationTtlMinutes, MaskOtp(otp));

                    // Bildiriş (SMS/email)
                    var destination = card.Customer.PhoneNumber; // sizdə necə saxlanırsa
                    if (!string.IsNullOrWhiteSpace(destination))
                    {
                        var text = $"Sizin OTP: {otp}\nwalvero.com #{otp}";
                        try
                        {
                            var data = new Dictionary<string, string>
                            {
                                { "type", "otp" },
                                { "otpCode", otp },
                                { "expiryMinutes", policy.ConfirmationTtlMinutes.ToString() }
                            };
                            await _notificationService.SendToUserAsync(appUserName.Value,"Təsdiq",text,data);
                             await Helper.SendSmsAsync(destination, text);
                            _logger.LogInformation("OTP SMS sent. Phone={Phone}", MaskPhone(destination));
                        }
                        catch (Exception smsEx)
                        {
                            _logger.LogError(smsEx, "OTP SMS send failed. Phone={Phone}", MaskPhone(destination));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No phone number to send OTP. CustomerId={CustomerId}", card.CustomerId);
                    }

                    return new RedeemResultDto
                    {
                        Success = true,
                        IsPending = true,
                        LoyaltyCardId = card.Id,
                        RequiresOtp = mustConfirm,
                        AppliedDelta = delta,
                        RedeemRequestId = rr.Id,
                        Message = $"Təsdiq kodu göndərildi. Keçərlilik: {policy.ConfirmationTtlMinutes} dəqiqə."
                    };
                }

                // 4) Dərhal tətbiq (Progress/Points-based və ya policy OFF)
                if (request.DryRun)
                {
                    _logger.LogInformation("DryRun mode -> no DB changes. CardId={CardId}, Delta={Delta}", card.Id, delta);
                    return new RedeemResultDto
                    {
                        Success = true,
                        IsPending = false,
                        LoyaltyCardId = card.Id,
                        AppliedDelta = 0,
                        Message = $"Kart #{card.Id} üçün {(delta >= 0 ? "+" : "")}{delta} tətbiq olundu.",
                    };
                }

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                card.LifetimePoints += 1;
                ApplyDelta(card, snapshot, delta);

                var tx = BuildTransaction(card, delta, request.OrderId, request.Note, TransactionType.Earned);
                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Commit();

                _logger.LogInformation("Delta applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, Delta={Delta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, delta);

                // 5) Platforma → router
                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // 1) PRIMARY cəhd
                _logger.LogInformation("Routing after confirm to PRIMARY {Platform}.", primaryPlatform);
                var primaryRes = await _walletRouter.RouteAsync(
                    primaryPlatform,
                    card.Id,
                    snapshot,
                    card.CurrentPoints,
                    card.CurrentSteps,
                    delta,
                    ct);

                if (!primaryRes.Success)
                {
                    _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                }
                else
                {
                    primaryOk = true;
                }

                // 2) SECONDARY cəhd – primary uğursuz olsa belə MÜTLƏQ çağırırıq
                _logger.LogInformation("Trying SECONDARY {Platform}.", secondaryPlatform);
                var secondaryRes = await _walletRouter.RouteAsync(
                    secondaryPlatform,
                    card.Id,
                    snapshot,
                    card.CurrentPoints,
                    card.CurrentSteps,
                    delta,
                    ct);

                if (!secondaryRes.Success)
                {
                    _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                }
                else
                {
                    secondaryOk = true;
                }
               

                if (!string.IsNullOrWhiteSpace(appUserName.ToString()))
                {
                    var message = new CardUpdatedMessage
                    {
                        CardId = card.Id,
                        ProgramId = card.LoyaltyProgramId,
                        TenantId = card.TenantId,
                        CurrentPoints = card.CurrentPoints,
                        LastRedeemAmount = request.Delta,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var groupName = $"user-{appUserName}";
                    var data = new Dictionary<string, string>
{
    { "type", "normal" }
};
                    await _hubContext
                        .Clients
                        .Group(groupName)
                        .SendAsync("CardUpdated", message, cancellationToken: ct);
                    var message1 = delta > 0 ? $"{snapshot.ProgramName} balansınıza {delta} AZN əlavə olundu!" : $"{snapshot.ProgramName} balansınızdan {delta * 1} AZN çıxıldı!";
                    await _notificationService.SendToUserAsync(appUserName.Value, snapshot.ProgramName, message1,data);
                }
                // 3) Nəticəni qərar ver
                if (!primaryOk && !secondaryOk)
                {
                    // hər ikisi batdı
                    return Fail("Wallet yenilənmədi (Apple və ya Google).");
                }


                _logger.LogInformation("Redeem completed successfully. CardId={CardId}, AppliedDelta={Delta}", card.Id, delta);
                

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"Kart #{card.Id} üçün {(delta >= 0 ? "+" : "")}{delta} tətbiq olundu.",
                    LoyaltyCardId = card.Id,
                    RequiresOtp = false,
                    AppliedDelta = delta
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RedeemAsync exception.");
                return Fail("Yeniləmə zamanı sistem xətası baş verdi.");
            }
        }

        // CONFIRM (OTP)
        public async Task<RedeemResultDto> ConfirmRedeemAsync(int redeemRequestId, string code, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["RedeemRequestId"] = redeemRequestId
            });

            try
            {
                _logger.LogInformation("Confirm started. RequestId={RequestId}", redeemRequestId);

                var rr = await uow.Repository<RedeemRequest>().GetByIdAsync(redeemRequestId);
                if (rr is null)
                {
                    _logger.LogWarning("Redeem request not found. RequestId={RequestId}", redeemRequestId);
                    return Fail("Sorğu tapılmadı.");
                }

                if (rr.Status != RedeemStatus.Pending)
                {
                    _logger.LogWarning("Redeem request status is not Pending. Status={Status}", rr.Status);
                    return Fail("Sorğunun statusu PENDING deyil.");
                }

                if (rr.ExpiresUtc < DateTime.UtcNow)
                {
                    rr.Status = RedeemStatus.Expired;
                    uow.Repository<RedeemRequest>().Update(rr);
                    uow.Commit();
                    _logger.LogWarning("Redeem request expired. RequestId={RequestId}", redeemRequestId);
                    return Fail("Kodun vaxtı keçib.");
                }

                // OTP yoxlamanı aktiv etmək istəsəniz bu hissəni açın:
                // if (!string.Equals(rr.Code, code, StringComparison.Ordinal))
                // {
                //     _logger.LogWarning("OTP mismatch. Provided={Provided}, Stored={StoredMasked}", MaskOtp(code), MaskOtp(rr.Code));
                //     return Fail("Təsdiq kodu yanlışdır.");
                // }

                var card = await _cardSvc.GetById(rr.LoyaltyCardId, ct);
                if (card is null)
                {
                    _logger.LogWarning("Card not found for Confirm. CardId={CardId}", rr.LoyaltyCardId);
                    return Fail("Kart tapılmadı.");
                }

                var program = await _programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                var branding = await _brandingSvc.GetByTenantIdAsync(rr.TenantId, ct);
                string baseUrl = "https://walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    var rel = "/" + dbPath.TrimStart('/', '\\');
                    return baseUrl + rel;
                }
                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                ApplyDelta(card, snapshot, rr.PointsDelta);

                var tx = BuildTransaction(card, rr.PointsDelta, rr.OrderId, rr.Note,TransactionType.Redeemed);
                rr.Status = RedeemStatus.Applied;

                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Repository<RedeemRequest>().Update(rr);
                uow.Commit();

                _logger.LogInformation("Confirm applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, Delta={Delta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, rr.PointsDelta);
                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // 1) PRIMARY cəhd
                _logger.LogInformation("Routing after confirm to PRIMARY {Platform}.", primaryPlatform);
                var primaryRes = await _walletRouter.RouteAsync(
                    primaryPlatform,
                    card.Id,
                    snapshot,
                    card.CurrentPoints,
                    card.CurrentSteps,
                    rr.PointsDelta,
                    ct);

                if (!primaryRes.Success)
                {
                    _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                }
                else
                {
                    primaryOk = true;
                }

                // 2) SECONDARY cəhd – primary uğursuz olsa belə MÜTLƏQ çağırırıq
                _logger.LogInformation("Trying SECONDARY {Platform}.", secondaryPlatform);
                var secondaryRes = await _walletRouter.RouteAsync(
                    secondaryPlatform,
                    card.Id,
                    snapshot,
                    card.CurrentPoints,
                    card.CurrentSteps,
                    rr.PointsDelta,
                    ct);

                if (!secondaryRes.Success)
                {
                    _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                }
                else
                {
                    secondaryOk = true;
                }

                // 3) Nəticəni qərar ver
                if (!primaryOk && !secondaryOk)
                {
                    // hər ikisi batdı
                    return Fail("Wallet yenilənmədi (Apple və ya Google).");
                }
                var appUserName = card.Customer?.applicationUser?.Id;

                if (!string.IsNullOrWhiteSpace(appUserName.ToString()))
                {
                    var message = new CardUpdatedMessage
                    {
                        CardId = card.Id,
                        ProgramId = card.LoyaltyProgramId,
                        TenantId = card.TenantId,
                        CurrentPoints = card.CurrentPoints,
                        LastRedeemAmount = rr.PointsDelta,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var groupName = $"user-{appUserName}";
                    var data = new Dictionary<string, string>
{
    { "type", "normal" }
};
                    await _hubContext
                        .Clients
                        .Group(groupName)
                        .SendAsync("CardUpdated", message, cancellationToken: ct);
                    var message1 = rr.PointsDelta > 0 ? $"{snapshot.ProgramName} balansınıza {rr.PointsDelta} AZN əlavə olundu!" : $"{snapshot.ProgramName} balansınızdan {rr.PointsDelta * 1} AZN çıxıldı!";
                await _notificationService.SendToUserAsync(appUserName.Value, snapshot.ProgramName, message1,data);
                }



                _logger.LogInformation("Confirm completed successfully. CardId={CardId}, AppliedDelta={Delta}", card.Id, rr.PointsDelta);

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"Təsdiq edildi. Kart #{card.Id} üçün {(rr.PointsDelta >= 0 ? "+" : "")}{rr.PointsDelta} tətbiq olundu.",
                    LoyaltyCardId = card.Id,
                    AppliedDelta = rr.PointsDelta
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmRedeemAsync exception. RequestId={RequestId}", redeemRequestId);
                return Fail("Təsdiq zamanı sistem xətası baş verdi.");
            }
        }

        // ------- Helpers -------

        private static void ApplyDelta(LoyaltyCard card, CardTemplateSnapshot snapshot, int delta)
        {
            switch (snapshot.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    card.CurrentSteps = Math.Max(0, card.CurrentSteps + delta);
                    card.CurrentPoints = Math.Max(0, card.CurrentPoints + delta);
                    break;

                case CardTemplateType.PointsBased:
                case CardTemplateType.TierBased:
                    card.CurrentPoints = Math.Max(0, card.CurrentPoints + delta);
                    break;
            }
        }

        private static Transaction BuildTransaction(LoyaltyCard card, int delta, string? orderId, string? note, TransactionType type)
        {
            return new Transaction
            {
                LoyaltyCardId = card.Id,
                Points = card.CurrentPoints,
                BalanceAfter = card.CurrentPoints,
                ReferenceNumber = card.BarcodeValue,
                Type = type,
                OrderId = orderId,
                Description = delta >= 0
                    ? $"Kart balansının artımı: +{delta}"
                    : $"Ödəniş zamanı bonus istifadə edildi: {delta}",
                // istəsən burada OperatorId/Note da saxla
            };
        }

      
        private static RedeemResultDto Fail(string msg) => new() { Success = false, Message = msg };

        // ---- Small masking helpers for safe logging ----
        private static string MaskOtp(string? otp)
            => string.IsNullOrWhiteSpace(otp) ? "<null>" : new string('*', Math.Max(0, otp.Length - 2)) + otp[^2..];

        private static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "<null>";
            var trimmed = phone.Trim();
            if (trimmed.Length <= 4) return new string('*', trimmed.Length);
            return new string('*', trimmed.Length - 4) + trimmed[^4..];
        }
    }
}
