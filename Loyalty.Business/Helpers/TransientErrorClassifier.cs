using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Exception-ı "keçici" (retry mənalıdır) və "daimi" (retry mənasızdır) kimi ayırır.
    /// iiko webhook retry növbəsi və Hangfire AutomaticRetry bunun üzərində qurulub:
    /// DB timeout / deadlock / şəbəkə → retry; NullReference / InvalidOperation / Json xətası → Rejected.
    /// </summary>
    public static class TransientErrorClassifier
    {
        // Azure SQL / SQL Server keçici xəta nömrələri (Microsoft-un tövsiyə siyahısı + deadlock + timeout)
        private static readonly HashSet<int> TransientSqlErrors = new()
        {
            -2,     // timeout
            64,     // bağlantı kəsildi
            233,    // transport-level error
            1205,   // deadlock victim
            4060,   // database unavailable
            10053, 10054, 10060, // socket errors
            10928, 10929,        // resource limits
            40197, 40501, 40613, // Azure SQL service busy / unavailable
            49918, 49919, 49920  // Azure SQL request limits
        };

        public static bool IsTransient(Exception? ex)
        {
            var depth = 0;
            while (ex != null && depth++ < 10)
            {
                switch (ex)
                {
                    case SqlException sql:
                        foreach (SqlError err in sql.Errors)
                            if (TransientSqlErrors.Contains(err.Number)) return true;
                        return TransientSqlErrors.Contains(sql.Number);
                    case TimeoutException: // Hangfire DistributedLockTimeoutException da buradan törəyir
                    case HttpRequestException:
                    case OperationCanceledException:
                    case DbUpdateConcurrencyException:
                        return true;
                    case AggregateException agg when agg.InnerExceptions.Count > 0:
                        foreach (var inner in agg.InnerExceptions)
                            if (IsTransient(inner)) return true;
                        return false;
                }
                ex = ex.InnerException; // DbUpdateException → SqlException və s.
            }
            return false;
        }
    }
}
