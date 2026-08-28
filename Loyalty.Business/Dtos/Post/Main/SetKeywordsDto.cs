namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Super-admin AI səhifəsi — tenant açar sözlərini tək sahə kimi yeniləmək.</summary>
    public class SetKeywordsDto
    {
        public int Id { get; set; }
        public string? Keywords { get; set; }
    }
}
