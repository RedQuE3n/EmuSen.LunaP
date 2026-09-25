using System;
using Avalonia;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    // SVG path data into a StreamGeometry: all twenty commands, implicit repeats, packed numbers and arc flags - see docs/LunaP.md §99.2.
    internal static class SvgPathData
    {
        // Null when the data is malformed before any segment; SVG 1.1 draws what came before an error, and so does this.
        internal static StreamGeometry? Parse(string? data, FillRule rule)
        {
            if (string.IsNullOrWhiteSpace(data)) return null;
            var geometry = new StreamGeometry();
            using StreamGeometryContext g = geometry.Open();
            g.SetFillRule(rule);

            Point current = default, start = default, lastControl = default;
            char previous = ' ';
            bool open = false;
            int i = 0;
            char command = ' ';

            void Begin(Point p)
            {
                if (open) g.EndFigure(false);
                g.BeginFigure(p, true);
                open = true;
                start = p;
            }

            void EnsureOpen()
            {
                if (!open) Begin(current);
            }

            while (true)
            {
                Skip(data, ref i);
                if (i >= data.Length) break;
                char c = data[i];
                if (char.IsLetter(c) && c != 'e' && c != 'E')
                {
                    command = c;
                    i++;
                }
                else if (command == ' ')
                {
                    break;
                }

                bool rel = char.IsLower(command);
                Point Rel(double x, double y) => rel ? new Point(current.X + x, current.Y + y) : new Point(x, y);
                double n1, n2, n3, n4, n5, n6, n7;

                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        if (!Two(data, ref i, out n1, out n2)) goto done;
                        current = Rel(n1, n2);
                        Begin(current);
                        command = rel ? 'l' : 'L';
                        break;
                    case 'L':
                        if (!Two(data, ref i, out n1, out n2)) goto done;
                        EnsureOpen();
                        current = Rel(n1, n2);
                        g.LineTo(current);
                        break;
                    case 'H':
                        if (!One(data, ref i, out n1)) goto done;
                        EnsureOpen();
                        current = new Point(rel ? current.X + n1 : n1, current.Y);
                        g.LineTo(current);
                        break;
                    case 'V':
                        if (!One(data, ref i, out n1)) goto done;
                        EnsureOpen();
                        current = new Point(current.X, rel ? current.Y + n1 : n1);
                        g.LineTo(current);
                        break;
                    case 'C':
                        if (!Two(data, ref i, out n1, out n2) || !Two(data, ref i, out n3, out n4) || !Two(data, ref i, out n5, out n6)) goto done;
                        EnsureOpen();
                        {
                            Point c1 = Rel(n1, n2), c2 = Rel(n3, n4), end = Rel(n5, n6);
                            g.CubicBezierTo(c1, c2, end);
                            lastControl = c2;
                            current = end;
                        }
                        break;
                    case 'S':
                        if (!Two(data, ref i, out n1, out n2) || !Two(data, ref i, out n3, out n4)) goto done;
                        EnsureOpen();
                        {
                            Point c1 = "CcSs".IndexOf(previous) >= 0 ? new Point(2 * current.X - lastControl.X, 2 * current.Y - lastControl.Y) : current;
                            Point c2 = Rel(n1, n2), end = Rel(n3, n4);
                            g.CubicBezierTo(c1, c2, end);
                            lastControl = c2;
                            current = end;
                        }
                        break;
                    case 'Q':
                        if (!Two(data, ref i, out n1, out n2) || !Two(data, ref i, out n3, out n4)) goto done;
                        EnsureOpen();
                        {
                            Point c1 = Rel(n1, n2), end = Rel(n3, n4);
                            g.QuadraticBezierTo(c1, end);
                            lastControl = c1;
                            current = end;
                        }
                        break;
                    case 'T':
                        if (!Two(data, ref i, out n1, out n2)) goto done;
                        EnsureOpen();
                        {
                            Point c1 = "QqTt".IndexOf(previous) >= 0 ? new Point(2 * current.X - lastControl.X, 2 * current.Y - lastControl.Y) : current;
                            Point end = Rel(n1, n2);
                            g.QuadraticBezierTo(c1, end);
                            lastControl = c1;
                            current = end;
                        }
                        break;
                    case 'A':
                        if (!One(data, ref i, out n1) || !One(data, ref i, out n2) || !One(data, ref i, out n3)
                            || !Flag(data, ref i, out n4) || !Flag(data, ref i, out n5) || !Two(data, ref i, out n6, out n7)) goto done;
                        EnsureOpen();
                        {
                            Point end = Rel(n6, n7);
                            if (n1 == 0 || n2 == 0) g.LineTo(end);
                            else if (end != current) g.ArcTo(end, new Size(Math.Abs(n1), Math.Abs(n2)), n3, n4 != 0, n5 != 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                            current = end;
                        }
                        break;
                    case 'Z':
                        if (open) g.EndFigure(true);
                        open = false;
                        current = start;
                        previous = command;
                        command = ' ';
                        continue;
                    default:
                        goto done;
                }

                previous = command;
            }

        done:
            if (open) g.EndFigure(false);
            return geometry;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
        }

        private static bool One(string s, ref int i, out double a) => SvgValues.TryNumber(s, ref i, out a);

        private static bool Two(string s, ref int i, out double a, out double b)
        {
            b = 0;
            return SvgValues.TryNumber(s, ref i, out a) && SvgValues.TryNumber(s, ref i, out b);
        }

        // An arc flag is one character, so "a1 1 0 00 1 1" packs the large-arc and sweep flags together.
        private static bool Flag(string s, ref int i, out double value)
        {
            value = 0;
            Skip(s, ref i);
            if (i >= s.Length || (s[i] != '0' && s[i] != '1')) return false;
            value = s[i] - '0';
            i++;
            return true;
        }
    }
}
