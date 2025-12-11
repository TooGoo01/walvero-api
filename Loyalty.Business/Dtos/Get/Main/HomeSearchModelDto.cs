namespace Loyalty.Business.Dtos.Get.Main
{
    public class HomeSearchModelDto
    {
        public string ProductName { get; set; }
        public List<int> categoryIds { get; set; }
        public List<int> brandIds { get; set; }
    }
}
