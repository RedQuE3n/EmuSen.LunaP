using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Controls
{
    // A small mark as plain data: a stroked path in a 24-unit box, or a badge of square pixels - see docs/LunaP.md §222.3.
    /// <summary>A small mark drawn before a name: a path stroked in a 24-unit box, or a rounded badge with a grid of pixels cut into it.</summary>
    public sealed record PlanetMark
    {
        /// <summary>SVG path data in a 24-unit box, stroked 2 wide with round ends and joins; null when the mark is a badge.</summary>
        public string? Stroke { get; init; }

        /// <summary>A badge's pixels, one string per row, 'X' for a pixel; null when the mark is a path.</summary>
        public IReadOnlyList<string>? Pixels { get; init; }
    }

    // Draws a PlanetMark: a path in the ink, or the badge in the ink with its pixels in the dark ink, crisp - see docs/LunaP.md §222.3.
    /// <summary>Draws a PlanetMark in an ink, scaled to the control: a path's 24-unit box, or a badge's 11-unit square with its pixel grid one unit in.</summary>
    public sealed class MarkGlyph : Control
    {
        /// <summary>Defines the Ink property.</summary>
        public static readonly StyledProperty<IBrush?> InkProperty = AvaloniaProperty.Register<MarkGlyph, IBrush?>(nameof(Ink));

        /// <summary>Defines the Dark property.</summary>
        public static readonly StyledProperty<IBrush?> DarkProperty = AvaloniaProperty.Register<MarkGlyph, IBrush?>(nameof(Dark));

        /// <summary>A badge's side and its pixel grid's offset, in its own units.</summary>
        public const double BadgeSide = 11, PixelLeft = 1, PixelTop = 1.5;

        static MarkGlyph() => AffectsRender<MarkGlyph>(InkProperty, DarkProperty);

        private PlanetMark _mark = new();
        private Geometry? _path;

        /// <summary>An empty mark.</summary>
        public MarkGlyph() => IsHitTestVisible = false;

        /// <summary>The mark drawn.</summary>
        public PlanetMark Mark { get => _mark; set { _mark = value ?? new PlanetMark(); _path = null; InvalidateVisual(); } }

        /// <summary>The path's stroke, or the badge's fill.</summary>
        public IBrush? Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }

        /// <summary>A badge's pixels.</summary>
        public IBrush? Dark { get => GetValue(DarkProperty); set => SetValue(DarkProperty, value); }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0 || Ink is not { } ink) return;
            if (_mark.Pixels is { } rows)
            {
                using (context.PushTransform(Matrix.CreateScale(Bounds.Width / BadgeSide, Bounds.Height / BadgeSide)))
                {
                    context.DrawRectangle(ink, null, new RoundedRect(new Rect(0, 0, BadgeSide, BadgeSide), 1.6));
                    if (Dark is not { } dark) return;
                    using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
                        for (int y = 0; y < rows.Count; y++)
                            for (int x = 0; x < rows[y].Length; x++)
                                if (rows[y][x] == 'X') context.FillRectangle(dark, new Rect(PixelLeft + x, PixelTop + y, 1, 1));
                }
                return;
            }
            if (_mark.Stroke is not { Length: > 0 } stroke) return;
            _path ??= Geometry.Parse(stroke);
            using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 24, Bounds.Height / 24)))
                context.DrawGeometry(null, new ImmutablePen(ink.ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), _path);
        }
    }
}
