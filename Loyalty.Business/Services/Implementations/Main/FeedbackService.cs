using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;


namespace Loyalty.Business.Services.Implementations.Main
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FeedbackService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            LoyaltyWalletDbContext dbContext,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResult> CreateFeedbackAsync(int customerId, CreateFeedbackDto dto)
        {
            try
            {
                // Validate rating if provided
                if (dto.Rating.HasValue && (dto.Rating.Value < 0 || dto.Rating.Value > 5))
                {
                    return new ServiceResult(false, "Rating must be between 0 and 5.");
                }

                // Validate tenant exists
                var tenant = await _unitOfWork.Repository<Tenant>().GetByIdAsync(dto.TenantId);
                if (tenant == null)
                {
                    return new ServiceResult(false, "Store not found.");
                }

                // Validate customer exists
                var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(customerId);
                if (customer == null)
                {
                    return new ServiceResult(false, "Customer not found.");
                }

                // Ensure customer belongs to the tenant
                if (customer.TenantId != dto.TenantId)
                {
                    return new ServiceResult(false, "Customer does not belong to this store.");
                }

                var feedback = new StoreFeedback
                {
                    TenantId = dto.TenantId,
                    CustomerId = customerId,
                    Type = dto.Type,
                    Message = dto.Message,
                    Rating = dto.Rating,
                    SharePhoneNumber = dto.SharePhoneNumber,
                    Status = FeedbackStatus.Open,
                    CreatedAt = DateTime.UtcNow,
                    RegUserId = customerId, // Using customerId as RegUserId
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Repository<StoreFeedback>().AddAsync(feedback);
                _unitOfWork.Commit();

                var response = _mapper.Map<GetStoreFeedbackDto>(feedback);
                response.TenantName = tenant.CompanyName;
                response.CustomerName = $"{customer.FirstName} {customer.LastName}";
                if (dto.SharePhoneNumber)
                {
                    response.CustomerPhoneNumber = customer.PhoneNumber;
                }

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error creating feedback: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ReplyToFeedbackAsync(int storeUserId, int feedbackId, ReplyToFeedbackDto dto)
        {
            try
            {
                var feedback = await _dbContext.StoreFeedbacks
                    .Include(f => f.Tenant)
                    .Include(f => f.Customer)
                        .ThenInclude(c => c.applicationUser)
                    .FirstOrDefaultAsync(f => f.Id == feedbackId);

                if (feedback == null)
                {
                    return new ServiceResult(false, "Feedback not found.");
                }

                // Verify store user belongs to the tenant
                var storeUser = await _dbContext.Users.FindAsync(storeUserId);
                if (storeUser == null || storeUser.TenantId != feedback.TenantId)
                {
                    return new ServiceResult(false, "You are not authorized to reply to this feedback.");
                }

                var reply = new StoreFeedbackReply
                {
                    StoreFeedbackId = feedbackId,
                    SenderType = FeedbackReplySenderType.Store,
                    Message = dto.Message,
                    CreatedAt = DateTime.UtcNow,
                    RegUserId = storeUserId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Repository<StoreFeedbackReply>().AddAsync(reply);
                _unitOfWork.Commit();

                // Create notification for customer
                if (feedback.Customer?.applicationUser != null)
                {
                    var notification = new Notification
                    {
                        UserId = feedback.Customer.applicationUser.Id,
                        Title = "New reply from the store",
                        Body = $"The store responded to your {feedback.Type.ToString().ToLower()}.",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        RelatedFeedbackId = feedbackId,
                        RegUserId = storeUserId,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    };

                    await _unitOfWork.Repository<Notification>().AddAsync(notification);
                    _unitOfWork.Commit();
                }

                var response = _mapper.Map<GetStoreFeedbackReplyDto>(reply);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error replying to feedback: {ex.Message}");
            }
        }

        public async Task<ServiceResult> CustomerReplyAsync(int customerId, int feedbackId, ReplyToFeedbackDto dto)
        {
            try
            {
                var feedback = await _unitOfWork.Repository<StoreFeedback>().GetByIdAsync(feedbackId);
                if (feedback == null)
                {
                    return new ServiceResult(false, "Feedback not found.");
                }

                // Verify customer owns the feedback
                if (feedback.CustomerId != customerId)
                {
                    return new ServiceResult(false, "You are not authorized to reply to this feedback.");
                }

                var reply = new StoreFeedbackReply
                {
                    StoreFeedbackId = feedbackId,
                    SenderType = FeedbackReplySenderType.Customer,
                    Message = dto.Message,
                    CreatedAt = DateTime.UtcNow,
                    RegUserId = customerId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Repository<StoreFeedbackReply>().AddAsync(reply);
                _unitOfWork.Commit();

                var response = _mapper.Map<GetStoreFeedbackReplyDto>(reply);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error replying to feedback: {ex.Message}");
            }
        }

        public async Task<ServiceResult> CloseFeedbackAsync(int customerId, int feedbackId)
        {
            try
            {
                var feedback = await _unitOfWork.Repository<StoreFeedback>().GetByIdAsync(feedbackId);
                if (feedback == null)
                {
                    return new ServiceResult(false, "Feedback not found.");
                }

                // Verify customer owns the feedback
                if (feedback.CustomerId != customerId)
                {
                    return new ServiceResult(false, "You are not authorized to close this feedback.");
                }

                if (feedback.Status == FeedbackStatus.Closed)
                {
                    return new ServiceResult(false, "Feedback is already closed.");
                }

                feedback.Status = FeedbackStatus.Closed;
                feedback.UpdatedAt = DateTime.UtcNow;
                feedback.EditUserId = customerId;
                feedback.EditDate = DateTime.UtcNow;

                _unitOfWork.Repository<StoreFeedback>().Update(feedback);
                _unitOfWork.Commit();

                return new ServiceResult(true, "Feedback closed successfully.");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error closing feedback: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GetCustomerFeedbacksAsync(int customerId, FeedbackStatus? status = null)
        {
            try
            {
                var query = _dbContext.StoreFeedbacks
                    .Include(f => f.Tenant)
                    .Include(f => f.Customer)
                    .Include(f => f.Replies.Where(r => r.IsActive))
                    .Where(f => f.CustomerId == customerId && f.IsActive);

                if (status.HasValue)
                {
                    query = query.Where(f => f.Status == status.Value);
                }

                var feedbacks = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .ToListAsync();

                var response = feedbacks.Select(f =>
                {
                    var dto = _mapper.Map<GetStoreFeedbackDto>(f);
                    dto.TenantName = f.Tenant?.CompanyName ?? "";
                    dto.CustomerName = $"{f.Customer?.FirstName} {f.Customer?.LastName}".Trim();
                    dto.Replies = f.Replies
                        .Where(r => r.IsActive)
                        .OrderBy(r => r.CreatedAt)
                        .Select(r => _mapper.Map<GetStoreFeedbackReplyDto>(r))
                        .ToList();
                    return dto;
                }).ToList();

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error retrieving feedbacks: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GetCustomerFeedbacksByApplicationUserIdAsync(int applicationUserId, FeedbackStatus? status = null)
        {
            try
            {
                // Get all customers for this application user (can have multiple tenants)
                var customers = await _dbContext.Customers
                    .Where(c => c.applicationUserId == applicationUserId)
                    .Select(c => c.Id)
                    .ToListAsync();

                if (!customers.Any())
                {
                    return new ServiceResult(true, new List<GetStoreFeedbackDto>());
                }

                var query = _dbContext.StoreFeedbacks
                    .Include(f => f.Tenant)
                    .Include(f => f.Customer)
                    .Include(f => f.Replies.Where(r => r.IsActive))
                    .Where(f => customers.Contains(f.CustomerId) && f.IsActive);

                if (status.HasValue)
                {
                    query = query.Where(f => f.Status == status.Value);
                }

                var feedbacks = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .ToListAsync();

                var response = feedbacks.Select(f =>
                {
                    var dto = _mapper.Map<GetStoreFeedbackDto>(f);
                    dto.TenantName = f.Tenant?.CompanyName ?? "";
                    dto.CustomerName = $"{f.Customer?.FirstName} {f.Customer?.LastName}".Trim();
                    dto.Replies = f.Replies
                        .Where(r => r.IsActive)
                        .OrderBy(r => r.CreatedAt)
                        .Select(r => _mapper.Map<GetStoreFeedbackReplyDto>(r))
                        .ToList();
                    return dto;
                }).ToList();

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error retrieving feedbacks: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GetStoreFeedbacksAsync(int tenantId, FeedbackStatus? status = null)
        {
            try
            {
                var query = _dbContext.StoreFeedbacks
                    .Include(f => f.Tenant)
                    .Include(f => f.Customer)
                    .Include(f => f.Replies.Where(r => r.IsActive))
                    .Where(f => f.TenantId == tenantId && f.IsActive);

                if (status.HasValue)
                {
                    query = query.Where(f => f.Status == status.Value);
                }

                var feedbacks = await query
                    .OrderByDescending(f => f.CreatedAt)
                    .ToListAsync();

                var response = feedbacks.Select(f =>
                {
                    var dto = _mapper.Map<GetStoreFeedbackDto>(f);
                    dto.TenantName = f.Tenant?.CompanyName ?? "";
                    dto.CustomerName = $"{f.Customer?.FirstName} {f.Customer?.LastName}".Trim();
                    // Only include phone if SharePhoneNumber is true
                    if (f.SharePhoneNumber)
                    {
                        dto.CustomerPhoneNumber = f.Customer?.PhoneNumber;
                    }
                    dto.Replies = f.Replies
                        .Where(r => r.IsActive)
                        .OrderBy(r => r.CreatedAt)
                        .Select(r => _mapper.Map<GetStoreFeedbackReplyDto>(r))
                        .ToList();
                    return dto;
                }).ToList();

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error retrieving feedbacks: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GetFeedbackDetailsAsync(int feedbackId, int? userId = null, bool isStoreUser = false)
        {
            try
            {
                var feedback = await _dbContext.StoreFeedbacks
                    .Include(f => f.Tenant)
                    .Include(f => f.Customer)
                    .Include(f => f.Replies.Where(r => r.IsActive))
                    .FirstOrDefaultAsync(f => f.Id == feedbackId && f.IsActive);

                if (feedback == null)
                {
                    return new ServiceResult(false, "Feedback not found.");
                }

                // Authorization check
                if (userId.HasValue)
                {
                    if (isStoreUser)
                    {
                        var user = await _dbContext.Users.FindAsync(userId.Value);
                        if (user == null || user.TenantId != feedback.TenantId)
                        {
                            return new ServiceResult(false, "You are not authorized to view this feedback.");
                        }
                    }
                    else
                    {
                        // Customer check - need to get customer from ApplicationUser
                        var customer = await _dbContext.Customers
                            .FirstOrDefaultAsync(c => c.applicationUserId == userId.Value && c.TenantId==feedback.TenantId);
                        if (customer == null || customer.Id != feedback.CustomerId)
                        {
                            return new ServiceResult(false, "You are not authorized to view this feedback.");
                        }
                    }
                }

                var response = _mapper.Map<GetStoreFeedbackDto>(feedback);
                response.TenantName = feedback.Tenant?.CompanyName ?? "";
                response.CustomerName = $"{feedback.Customer?.FirstName} {feedback.Customer?.LastName}".Trim();
                
                // Phone number sharing logic
                if (isStoreUser && feedback.SharePhoneNumber)
                {
                    response.CustomerPhoneNumber = feedback.Customer?.PhoneNumber;
                }
                else if (!isStoreUser)
                {
                    // Customer can always see their own phone
                    response.CustomerPhoneNumber = feedback.Customer?.PhoneNumber;
                }

                response.Replies = feedback.Replies
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => _mapper.Map<GetStoreFeedbackReplyDto>(r))
                    .ToList();

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error retrieving feedback details: {ex.Message}");
            }
        }
    }
}

