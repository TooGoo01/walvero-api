namespace Loyalty.Business.Services.Abstractions.Sequences
{
    /// <summary>
    /// Service for generating unique CardNumbers for customers
    /// Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
    /// Example: 789017890001, 789017890002, ...
    /// </summary>
    public interface ICardNumberGenerator
    {
        /// <summary>
        /// Generates a unique CardNumber for a tenant
        /// </summary>
        /// <param name="tenantId">Tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Generated CardNumber</returns>
        Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken ct = default);
    }
}

