using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>How an ImageCarousel lays its items out.</summary>
    public enum CarouselLayout
    {
        /// <summary>A straight row or column, the items spaced evenly along it.</summary>
        Row,
        /// <summary>A wheel: every item starts at the hub and is turned about a centre by its distance from the selection.</summary>
        Wheel,
    }

    // The wheel layout, the content offset and the reflections beneath a row - see docs/LunaP.md §190.
    public partial class ImageCarousel
    {
        public static readonly StyledProperty<CarouselLayout> LayoutProperty = AvaloniaProperty.Register<ImageCarousel, CarouselLayout>(nameof(Layout));
        public static readonly StyledProperty<double> WheelRotationProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(WheelRotation), 7.5);
        public static readonly StyledProperty<Point> WheelOriginProperty = AvaloniaProperty.Register<ImageCarousel, Point>(nameof(WheelOrigin), new Point(-3, 0.5));
        public static readonly StyledProperty<int> ItemsBeforeProperty = AvaloniaProperty.Register<ImageCarousel, int>(nameof(ItemsBefore), 8);
        public static readonly StyledProperty<int> ItemsAfterProperty = AvaloniaProperty.Register<ImageCarousel, int>(nameof(ItemsAfter), 8);
        public static readonly StyledProperty<bool> ItemsUprightProperty = AvaloniaProperty.Register<ImageCarousel, bool>(nameof(ItemsUpright));
        public static readonly StyledProperty<HorizontalAlignment> WheelHorizontalAlignmentProperty = AvaloniaProperty.Register<ImageCarousel, HorizontalAlignment>(nameof(WheelHorizontalAlignment), HorizontalAlignment.Center);
        public static readonly StyledProperty<VerticalAlignment> WheelVerticalAlignmentProperty = AvaloniaProperty.Register<ImageCarousel, VerticalAlignment>(nameof(WheelVerticalAlignment), VerticalAlignment.Center);
        public static readonly StyledProperty<Point> ContentOffsetProperty = AvaloniaProperty.Register<ImageCarousel, Point>(nameof(ContentOffset));
        public static readonly StyledProperty<bool> ReflectionsProperty = AvaloniaProperty.Register<ImageCarousel, bool>(nameof(Reflections));
        public static readonly StyledProperty<double> ReflectionOpacityProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(ReflectionOpacity), 0.5);
        public static readonly StyledProperty<double> ReflectionFalloffProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(ReflectionFalloff), 1);

        public static readonly StyledProperty<Point> SelectedItemMarginsProperty = AvaloniaProperty.Register<ImageCarousel, Point>(nameof(SelectedItemMargins));
        public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(LineSpacing), 1.2);

        private readonly List<(Control Item, Control Reflection)> _reflections = new();

        /// <summary>For a row, extra room in pixels before the selected item (X) and after it (Y), along the row, either negative; every item on that side moves by it, and an item between its slots by the fraction of the way it is from the selection. (0, 0) by default.</summary>
        public Point SelectedItemMargins { get => GetValue(SelectedItemMarginsProperty); set => SetValue(SelectedItemMarginsProperty, value); }

        /// <summary>The line pitch of items shown as text, as a multiple of FontSize. 1.2 by default.</summary>
        public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

        // The selected item's margins as the row moves: full beyond one item from the selection, in proportion within it, as the consumer measured (§192).
        private double MarginShift(double offset) =>
            offset < 0 ? -SelectedItemMargins.X * Math.Min(1, -offset) : SelectedItemMargins.Y * Math.Min(1, offset);

        /// <summary>Creates a carousel that clips its items to its own box.</summary>
        public ImageCarousel() => ClipToBounds = true;

        /// <summary>A straight row or a wheel. Row by default.</summary>
        public CarouselLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

        /// <summary>For a wheel, the degrees each item is turned per item from the selection, clockwise on screen when positive. 7.5 by default.</summary>
        public double WheelRotation { get => GetValue(WheelRotationProperty); set => SetValue(WheelRotationProperty, value); }

        /// <summary>For a wheel, the centre it turns about, in multiples of the item size from the hub item's top left; a horizontal wheel reads it a quarter turn anticlockwise. (-3, 0.5) by default.</summary>
        public Point WheelOrigin { get => GetValue(WheelOriginProperty); set => SetValue(WheelOriginProperty, value); }

        /// <summary>For a wheel, how many items are drawn before the selection. 8 by default.</summary>
        public int ItemsBefore { get => GetValue(ItemsBeforeProperty); set => SetValue(ItemsBeforeProperty, value); }

        /// <summary>For a wheel, how many items are drawn after the selection. 8 by default.</summary>
        public int ItemsAfter { get => GetValue(ItemsAfterProperty); set => SetValue(ItemsAfterProperty, value); }

        /// <summary>For a wheel, whether the items keep their upright orientation and are only moved round it. False by default.</summary>
        public bool ItemsUpright { get => GetValue(ItemsUprightProperty); set => SetValue(ItemsUprightProperty, value); }

        /// <summary>For a vertical wheel, where the hub item sits across the width. Center by default.</summary>
        public HorizontalAlignment WheelHorizontalAlignment { get => GetValue(WheelHorizontalAlignmentProperty); set => SetValue(WheelHorizontalAlignmentProperty, value); }

        /// <summary>For a horizontal wheel, where the hub item sits across the height. Center by default.</summary>
        public VerticalAlignment WheelVerticalAlignment { get => GetValue(WheelVerticalAlignmentProperty); set => SetValue(WheelVerticalAlignmentProperty, value); }

        /// <summary>A shift of every item, as fractions of the control's width and height. (0, 0) by default.</summary>
        public Point ContentOffset { get => GetValue(ContentOffsetProperty); set => SetValue(ContentOffsetProperty, value); }

        /// <summary>For a horizontal row, whether each image is mirrored beneath itself, the row making room for it. False by default.</summary>
        public bool Reflections { get => GetValue(ReflectionsProperty); set => SetValue(ReflectionsProperty, value); }

        /// <summary>A reflection's opacity where it meets its image. 0.5 by default.</summary>
        public double ReflectionOpacity { get => GetValue(ReflectionOpacityProperty); set => SetValue(ReflectionOpacityProperty, value); }

        /// <summary>How fast a reflection fades, relative to its image's height: 1 is gone at the image's height, 2 at half of it, 0 never fades. 1 by default.</summary>
        public double ReflectionFalloff { get => GetValue(ReflectionFalloffProperty); set => SetValue(ReflectionFalloffProperty, value); }

        /// <summary>The reflection controls now shown, each with the item it mirrors.</summary>
        public IReadOnlyList<(Control Item, Control Reflection)> ShownReflections => _reflections;

        private bool Reflects => Reflections && Layout == CarouselLayout.Row && Orientation == Orientation.Horizontal;

        // The one box every wheel item starts from: centred along the wheel's axis, placed across it by the wheel alignment.
        private Rect HubRect(Size bounds)
        {
            double w = ItemSize.Width > 0 ? ItemSize.Width : bounds.Width, h = ItemSize.Height > 0 ? ItemSize.Height : bounds.Height;
            double x = Orientation == Orientation.Horizontal ? (bounds.Width - w) / 2
                : WheelHorizontalAlignment switch { HorizontalAlignment.Left => 0, HorizontalAlignment.Right => bounds.Width - w, _ => (bounds.Width - w) / 2 };
            double y = Orientation == Orientation.Vertical ? (bounds.Height - h) / 2
                : WheelVerticalAlignment switch { VerticalAlignment.Top => 0, VerticalAlignment.Bottom => bounds.Height - h, _ => (bounds.Height - h) / 2 };
            return new Rect(x, y, w, h).Translate(new Vector(ContentOffset.X * bounds.Width, ContentOffset.Y * bounds.Height));
        }

        // The point the selected item grows from: a wheel's item centre, or a row item's edge its cross-axis alignment names.
        private Point GrowthPoint(Rect box)
        {
            if (Layout == CarouselLayout.Wheel && Orientation == Orientation.Horizontal) return box.Center;
            if (Orientation == Orientation.Horizontal)
                return new Point(box.Center.X, ItemVerticalAlignment switch { VerticalAlignment.Top => box.Top, VerticalAlignment.Bottom => box.Bottom, _ => box.Center.Y });
            return new Point(ItemHorizontalAlignment switch { HorizontalAlignment.Left => box.Left, HorizontalAlignment.Right => box.Right, _ => box.Center.X }, box.Center.Y);
        }

        // A vertical wheel aligns its items to the selected item's grown box, so a left or right alignment moves every item by half the growth.
        private Vector WheelAlignShift(Rect box)
        {
            if (Layout != CarouselLayout.Wheel || Orientation != Orientation.Vertical) return default;
            double half = (ItemScale - 1) * box.Width / 2;
            return ItemHorizontalAlignment switch { HorizontalAlignment.Left => new Vector(-half, 0), HorizontalAlignment.Right => new Vector(half, 0), _ => default };
        }

        /// <summary>Where a wheel moves the item a distance from the selection: a turn about the wheel's centre, or with ItemsUpright the same travel without the turn. Identity for a row.</summary>
        /// <param name="offset">Items from the selection, fractional while the wheel moves.</param>
        /// <param name="bounds">The control's size.</param>
        /// <returns>A transform in the control's coordinates.</returns>
        public Matrix WheelTransform(double offset, Size bounds)
        {
            if (Layout != CarouselLayout.Wheel) return Matrix.Identity;
            Rect hub = HubRect(bounds);
            (double ox, double oy) = (WheelOrigin.X, WheelOrigin.Y);
            bool vertical = Orientation == Orientation.Vertical;
            Vector toCentre = vertical ? new Vector((ox - 0.5) * hub.Width, (oy - 0.5) * hub.Height) : new Vector((oy - 0.5) * hub.Height, (0.5 - ox) * hub.Width);
            Point centre = hub.Center + toCentre;
            double angle = offset * WheelRotation * Math.PI / 180;
            if (!ItemsUpright) return Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(angle) * Matrix.CreateTranslation(centre.X, centre.Y);
            Vector arm = vertical ? new Vector(-ox * hub.Width, 0) : new Vector(0, ox * hub.Width);
            Vector turned = new(arm.X * Math.Cos(angle) - arm.Y * Math.Sin(angle), arm.X * Math.Sin(angle) + arm.Y * Math.Cos(angle));
            return Matrix.CreateTranslation(turned.X - arm.X, turned.Y - arm.Y);
        }

        private void AddReflection(int offset, FittedImage image)
        {
            var reflection = new FittedImage { Source = image.Source, Fit = image.Fit, Tint = image.Tint, Saturation = image.Saturation, IsHitTestVisible = false };
            _reflections.Add((image, reflection));
            LogicalChildren.Add(reflection);
            VisualChildren.Add(reflection);
        }

        // The item's opacity times ReflectionOpacity at the image's edge, gone at 1/falloff of its height; drawn before the flip, so its strong end is the local bottom.
        private IBrush ReflectionMask(double itemOpacity)
        {
            double opacity = Math.Clamp(ReflectionOpacity, 0, 1) * Math.Clamp(itemOpacity, 0, 1), falloff = Math.Max(0, ReflectionFalloff), end = falloff > 1 ? 1 / falloff : 1;
            var mask = new LinearGradientBrush { StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative) };
            mask.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(255 * opacity), 255, 255, 255), 0));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(255 * opacity * Math.Max(0, 1 - falloff * end)), 255, 255, 255), end));
            return mask;
        }
    }
}
