using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Where an ImageGrid draws the selector against an item's background and picture.</summary>
    public enum GridSelectorLayer
    {
        /// <summary>Beneath the item's background.</summary>
        Bottom,
        /// <summary>Between the background and the picture or text.</summary>
        Middle,
        /// <summary>Over the picture or text.</summary>
        Top,
    }

    // Items in rows and columns, the selected one scaled, the rows scrolled to keep it in view, every state a function of properties a host sets from its own clock - see docs/LunaP.md §104.
    /// <summary>Items laid out in columns and rows, each an image or, without one, its text; the selected item is scaled, the others faded, desaturated and dimmed, and the rows scroll to keep the selection on the last row shown. A fractional ScrollRow and a FocusProgress draw it between states; the control keeps no clock.</summary>
    public class ImageGrid : Control
    {
        public static readonly StyledProperty<IReadOnlyList<CarouselItem>?> ItemsProperty = AvaloniaProperty.Register<ImageGrid, IReadOnlyList<CarouselItem>?>(nameof(Items));
        public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<ImageGrid, int>(nameof(SelectedIndex));
        public static readonly StyledProperty<Size> ItemSizeProperty = AvaloniaProperty.Register<ImageGrid, Size>(nameof(ItemSize), new Size(120, 160));
        public static readonly StyledProperty<Size?> ItemSpacingProperty = AvaloniaProperty.Register<ImageGrid, Size?>(nameof(ItemSpacing));
        public static readonly StyledProperty<double> ItemScaleProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ItemScale), 1.05);
        public static readonly StyledProperty<bool> ScaleInwardsProperty = AvaloniaProperty.Register<ImageGrid, bool>(nameof(ScaleInwards));
        public static readonly StyledProperty<bool> FractionalRowsProperty = AvaloniaProperty.Register<ImageGrid, bool>(nameof(FractionalRows));
        public static readonly StyledProperty<double> UnfocusedItemOpacityProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(UnfocusedItemOpacity), 1);
        public static readonly StyledProperty<double> UnfocusedItemSaturationProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(UnfocusedItemSaturation), 1);
        public static readonly StyledProperty<double> UnfocusedItemDimmingProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(UnfocusedItemDimming), 1);
        public static readonly StyledProperty<ImageFit> ImageFitProperty = AvaloniaProperty.Register<ImageGrid, ImageFit>(nameof(ImageFit), ImageFit.Contain);
        public static readonly StyledProperty<Point> ImageCropPositionProperty = AvaloniaProperty.Register<ImageGrid, Point>(nameof(ImageCropPosition), new Point(0.5, 0.5));
        public static readonly StyledProperty<BitmapInterpolationMode> ImageInterpolationProperty = AvaloniaProperty.Register<ImageGrid, BitmapInterpolationMode>(nameof(ImageInterpolation), BitmapInterpolationMode.HighQuality);
        public static readonly StyledProperty<double> ImageRelativeScaleProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ImageRelativeScale), 1);
        public static readonly StyledProperty<double> ImageCornerRadiusProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ImageCornerRadius));
        public static readonly StyledProperty<Color> ImageTintProperty = AvaloniaProperty.Register<ImageGrid, Color>(nameof(ImageTint), Colors.White);
        public static readonly StyledProperty<Color?> ImageTintEndProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(ImageTintEnd));
        public static readonly StyledProperty<Orientation> ImageTintDirectionProperty = AvaloniaProperty.Register<ImageGrid, Orientation>(nameof(ImageTintDirection));
        public static readonly StyledProperty<Color?> ImageSelectedTintProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(ImageSelectedTint));
        public static readonly StyledProperty<double> ImageSaturationProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ImageSaturation), 1);
        public static readonly StyledProperty<Color?> ItemBackgroundProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(ItemBackground));
        public static readonly StyledProperty<string?> ItemBackgroundImageProperty = AvaloniaProperty.Register<ImageGrid, string?>(nameof(ItemBackgroundImage));
        public static readonly StyledProperty<double> ItemBackgroundRelativeScaleProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ItemBackgroundRelativeScale), 1);
        public static readonly StyledProperty<double> ItemBackgroundCornerRadiusProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ItemBackgroundCornerRadius));
        public static readonly StyledProperty<Color?> SelectorColorProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(SelectorColor));
        public static readonly StyledProperty<string?> SelectorImageProperty = AvaloniaProperty.Register<ImageGrid, string?>(nameof(SelectorImage));
        public static readonly StyledProperty<double> SelectorRelativeScaleProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(SelectorRelativeScale), 1);
        public static readonly StyledProperty<double> SelectorCornerRadiusProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(SelectorCornerRadius));
        public static readonly StyledProperty<GridSelectorLayer> SelectorLayerProperty = AvaloniaProperty.Register<ImageGrid, GridSelectorLayer>(nameof(SelectorLayer), GridSelectorLayer.Top);
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<ImageGrid, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(FontSize), 24);
        public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(LineSpacing), 1.5);
        public static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<ImageGrid, Color>(nameof(TextColor), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color?> TextSelectedColorProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(TextSelectedColor));
        public static readonly StyledProperty<Color> TextBackgroundProperty = AvaloniaProperty.Register<ImageGrid, Color>(nameof(TextBackground), Colors.Transparent);
        public static readonly StyledProperty<Color?> TextSelectedBackgroundProperty = AvaloniaProperty.Register<ImageGrid, Color?>(nameof(TextSelectedBackground));
        public static readonly StyledProperty<double> TextBackgroundCornerRadiusProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(TextBackgroundCornerRadius));
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<ImageGrid, LetterCase>(nameof(LetterCase));
        public static readonly StyledProperty<double> ScrollRowProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(ScrollRow), double.NaN);
        public static readonly StyledProperty<int> FocusFromProperty = AvaloniaProperty.Register<ImageGrid, int>(nameof(FocusFrom), -1);
        public static readonly StyledProperty<double> FocusProgressProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(FocusProgress), 1);
        public static readonly StyledProperty<double> FocusFromLevelProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(FocusFromLevel), 1);
        public static readonly StyledProperty<double> FocusToLevelProperty = AvaloniaProperty.Register<ImageGrid, double>(nameof(FocusToLevel));

        private readonly List<(int Index, Control Child)> _shown = new();
        private bool _dirty = true;
        private (int First, int Last, int Selected, int From, bool Picked) _key = (-1, -1, -1, -1, false);

        static ImageGrid()
        {
            AffectsMeasure<ImageGrid>(ItemsProperty, SelectedIndexProperty, ItemSizeProperty, ItemSpacingProperty, ItemScaleProperty, ScaleInwardsProperty, FractionalRowsProperty,
                UnfocusedItemSaturationProperty, UnfocusedItemDimmingProperty, ImageFitProperty, ImageCropPositionProperty, ImageInterpolationProperty, ImageRelativeScaleProperty,
                ImageCornerRadiusProperty, ImageTintProperty, ImageTintEndProperty, ImageTintDirectionProperty, ImageSelectedTintProperty, ImageSaturationProperty, ItemBackgroundProperty,
                ItemBackgroundImageProperty, ItemBackgroundRelativeScaleProperty, ItemBackgroundCornerRadiusProperty, SelectorColorProperty, SelectorImageProperty,
                SelectorRelativeScaleProperty, SelectorCornerRadiusProperty, SelectorLayerProperty, FontPathProperty, FontSizeProperty, LineSpacingProperty, TextColorProperty,
                TextSelectedColorProperty, TextBackgroundProperty, TextSelectedBackgroundProperty, TextBackgroundCornerRadiusProperty, LetterCaseProperty);
            AffectsArrange<ImageGrid>(UnfocusedItemOpacityProperty, ScrollRowProperty, FocusFromProperty, FocusProgressProperty, FocusFromLevelProperty, FocusToLevelProperty);
        }

        /// <summary>The items, in order, laid out row by row.</summary>
        public IReadOnlyList<CarouselItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        /// <summary>The selected item.</summary>
        public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

        /// <summary>An unselected item's size in pixels, 120 by 160 by default.</summary>
        public Size ItemSize { get => GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }

        /// <summary>The space between neighbouring unscaled items in pixels; null, the default, is half an item's growth on each axis, so a scaled item never overlaps.</summary>
        public Size? ItemSpacing { get => GetValue(ItemSpacingProperty); set => SetValue(ItemSpacingProperty, value); }

        /// <summary>The selected item's scale, 1.05 by default.</summary>
        public double ItemScale { get => GetValue(ItemScaleProperty); set => SetValue(ItemScaleProperty, value); }

        /// <summary>Whether items in the outer columns and rows scale towards the middle, keeping their outer edge, so the grid can fill its box; false by default.</summary>
        public bool ScaleInwards { get => GetValue(ScaleInwardsProperty); set => SetValue(ScaleInwardsProperty, value); }

        /// <summary>Whether a row cut by the bottom is drawn in part; false by default, when only whole rows are drawn.</summary>
        public bool FractionalRows { get => GetValue(FractionalRowsProperty); set => SetValue(FractionalRowsProperty, value); }

        /// <summary>The opacity of every item but the selected one, 1 by default.</summary>
        public double UnfocusedItemOpacity { get => GetValue(UnfocusedItemOpacityProperty); set => SetValue(UnfocusedItemOpacityProperty, value); }

        /// <summary>A further saturation for every item but the selected one, 1 by default.</summary>
        public double UnfocusedItemSaturation { get => GetValue(UnfocusedItemSaturationProperty); set => SetValue(UnfocusedItemSaturationProperty, value); }

        /// <summary>A brightness multiplier for every item but the selected one, 1 by default.</summary>
        public double UnfocusedItemDimming { get => GetValue(UnfocusedItemDimmingProperty); set => SetValue(UnfocusedItemDimmingProperty, value); }

        /// <summary>How each image fills its part of the item. Contain by default.</summary>
        public ImageFit ImageFit { get => GetValue(ImageFitProperty); set => SetValue(ImageFitProperty, value); }

        /// <summary>For Cover, the point of the image kept in view, (0.5, 0.5) by default.</summary>
        public Point ImageCropPosition { get => GetValue(ImageCropPositionProperty); set => SetValue(ImageCropPositionProperty, value); }

        /// <summary>How images are sampled when scaled. HighQuality by default.</summary>
        public BitmapInterpolationMode ImageInterpolation { get => GetValue(ImageInterpolationProperty); set => SetValue(ImageInterpolationProperty, value); }

        /// <summary>The image's box as a fraction of the item's, centred, 1 by default.</summary>
        public double ImageRelativeScale { get => GetValue(ImageRelativeScaleProperty); set => SetValue(ImageRelativeScaleProperty, value); }

        /// <summary>The images' corner radius in pixels, 0 by default.</summary>
        public double ImageCornerRadius { get => GetValue(ImageCornerRadiusProperty); set => SetValue(ImageCornerRadiusProperty, value); }

        /// <summary>The tint of every image, white by default.</summary>
        public Color ImageTint { get => GetValue(ImageTintProperty); set => SetValue(ImageTintProperty, value); }

        /// <summary>When set, the tint runs from ImageTint to this colour. Null by default.</summary>
        public Color? ImageTintEnd { get => GetValue(ImageTintEndProperty); set => SetValue(ImageTintEndProperty, value); }

        /// <summary>The direction a two-colour tint runs in. Horizontal by default.</summary>
        public Orientation ImageTintDirection { get => GetValue(ImageTintDirectionProperty); set => SetValue(ImageTintDirectionProperty, value); }

        /// <summary>The selected image's tint; null, the default, uses ImageTint.</summary>
        public Color? ImageSelectedTint { get => GetValue(ImageSelectedTintProperty); set => SetValue(ImageSelectedTintProperty, value); }

        /// <summary>The saturation of every image, 1 by default.</summary>
        public double ImageSaturation { get => GetValue(ImageSaturationProperty); set => SetValue(ImageSaturationProperty, value); }

        /// <summary>A rectangle behind each item, or the colour its background image is multiplied by; null, the default, draws none.</summary>
        public Color? ItemBackground { get => GetValue(ItemBackgroundProperty); set => SetValue(ItemBackgroundProperty, value); }

        /// <summary>An image stretched behind each item. Null by default.</summary>
        public string? ItemBackgroundImage { get => GetValue(ItemBackgroundImageProperty); set => SetValue(ItemBackgroundImageProperty, value); }

        /// <summary>The background's box as a fraction of the item's, centred, 1 by default.</summary>
        public double ItemBackgroundRelativeScale { get => GetValue(ItemBackgroundRelativeScaleProperty); set => SetValue(ItemBackgroundRelativeScaleProperty, value); }

        /// <summary>The background's corner radius in pixels, 0 by default.</summary>
        public double ItemBackgroundCornerRadius { get => GetValue(ItemBackgroundCornerRadiusProperty); set => SetValue(ItemBackgroundCornerRadiusProperty, value); }

        /// <summary>A rectangle marking the selected item, or the colour its selector image is multiplied by; null, the default, draws none.</summary>
        public Color? SelectorColor { get => GetValue(SelectorColorProperty); set => SetValue(SelectorColorProperty, value); }

        /// <summary>An image stretched over the selected item as its selector. Null by default.</summary>
        public string? SelectorImage { get => GetValue(SelectorImageProperty); set => SetValue(SelectorImageProperty, value); }

        /// <summary>The selector's box as a fraction of the item's, centred, 1 by default.</summary>
        public double SelectorRelativeScale { get => GetValue(SelectorRelativeScaleProperty); set => SetValue(SelectorRelativeScaleProperty, value); }

        /// <summary>The selector's corner radius in pixels, 0 by default.</summary>
        public double SelectorCornerRadius { get => GetValue(SelectorCornerRadiusProperty); set => SetValue(SelectorCornerRadiusProperty, value); }

        /// <summary>Where the selector sits against the background and the picture. Top by default.</summary>
        public GridSelectorLayer SelectorLayer { get => GetValue(SelectorLayerProperty); set => SetValue(SelectorLayerProperty, value); }

        /// <summary>The font file for items shown as text.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size of items shown as text, 24 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>The line pitch of items shown as text, as a multiple of the font size, 1.5 by default.</summary>
        public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

        /// <summary>The colour of items shown as text, the palette's text colour by default.</summary>
        public Color TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

        /// <summary>The selected item's text colour; null, the default, uses TextColor.</summary>
        public Color? TextSelectedColor { get => GetValue(TextSelectedColorProperty); set => SetValue(TextSelectedColorProperty, value); }

        /// <summary>The fill of an item shown as text. Transparent by default.</summary>
        public Color TextBackground { get => GetValue(TextBackgroundProperty); set => SetValue(TextBackgroundProperty, value); }

        /// <summary>The selected item's text fill; null, the default, uses TextBackground.</summary>
        public Color? TextSelectedBackground { get => GetValue(TextSelectedBackgroundProperty); set => SetValue(TextSelectedBackgroundProperty, value); }

        /// <summary>The corner radius of a text item's fill in pixels, 0 by default.</summary>
        public double TextBackgroundCornerRadius { get => GetValue(TextBackgroundCornerRadiusProperty); set => SetValue(TextBackgroundCornerRadiusProperty, value); }

        /// <summary>The casing of items shown as text. None by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>Rows scrolled off the top, fractional while the rows slide; NaN, the default, is where the selection settles. A host moving the rows sets it from its own clock.</summary>
        public double ScrollRow { get => GetValue(ScrollRowProperty); set => SetValue(ScrollRowProperty, value); }

        /// <summary>The item the selection is moving from, or -1, the default, for none.</summary>
        public int FocusFrom { get => GetValue(FocusFromProperty); set => SetValue(FocusFromProperty, value); }

        /// <summary>How far the selection has moved from FocusFrom to SelectedIndex, 0 to 1; 1, the default, is settled.</summary>
        public double FocusProgress { get => GetValue(FocusProgressProperty); set => SetValue(FocusProgressProperty, value); }

        /// <summary>FocusFrom's focus when the move began, 1 by default: it falls from here to 0.</summary>
        public double FocusFromLevel { get => GetValue(FocusFromLevelProperty); set => SetValue(FocusFromLevelProperty, value); }

        /// <summary>SelectedIndex's focus when the move began, 0 by default: it rises from here to 1.</summary>
        public double FocusToLevel { get => GetValue(FocusToLevelProperty); set => SetValue(FocusToLevelProperty, value); }

        /// <summary>The layout at a size: columns, rows, where the rows are cut and where they scroll to.</summary>
        /// <param name="bounds">The grid's size.</param>
        /// <returns>The layout at that size.</returns>
        public GridGeometry GeometryFor(Size bounds) =>
            GridGeometry.Of(bounds, ItemSize, ItemSpacing, ItemScale, ScaleInwards, FractionalRows, Items?.Count ?? 0);

        /// <summary>The rows scrolled off the top as drawn: ScrollRow, or where the selection settles while it is NaN.</summary>
        public double DrawnScrollRow => ScrollAt(Bounds.Size);

        // During measure and arrange Bounds is the old size, so the scroll is asked for at the size being laid out.
        private double ScrollAt(Size size) => double.IsNaN(ScrollRow) || double.IsInfinity(ScrollRow) ? GeometryFor(size).ScrollFor(SelectedIndex) : ScrollRow;

        /// <summary>The controls standing for the items now drawn, with each item's index, the selected one last.</summary>
        public IReadOnlyList<(int Index, Control Child)> Shown => _shown;

        // An item's focus now: 1 when selected and settled, 0 when unfocused, between while the selection moves.
        private double Focus(int index)
        {
            double p = Math.Clamp(FocusProgress, 0, 1);
            if (index == SelectedIndex) return FocusFrom == SelectedIndex ? 1 : FocusToLevel + (1 - FocusToLevel) * p;
            return index == FocusFrom ? FocusFromLevel * (1 - p) : 0;
        }

        // The items whose rows reach into the drawn height, the selection and the item it moves from always among them.
        private (int First, int Last) Range(GridGeometry g, double scroll)
        {
            int count = Items?.Count ?? 0;
            if (count == 0) return (0, -1);
            int firstRow = Math.Max(0, (int)Math.Floor(scroll - 0.5));
            int lastRow = (int)Math.Ceiling(scroll + g.ClipHeight / Math.Max(1, g.Pitch.Height) + 0.5);
            return (firstRow * g.Columns, Math.Min(count - 1, (lastRow + 1) * g.Columns - 1));
        }

        private Control Cell(int index, bool picked)
        {
            CarouselItem item = Items![index];
            bool selected = picked;
            double dim = selected ? 1 : Math.Clamp(UnfocusedItemDimming, 0, 1);
            var cell = new GridCell { Tag = index };
            Control? selector = SelectorColor is null && string.IsNullOrEmpty(SelectorImage) || index != SelectedIndex ? null : Layer(SelectorImage, SelectorColor, SelectorRelativeScale, SelectorCornerRadius);
            if (selector is not null && SelectorLayer == GridSelectorLayer.Bottom) cell.Children.Add(selector);
            if (ItemBackground is not null || !string.IsNullOrEmpty(ItemBackgroundImage))
                cell.Children.Add(Layer(ItemBackgroundImage, ItemBackground, ItemBackgroundRelativeScale, ItemBackgroundCornerRadius));
            if (selector is not null && SelectorLayer == GridSelectorLayer.Middle) cell.Children.Add(selector);
            if (PictureFiles.Exists(item.ImagePath))
            {
                Color tint = selected ? ImageSelectedTint ?? ImageTint : ImageTint;
                double scale = Math.Clamp(ImageRelativeScale, 0, 1);
                cell.Children.Add(new FittedImage
                {
                    Source = item.ImagePath, Fit = ImageFit, CropPosition = ImageCropPosition, Interpolation = ImageInterpolation,
                    Tint = Dimmed(tint, dim), TintEnd = ImageTintEnd is { } end ? Dimmed(end, dim) : null, TintDirection = ImageTintDirection,
                    Saturation = Math.Clamp(ImageSaturation, 0, 1) * (selected ? 1 : Math.Clamp(UnfocusedItemSaturation, 0, 1)),
                    CornerRadius = ImageCornerRadius, Tag = scale,
                });
            }
            else
            {
                Color text = selected ? TextSelectedColor ?? TextColor : TextColor, fill = selected ? TextSelectedBackground ?? TextBackground : TextBackground;
                if (fill.A > 0) cell.Children.Add(new Border { Background = new SolidColorBrush(Dimmed(fill, dim)), CornerRadius = new CornerRadius(TextBackgroundCornerRadius) });
                cell.Children.Add(new FontText
                {
                    Text = item.Text, FontPath = FontPath, FontSize = FontSize, LineSpacing = LineSpacing, LetterCase = LetterCase, Wrap = true,
                    Foreground = new SolidColorBrush(Dimmed(text, dim)), TextAlignment = TextAlignment.Center, TextVerticalAlignment = VerticalAlignment.Center,
                });
            }
            if (selector is not null && SelectorLayer == GridSelectorLayer.Top) cell.Children.Add(selector);
            return cell;
        }

        private static Color Dimmed(Color c, double dim) => Color.FromArgb(c.A, (byte)Math.Round(c.R * dim), (byte)Math.Round(c.G * dim), (byte)Math.Round(c.B * dim));

        // A background or selector: an image stretched and tinted, or a rounded rectangle of the colour; its relative scale is kept in Tag for arrange.
        private static Control Layer(string? image, Color? colour, double relative, double corner)
        {
            Control layer = !string.IsNullOrEmpty(image)
                ? new FittedImage { Source = image, Fit = ImageFit.Fill, Tint = colour ?? Colors.White, CornerRadius = corner }
                : new Border { Background = new SolidColorBrush(colour ?? Colors.Transparent), CornerRadius = new CornerRadius(corner) };
            layer.Tag = Math.Clamp(relative, 0, 1);
            return layer;
        }

        private void Rebuild((int First, int Last, int Selected, int From, bool Picked) key)
        {
            foreach ((_, Control old) in _shown)
            {
                VisualChildren.Remove(old);
                LogicalChildren.Remove(old);
            }
            _shown.Clear();
            _key = key;
            if (Items is not { Count: > 0 } items) return;
            var order = Enumerable.Range(key.First, Math.Max(0, key.Last - key.First + 1)).Where(i => i != key.Selected && i != key.From).ToList();
            if (key.From >= 0 && key.From < items.Count && key.From != key.Selected) order.Add(key.From);
            if (key.Selected >= 0 && key.Selected < items.Count) order.Add(key.Selected);
            foreach (int index in order)
            {
                Control cell = Cell(index, index == key.Selected ? key.Picked : index == key.From && !key.Picked);
                _shown.Add((index, cell));
                LogicalChildren.Add(cell);
                VisualChildren.Add(cell);
            }
        }

        // What decides the children: the rows in reach, the two items in motion, and which of them wears the selected look (it changes hands half-way).
        private (int First, int Last, int Selected, int From, bool Picked) Key(Size size)
        {
            GridGeometry g = GeometryFor(size);
            (int first, int last) = Range(g, ScrollAt(size));
            return (first, last, SelectedIndex, FocusFrom, Focus(SelectedIndex) >= 0.5 || FocusFrom < 0);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ScrollRowProperty || change.Property == FocusProgressProperty || change.Property == FocusFromProperty || change.Property == FocusToLevelProperty)
            {
                if (Key(Bounds.Size) != _key) InvalidateMeasure();
                return;
            }
            if (change.Property != BoundsProperty && change.Property.OwnerType == typeof(ImageGrid)) _dirty = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
            var key = Key(size);
            if (_dirty || key != _key) Rebuild(key);
            _dirty = false;
            GridGeometry g = GeometryFor(size);
            foreach ((_, Control cell) in _shown) cell.Measure(g.Item);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            GridGeometry g = GeometryFor(finalSize);
            double scroll = ScrollAt(finalSize), unfocused = Math.Clamp(UnfocusedItemOpacity, 0, 1);
            Clip = new RectangleGeometry(new Rect(0, 0, finalSize.Width, g.ClipHeight));
            foreach ((int index, Control cell) in _shown)
            {
                cell.Arrange(g.CellRect(index, scroll));
                double focus = Focus(index), scale = 1 + (ItemScale - 1) * focus;
                cell.Opacity = unfocused + (1 - unfocused) * focus;
                cell.RenderTransformOrigin = g.Anchor(index, scroll);
                cell.RenderTransform = Math.Abs(scale - 1) > 1e-9 ? new ScaleTransform(scale, scale) : null;
            }
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.List, status: () => Items is { } items && SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex].Text : null);
    }

    // One item's layers, each in a box its relative scale gives, centred; a contained picture keeps its own shape - see docs/LunaP.md §104.2.
    internal sealed class GridCell : Panel
    {
        private static Rect Part(Control part, Size item)
        {
            double s = part.Tag is double relative ? relative : 1;
            Rect box = new((item.Width - item.Width * s) / 2, (item.Height - item.Height * s) / 2, item.Width * s, item.Height * s);
            return part is FittedImage { Fit: ImageFit.Contain } image && image.IntrinsicSize is { Width: > 0, Height: > 0 } own ? FittedImage.Contain(box, own) : box;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (Control part in Children) part.Measure(Part(part, availableSize).Size);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (Control part in Children) part.Arrange(Part(part, finalSize));
            return finalSize;
        }
    }
}
