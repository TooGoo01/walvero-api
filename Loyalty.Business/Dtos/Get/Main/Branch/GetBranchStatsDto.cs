namespace Loyalty.Business.Dtos.Get.Main.Branch
{
    public class GetBranchStatsDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int StaffCount { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalPointsIssued { get; set; }
        public decimal TotalPointsRedeemed { get; set; }
    }
}
