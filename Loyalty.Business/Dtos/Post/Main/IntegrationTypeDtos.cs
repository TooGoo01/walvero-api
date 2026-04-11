namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddIntegrationTypeDto
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? DefaultBaseUrl { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateIntegrationTypeDto
    {
        public string? Name { get; set; }
        public string? Source { get; set; }
        public string? DefaultBaseUrl { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
