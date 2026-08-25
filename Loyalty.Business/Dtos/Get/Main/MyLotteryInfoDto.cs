namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>Mobil app-da istifadəçiyə göstərilən lotereya məlumatı (customer).</summary>
    public class MyLotteryInfoDto
    {
        public bool IsEnabled { get; set; }
        public GetLotteryDrawDto? NextDraw { get; set; }
        public int MyActiveTicketCount { get; set; }
        public int MyTotalTicketCount { get; set; }
        public List<GetLotteryTicketDto> MyWins { get; set; } = new();
    }
}
