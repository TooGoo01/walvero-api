namespace Loyalty.DataAccess.Models
{
    public class UniversitySearchModel
    {
        public List<string> CountryNames { get; set; }
        public List<string> SpecialityNames { get; set; }
        public List<string> ProgramNames { get; set; }
        public List<string> DirectionNames { get; set; }
        public List<string> Addresses { get; set; }
        public List<string> Durations { get; set; }
        public List<string> Attendances { get; set; }
    }
}
