using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PosSessionService : IPosSessionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILoyaltyCardRepository _loyaltyCardRepository;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ITenantProvider _tenantProvider;

        public PosSessionService(
            IUnitOfWork unitOfWork,
            ICustomerRepository customerRepository,
            ILoyaltyCardRepository loyaltyCardRepository,
            LoyaltyWalletDbContext dbContext,
            ITenantProvider tenantProvider)
        {
            _unitOfWork = unitOfWork;
            _customerRepository = customerRepository;
            _loyaltyCardRepository = loyaltyCardRepository;
            _dbContext = dbContext;
            _tenantProvider = tenantProvider;
        }

        public async Task<ServiceResult<ScannerSessionResponseDto>> StartSessionAsync(
            StartScannerSessionDto dto, 
            CancellationToken ct = default)
        {
            try
            {
                // Get TenantId from JWT using TenantProvider
                var tenantId = _tenantProvider.CurrentTenantId;
                if (tenantId == null)
                {
                    return ServiceResult<ScannerSessionResponseDto>.Fail("TenantId is required. Unable to get TenantId from JWT token.");
                }

                var barcode = dto.Barcode.Trim();
                Customer? customer = null;
                LoyaltyCard? activeCard = null;

                // First, try to find card by barcode
                activeCard = await _loyaltyCardRepository.GetByBarcodeValueAsync(tenantId.Value, barcode, ct);

                if (activeCard != null)
                {
                    // Card found by barcode, get customer
                    customer = activeCard.Customer;
                    
                    if (customer == null || !customer.IsActive)
                    {
                        return ServiceResult<ScannerSessionResponseDto>.Fail("Customer not found or inactive for this barcode.");
                    }
                }
                else if (long.TryParse(barcode, out long walveroId))
                {
                    // Barcode not found, try WalveroId
                    customer = await _customerRepository.GetByWalveroIdAsync(tenantId.Value, walveroId, ct);
                    
                    if (customer == null || !customer.IsActive)
                    {
                        return ServiceResult<ScannerSessionResponseDto>.Fail("Customer not found or inactive by barcode or WalveroId.");
                    }

                    // Find the active card for this customer
                    var loyaltyCards = await _loyaltyCardRepository.ListByCustomerAsync(tenantId.Value, customer.Id, ct);
                    activeCard = loyaltyCards
                        .Where(c => c.IsActive && c.Status == CardStatus.Active)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                }
                else
                {
                    return ServiceResult<ScannerSessionResponseDto>.Fail("Invalid barcode format.");
                }

                if (customer == null)
                {
                    return ServiceResult<ScannerSessionResponseDto>.Fail("Customer not found.");
                }

                // Get loyalty balance (points)
                // Note: Balance can be negative in reverse scenarios (e.g., when reversing an EARN transaction
                // that was already spent). We allow negative balances to accurately reflect the true state.
                decimal loyaltyBalance = activeCard?.CurrentPoints ?? 0;

                // Get user name
                string name = $"{customer.FirstName} {customer.LastName}".Trim();
                if (string.IsNullOrEmpty(name))
                {
                    name = customer.PhoneNumber ?? "Customer";
                }

                // Get WalveroId from applicationUser
                long? walveroUserId = null;
                if (customer.applicationUser != null)
                {
                    walveroUserId = customer.applicationUser.WalveroId;
                }

                // Generate session token (GUID-based)
                string sessionToken = GenerateSessionToken();

                // Set session expiry (8 hours from now)
                DateTime sessionExpiry = DateTime.UtcNow.AddHours(8);

                // Create PosSession
                var posSession = new PosSession
                {
                    TenantId = tenantId.Value,
                    scannerDeviceId = dto.ScannerDeviceId,
                    userId = dto.UserId,
                    tableId = dto.TableId,
                    Barcode = barcode,
                    SessionToken = sessionToken,
                    IsActive = true
                };

                var posSessionRepo = _unitOfWork.Repository<PosSession>();
                await posSessionRepo.AddAsync(posSession);
                _unitOfWork.Commit();

                // Create response
                var response = new ScannerSessionResponseDto
                {
                    SessionToken = sessionToken,
                    WalveroUserId = walveroUserId,
                    Name = name,
                    LoyaltyBalance = loyaltyBalance,
                    SessionExpiry = sessionExpiry,
                    Status = posSession.Status
                };

                return ServiceResult<ScannerSessionResponseDto>.Ok(response, "Session started successfully.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ScannerSessionResponseDto>.Fail($"Failed to start session: {ex.Message}");
            }
        }

        public async Task<ServiceResult<LinkSessionResponseDto>> LinkSessionAsync(
            LinkSessionDto dto,
            CancellationToken ct = default)
        {
            try
            {
                // Find the session by sessionToken
                var posSession = await _dbContext.Set<PosSession>()
                    .Include(ps => ps.applicationUser)
                    .ThenInclude(u => u.Customers)
                    .FirstOrDefaultAsync(ps => ps.SessionToken == dto.SessionToken && ps.IsActive, ct);

                if (posSession == null)
                {
                    return ServiceResult<LinkSessionResponseDto>.Fail("Session not found or inactive.");
                }

                if (posSession.Status == SessionStatus.Linked)
                {
                    return ServiceResult<LinkSessionResponseDto>.Fail("Session is already linked.");
                }

                // Update session with orderId and set status to Linked
                if (int.TryParse(dto.OrderId, out int orderIdInt))
                {
                    posSession.orderId = orderIdInt;
                }
                else
                {
                    // If orderId is not a number, we might need to store it as string
                    // For now, we'll just store it in a description field or skip
                    // Actually, orderId is int?, so we'll need to handle this differently
                    // Let's assume orderId should be numeric
                    return ServiceResult<LinkSessionResponseDto>.Fail("Invalid OrderId format. Must be a number.");
                }

                posSession.Status = SessionStatus.Linked;
                posSession.EditDate = DateTime.UtcNow;

                // Get customer from barcode (walveroId or loyalty card barcode)
                Customer? customer = null;
                LoyaltyCard? activeCard = null;

                if (string.IsNullOrEmpty(posSession.Barcode))
                {
                    return ServiceResult<LinkSessionResponseDto>.Fail("Barcode not found in session.");
                }

                var barcode = posSession.Barcode.Trim();

                // First, try to find card by barcode
                activeCard = await _loyaltyCardRepository.GetByBarcodeValueAsync(posSession.TenantId, barcode, ct);

                if (activeCard != null)
                {
                    // Card found by barcode, get customer
                    customer = activeCard.Customer;
                    
                    if (customer == null || !customer.IsActive)
                    {
                        return ServiceResult<LinkSessionResponseDto>.Fail("Customer not found or inactive for this barcode.");
                    }
                }
                else if (long.TryParse(barcode, out long walveroUserId))
                {
                    // Barcode not found, try WalveroId
                    customer = await _customerRepository.GetByWalveroIdAsync(posSession.TenantId, walveroUserId, ct);
                    
                    if (customer == null || !customer.IsActive)
                    {
                        return ServiceResult<LinkSessionResponseDto>.Fail("Customer not found or inactive by barcode or WalveroId.");
                    }

                    // Find the active card for this customer
                    var loyaltyCards = await _loyaltyCardRepository.ListByCustomerAsync(posSession.TenantId, customer.Id, ct);
                    activeCard = loyaltyCards
                        .Where(c => c.IsActive && c.Status == CardStatus.Active)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                }
                else
                {
                    return ServiceResult<LinkSessionResponseDto>.Fail("Invalid barcode format in session.");
                }

                if (activeCard == null)
                {
                    return ServiceResult<LinkSessionResponseDto>.Fail("Active loyalty card not found.");
                }

                // Note: Balance can be negative in reverse scenarios (e.g., when reversing an EARN transaction
                // that was already spent). We allow negative balances to accurately reflect the true state.

                // Get loyalty program and template for rewards summary
                var program = await _dbContext.Set<LoyaltyProgram>()
                    .Include(p => p.CardTemplate)
                    .FirstOrDefaultAsync(p => p.Id == activeCard.LoyaltyProgramId, ct);

                // Get WalveroId
                long? walveroId = null;
                if (customer.applicationUser != null)
                {
                    walveroId = customer.applicationUser.WalveroId;
                }

                // Build rewards summary
                var rewardsSummary = new RewardsSummary
                {
                    AvailableRewardCount = activeCard.CompletedCycles,
                    CompletedCycles = activeCard.CompletedCycles,
                    CurrentSteps = activeCard.CurrentSteps,
                    TotalSteps = program?.CardTemplate?.TotalSteps ?? 0
                };

                if (program?.CardTemplate != null)
                {
                    rewardsSummary.FreeRewardLabel = string.IsNullOrWhiteSpace(program.CardTemplate.FreeRewardLabel)
                        ? "bonus"
                        : program.CardTemplate.FreeRewardLabel;
                }
                else
                {
                    rewardsSummary.FreeRewardLabel = "bonus";
                }

                // Update and save session
                var posSessionRepo = _unitOfWork.Repository<PosSession>();
                posSessionRepo.Update(posSession);
                _unitOfWork.Commit();

                // Create response
                var response = new LinkSessionResponseDto
                {
                    WalveroId = walveroId,
                    LoyaltyBalance = activeCard.CurrentPoints,
                    RewardsSummary = rewardsSummary
                };

                return ServiceResult<LinkSessionResponseDto>.Ok(response, "Session linked successfully.");
            }
            catch (Exception ex)
            {
                return ServiceResult<LinkSessionResponseDto>.Fail($"Failed to link session: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<GetPosSessionDto>>> GetSessionsAsync(
            SessionStatus? status, 
            CancellationToken ct = default)
        {
            try
            {
                // Get TenantId from JWT using TenantProvider
                var tenantId = _tenantProvider.CurrentTenantId;
                if (tenantId == null)
                {
                    return ServiceResult<List<GetPosSessionDto>>.Fail("TenantId is required. Unable to get TenantId from JWT token.");
                }

                // Build query
                var query = _dbContext.Set<PosSession>()
                    .Where(ps => ps.TenantId == tenantId.Value && ps.IsActive);

                // Filter by status if provided
                if (status.HasValue)
                {
                    query = query.Where(ps => ps.Status == status.Value);
                }

                // Order by RegDate descending (newest first)
                var sessions = await query
                    .OrderByDescending(ps => ps.RegDate)
                    .Select(ps => new GetPosSessionDto
                    {
                        Id = ps.Id,
                        TenantId = ps.TenantId,
                        ScannerDeviceId = ps.scannerDeviceId,
                        UserId = ps.userId,
                        OrderId = ps.orderId,
                        TableId = ps.tableId,
                        Barcode = ps.Barcode,
                        Status = ps.Status,
                        SessionToken = ps.SessionToken,
                        RegDate = ps.RegDate,
                        EditDate = ps.EditDate,
                        IsActive = ps.IsActive
                    })
                    .ToListAsync(ct);

                return ServiceResult<List<GetPosSessionDto>>.Ok(sessions, $"Found {sessions.Count} session(s).");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<GetPosSessionDto>>.Fail($"Failed to get sessions: {ex.Message}");
            }
        }

        private string GenerateSessionToken()
        {
            // Generate a secure random token
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
        }
    }
}

