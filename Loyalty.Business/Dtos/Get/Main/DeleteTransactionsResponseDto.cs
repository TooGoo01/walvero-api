namespace Loyalty.Business.Dtos.Get.Main
{
    public class DeleteTransactionsResponseDto
    {
        public int DeletedCount { get; set; }

        /// <summary>Tapılmayan / artıq silinmiş / başqa tenant-a aid olduğu üçün ötürülənlər.</summary>
        public int SkippedCount { get; set; }

        public int AffectedCardCount { get; set; }
    }
}
