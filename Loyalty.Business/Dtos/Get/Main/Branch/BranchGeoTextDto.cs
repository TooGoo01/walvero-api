namespace Loyalty.Business.Dtos.Get.Main.Branch
{
    /// <summary>
    /// Filial yaxınlıq bildirişi cütü — Apple Wallet (title → organizationName, text → locations[].relevantText)
    /// və app geofence bildirişi (title + body). Title boş → default başlıq.
    /// </summary>
    public class BranchGeoTextDto
    {
        public string? Title { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
