namespace Loyalty.DataAccess.Repositories.Abstractions.Sequences
{
    /// <summary>
    /// Repository for atomic sequence generation using SQL Server UPDATE...OUTPUT
    /// </summary>
    public interface ISequenceRepository
    {
        /// <summary>
        /// Atomically increments and returns the next sequence number for a tenant.
        /// Handles prefix rollover when LastNumber >= 9999.
        /// </summary>
        /// <param name="tenantId">Tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Tuple of (LastNumber, Prefix)</returns>
        Task<(int LastNumber, string Prefix)> GetNextTenantSequenceAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Atomically increments and returns the next sequence number for a country.
        /// </summary>
        /// <param name="countryCode">Country code (e.g., 994, 336)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>LastNumber</returns>
        Task<int> GetNextCountrySequenceAsync(int countryCode, CancellationToken ct = default);
    }
}

