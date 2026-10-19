using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Tenant-ın "default" proqramı (staff login, guest, app tenant-only rejimi): aktiv Public proqramlardan
    /// ən kiçik Id. Ambassador proqramı yalnız ambassador kartları üçündür, default kimi seçilmir.
    /// </summary>
    public static class ProgramSelection
    {
        public static bool IsPublic(this LoyaltyProgram p) => p.Audience != ProgramAudience.Ambassador;

        public static IEnumerable<LoyaltyProgram> ActivePublic(this IEnumerable<LoyaltyProgram>? programs)
            => (programs ?? Enumerable.Empty<LoyaltyProgram>())
                .Where(p => p.IsActive && p.IsPublic())
                .OrderBy(p => p.Id);

        public static LoyaltyProgram? DefaultProgram(this IEnumerable<LoyaltyProgram>? programs)
            => programs.ActivePublic().FirstOrDefault();

        public static LoyaltyProgram? DefaultProgramWithTemplate(this IEnumerable<LoyaltyProgram>? programs)
            => programs.ActivePublic().FirstOrDefault(p => p.CardTemplate != null);
    }
}
