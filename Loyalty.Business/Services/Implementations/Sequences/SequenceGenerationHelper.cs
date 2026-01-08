using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Helper for retrying operations on unique constraint violations
    /// </summary>
    public static class SequenceGenerationHelper
    {
        /// <summary>
        /// Retries an operation up to maxRetries times if it throws a unique constraint violation
        /// </summary>
        public static async Task<T> RetryOnUniqueViolationAsync<T>(
            Func<Task<T>> operation,
            Func<Task<T>> regenerateValue,
            int maxRetries = 2,
            ILogger? logger = null)
        {
            int attempt = 0;
            while (attempt <= maxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (SqlException ex) when ((ex.Number == 2601 || ex.Number == 2627) && attempt < maxRetries)
                {
                    // Unique constraint violation - regenerate value and retry
                    attempt++;
                    logger?.LogWarning(
                        "Unique constraint violation (attempt {Attempt}/{MaxRetries}): {Error}. Regenerating value...",
                        attempt, maxRetries, ex.Message);

                    // Regenerate the value (e.g., get new sequence)
                    await regenerateValue();
                }
            }

            // If we get here, all retries failed
            throw new InvalidOperationException(
                $"Failed after {maxRetries} retries due to unique constraint violations");
        }
    }
}

