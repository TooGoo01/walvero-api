using Loyalty.DataAccess.Abstractions;

namespace Loyalty.DataAccess.Entities.Sequences
{
    /// <summary>
    /// Global sequence for card number prefix (always single row with Id = 1)
    /// </summary>
    public class CardPrefixSequence : IEntity
    {
        public int Id { get; set; } = 1; // Always 1, single row
        public int LastPrefix { get; set; } = 789; // Starting prefix (e.g., 789, 790, 791...)
    }
}

