using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IFeedbackService
    {
        Task<ServiceResult> CreateFeedbackAsync(int customerId, CreateFeedbackDto dto);
        Task<ServiceResult> ReplyToFeedbackAsync(int storeUserId, int feedbackId, ReplyToFeedbackDto dto);
        Task<ServiceResult> CustomerReplyAsync(int customerId, int feedbackId, ReplyToFeedbackDto dto);
        Task<ServiceResult> CloseFeedbackAsync(int customerId, int feedbackId);
        Task<ServiceResult> GetCustomerFeedbacksAsync(int customerId, FeedbackStatus? status = null);
        Task<ServiceResult> GetCustomerFeedbacksByApplicationUserIdAsync(int applicationUserId, FeedbackStatus? status = null);
        Task<ServiceResult> GetStoreFeedbacksAsync(int tenantId, FeedbackStatus? status = null);
        Task<ServiceResult> GetFeedbackDetailsAsync(int feedbackId, int? userId = null, bool isStoreUser = false);
    }
}

