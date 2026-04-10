namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Satış mütəxəssisinin ziyarət nəticəsi statusu.
    /// </summary>
    public enum SalesVisitStatus
    {
        Visited = 0,         // Ziyarət edildi
        Interested = 1,      // İstədi / bəyəndi
        Considering = 2,     // Maraqlanır / düşünür
        WantsMoreInfo = 3,   // Məlumat gözləyir
        ContactOwner = 4,    // Sahibkara məlumat verilməlidir
        NotInterested = 5    // İstəmədi
    }
}
