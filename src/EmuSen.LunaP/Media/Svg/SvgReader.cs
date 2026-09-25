using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    // The cascade and the element walk: presentation attributes, then style sheets by specificity, then style attributes - see docs/LunaP.md §99.3.
    internal sealed class SvgReader
    {
        internal const string Namespace = "http://www.w3.org/2000/svg";
        private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

        // Elements that describe and draw nothing.
        private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal) { "title", "desc", "metadata", "style" };

        // Properties that pass from a parent to its children when a child does not set them.
        private static readonly HashSet<string> Inherited = new(StringComparer.Ordinal)
        {
            "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-opacity", "stroke-linejoin", "stroke-linecap",
            "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "color", "visibility", "clip-rule",
        };

        // Properties whose presence alone refuses the document, since drawing without them would be drawing in part.
        private static readonly HashSet<string> RefusedProperties = new(StringComparer.Ordinal) { "filter", "mask", "marker", "marker-start", "marker-mid", "marker-end" };

        private readonly XElement _svg;
        private readonly Dictionary<string, XElement> _ids = new(StringComparer.Ordinal);
        private readonly List<SvgCssRule> _rules = new();
        private readonly SortedSet<string> _refusals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SvgLinear?> _gradients = new(StringComparer.Ordinal);
        private Rect _viewBox;
        private int _shapes;

        internal SvgReader(XElement svg) => _svg = svg;

        internal static bool IsSvg(XElement e) => e.Name.NamespaceName is "" or Namespace;

        internal SvgDocument Read()
        {
            foreach (XElement e in _svg.DescendantsAndSelf().Where(IsSvg))
            {
                if (e.Attribute("id")?.Value is { Length: > 0 } id) _ids.TryAdd(id, e);
                if (e.Name.LocalName == "style")
                {
                    var sheet = new List<string>();
                    SvgCss.ParseSheet(e.Value, _rules, sheet);
                    foreach (string reason in sheet) Refuse(reason);
                }
            }

            double? width = Absolute(_svg.Attribute("width")?.Value);
            double? height = Absolute(_svg.Attribute("height")?.Value);
            List<double> vb = SvgValues.Numbers(_svg.Attribute("viewBox")?.Value);
            _viewBox = vb.Count == 4 && vb[2] > 0 && vb[3] > 0 ? new Rect(vb[0], vb[1], vb[2], vb[3]) : new Rect(0, 0, width ?? 300, height ?? 150);
            var size = new Size(width ?? _viewBox.Width, height ?? _viewBox.Height);

            string[] aspect = (_svg.Attribute("preserveAspectRatio")?.Value ?? "xMidYMid meet").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string align = aspect.FirstOrDefault(a => a != "defer") ?? "xMidYMid";
            bool slice = aspect.Contains("slice");

            var root = new SvgGroup();
            Dictionary<string, string> style = Cascade(_svg, Root());
            ApplyNodeProperties(root, _svg, style);
            foreach (XElement child in _svg.Elements()) Walk(child, style, root.Children);

            string[] refusals = _refusals.ToArray();
            return SvgDocument.Create(root, _viewBox, size, align, slice, refusals, _shapes);
        }

        private void Refuse(string reason) => _refusals.Add(reason);

        // Width and height of the root: absolute lengths only; a percentage leaves the viewBox to decide.
        private static double? Absolute(string? text) => string.IsNullOrWhiteSpace(text) || text.Trim().EndsWith('%') ? null : SvgValues.Length(text);

        private static Dictionary<string, string> Root() => new(StringComparer.Ordinal);

        private void Walk(XElement e, Dictionary<string, string> parent, List<SvgNode> into)
        {
            if (!IsSvg(e)) return;
            string name = e.Name.LocalName;
            if (Ignored.Contains(name)) return;
            if (name is "defs" or "linearGradient" or "clipPath")
            {
                if (name == "defs") foreach (XElement d in e.Elements().Where(IsSvg)) Screen(d);
                return;
            }

            Dictionary<string, string> style = Cascade(e, parent);
            if (style.GetValueOrDefault("display") == "none") return;

            switch (name)
            {
                case "g":
                {
                    var group = new SvgGroup();
                    ApplyNodeProperties(group, e, style);
                    foreach (XElement child in e.Elements()) Walk(child, style, group.Children);
                    if (group.Children.Count > 0) into.Add(group);
                    return;
                }
                case "path" or "rect" or "circle" or "ellipse" or "line" or "polyline" or "polygon":
                {
                    FillRule rule = style.GetValueOrDefault("fill-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero;
                    if (Geometry(e, rule) is not { } geometry) return;
                    SvgShape shape = Shape(geometry, e, style);
                    if (style.GetValueOrDefault("visibility") is "hidden" or "collapse") return;
                    into.Add(shape);
                    _shapes++;
                    return;
                }
                default:
                    Refuse($"the element <{name}>");
                    return;
            }
        }

        // Inside defs, only gradients, clip paths, styles and descriptive elements are understood; anything else is refused.
        private void Screen(XElement d)
        {
            string name = d.Name.LocalName;
            if (name is "linearGradient" or "clipPath" or "style" || Ignored.Contains(name)) return;
            if (name is "g" or "path" or "rect" or "circle" or "ellipse" or "line" or "polyline" or "polygon") return;
            Refuse($"the element <{name}>");
        }

        // The element's computed style: inherited values, then attributes, then matching rules, then its style attribute.
        private Dictionary<string, string> Cascade(XElement e, Dictionary<string, string> parent)
        {
            var style = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string key, string value) in parent)
                if (Inherited.Contains(key)) style[key] = value;

            foreach (XAttribute a in e.Attributes())
                if (a.Name.NamespaceName.Length == 0 && a.Name.LocalName is not ("style" or "class" or "id" or "transform" or "d" or "points")) style[a.Name.LocalName] = a.Value.Trim();

            string tag = e.Name.LocalName;
            string? id = e.Attribute("id")?.Value;
            string[] classes = (e.Attribute("class")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (SvgCssRule rule in _rules.Where(r => r.Matches(tag, id, classes)).OrderBy(r => r.Specificity).ThenBy(r => r.Order))
                foreach ((string key, string value) in rule.Declarations) style[key] = value;

            foreach ((string key, string value) in SvgCss.Declarations(e.Attribute("style")?.Value)) style[key] = value;

            foreach ((string key, string value) in style.ToList())
            {
                if (value == "inherit") { if (parent.TryGetValue(key, out string? up)) style[key] = up; else style.Remove(key); }
            }

            foreach (string key in RefusedProperties)
                if (style.TryGetValue(key, out string? v) && v != "none") Refuse($"the property {key}");
            if (style.TryGetValue("mix-blend-mode", out string? blend) && blend != "normal") Refuse($"mix-blend-mode {blend}");
            return style;
        }

        private void ApplyNodeProperties(SvgNode node, XElement e, Dictionary<string, string> style)
        {
            if (e != _svg)
            {
                if (SvgValues.Transform(e.Attribute("transform")?.Value) is { } m) node.Transform = m;
                else Refuse($"the transform \"{e.Attribute("transform")?.Value}\"");
            }

            node.Opacity = Fraction(style.GetValueOrDefault("opacity"), 1);
            if (style.GetValueOrDefault("clip-path") is { } clip && clip != "none") node.Clip = ClipPath(clip);
        }

        private SvgShape Shape(Geometry geometry, XElement e, Dictionary<string, string> style)
        {
            var shape = new SvgShape(geometry)
            {
                Fill = Paint(style.GetValueOrDefault("fill") ?? "black", Fraction(style.GetValueOrDefault("fill-opacity"), 1), style),
                Stroke = Paint(style.GetValueOrDefault("stroke") ?? "none", Fraction(style.GetValueOrDefault("stroke-opacity"), 1), style),
                StrokeWidth = SvgValues.Length(style.GetValueOrDefault("stroke-width"), 1) ?? RefuseValue("stroke-width", 1, style),
                Join = style.GetValueOrDefault("stroke-linejoin") switch { "round" => PenLineJoin.Round, "bevel" => PenLineJoin.Bevel, _ => PenLineJoin.Miter },
                Cap = style.GetValueOrDefault("stroke-linecap") switch { "round" => PenLineCap.Round, "square" => PenLineCap.Square, _ => PenLineCap.Flat },
                MiterLimit = Number(style.GetValueOrDefault("stroke-miterlimit"), 4),
                DashOffset = SvgValues.Length(style.GetValueOrDefault("stroke-dashoffset"), 0) ?? 0,
            };
            if (style.GetValueOrDefault("stroke-dasharray") is { } dashes && dashes != "none")
            {
                List<double> d = SvgValues.Numbers(dashes);
                shape.Dashes = (d.Count % 2 == 1 ? d.Concat(d) : d).ToArray();
            }

            ApplyNodeProperties(shape, e, style);
            return shape;
        }

        private double RefuseValue(string property, double fallback, Dictionary<string, string> style)
        {
            Refuse($"the {property} \"{style.GetValueOrDefault(property)}\"");
            return fallback;
        }

        private static double Fraction(string? text, double fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            string t = text.Trim();
            bool percent = t.EndsWith('%');
            if (!double.TryParse(percent ? t[..^1] : t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return fallback;
            return Math.Clamp(percent ? v / 100 : v, 0, 1);
        }

        private static double Number(string? text, double fallback) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

        private SvgPaint? Paint(string value, double opacity, Dictionary<string, string> style)
        {
            string v = value.Trim();
            if (v == "none" || v.Length == 0) return null;
            if (v.StartsWith("url(", StringComparison.Ordinal))
            {
                string? id = SvgValues.UrlId(v);
                if (id is null || !_ids.TryGetValue(id, out XElement? target))
                {
                    string fallback = v[(v.IndexOf(')') + 1)..].Trim();
                    return fallback.Length > 0 ? Paint(fallback, opacity, style) : null;
                }

                if (target.Name.LocalName != "linearGradient")
                {
                    Refuse($"the paint server <{target.Name.LocalName}>");
                    return null;
                }

                if (Gradient(id) is not { } gradient) return null;
                return new SvgLinear
                {
                    Start = gradient.Start, End = gradient.End, BoundingBoxUnits = gradient.BoundingBoxUnits, GradientTransform = gradient.GradientTransform,
                    Spread = gradient.Spread, Stops = gradient.Stops, Opacity = opacity,
                };
            }

            if (v == "currentColor") v = style.GetValueOrDefault("color") ?? "black";
            if (SvgValues.Colour(v) is { } colour) return new SvgSolid(colour, opacity);
            Refuse($"the colour \"{value}\"");
            return null;
        }

        // A linear gradient with its href chain resolved: attributes and stops come from the nearest gradient that has them.
        private SvgLinear? Gradient(string id)
        {
            if (_gradients.TryGetValue(id, out SvgLinear? done)) return done;
            _gradients[id] = null;
            var chain = new List<XElement>();
            for (XElement? g = _ids.GetValueOrDefault(id); g is not null && chain.Count < 16 && !chain.Contains(g);)
            {
                if (g.Name.LocalName != "linearGradient") { Refuse($"a gradient that refers to <{g.Name.LocalName}>"); break; }
                chain.Add(g);
                string? href = (g.Attribute(XLink + "href") ?? g.Attribute("href"))?.Value;
                g = href is { Length: > 1 } && href[0] == '#' ? _ids.GetValueOrDefault(href[1..]) : null;
            }

            string? Attr(string name) => chain.Select(g => g.Attribute(name)?.Value).FirstOrDefault(v => v is not null);
            bool bbox = Attr("gradientUnits") != "userSpaceOnUse";
            double Coord(string name, double fallback, double extent)
            {
                string? text = Attr(name);
                if (string.IsNullOrWhiteSpace(text)) return fallback;
                string t = text.Trim();
                if (t.EndsWith('%')) return double.Parse(t[..^1], CultureInfo.InvariantCulture) / 100 * (bbox ? 1 : extent);
                return SvgValues.Length(t) ?? fallback;
            }

            Matrix? transform = SvgValues.Transform(Attr("gradientTransform"));
            if (transform is null) Refuse("a gradientTransform that cannot be read");
            XElement? withStops = chain.FirstOrDefault(g => g.Elements().Any(s => s.Name.LocalName == "stop"));
            var stops = new List<SvgStop>();
            double last = 0;
            foreach (XElement s in withStops?.Elements().Where(s => s.Name.LocalName == "stop") ?? Enumerable.Empty<XElement>())
            {
                Dictionary<string, string> st = Cascade(s, Root());
                double offset = Math.Max(last, Fraction(s.Attribute("offset")?.Value, 0));
                last = offset;
                Color c = SvgValues.Colour(st.GetValueOrDefault("stop-color") ?? "black") ?? Colors.Black;
                double a = Fraction(st.GetValueOrDefault("stop-opacity"), 1);
                stops.Add(new SvgStop(offset, Color.FromArgb((byte)Math.Round(c.A * a), c.R, c.G, c.B)));
            }

            var gradient = new SvgLinear
            {
                Start = new Point(Coord("x1", 0, _viewBox.Width), Coord("y1", 0, _viewBox.Height)),
                End = new Point(Coord("x2", bbox ? 1 : _viewBox.Width, _viewBox.Width), Coord("y2", 0, _viewBox.Height)),
                BoundingBoxUnits = bbox,
                GradientTransform = transform ?? Matrix.Identity,
                Spread = Attr("spreadMethod") switch { "reflect" => GradientSpreadMethod.Reflect, "repeat" => GradientSpreadMethod.Repeat, _ => GradientSpreadMethod.Pad },
                Stops = stops,
            };
            _gradients[id] = gradient;
            return gradient;
        }

        // A clip path in the referencing element's user space: the union of its shapes, each with its own transform and clip-rule.
        private Geometry? ClipPath(string reference)
        {
            string? id = SvgValues.UrlId(reference);
            if (id is null || !_ids.TryGetValue(id, out XElement? clip) || clip.Name.LocalName != "clipPath")
            {
                Refuse($"the clip-path \"{reference}\"");
                return null;
            }

            if (clip.Attribute("clipPathUnits")?.Value == "objectBoundingBox") Refuse("clipPathUnits objectBoundingBox");
            if (clip.Attribute("clip-path") is not null) Refuse("a clip path that is itself clipped");
            Dictionary<string, string> clipStyle = Cascade(clip, Root());
            Geometry? union = null;
            foreach (XElement child in clip.Elements().Where(IsSvg))
            {
                if (Ignored.Contains(child.Name.LocalName)) continue;
                Dictionary<string, string> st = Cascade(child, clipStyle);
                FillRule rule = st.GetValueOrDefault("clip-rule") == "evenodd" ? FillRule.EvenOdd : FillRule.NonZero;
                if (Geometry(child, rule) is not { } g)
                {
                    if (child.Name.LocalName is not ("path" or "rect" or "circle" or "ellipse" or "line" or "polyline" or "polygon")) Refuse($"the element <{child.Name.LocalName}> in a clip path");
                    continue;
                }

                if (SvgValues.Transform(child.Attribute("transform")?.Value) is { } m && !m.IsIdentity) g.Transform = new MatrixTransform(m);
                union = union is null ? g : new CombinedGeometry(GeometryCombineMode.Union, union, g);
            }

            union ??= new RectangleGeometry(new Rect(0, 0, 0, 0));
            if (SvgValues.Transform(clip.Attribute("transform")?.Value) is { } ct && !ct.IsIdentity)
                union = new CombinedGeometry(GeometryCombineMode.Union, union, new RectangleGeometry(default)) { Transform = new MatrixTransform(ct) };
            return union;
        }

        private Geometry? Geometry(XElement e, FillRule rule)
        {
            double L(string name, double fallback = 0)
            {
                string? text = e.Attribute(name)?.Value;
                if (text is not null && text.Trim().EndsWith('%'))
                {
                    Refuse($"a percentage in {name}");
                    return fallback;
                }

                return SvgValues.Length(text, fallback) ?? fallback;
            }

            switch (e.Name.LocalName)
            {
                case "path":
                    return SvgPathData.Parse(e.Attribute("d")?.Value, rule);
                case "rect":
                {
                    double x = L("x"), y = L("y"), w = L("width"), h = L("height");
                    if (w <= 0 || h <= 0) return null;
                    double rx = L("rx", -1), ry = L("ry", -1);
                    if (rx < 0) rx = ry;
                    if (ry < 0) ry = rx;
                    rx = Math.Clamp(rx, 0, w / 2);
                    ry = Math.Clamp(ry, 0, h / 2);
                    return Polygon(rule, g =>
                    {
                        if (rx <= 0 || ry <= 0)
                        {
                            g.BeginFigure(new Point(x, y), true);
                            g.LineTo(new Point(x + w, y));
                            g.LineTo(new Point(x + w, y + h));
                            g.LineTo(new Point(x, y + h));
                            g.EndFigure(true);
                            return;
                        }

                        var r = new Size(rx, ry);
                        g.BeginFigure(new Point(x + rx, y), true);
                        g.LineTo(new Point(x + w - rx, y));
                        g.ArcTo(new Point(x + w, y + ry), r, 0, false, SweepDirection.Clockwise);
                        g.LineTo(new Point(x + w, y + h - ry));
                        g.ArcTo(new Point(x + w - rx, y + h), r, 0, false, SweepDirection.Clockwise);
                        g.LineTo(new Point(x + rx, y + h));
                        g.ArcTo(new Point(x, y + h - ry), r, 0, false, SweepDirection.Clockwise);
                        g.LineTo(new Point(x, y + ry));
                        g.ArcTo(new Point(x + rx, y), r, 0, false, SweepDirection.Clockwise);
                        g.EndFigure(true);
                    });
                }
                case "circle":
                {
                    double r = L("r");
                    return r > 0 ? Ellipse(rule, L("cx"), L("cy"), r, r) : null;
                }
                case "ellipse":
                {
                    double rx = L("rx"), ry = L("ry");
                    return rx > 0 && ry > 0 ? Ellipse(rule, L("cx"), L("cy"), rx, ry) : null;
                }
                case "line":
                    return Polygon(rule, g =>
                    {
                        g.BeginFigure(new Point(L("x1"), L("y1")), false);
                        g.LineTo(new Point(L("x2"), L("y2")));
                        g.EndFigure(false);
                    });
                case "polyline" or "polygon":
                {
                    List<Point> points = SvgValues.Points(e.Attribute("points")?.Value);
                    if (points.Count < 2) return null;
                    bool closed = e.Name.LocalName == "polygon";
                    return Polygon(rule, g =>
                    {
                        g.BeginFigure(points[0], true);
                        foreach (Point p in points.Skip(1)) g.LineTo(p);
                        g.EndFigure(closed);
                    });
                }
                default:
                    return null;
            }
        }

        private static Geometry Ellipse(FillRule rule, double cx, double cy, double rx, double ry) => Polygon(rule, g =>
        {
            var r = new Size(rx, ry);
            g.BeginFigure(new Point(cx + rx, cy), true);
            g.ArcTo(new Point(cx, cy + ry), r, 0, false, SweepDirection.Clockwise);
            g.ArcTo(new Point(cx - rx, cy), r, 0, false, SweepDirection.Clockwise);
            g.ArcTo(new Point(cx, cy - ry), r, 0, false, SweepDirection.Clockwise);
            g.ArcTo(new Point(cx + rx, cy), r, 0, false, SweepDirection.Clockwise);
            g.EndFigure(true);
        });

        private static Geometry Polygon(FillRule rule, Action<StreamGeometryContext> build)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext g = geometry.Open())
            {
                g.SetFillRule(rule);
                build(g);
            }

            return geometry;
        }
    }
}
