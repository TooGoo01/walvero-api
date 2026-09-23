namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetContactDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }
        public System.DateTime RegDate { get; set; }
    }
}
