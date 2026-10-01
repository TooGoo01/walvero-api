using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Admin "Sahələr" tab-ı (Faza 2) — FormData `LayoutJson` sahəsində JSON: {"barcode":2,"googleBanner":1,"fields":[{"id":"balance","slot":1,"order":1,"label":"Balans"}]}.
    /// barcode: 0 = default (Code128) / BarcodeType dəyəri; googleBanner: -1 = default, 0 Bottom, 1 Top, 2 None; fields: boş siyahı = default-a qayıt.
    /// </summary>
    public sealed class CardLayoutUpdateDto
    {
        public int? Barcode { get; set; }
        public int? GoogleBanner { get; set; }
        public List<CardFieldUpdateDto>? Fields { get; set; }

        public static CardLayoutUpdateDto? Parse(string? json, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var dto = JsonSerializer.Deserialize<CardLayoutUpdateDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                });
                if (dto?.Fields != null)
                {
                    foreach (var f in dto.Fields)
                    {
                        if (f.Slot is < 0 or > 5) { error = $"Sahə '{f.Id}': yuva dəyəri yanlışdır."; return null; }
                        if (f.Label != null && f.Label.Length > 40) { error = $"Sahə '{f.Id}': etiket maksimum 40 simvol."; return null; }
                    }
                    if (dto.Fields.Select(f => f.Id).Distinct().Count() != dto.Fields.Count) { error = "Sahə id-ləri təkrarlanır."; return null; }
                }
                if (dto?.Barcode is < 0 or > 4) { error = "Barkod tipi yanlışdır."; return null; }
                if (dto?.GoogleBanner is < -1 or > 2) { error = "Google banner yeri yanlışdır."; return null; }
                return dto;
            }
            catch (JsonException ex)
            {
                error = "LayoutJson oxunmadı: " + ex.Message;
                return null;
            }
        }
    }

    public sealed class CardFieldUpdateDto
    {
        public string Id { get; set; } = "";
        public int Slot { get; set; }
        public int Order { get; set; }
        public string? Label { get; set; }
    }
}
