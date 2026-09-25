using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>How a FittedImage fills its box.</summary>
    public enum ImageFit
    {
        /// <summary>Stretched to the box, ignoring the aspect ratio.</summary>
        Fill,
        /// <summary>As large as fits inside the box with its aspect ratio kept; the control measures to the fitted size.</summary>
        Contain,
        /// <summary>Filling the box with its aspect ratio kept, cropped at CropPosition.</summary>
        Cover,
        /// <summary>Repeated at TileSize from the corner the tile alignments name.</summary>
        Tile,
    }

    // A raster or SVG file fitted, cropped or tiled, tinted by a colour or a gradient, desaturated and rounded - see docs/LunaP.md §98.2.
    /// <summary>An image read from a raster or SVG file, fitted, cropped or tiled into its box, multiplied by a colour or a two-colour gradient, desaturated, and clipped to rounded corners.</summary>
    public class FittedImage : Control
    {
        public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<FittedImage, string?>(nameof(Source));
        public static readonly StyledProperty<ImageFit> FitProperty = AvaloniaProperty.Register<FittedImage, ImageFit>(nameof(Fit), ImageFit.Contain);
        public static readonly StyledProperty<Point> CropPositionProperty = AvaloniaProperty.Register<FittedImage, Point>(nameof(CropPosition), new Point(0.5, 0.5));
        public static readonly StyledProperty<Size> TileSizeProperty = AvaloniaProperty.Register<FittedImage, Size>(nameof(TileSize));
        public static readonly StyledProperty<HorizontalAlignment> TileHorizontalAlignmentProperty = AvaloniaProperty.Register<FittedImage, HorizontalAlignment>(nameof(TileHorizontalAlignment), HorizontalAlignment.Left);
        public static readonly StyledProperty<VerticalAlignment> TileVerticalAlignmentProperty = AvaloniaProperty.Register<FittedImage, VerticalAlignment>(nameof(TileVerticalAlignment), VerticalAlignment.Top);
        public static readonly StyledProperty<Color> TintProperty = AvaloniaProperty.Register<FittedImage, Color>(nameof(Tint), Colors.White);
        public static readonly StyledProperty<Color?> TintEndProperty = AvaloniaProperty.Register<FittedImage, Color?>(nameof(TintEnd));
        public static readonly StyledProperty<Orientation> TintDirectionProperty = AvaloniaProperty.Register<FittedImage, Orientation>(nameof(TintDirection));
        public static readonly StyledProperty<double> SaturationProperty = AvaloniaProperty.Register<FittedImage, double>(nameof(Saturation), 1);
        public static readonly StyledProperty<double> CornerRadiusProperty = AvaloniaProperty.Register<FittedImage, double>(nameof(CornerRadius));
        public static readonly StyledProperty<BitmapInterpolationMode> InterpolationProperty = AvaloniaProperty.Register<FittedImage, BitmapInterpolationMode>(nameof(Interpolation), BitmapInterpolationMode.HighQuality);

        private ImageFile? _file;
        private Bitmap? _bitmap;

        static FittedImage()
        {
            AffectsMeasure<FittedImage>(SourceProperty, FitProperty);
            AffectsRender<FittedImage>(CropPositionProperty, TileSizeProperty, TileHorizontalAlignmentProperty, TileVerticalAlignmentProperty, TintProperty, TintEndProperty,
                TintDirectionProperty, SaturationProperty, CornerRadiusProperty, InterpolationProperty);
        }

        /// <summary>The file to show: a raster the platform decodes, or an .svg drawn by SvgDocument. A missing file shows nothing; a refused SVG shows a crossed box.</summary>
        public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

        /// <summary>How the image fills its box. Contain by default.</summary>
        public ImageFit Fit { get => GetValue(FitProperty); set => SetValue(FitProperty, value); }

        /// <summary>For Cover, the point of the image, as fractions of it, kept in view: (0.5, 0.5), the centre, by default.</summary>
        public Point CropPosition { get => GetValue(CropPositionProperty); set => SetValue(CropPositionProperty, value); }

        /// <summary>For Tile, each tile's size in pixels. An axis of 0 follows the other by the aspect ratio; both 0 is the image's own size.</summary>
        public Size TileSize { get => GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }

        /// <summary>For Tile, the edge the first whole tile starts from. Left by default.</summary>
        public HorizontalAlignment TileHorizontalAlignment { get => GetValue(TileHorizontalAlignmentProperty); set => SetValue(TileHorizontalAlignmentProperty, value); }

        /// <summary>For Tile, the edge the first whole tile starts from. Top by default.</summary>
        public VerticalAlignment TileVerticalAlignment { get => GetValue(TileVerticalAlignmentProperty); set => SetValue(TileVerticalAlignmentProperty, value); }

        /// <summary>A colour every pixel is multiplied by, alpha included. White, no change, by default.</summary>
        public Color Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

        /// <summary>When set, the tint runs from Tint to this colour across the image. Null by default.</summary>
        public Color? TintEnd { get => GetValue(TintEndProperty); set => SetValue(TintEndProperty, value); }

        /// <summary>The direction a two-colour tint runs in. Horizontal by default.</summary>
        public Orientation TintDirection { get => GetValue(TintDirectionProperty); set => SetValue(TintDirectionProperty, value); }

        /// <summary>1 keeps the colours, 0 is greyscale. 1 by default.</summary>
        public double Saturation { get => GetValue(SaturationProperty); set => SetValue(SaturationProperty, value); }

        /// <summary>The corner radius, in pixels, the drawn image is clipped to. 0 by default.</summary>
        public double CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

        /// <summary>How the image is sampled when scaled: HighQuality, linear, by default; None for nearest.</summary>
        public BitmapInterpolationMode Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }

        /// <summary>The image's own size in pixels, or 0 by 0 when there is nothing to show.</summary>
        public Size IntrinsicSize => File()?.Intrinsic ?? default;

        /// <summary>Whether the source is set and could not be read.</summary>
        public bool IsMissing => Source is { Length: > 0 } && File() is { Missing: true };

        /// <summary>Whether the source is an SVG outside the supported subset, drawn as a crossed box.</summary>
        public bool IsRefused => File() is { Refused: true };

        /// <summary>The rectangle the image was last drawn into, in the control's coordinates.</summary>
        public Rect DrawnRect { get; private set; }

        private ImageFile? File()
        {
            if (Source is not { Length: > 0 } path) return null;
            return _file ??= ImageFile.Open(path);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceProperty) _file = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size own = IntrinsicSize;
            if (own.Width <= 0 || own.Height <= 0) return new Size(Finite(availableSize.Width), Finite(availableSize.Height));
            bool wInf = double.IsInfinity(availableSize.Width), hInf = double.IsInfinity(availableSize.Height);
            if (wInf && hInf) return own;
            if (wInf) return new Size(availableSize.Height * own.Width / own.Height, availableSize.Height);
            if (hInf) return new Size(availableSize.Width, availableSize.Width * own.Height / own.Width);
            if (Fit != ImageFit.Contain) return availableSize;
            double s = Math.Min(availableSize.Width / own.Width, availableSize.Height / own.Height);
            return new Size(own.Width * s, own.Height * s);
        }

        private static double Finite(double v) => double.IsInfinity(v) ? 0 : v;

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            DrawnRect = default;
            ImageFile? file = File();
            if (file is null || file.Missing || bounds.Width <= 0 || bounds.Height <= 0) return;
            if (file.Refused)
            {
                DrawRefusal(context, bounds);
                return;
            }

            Size own = file.Intrinsic;
            if (own.Width <= 0 || own.Height <= 0) return;
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var effects = new ImageEffects(Tint, TintEnd ?? Tint, TintDirection == Orientation.Vertical, Saturation);

            using DrawingContext.PushedState options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Interpolation });
            using DrawingContext.PushedState? clip = CornerRadius > 0 ? context.PushClip(new RoundedRect(bounds, CornerRadius)) : null;
            using DrawingContext.PushedState? boundsClip = Fit is ImageFit.Cover or ImageFit.Tile ? context.PushClip(bounds) : null;

            if (Fit == ImageFit.Tile)
            {
                Size tile = TileSize;
                if (tile.Width <= 0 && tile.Height <= 0) tile = own;
                else if (tile.Width <= 0) tile = new Size(tile.Height * own.Width / own.Height, tile.Height);
                else if (tile.Height <= 0) tile = new Size(tile.Width, tile.Width * own.Height / own.Width);
                _bitmap = ImagePixels.Prepare(Source!, file, Pixels(tile, scaling), effects with { TintEnd = effects.Tint });
                if (_bitmap is null) return;
                double x0 = TileHorizontalAlignment == HorizontalAlignment.Right ? Positive(bounds.Width, tile.Width) : 0;
                double y0 = TileVerticalAlignment == VerticalAlignment.Bottom ? Positive(bounds.Height, tile.Height) : 0;
                var brush = new ImageBrush(_bitmap)
                {
                    TileMode = TileMode.Tile,
                    Stretch = Stretch.Fill,
                    DestinationRect = new RelativeRect(0, 0, tile.Width, tile.Height, RelativeUnit.Absolute),
                    Transform = new TranslateTransform(x0, y0),
                    TransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
                };
                context.FillRectangle(brush, bounds);
                DrawnRect = bounds;
                return;
            }

            Rect target = Fit switch
            {
                ImageFit.Cover => Cover(bounds, own, CropPosition),
                ImageFit.Contain => Contain(bounds, own),
                _ => bounds,
            };
            _bitmap = ImagePixels.Prepare(Source!, file, Pixels(target.Size, scaling), effects);
            if (_bitmap is null) return;
            context.DrawImage(_bitmap, new Rect(_bitmap.Size), target);
            DrawnRect = target.Intersect(bounds);
        }

        // The first tile's offset so that a tile ends flush with the far edge, taken into [0, tile).
        private static double Positive(double length, double tile) => ((length % tile) + tile) % tile;

        private static PixelSize Pixels(Size size, double scaling) =>
            new(Math.Max(1, (int)Math.Ceiling(size.Width * scaling - 0.001)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling - 0.001)));

        /// <summary>The rectangle an image of a given size covers a box with, cropped about a point of the image.</summary>
        /// <param name="box">The box to fill.</param>
        /// <param name="image">The image's own size.</param>
        /// <param name="crop">The point of the image, as fractions of it, kept in view.</param>
        /// <returns>Where the whole image is drawn; it overhangs the box on one axis.</returns>
        public static Rect Cover(Rect box, Size image, Point crop)
        {
            double s = Math.Max(box.Width / image.Width, box.Height / image.Height);
            double w = image.Width * s, h = image.Height * s;
            return new Rect(box.X - (w - box.Width) * crop.X, box.Y - (h - box.Height) * crop.Y, w, h);
        }

        /// <summary>The largest rectangle of an image's aspect ratio inside a box, centred.</summary>
        /// <param name="box">The box to fit inside.</param>
        /// <param name="image">The image's own size.</param>
        /// <returns>Where the image is drawn.</returns>
        public static Rect Contain(Rect box, Size image)
        {
            double s = Math.Min(box.Width / image.Width, box.Height / image.Height);
            double w = image.Width * s, h = image.Height * s;
            return new Rect(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        }

        // A refused SVG is shown as a crossed box in the palette's error colour, so a gap is never mistaken for a design.
        internal static void DrawRefusal(DrawingContext context, Rect bounds)
        {
            var pen = new Pen(LunaPalette.Error, Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 32));
            Rect r = bounds.Deflate(pen.Thickness / 2);
            context.DrawRectangle(null, pen, r);
            context.DrawLine(pen, r.TopLeft, r.BottomRight);
            context.DrawLine(pen, r.TopRight, r.BottomLeft);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image);
    }
}
