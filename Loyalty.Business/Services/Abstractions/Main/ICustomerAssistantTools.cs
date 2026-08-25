using Loyalty.Business.Dtos.Chatbot;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// AI köməkçinin şəxsi-hesab tool-ları. İstifadəçi JWT-dən (ITenantProvider.CurrentUserId) həll olunur;
    /// model heç vaxt userId vermir. IsAuthenticated=false olduqda tool-lar aktiv edilmir.
    /// </summary>
    public interface ICustomerAssistantTools
    {
        bool IsAuthenticated { get; }

        Task<List<MyCardDto>> GetMyCardsAsync(string? placeName, CancellationToken ct = default);
        Task<List<MyTransactionDto>> GetMyTransactionsAsync(string? placeName, int limit, CancellationToken ct = default);
    }
}
