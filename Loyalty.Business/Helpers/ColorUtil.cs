public static class ColorUtil
{
    public static string HexToRgbCssOrWhite(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return "rgb(255,255,255)"; // default white

        try { return HexToRgbCss(hex); }
        catch { return "rgb(255,255,255)"; } // parse alınmasa da ağ
    }

    // əvvəlki funksiya (qısaldılmış)
    public static string HexToRgbCss(string hex)
    {
        hex = hex.Trim().TrimStart('#').TrimStart('0', 'x', 'X');
        if (hex.Length == 3) hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        else if (hex.Length == 8) hex = hex[..6]; // alfa atılır (RRGGBBAA -> RRGGBB)
        if (hex.Length != 6) throw new ArgumentException("Unsupported hex");

        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return $"rgb({r},{g},{b})";
    }
}
