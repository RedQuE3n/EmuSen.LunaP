using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // A row of stars, the filled part cut at the value's fraction of the row's width - see docs/LunaP.md §101.1.
    /// <summary>A row of stars showing a value from 0 to 1: unfilled stars across the row and filled ones cut at the value, from image files or a drawn star.</summary>
    public class StarRating : Control
    {
        public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<StarRating, double>(nameof(Value));
        public static readonly StyledProperty<int> StarCountProperty = AvaloniaProperty.Register<StarRating, int>(nameof(StarCount), 5);
        public static readonly StyledProperty<string?> FilledPathProperty = AvaloniaProperty.Register<StarRating, string?>(nameof(FilledPath));
        public static readonly StyledProperty<string?> UnfilledPathProperty = AvaloniaProperty.Register<StarRating, string?>(nameof(UnfilledPath));
        public static readonly StyledProperty<Color> TintProperty = AvaloniaProperty.Register<StarRating, Color>(nameof(Tint), Colors.White);
        public static readonly StyledProperty<bool> OverlayProperty = AvaloniaProperty.Register<StarRating, bool>(nameof(Overlay), true);

        static StarRating()
        {
            AffectsMeasure<StarRating>(StarCountProperty, FilledPathProperty, UnfilledPathProperty);
            AffectsRender<StarRating>(ValueProperty, TintProperty, OverlayProperty);
        }

        /// <summary>The rating from 0 to 1; outside that range it is clamped.</summary>
        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        /// <summary>How many stars the row holds, 5 by default.</summary>
        public int StarCount { get => GetValue(StarCountProperty); set => SetValue(StarCountProperty, value); }

        /// <summary>An image of one filled star; null draws the built-in star in the palette's warning colour.</summary>
        public string? FilledPath { get => GetValue(FilledPathProperty); set => SetValue(FilledPathProperty, value); }

        /// <summary>An image of one unfilled star; null draws the built-in star in the palette's muted colour.</summary>
        public string? UnfilledPath { get => GetValue(UnfilledPathProperty); set => SetValue(UnfilledPathProperty, value); }

        /// <summary>A colour both images are multiplied by. White by default.</summary>
        public Color Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

        /// <summary>Whether the filled stars are drawn over the whole unfilled row (true, the default) or the unfilled stars only where the filled ones end.</summary>
        public bool Overlay { get => GetValue(OverlayProperty); set => SetValue(OverlayProperty, value); }

        /// <summary>The width of one star at a height, from the unfilled image's aspect ratio, else square.</summary>
        /// <param name="height">The row's height in pixels.</param>
        /// <returns>One star's width in pixels.</returns>
        public double StarWidth(double height)
        {
            Size own = PictureFiles.Intrinsic(UnfilledPath ?? FilledPath);
            return own.Width > 0 && own.Height > 0 ? height * own.Width / own.Height : height;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int n = Math.Max(1, StarCount);
            if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
            {
                double h = availableSize.Height;
                double w = StarWidth(h) * n;
                if (!double.IsInfinity(availableSize.Width) && w > availableSize.Width) { h *= availableSize.Width / w; w = availableSize.Width; }
                return new Size(w, h);
            }

            if (!double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
            {
                double star = availableSize.Width / n;
                return new Size(availableSize.Width, star * star / StarWidth(star));
            }

            return new Size(16 * n, 16);
        }

        public override void Render(DrawingContext context)
        {
            int n = Math.Max(1, StarCount);
            double h = Bounds.Height, w = StarWidth(h);
            double cut = Math.Clamp(Value, 0, 1) * w * n;
            using (context.PushClip(new Rect(Overlay ? 0 : cut, 0, Math.Max(0, Bounds.Width - (Overlay ? 0 : cut)), h)))
                for (int i = 0; i < n; i++) Star(context, UnfilledPath, new Rect(i * w, 0, w, h), LunaPalette.Muted.Color);
            using (context.PushClip(new Rect(0, 0, cut, h)))
                for (int i = 0; i < n; i++) Star(context, FilledPath, new Rect(i * w, 0, w, h), LunaPalette.Warning.Color);
        }

        private void Star(DrawingContext context, string? path, Rect box, Color builtIn)
        {
            if (path is { Length: > 0 })
            {
                PictureFiles.Draw(context, this, path, box, Tint, 1, contain: false);
                return;
            }

            context.DrawGeometry(new ImmutableSolidColorBrush(builtIn), null, StarGeometry(box));
        }

        // A five-pointed star inscribed in the box, its inner radius 0.4 of the outer.
        internal static Geometry StarGeometry(Rect box)
        {
            var g = new StreamGeometry();
            using StreamGeometryContext c = g.Open();
            Point centre = box.Center;
            double r = Math.Min(box.Width, box.Height) / 2;
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, radius = i % 2 == 0 ? r : r * 0.4;
                var p = new Point(centre.X + radius * Math.Cos(a), centre.Y + radius * Math.Sin(a));
                if (i == 0) c.BeginFigure(p, true);
                else c.LineTo(p);
            }

            c.EndFigure(true);
            return g;
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Text, () => $"{Math.Round(Math.Clamp(Value, 0, 1) * Math.Max(1, StarCount), 1)} of {Math.Max(1, StarCount)}");
    }
}
