using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Chatbot
{
    /// <summary>Flutter-dən gələn chatbot sorğusu. Backend stateless — tam mesaj tarixçəsi göndərilir.</summary>
    public class ChatbotAskRequest
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
        public double? Lat { get; set; }   // "mənə yaxın" üçün cihaz GPS-i (opsional)
        public double? Lng { get; set; }
        public string? City { get; set; }  // opsional şəhər ipucu
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = "user";   // "user" | "assistant"
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>Chatbot cavabı: təbii-dil mətn + (varsa) struktur məkan kartları.</summary>
    public class AssistantReply
    {
        public string Reply { get; set; } = string.Empty;
        public List<PlaceResultDto> Places { get; set; } = new();
    }

    /// <summary>search_places tool-unun daxili sorğu modeli (model tərəfindən doldurulur).</summary>
    public class PlaceSearchQuery
    {
        public string[] Keywords { get; set; } = System.Array.Empty<string>();
        public string? CategoryName { get; set; }
        public string? City { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public double? RadiusKm { get; set; }
        public int Take { get; set; } = 8;
    }

    /// <summary>Bir məkan nəticəsi — Flutter kart kimi göstərir.</summary>
    public class PlaceResultDto
    {
        public int TenantId { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? Address { get; set; }
        public double? DistanceKm { get; set; }
        public string? Keywords { get; set; }
        public string? Phone { get; set; }
        public string? LogoUrl { get; set; }
        public string? LocationUrl { get; set; }
        public PlaceMenuDto Menu { get; set; } = new();
    }

    public class PlaceMenuDto
    {
        public bool InApp { get; set; }              // true → app-daxili menyu (tenantSlug/branchSlug)
        public string? TenantSlug { get; set; }
        public string? BranchSlug { get; set; }
        public string? ExternalMenuUrl { get; set; } // fallback xarici link
    }

    /// <summary>search_menu_items tool sorğusu — yeməyə/məhsula görə məkan axtarışı.</summary>
    public class MenuItemSearchQuery
    {
        public string[] Keywords { get; set; } = System.Array.Empty<string>(); // yemək adı: "lazanya", "vegan burger"
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public double? RadiusKm { get; set; }
        public int Take { get; set; } = 8;
    }

    /// <summary>Menyu axtarışı nəticəsi: uyğun yemək + onu satan məkan (Flutter kartı = Place).</summary>
    public class MenuItemResultDto
    {
        public PlaceResultDto Place { get; set; } = new();
        public string DishName { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>Effektiv (satış) qiymət — endirim varsa endirimli.</summary>
        public double Price { get; set; }
        /// <summary>Endirim aktivdirsə adi (endirimsiz) qiymət; yoxdursa null.</summary>
        public double? RegularPrice { get; set; }
        public string? CurrencySign { get; set; }
    }

    // ---- Şəxsi hesab tool-larının nəticələri (AI-a JSON kimi qaytarılır) ----
    public class MyCardDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public decimal CurrentPoints { get; set; }
        public int CurrentSteps { get; set; }
        public int CompletedCycles { get; set; }
    }

    public class MyTransactionDto
    {
        public string TenantName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string Type { get; set; } = string.Empty;      // Earned/Redeemed/Bonus/...
        public int Points { get; set; }
        public decimal? Amount { get; set; }
        public int BalanceAfter { get; set; }
        public System.DateTimeOffset OccurredAt { get; set; }
    }
}
