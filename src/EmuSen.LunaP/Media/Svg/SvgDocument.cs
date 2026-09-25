using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    // A subset of SVG 1.1 drawn through Avalonia's DrawingContext, refusing whole what it cannot draw - see docs/LunaP.md §99.
    /// <summary>An SVG file parsed into shapes that draw through Avalonia, for the subset icons and logos use; a file needing anything outside it is refused whole, with the reasons, and draws nothing.</summary>
    public sealed class SvgDocument
    {
        private readonly SvgGroup _root;
        private readonly string _align;
        private readonly bool _slice;

        private SvgDocument(SvgGroup root, Rect viewBox, Size size, string align, bool slice, IReadOnlyList<string> refusals, int shapes)
        {
            _root = root;
            ViewBox = viewBox;
            Size = size;
            _align = align;
            _slice = slice;
            Refusals = refusals;
            ShapeCount = shapes;
        }

        /// <summary>The document's own size in pixels: its width and height, else its viewBox's.</summary>
        public Size Size { get; }

        /// <summary>The user-space rectangle the document maps onto its viewport.</summary>
        public Rect ViewBox { get; }

        /// <summary>Why the document is refused: every element, attribute or value outside the supported subset, once each. Empty for a document that draws.</summary>
        public IReadOnlyList<string> Refusals { get; }

        /// <summary>Whether anything in the document is outside the supported subset, in which case it draws nothing.</summary>
        public bool IsRefused => Refusals.Count > 0;

        /// <summary>How many shapes the document draws.</summary>
        public int ShapeCount { get; }

        /// <summary>Reads a file. Never throws for the file's content or absence; an unreadable file is a refused document.</summary>
        /// <param name="path">The .svg file.</param>
        /// <returns>The document, refused with its reasons when anything is outside the subset.</returns>
        public static SvgDocument Load(string path)
        {
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return Refused($"the file cannot be read: {e.Message}");
            }
        }

        /// <summary>Parses SVG markup. Never throws; malformed markup is a refused document.</summary>
        /// <param name="markup">The whole of an SVG file's text.</param>
        /// <returns>The document, refused with its reasons when anything is outside the subset.</returns>
        public static SvgDocument Parse(string markup)
        {
            XDocument xml;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersFromEntities = 1_000_000 };
                using var reader = XmlReader.Create(new StringReader(markup), settings);
                xml = XDocument.Load(reader);
            }
            catch (XmlException e)
            {
                return Refused($"not well-formed XML: {e.Message}");
            }

            if (xml.Root is not { } svg || svg.Name.LocalName != "svg" || !SvgReader.IsSvg(svg)) return Refused("the root element is not <svg>");
            return new SvgReader(svg).Read();
        }

        private static SvgDocument Refused(string reason) =>
            new(new SvgGroup(), new Rect(0, 0, 0, 0), default, "xMidYMid", false, new[] { reason }, 0);

        internal static SvgDocument Create(SvgGroup root, Rect viewBox, Size size, string align, bool slice, IReadOnlyList<string> refusals, int shapes) =>
            new(root, viewBox, size, align, slice, refusals, shapes);

        /// <summary>The transform from the document's user space to a viewport, honouring its preserveAspectRatio.</summary>
        /// <param name="viewport">The rectangle the document is drawn into.</param>
        /// <returns>The matrix Draw applies.</returns>
        public Matrix ViewportTransform(Rect viewport)
        {
            if (ViewBox.Width <= 0 || ViewBox.Height <= 0) return Matrix.CreateTranslation(viewport.X, viewport.Y);
            double sx = viewport.Width / ViewBox.Width, sy = viewport.Height / ViewBox.Height;
            if (_align == "none") return Matrix.CreateTranslation(-ViewBox.X, -ViewBox.Y) * Matrix.CreateScale(sx, sy) * Matrix.CreateTranslation(viewport.X, viewport.Y);
            double s = _slice ? Math.Max(sx, sy) : Math.Min(sx, sy);
            double fx = _align.Contains("xMid", StringComparison.Ordinal) ? 0.5 : _align.Contains("xMax", StringComparison.Ordinal) ? 1 : 0;
            double fy = _align.Contains("YMid", StringComparison.Ordinal) ? 0.5 : _align.Contains("YMax", StringComparison.Ordinal) ? 1 : 0;
            double tx = viewport.X + (viewport.Width - ViewBox.Width * s) * fx;
            double ty = viewport.Y + (viewport.Height - ViewBox.Height * s) * fy;
            return Matrix.CreateTranslation(-ViewBox.X, -ViewBox.Y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(tx, ty);
        }

        /// <summary>Draws the document into a viewport, clipped to it. A refused document draws nothing.</summary>
        /// <param name="context">The context to draw into.</param>
        /// <param name="viewport">The rectangle the viewBox is mapped onto.</param>
        public void Draw(DrawingContext context, Rect viewport)
        {
            if (IsRefused || viewport.Width <= 0 || viewport.Height <= 0) return;
            using DrawingContext.PushedState clip = context.PushClip(viewport);
            using DrawingContext.PushedState place = context.PushTransform(ViewportTransform(viewport));
            _root.Draw(context);
        }
    }
}
