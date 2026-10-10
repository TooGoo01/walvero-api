using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Resources;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class PosCustomerImportService : IPosCustomerImportService
    {
        private const int SystemUserId = 1;

        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ICardNumberGenerator _cardNumberGenerator;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<PosCustomerImportService> _logger;

        public PosCustomerImportService(
            ILoyaltyProgramRepository programRepo,
            ILoyaltyCardRepository cardRepo,
            ICardNumberGenerator cardNumberGenerator,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<PosCustomerImportService> logger)
        {
            _programRepo = programRepo;
            _cardRepo = cardRepo;
            _cardNumberGenerator = cardNumberGenerator;
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        public async Task<ServiceResult<ImportCustomerResultDto>> ImportAsync(int tenantId, ImportCustomerDto dto, bool dryRun, CancellationToken ct = default)
        {
            if (tenantId <= 0) return ServiceResult<ImportCustomerResultDto>.Fail("Tenant not resolved.");
            if (dto == null) return ServiceResult<ImportCustomerResultDto>.Fail("Body required.");

            // 1) Telefon
            var phone = PhoneNormalizer.ToE164(dto.Phone);
            if (phone == null || phone.Length != 13 || !phone.StartsWith("+994"))
                return ServiceResult<ImportCustomerResultDto>.Fail($"Telefon Azərbaycan nömrəsi deyil: '{dto.Phone}'.");
            if (!BirthDateRules.IsValidInput(dto.DateOfBirth, DateTime.UtcNow))
                return ServiceResult<ImportCustomerResultDto>.Fail(AppMessages.Get("DobInvalid"));

            // 2) Proqram
            var program = await _programRepo.GetByIdNameAsync(dto.LoyaltyProgramId, ct);
            if (program == null || program.TenantId != tenantId)
                return ServiceResult<ImportCustomerResultDto>.Fail("Program not found for this tenant.");
            var templateType = program.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            if (templateType == CardTemplateType.ProgressBased && dto.Balance != 0)
                return ServiceResult<ImportCustomerResultDto>.Fail("ProgressBased (ştamp) proqrama balans köçürməsi dəstəklənmir.");

            var result = new ImportCustomerResultDto { Phone = phone, DryRun = dryRun };
            var now = DateTime.UtcNow;

            // 3) Müştəri — telefon variantları ilə (inactive də daxil)
            var variants = PhoneNormalizer.Variants(phone);
            var customer = await _db.Set<Customer>()
                .Where(c => c.TenantId == tenantId && variants.Contains(c.PhoneNumber))
                .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (customer == null)
            {
                result.CustomerCreated = true;
                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName?.Trim() ?? string.Empty,
                    PhoneNumber = phone,
                    Email = NullIfEmpty(dto.Email),
                    Gender = dto.Gender,
                    DateOfBirth = dto.DateOfBirth,
                    IsActive = true,
                    LastActivityDate = now,
                    RegUserId = SystemUserId,
                    RegDate = now
                };

                if (!dryRun)
                {
                    customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                    await _uow.Repository<Customer>().AddAsync(customer);
                    try
                    {
                        await _uow.CommitAsync(ct);
                    }
                    catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                    {
                        // CardNumber toqquşması (qlobal unikal indeks) — bir dəfə yenidən yarat
                        customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                        await _uow.CommitAsync(ct);
                    }
                }
                else
                {
                    customer.CardNumber = "(generasiya olunacaq)";
                }
            }
            else
            {
                result.Warnings.Add($"Mövcud müştəri #{customer.Id} ({customer.PhoneNumber}).");
                if (!customer.IsActive) { customer.IsActive = true; result.Warnings.Add("Deaktiv müştəri aktivləşdirildi."); }
                if (string.IsNullOrWhiteSpace(customer.LastName) && !string.IsNullOrWhiteSpace(dto.LastName)) customer.LastName = dto.LastName.Trim();
                if (string.IsNullOrWhiteSpace(customer.Email) && !string.IsNullOrWhiteSpace(dto.Email)) customer.Email = dto.Email.Trim();
                customer.Gender ??= dto.Gender;
                customer.DateOfBirth ??= dto.DateOfBirth;
                customer.LastActivityDate = now;
                if (!dryRun)
                {
                    if (string.IsNullOrWhiteSpace(customer.CardNumber))
                        customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                    _uow.Repository<Customer>().Update(customer);
                    await _uow.CommitAsync(ct);
                }
            }
            result.CustomerId = customer.Id;

            // 4) Kart — aktiv → deaktiv reaktiv → yeni
            LoyaltyCard? card = null;
            if (customer.Id > 0)
            {
                card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
                if (card == null)
                {
                    var inactive = await _cardRepo.GetLastInactiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
                    if (inactive != null)
                    {
                        inactive.IsActive = true;
                        inactive.Status = CardStatus.Active;
                        inactive.ActivatedAt = now;
                        inactive.EditDate = now;
                        inactive.EditUserId = SystemUserId;
                        result.Warnings.Add($"Deaktiv kart #{inactive.Id} aktivləşdirildi.");
                        if (!dryRun)
                        {
                            _uow.Repository<LoyaltyCard>().Update(inactive);
                            await _uow.CommitAsync(ct);
                        }
                        card = inactive;
                    }
                }
            }

            if (card == null)
            {
                result.CardCreated = true;
                card = new LoyaltyCard
                {
                    TenantId = tenantId,
                    CustomerId = customer.Id,
                    LoyaltyProgramId = program.Id,
                    // CardNumber / BarcodeValue boş — SaveChanges Customer.CardNumber-dən doldurur
                    BarcodeType = BarcodeType.Code128,
                    Status = CardStatus.Active,
                    ActivatedAt = now,
                    RegUserId = SystemUserId,
                    RegDate = now,
                    IsActive = true
                };
                if (!dryRun)
                {
                    await _uow.Repository<LoyaltyCard>().AddAsync(card);
                    await _uow.CommitAsync(ct);
                }
                else
                {
                    card.CardNumber = customer.CardNumber ?? string.Empty;
                }
            }

            result.LoyaltyCardId = card.Id;
            result.CardNumber = card.CardNumber;
            result.BalanceBefore = card.CurrentPoints;

            // 5) Köhnə müştəri — welcome bonus / hədiyyə yoxdur
            var cardDirty = false;
            if (!dto.GrantWelcomeBonus)
            {
                if (card.FirstPurchaseBonusGrantedAt == null) { card.FirstPurchaseBonusGrantedAt = now; cardDirty = true; }
                if (card.SignupGiftGrantedAt == null) { card.SignupGiftGrantedAt = now; cardDirty = true; }
            }

            // 6) Idempotency
            var orderRef = NullIfEmpty(dto.OrderRef);
            if (orderRef != null && card.Id > 0)
            {
                var already = await _db.Set<Transaction>()
                    .AnyAsync(t => t.LoyaltyCardId == card.Id && t.OrderId == orderRef, ct);
                if (already)
                {
                    result.AlreadyImported = true;
                    result.Warnings.Add($"'{orderRef}' artıq tətbiq olunub — balans yazılmadı.");
                }
            }

            // 7) Balans
            if (dto.Balance != 0 && !result.AlreadyImported)
            {
                var delta = Math.Round(dto.Balance, 2);
                if (card.CurrentPoints + delta < 0)
                    return ServiceResult<ImportCustomerResultDto>.Fail($"Balans mənfi olardı: {card.CurrentPoints} + ({delta}).");

                card.CurrentPoints += delta;
                if (delta > 0) card.LifetimePoints += delta;
                card.EditDate = now;
                cardDirty = true;
                result.BalanceApplied = true;

                if (!dryRun)
                {
                    await _uow.Repository<Transaction>().AddAsync(new Transaction
                    {
                        LoyaltyCardId = card.Id,
                        Type = TransactionType.Adjusted,
                        Status = RedeemStatus.Applied,
                        Points = delta,
                        BalanceAfter = card.CurrentPoints,
                        Description = NullIfEmpty(dto.Description) ?? "Köçürmə balansı",
                        ReferenceNumber = NullIfEmpty(dto.ExternalCardNumber),
                        OrderId = orderRef,
                        BranchId = dto.BranchId,
                        RegUserId = SystemUserId,
                        RegDate = now,
                        IsActive = true,
                        LocalOccurredAt = DateTimeOffset.UtcNow,
                        TimeZoneIdUsed = "UTC"
                    });
                }
            }
            result.BalanceAfter = card.CurrentPoints;

            if (!dryRun && cardDirty)
            {
                _uow.Repository<LoyaltyCard>().Update(card);
                await _uow.CommitAsync(ct);
            }

            if (!dryRun)
            {
                _logger.LogInformation(
                    "Customer import: TenantId={TenantId}, CustomerId={CustomerId}, CardId={CardId}, Phone={Phone}, Created={Created}, Balance {Before}->{After}, Ref={Ref}",
                    tenantId, customer.Id, card.Id, phone, result.CustomerCreated, result.BalanceBefore, result.BalanceAfter, orderRef);
            }

            return ServiceResult<ImportCustomerResultDto>.Ok(result, dryRun ? "Dry run." : "Köçürüldü.");
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
    }
}
