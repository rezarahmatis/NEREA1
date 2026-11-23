using System.Numerics;

namespace NEREA.Core.Utilities;

/// <summary>Parses NEREA palette hex strings ("#RRGGBB" or "#RRGGBBAA") into linear-friendly Vector4 RGBA.</summary>
public static class ColorHex
{
    public static Vector4 Parse(string hex, Vector4 fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var s = hex.Trim().TrimStart('#');
        try
        {
            byte r, g, b, a = 255;
            if (s.Length == 6)
            {
                r = Convert.ToByte(s[..2], 16);
                g = Convert.ToByte(s[2..4], 16);
                b = Convert.ToByte(s[4..6], 16);
            }
            else if (s.Length == 8)
            {
                r = Convert.ToByte(s[..2], 16);
                g = Convert.ToByte(s[2..4], 16);
                b = Convert.ToByte(s[4..6], 16);
                a = Convert.ToByte(s[6..8], 16);
            }
            else
            {
                return fallback;
            }
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        catch
        {
            return fallback;
        }
    }
}
