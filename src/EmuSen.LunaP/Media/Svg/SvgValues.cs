using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    // Numbers, lengths, colours, transforms and point lists as SVG 1.1 writes them - see docs/LunaP.md §99.2.
    internal static class SvgValues
    {
        // Reads the numbers of a list, allowing SVG's packing: "1.5.5" is 1.5 and .5, "1-2" is 1 and -2.
        internal static List<double> Numbers(string? text)
        {
            var numbers = new List<double>();
            if (text is null) return numbers;
            int i = 0;
            while (TryNumber(text, ref i, out double value)) numbers.Add(value);
            return numbers;
        }

        internal static bool TryNumber(string text, ref int i, out double value)
        {
            value = 0;
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',')) i++;
            if (i >= text.Length) return false;
            int start = i;
            if (text[i] == '+' || text[i] == '-') i++;
            bool digits = false, dot = false;
            while (i < text.Length)
            {
                char c = text[i];
                if (c >= '0' && c <= '9') { digits = true; i++; }
                else if (c == '.' && !dot) { dot = true; i++; }
                else break;
            }

            if (!digits) { i = start; return false; }
            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                int mark = i++;
                if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;
                if (i < text.Length && char.IsDigit(text[i])) { while (i < text.Length && char.IsDigit(text[i])) i++; }
                else i = mark;
            }

            return double.TryParse(text.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // A length in user units; percentages and font-relative units are refused by returning null.
        internal static double? Length(string? text, double fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            string t = text.Trim();
            int i = 0;
            if (!TryNumber(t, ref i, out double v)) return null;
            string unit = t[i..].Trim().ToLowerInvariant();
            return unit switch
            {
                "" or "px" => v,
                "pt" => v * 4.0 / 3.0,
                "pc" => v * 16,
                "mm" => v * 96 / 25.4,
                "cm" => v * 96 / 2.54,
                "in" => v * 96,
                _ => null,
            };
        }

        // A colour: #rgb, #rrggbb, #rrggbbaa, rgb()/rgba() and CSS names; null when unreadable.
        internal static Color? Colour(string text)
        {
            string t = text.Trim();
            if (t.StartsWith('#'))
            {
                string hex = t[1..];
                if (hex.Length == 3 || hex.Length == 4) hex = string.Concat(Array.ConvertAll(hex.ToCharArray(), c => new string(c, 2)));
                if ((hex.Length != 6 && hex.Length != 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return null;
                return hex.Length == 6
                    ? Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v)
                    : Color.FromArgb((byte)v, (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8));
            }

            string lower = t.ToLowerInvariant();
            if (lower.StartsWith("rgb"))
            {
                int open = lower.IndexOf('('), close = lower.LastIndexOf(')');
                if (open < 0 || close < open) return null;
                string[] parts = lower[(open + 1)..close].Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return null;
                byte Channel(string p) => p.EndsWith('%')
                    ? (byte)Math.Clamp(Math.Round(double.Parse(p[..^1], CultureInfo.InvariantCulture) * 2.55), 0, 255)
                    : (byte)Math.Clamp(Math.Round(double.Parse(p, CultureInfo.InvariantCulture)), 0, 255);
                double alpha = parts.Length > 3 ? (parts[3].EndsWith('%') ? double.Parse(parts[3][..^1], CultureInfo.InvariantCulture) / 100 : double.Parse(parts[3], CultureInfo.InvariantCulture)) : 1;
                try { return Color.FromArgb((byte)Math.Clamp(Math.Round(alpha * 255), 0, 255), Channel(parts[0]), Channel(parts[1]), Channel(parts[2])); }
                catch (FormatException) { return null; }
            }

            if (lower == "transparent") return Colors.Transparent;
            if (lower.Length > 0 && char.IsLetter(lower[0]) && Color.TryParse(lower, out Color named)) return named;
            return null;
        }

        // A transform list, composed so that the first-written transform is applied last, as SVG requires.
        internal static Matrix? Transform(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Matrix.Identity;
            Matrix result = Matrix.Identity;
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',')) i++;
                if (i >= text.Length) break;
                int nameStart = i;
                while (i < text.Length && char.IsLetter(text[i])) i++;
                string name = text[nameStart..i];
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                if (i >= text.Length || text[i] != '(') return null;
                int close = text.IndexOf(')', i);
                if (close < 0) return null;
                List<double> a = Numbers(text[(i + 1)..close]);
                i = close + 1;
                Matrix? m = name switch
                {
                    "matrix" when a.Count == 6 => new Matrix(a[0], a[1], a[2], a[3], a[4], a[5]),
                    "translate" when a.Count is 1 or 2 => Matrix.CreateTranslation(a[0], a.Count == 2 ? a[1] : 0),
                    "scale" when a.Count is 1 or 2 => Matrix.CreateScale(a[0], a.Count == 2 ? a[1] : a[0]),
                    "rotate" when a.Count == 1 => Matrix.CreateRotation(a[0] * Math.PI / 180),
                    "rotate" when a.Count == 3 => Matrix.CreateTranslation(-a[1], -a[2]) * Matrix.CreateRotation(a[0] * Math.PI / 180) * Matrix.CreateTranslation(a[1], a[2]),
                    "skewX" when a.Count == 1 => new Matrix(1, 0, Math.Tan(a[0] * Math.PI / 180), 1, 0, 0),
                    "skewY" when a.Count == 1 => new Matrix(1, Math.Tan(a[0] * Math.PI / 180), 0, 1, 0, 0),
                    _ => null,
                };
                if (m is null) return null;
                result = m.Value * result;
            }

            return result;
        }

        // The points of a polyline or polygon; an odd count drops the last number, as SVG 1.1 says.
        internal static List<Point> Points(string? text)
        {
            List<double> n = Numbers(text);
            var points = new List<Point>(n.Count / 2);
            for (int i = 0; i + 1 < n.Count; i += 2) points.Add(new Point(n[i], n[i + 1]));
            return points;
        }

        // The id inside url(#id), or null.
        internal static string? UrlId(string text)
        {
            string t = text.Trim();
            if (!t.StartsWith("url(", StringComparison.Ordinal)) return null;
            int close = t.IndexOf(')');
            if (close < 0) return null;
            string inner = t[4..close].Trim().Trim('"', '\'');
            return inner.StartsWith('#') ? inner[1..] : null;
        }
    }
}
