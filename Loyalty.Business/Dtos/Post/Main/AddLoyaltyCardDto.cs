using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AddLoyaltyCardDto
    {
        public int CustomerId { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string? BarcodeValue { get; set; } // boşdursa auto 6-rəqəm
        public BarcodeType BarcodeType { get; set; } = BarcodeType.Code128;
        public string? TemplateSnapshotJson { get; set; }
        // Helper:
        public CardTemplateSnapshot? TemplateSnapshot
            => string.IsNullOrWhiteSpace(TemplateSnapshotJson)
               ? null
               : System.Text.Json.JsonSerializer.Deserialize<CardTemplateSnapshot>(TemplateSnapshotJson);
    }
}
