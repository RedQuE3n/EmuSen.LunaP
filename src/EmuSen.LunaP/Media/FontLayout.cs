using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace EmuSen.LunaP.Media
{
    /// <summary>How a text's letters are cased before it is laid out.</summary>
    public enum LetterCase
    {
        /// <summary>As written, unchanged.</summary>
        None,
        /// <summary>Every letter upper case.</summary>
        Upper,
        /// <summary>Every letter lower case.</summary>
        Lower,
        /// <summary>The first letter of each word upper case, the rest as written.</summary>
        Capitalize,
    }

    // Lines of text in one typeface: greedy word wrap, a CSS-style line box, and an ellipsis on the last line that fits - see docs/LunaP.md §98.3.
    internal sealed class FontLayout
    {
        private FontLayout(GlyphTypeface typeface, double size, double lineHeight, IReadOnlyList<(string Text, double Width)> lines, bool truncated)
        {
            Typeface = typeface;
            Size = size;
            LineHeight = lineHeight;
            Lines = lines;
            Truncated = truncated;
        }

        internal GlyphTypeface Typeface { get; }
        internal double Size { get; }
        internal double LineHeight { get; }
        internal IReadOnlyList<(string Text, double Width)> Lines { get; }
        internal bool Truncated { get; }
        internal double Width => Lines.Count == 0 ? 0 : Lines.Max(l => l.Width);
        internal double Height => Lines.Count * LineHeight;

        internal double Ascent => Math.Abs(Typeface.Metrics.Ascent) * Size / Typeface.Metrics.DesignEmHeight;
        internal double Descent => Math.Abs(Typeface.Metrics.Descent) * Size / Typeface.Metrics.DesignEmHeight;

        internal static string Cased(string text, LetterCase letterCase) => letterCase switch
        {
            LetterCase.Upper => text.ToUpper(CultureInfo.CurrentCulture),
            LetterCase.Lower => text.ToLower(CultureInfo.CurrentCulture),
            LetterCase.Capitalize => string.Concat(text.Select((c, i) => i == 0 || char.IsWhiteSpace(text[i - 1]) ? char.ToUpper(c, CultureInfo.CurrentCulture) : c)),
            _ => text,
        };

        // Lays out text within a width (infinite for one line per paragraph) and a height (infinite for every line).
        internal static FontLayout Create(GlyphTypeface typeface, double size, string text, double lineSpacing, double maxWidth, double maxHeight, bool wrap, string? ellipsis)
        {
            double lineHeight = Math.Max(0, Math.Round(lineSpacing * size * 100) / 100);
            var lines = new List<(string, double)>();
            bool truncated = false;
            if (size <= 0) return new FontLayout(typeface, size, lineHeight, lines, false);

            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (!wrap || double.IsInfinity(maxWidth)) lines.Add((paragraph, Measure(typeface, size, paragraph)));
                else Wrap(typeface, size, paragraph, maxWidth, lines);
            }

            int fits = double.IsInfinity(maxHeight) || lineHeight <= 0 ? lines.Count : Math.Max(1, (int)Math.Floor((maxHeight + 0.01) / lineHeight));
            if (fits < lines.Count)
            {
                lines.RemoveRange(fits, lines.Count - fits);
                truncated = true;
            }

            if (!double.IsInfinity(maxWidth))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    bool last = i == lines.Count - 1;
                    if (lines[i].Item2 > maxWidth + 0.01 || (last && truncated))
                    {
                        lines[i] = Shorten(typeface, size, lines[i].Item1, maxWidth, ellipsis, last && truncated);
                        truncated = true;
                    }
                }
            }

            return new FontLayout(typeface, size, lineHeight, lines, truncated);
        }

        // The longest prefix that fits with the ellipsis after it; the ellipsis is added even when the line fits, if text followed it.
        private static (string, double) Shorten(GlyphTypeface typeface, double size, string line, double maxWidth, string? ellipsis, bool more)
        {
            string tail = ellipsis ?? "";
            double tailWidth = Measure(typeface, size, tail);
            double[] ends = Advances(typeface, size, line);
            if (!more && ends[line.Length] <= maxWidth + 0.01) return (line, ends[line.Length]);
            int keep = line.Length;
            while (keep > 0 && ends[keep] + tailWidth > maxWidth + 0.01) keep--;
            string kept = line[..keep].TrimEnd() + tail;
            return (kept, Measure(typeface, size, kept));
        }

        private static void Wrap(GlyphTypeface typeface, double size, string paragraph, double maxWidth, List<(string, double)> lines)
        {
            double[] ends = Advances(typeface, size, paragraph);
            int start = 0;
            while (start < paragraph.Length)
            {
                int end = start, lastBreak = -1;
                while (end < paragraph.Length && ends[end + 1] - ends[start] <= maxWidth + 0.01)
                {
                    end++;
                    if (end < paragraph.Length && paragraph[end] == ' ') lastBreak = end;
                }

                if (end >= paragraph.Length) { lastBreak = paragraph.Length; }
                else if (lastBreak <= start) { lastBreak = Math.Max(end, start + 1); }

                string line = paragraph[start..lastBreak].TrimEnd();
                lines.Add((line, Measure(typeface, size, line)));
                start = lastBreak;
                while (start < paragraph.Length && paragraph[start] == ' ') start++;
            }

            if (paragraph.Length == 0) lines.Add(("", 0));
        }

        // ends[i] is the advance of the first i characters, read from one shaping of the whole string.
        private static double[] Advances(GlyphTypeface typeface, double size, string text)
        {
            var ends = new double[text.Length + 1];
            if (text.Length == 0) return ends;
            ShapedBuffer shaped = Shape(typeface, size, text);
            var perChar = new double[text.Length];
            foreach (GlyphInfo g in shaped) if (g.GlyphCluster >= 0 && g.GlyphCluster < text.Length) perChar[g.GlyphCluster] += g.GlyphAdvance;
            for (int i = 0; i < text.Length; i++) ends[i + 1] = ends[i] + perChar[i];
            return ends;
        }

        internal static double Measure(GlyphTypeface typeface, double size, string text)
        {
            if (text.Length == 0) return 0;
            double width = 0;
            foreach (GlyphInfo g in Shape(typeface, size, text)) width += g.GlyphAdvance;
            return width;
        }

        private static ShapedBuffer Shape(GlyphTypeface typeface, double size, string text) =>
            TextShaper.Current.ShapeText(text.AsMemory(), new TextShaperOptions(typeface, size, 0, CultureInfo.CurrentCulture, 0, 0, null));

        // Draws each line in its box: aligned across the width, and its glyphs centred in the line's height as CSS's half-leading does.
        internal void Draw(DrawingContext dc, IBrush brush, Rect box, TextAlignment alignment, double verticalFraction)
        {
            double top = box.Y + (box.Height - Height) * verticalFraction;
            double halfLeading = (LineHeight - (Ascent + Descent)) / 2;
            for (int i = 0; i < Lines.Count; i++)
            {
                (string text, double width) = Lines[i];
                if (text.Length == 0) continue;
                double x = alignment switch
                {
                    TextAlignment.Center => box.X + (box.Width - width) / 2,
                    TextAlignment.Right or TextAlignment.End => box.Right - width,
                    _ => box.X,
                };
                double baseline = top + i * LineHeight + halfLeading + Ascent;
                ShapedBuffer shaped = Shape(Typeface, Size, text);
                var run = new GlyphRun(Typeface, Size, text.AsMemory(), shaped.ToArray(), new Point(x, baseline), 0);
                dc.DrawGlyphRun(brush, run);
            }
        }
    }
}
