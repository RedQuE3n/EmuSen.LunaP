using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>One item of an ImageCarousel: an image file, and the text shown in its place when there is no image.</summary>
    /// <param name="ImagePath">The image file; a missing or null one shows Text instead.</param>
    /// <param name="Text">The words shown when there is no image, and read out by a screen reader.</param>
    public sealed record CarouselItem(string? ImagePath, string? Text = null);

    // Items at equal spacing about a centred selection, the others faded, desaturated and dimmed, at rest - see docs/LunaP.md §100.2.
    /// <summary>A horizontal or vertical row of images, or of text where an item has none, spaced evenly about the selected item, which is centred and scaled; the others are faded, desaturated and dimmed. It shows the row at rest and does not animate.</summary>
    public class ImageCarousel : Control
    {
        public static readonly StyledProperty<IReadOnlyList<CarouselItem>?> ItemsProperty = AvaloniaProperty.Register<ImageCarousel, IReadOnlyList<CarouselItem>?>(nameof(Items));
        public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<ImageCarousel, int>(nameof(SelectedIndex));
        public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ImageCarousel, Orientation>(nameof(Orientation));
        public static readonly StyledProperty<double> MaxItemCountProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(MaxItemCount), 3);
        public static readonly StyledProperty<Size> ItemSizeProperty = AvaloniaProperty.Register<ImageCarousel, Size>(nameof(ItemSize));
        public static readonly StyledProperty<double> ItemScaleProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(ItemScale), 1.2);
        public static readonly StyledProperty<bool> WrapsProperty = AvaloniaProperty.Register<ImageCarousel, bool>(nameof(Wraps), true);
        public static readonly StyledProperty<double> UnfocusedItemOpacityProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(UnfocusedItemOpacity), 0.5);
        public static readonly StyledProperty<double> UnfocusedItemSaturationProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(UnfocusedItemSaturation), 1);
        public static readonly StyledProperty<double> UnfocusedItemDimmingProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(UnfocusedItemDimming), 1);
        public static readonly StyledProperty<Color> ImageTintProperty = AvaloniaProperty.Register<ImageCarousel, Color>(nameof(ImageTint), Colors.White);
        public static readonly StyledProperty<Color?> ImageSelectedTintProperty = AvaloniaProperty.Register<ImageCarousel, Color?>(nameof(ImageSelectedTint));
        public static readonly StyledProperty<double> ImageSaturationProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(ImageSaturation), 1);
        public static readonly StyledProperty<ImageFit> ImageFitProperty = AvaloniaProperty.Register<ImageCarousel, ImageFit>(nameof(ImageFit), ImageFit.Contain);
        public static readonly StyledProperty<VerticalAlignment> ItemVerticalAlignmentProperty = AvaloniaProperty.Register<ImageCarousel, VerticalAlignment>(nameof(ItemVerticalAlignment), VerticalAlignment.Center);
        public static readonly StyledProperty<HorizontalAlignment> ItemHorizontalAlignmentProperty = AvaloniaProperty.Register<ImageCarousel, HorizontalAlignment>(nameof(ItemHorizontalAlignment), HorizontalAlignment.Center);
        public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<ImageCarousel, IBrush?>(nameof(Background));
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<ImageCarousel, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<ImageCarousel, double>(nameof(FontSize), 24);
        public static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<ImageCarousel, Color>(nameof(TextColor), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color> TextBackgroundProperty = AvaloniaProperty.Register<ImageCarousel, Color>(nameof(TextBackground), Colors.Transparent);
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<ImageCarousel, LetterCase>(nameof(LetterCase));

        private readonly List<(int Offset, Control Child)> _shown = new();
        private bool _dirty = true;

        static ImageCarousel()
        {
            AffectsMeasure<ImageCarousel>(ItemsProperty, SelectedIndexProperty, OrientationProperty, MaxItemCountProperty, ItemSizeProperty, ItemScaleProperty, WrapsProperty,
                UnfocusedItemOpacityProperty, UnfocusedItemSaturationProperty, UnfocusedItemDimmingProperty, ImageTintProperty, ImageSelectedTintProperty, ImageSaturationProperty,
                ImageFitProperty, ItemVerticalAlignmentProperty, ItemHorizontalAlignmentProperty, FontPathProperty, FontSizeProperty, TextColorProperty, TextBackgroundProperty, LetterCaseProperty);
            AffectsRender<ImageCarousel>(BackgroundProperty);
        }

        /// <summary>The items, in order.</summary>
        public IReadOnlyList<CarouselItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        /// <summary>The item at the centre.</summary>
        public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

        /// <summary>The axis the items run along. Horizontal by default.</summary>
        public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

        /// <summary>How many item spacings the control's length holds: the distance between item centres is the length divided by this. 3 by default.</summary>
        public double MaxItemCount { get => GetValue(MaxItemCountProperty); set => SetValue(MaxItemCountProperty, value); }

        /// <summary>Each item's box in pixels; an axis of 0 is one spacing along the axis, or the whole of the cross axis.</summary>
        public Size ItemSize { get => GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }

        /// <summary>The selected item's scale about its centre, 1.2 by default.</summary>
        public double ItemScale { get => GetValue(ItemScaleProperty); set => SetValue(ItemScaleProperty, value); }

        /// <summary>Whether the row continues past the last item with the first, when there are more items than fit. True by default.</summary>
        public bool Wraps { get => GetValue(WrapsProperty); set => SetValue(WrapsProperty, value); }

        /// <summary>The opacity of every item but the selected one, 0.5 by default.</summary>
        public double UnfocusedItemOpacity { get => GetValue(UnfocusedItemOpacityProperty); set => SetValue(UnfocusedItemOpacityProperty, value); }

        /// <summary>A further saturation for every item but the selected one, 1 by default.</summary>
        public double UnfocusedItemSaturation { get => GetValue(UnfocusedItemSaturationProperty); set => SetValue(UnfocusedItemSaturationProperty, value); }

        /// <summary>A brightness multiplier for every item but the selected one, 1 by default.</summary>
        public double UnfocusedItemDimming { get => GetValue(UnfocusedItemDimmingProperty); set => SetValue(UnfocusedItemDimmingProperty, value); }

        /// <summary>The tint of every image, white by default.</summary>
        public Color ImageTint { get => GetValue(ImageTintProperty); set => SetValue(ImageTintProperty, value); }

        /// <summary>The selected image's tint; null uses ImageTint.</summary>
        public Color? ImageSelectedTint { get => GetValue(ImageSelectedTintProperty); set => SetValue(ImageSelectedTintProperty, value); }

        /// <summary>The saturation of every image, 1 by default.</summary>
        public double ImageSaturation { get => GetValue(ImageSaturationProperty); set => SetValue(ImageSaturationProperty, value); }

        /// <summary>How each image fills its item box. Contain by default.</summary>
        public ImageFit ImageFit { get => GetValue(ImageFitProperty); set => SetValue(ImageFitProperty, value); }

        /// <summary>For a horizontal row, where an item box sits across the height. Center by default.</summary>
        public VerticalAlignment ItemVerticalAlignment { get => GetValue(ItemVerticalAlignmentProperty); set => SetValue(ItemVerticalAlignmentProperty, value); }

        /// <summary>For a vertical row, where an item box sits across the width. Center by default.</summary>
        public HorizontalAlignment ItemHorizontalAlignment { get => GetValue(ItemHorizontalAlignmentProperty); set => SetValue(ItemHorizontalAlignmentProperty, value); }

        /// <summary>A fill behind the row. None by default.</summary>
        public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        /// <summary>The font file for items shown as text.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size of items shown as text, 24 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>The colour of items shown as text, the palette's text colour by default.</summary>
        public Color TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

        /// <summary>A fill behind items shown as text. Transparent by default.</summary>
        public Color TextBackground { get => GetValue(TextBackgroundProperty); set => SetValue(TextBackgroundProperty, value); }

        /// <summary>The casing of items shown as text. None by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The distance between neighbouring item centres, the length divided by MaxItemCount.</summary>
        public double Spacing => (Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height) / Math.Max(0.01, MaxItemCount);

        /// <summary>The controls standing for the items now shown, with each one's offset from the selection, selected last.</summary>
        public IReadOnlyList<(int Offset, Control Child)> Shown => _shown;

        /// <summary>The unscaled box of the item at an offset from the selection, in the control's coordinates.</summary>
        /// <param name="offset">Items from the selection: negative before it, positive after.</param>
        /// <param name="bounds">The control's size.</param>
        /// <returns>The item's box before the selected item's scale and before an image is fitted inside it.</returns>
        public Rect ItemRect(int offset, Size bounds)
        {
            bool across = Orientation == Orientation.Horizontal;
            double length = across ? bounds.Width : bounds.Height, cross = across ? bounds.Height : bounds.Width;
            double spacing = length / Math.Max(0.01, MaxItemCount);
            double w = ItemSize.Width > 0 ? ItemSize.Width : across ? spacing : cross;
            double h = ItemSize.Height > 0 ? ItemSize.Height : across ? cross : spacing;
            double centre = length / 2 + offset * spacing;
            if (across)
            {
                double y = ItemVerticalAlignment switch { VerticalAlignment.Top => 0, VerticalAlignment.Bottom => cross - h, _ => (cross - h) / 2 };
                return new Rect(centre - w / 2, y, w, h);
            }

            double x = ItemHorizontalAlignment switch { HorizontalAlignment.Left => 0, HorizontalAlignment.Right => cross - w, _ => (cross - w) / 2 };
            return new Rect(x, centre - h / 2, w, h);
        }

        // The offsets drawn, and which item each shows: a wrapped row repeats items, an unwrapped one stops at its ends.
        private IEnumerable<(int Offset, int Index)> Slots()
        {
            int count = Items?.Count ?? 0;
            if (count == 0) yield break;
            int selected = Math.Clamp(SelectedIndex, 0, count - 1);
            int reach = (int)Math.Ceiling(Math.Max(1, MaxItemCount) / 2) + 1;
            bool wraps = Wraps && count > 1;
            for (int k = -reach; k <= reach; k++)
            {
                int index = selected + k;
                if (wraps) index = ((index % count) + count) % count;
                else if (index < 0 || index >= count) continue;
                yield return (k, index);
            }
        }

        private void Rebuild()
        {
            foreach ((_, Control old) in _shown)
            {
                VisualChildren.Remove(old);
                LogicalChildren.Remove(old);
            }

            _shown.Clear();
            IReadOnlyList<CarouselItem> items = Items ?? Array.Empty<CarouselItem>();
            foreach ((int offset, int index) in Slots().OrderByDescending(s => Math.Abs(s.Offset)))
            {
                CarouselItem item = items[index];
                bool selected = offset == 0;
                double dim = selected ? 1 : Math.Clamp(UnfocusedItemDimming, 0, 1);
                Color tint = selected ? ImageSelectedTint ?? ImageTint : ImageTint;
                tint = Color.FromArgb(tint.A, (byte)(tint.R * dim), (byte)(tint.G * dim), (byte)(tint.B * dim));
                Control child = PictureFiles.Exists(item.ImagePath)
                    ? new FittedImage
                    {
                        Source = item.ImagePath, Fit = ImageFit, Tint = tint,
                        Saturation = Math.Clamp(ImageSaturation, 0, 1) * (selected ? 1 : Math.Clamp(UnfocusedItemSaturation, 0, 1)),
                    }
                    : new FontText
                    {
                        Text = item.Text, FontPath = FontPath, FontSize = FontSize, LetterCase = LetterCase, Wrap = true,
                        Foreground = new SolidColorBrush(Color.FromArgb(TextColor.A, (byte)(TextColor.R * dim), (byte)(TextColor.G * dim), (byte)(TextColor.B * dim))),
                        Background = TextBackground.A > 0 ? new SolidColorBrush(TextBackground) : null,
                        TextAlignment = TextAlignment.Center, TextVerticalAlignment = VerticalAlignment.Center,
                    };
                child.Opacity = selected ? 1 : Math.Clamp(UnfocusedItemOpacity, 0, 1);
                child.Tag = index;
                _shown.Add((offset, child));
                LogicalChildren.Add(child);
                VisualChildren.Add(child);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != BoundsProperty && change.Property.OwnerType == typeof(ImageCarousel)) _dirty = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_dirty) Rebuild();
            _dirty = false;
            var size = new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
            foreach ((int offset, Control child) in _shown) child.Measure(ItemRect(offset, size).Size);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach ((int offset, Control child) in _shown)
            {
                Rect box = ItemRect(offset, finalSize);
                if (child is FittedImage { Fit: ImageFit.Contain } image && image.IntrinsicSize is { Width: > 0, Height: > 0 } own)
                {
                    Rect fitted = FittedImage.Contain(new Rect(box.Size), own);
                    double x = Orientation == Orientation.Horizontal ? box.X + (box.Width - fitted.Width) / 2 : box.X + ItemHorizontalAlignment switch { HorizontalAlignment.Left => 0, HorizontalAlignment.Right => box.Width - fitted.Width, _ => (box.Width - fitted.Width) / 2 };
                    double y = Orientation == Orientation.Vertical ? box.Y + (box.Height - fitted.Height) / 2 : box.Y + ItemVerticalAlignment switch { VerticalAlignment.Top => 0, VerticalAlignment.Bottom => box.Height - fitted.Height, _ => (box.Height - fitted.Height) / 2 };
                    box = new Rect(x, y, fitted.Width, fitted.Height);
                }

                child.Arrange(box);
                child.RenderTransformOrigin = RelativePoint.Center;
                child.RenderTransform = offset == 0 && ItemScale != 1 ? new ScaleTransform(ItemScale, ItemScale) : null;
            }

            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            if (Background is { } background) context.FillRectangle(background, new Rect(Bounds.Size));
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.List, status: () => Items is { } items && SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex].Text : null);
    }
}
