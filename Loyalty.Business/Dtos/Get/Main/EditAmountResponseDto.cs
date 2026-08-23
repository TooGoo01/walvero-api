namespace Loyalty.Business.Dtos.Get.Main
{
    public class EditAmountResponseDto
    {
        public decimal? OldAmount { get; set; }
        public decimal NewAmount { get; set; }
        public decimal OldPoints { get; set; }
        public decimal NewPoints { get; set; }
        public decimal PointsDelta { get; set; }
        public decimal UpdatedBalance { get; set; }
    }
}
