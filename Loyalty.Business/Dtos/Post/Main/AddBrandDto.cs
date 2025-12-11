namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddBrandDto
    {
        public string AzName { get; set; }
        public string EnName { get; set; }
        public string RuName { get; set; }
        public bool IsActive { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public int categoryId { get; set; }
      
    }
}
