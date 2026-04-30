using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main.Room
{
    public class UpdateRoomDto
    {
        [MinLength(1)]
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(100)]
        public string? Type { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? Capacity { get; set; }

        /// <summary>
        /// Yeni minimum depozit dəyəri. Null verildikdə dəyişmir
        /// (təmizləmək üçün <see cref="ClearMinDeposit"/>=true istifadə et).
        /// </summary>
        public decimal? MinDeposit { get; set; }

        /// <summary>True olduqda MinDeposit null-a sıfırlanır (depozit silinir).</summary>
        public bool? ClearMinDeposit { get; set; }

        public int? OrderIndex { get; set; }
    }
}
