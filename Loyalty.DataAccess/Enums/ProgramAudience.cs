namespace Loyalty.DataAccess.Enums
{
    /// <summary>
    /// Proqramın kimə aid olduğu. Public — app/discover/public qeydiyyatda görünür;
    /// Ambassador — yalnız admin ambassador edəndə kart açılır (tenant başına ən çox bir aktiv).
    /// </summary>
    public enum ProgramAudience
    {
        Public = 1,
        Ambassador = 2
    }
}
