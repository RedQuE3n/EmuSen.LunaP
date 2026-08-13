using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Media;

namespace EmuSen.LunaP.Theme
{
    // TEXT TO VALUES, and the two name spellings that sit either side of the palette.
    //
    // Split out at §29.4 because none of it knows anything about the grammar: every method here
    // takes a string a caller has already isolated and answers whether it is a colour, a number, a
    // font list, or a resource key. That makes them the easiest part of the dialect to test directly
    // and the part most likely to grow - a CSS unit, a colour function, a named constant.
    public static partial class CssTheme
    {
        // --luna-section-header becomes LunaSectionHeader, the key Palette.axaml already spells.
        private static string ResourceKey(string token)
        {
            var sb = new StringBuilder();
            foreach (string word in token[2..].Split('-', StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append(char.ToUpperInvariant(word[0])).Append(word[1..].ToLowerInvariant());
            }

            return sb.ToString();
        }

        private static string Kebab(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) sb.Append('-');
                sb.Append(char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }

        private static string FontList(string text) =>
            string.Join(',', text.Split(',').Select(part => part.Trim().Trim('"', '\'')).Where(part => part.Length > 0));

        private static bool TryNumber(string text, out double value)
        {
            string trimmed = text.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? text[..^2] : text;
            return double.TryParse(trimmed.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // CSS channel order, not Avalonia's: #RRGGBBAA and #RGBA put alpha last, which is what someone writing CSS expects.
        private static bool TryColour(string text, out Color colour)
        {
            colour = default;
            text = text.Trim();

            if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
            {
                return TryRgb(text, out colour);
            }

            if (text.Length is 9 or 5 && text[0] == '#')
            {
                string digits = text[1..];
                int step = digits.Length / 4;
                if (!Nibbles(digits, 0, step, out byte r) || !Nibbles(digits, step, step, out byte g) ||
                    !Nibbles(digits, step * 2, step, out byte b) || !Nibbles(digits, step * 3, step, out byte a))
                {
                    return false;
                }

                colour = Color.FromArgb(a, r, g, b);
                return true;
            }

            return Color.TryParse(text, out colour);
        }

        private static bool Nibbles(string digits, int start, int length, out byte value)
        {
            value = 0;
            if (!byte.TryParse(digits.AsSpan(start, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsed))
                return false;

            value = length == 1 ? (byte)(parsed * 17) : parsed;
            return true;
        }

        private static bool TryRgb(string text, out Color colour)
        {
            colour = default;
            int open = text.IndexOf('(');
            if (!text.EndsWith(")", StringComparison.Ordinal)) return false;

            string[] parts = text[(open + 1)..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is not (3 or 4)) return false;

            var channels = new byte[3];
            for (int i = 0; i < 3; i++)
            {
                if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out channels[i])) return false;
            }

            byte alpha = 255;
            if (parts.Length == 4)
            {
                // CSS spells alpha 0..1, unlike every other channel.
                if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction)) return false;
                alpha = (byte)Math.Round(Math.Clamp(fraction, 0, 1) * 255);
            }

            colour = Color.FromArgb(alpha, channels[0], channels[1], channels[2]);
            return true;
        }
    }
}
