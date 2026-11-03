using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>One item a library holds, as a cover: its picture, or a generated card with its title and a mark, in its own box shape.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Title">The item's name, drawn on a generated card and read by a screen reader.</param>
    public sealed record CoverItem(string Key, string Title)
    {
        /// <summary>The cover; null draws the generated card.</summary>
        public IImage? Picture { get; init; }

        /// <summary>The cover's width over its height, the item's box shape; never stretched.</summary>
        public double Aspect { get; init; } = 0.75;

        /// <summary>A short name drawn in a ringed badge on a generated card, such as a console's.</summary>
        public string? Mark { get; init; }

        /// <summary>Whether a heart in the accent is drawn at the cell's top right.</summary>
        public bool IsFavourite { get; init; }

        /// <summary>A line of facts a screen shows for the focused item, such as its console and year; null for none.</summary>
        public string? Facts { get; init; }

        /// <summary>A line drawn under the cover on the desktop, muted; null for none.</summary>
        public string? Status { get; init; }

        /// <summary>A picture to show behind a screen while the item is focused, such as its fan art; null uses the cover.</summary>
        public IImage? Art { get; init; }
    }

    // A cover in its box shape standing on its cell's foot, or the generated card, with the ring, scale and shadow of a focused tile - see docs/LunaP.md §212.2.
    /// <summary>One cover in a cell: the picture or a generated card in the item's box shape, standing on the cell's foot; focused, it takes the ring, grows by the focus scale and takes the shadow.</summary>
    public class CoverCard : Panel
    {
        /// <summary>The smallest a generated card's title is set on the big screen, in units, so a small box keeps it legible.</summary>
        public const double MinimumTitle = 18;

        /// <summary>The generated card's content is designed at this width and height in each form, and shrunk to a smaller box.</summary>
        public const double BigReference = 180, DesktopReference = 100;

        private readonly Border _box = new() { ClipToBounds = true, Name = "CoverBox" };
        private readonly Border _ring = new() { IsHitTestVisible = false, IsVisible = false, Name = "CoverRing" };
        private CoverItem? _item;
        private bool _focused, _big = true;
        private double _cellWidth = 180, _cellHeight = 240;

        /// <summary>An empty cell on the big screen.</summary>
        public CoverCard()
        {
            ClipToBounds = false;
            Children.Add(_box);
            Children.Add(_ring);
            RenderTransformOrigin = RelativePoint.Center;
            Fill();
        }

        /// <summary>The item drawn; null draws nothing.</summary>
        public CoverItem? Item { get => _item; set { _item = value; Fill(); } }

        /// <summary>Whether the cell is in the big screen's form, with its radius, ring and type; false for the desktop's.</summary>
        public bool Big { get => _big; set { _big = value; Fill(); } }

        /// <summary>The cell's width; the cover is fitted inside it.</summary>
        public double CellWidth { get => _cellWidth; set { _cellWidth = value; Fill(); } }

        /// <summary>The cell's height; the cover stands on its foot.</summary>
        public double CellHeight { get => _cellHeight; set { _cellHeight = value; Fill(); } }

        /// <summary>Whether the cover has the focus: the ring drawn outside it, the focus scale and the shadow.</summary>
        public bool ShowsFocus { get => _focused; set { _focused = value; FillFocus(); } }

        /// <summary>The cover's box inside the cell, after fitting its shape.</summary>
        public Rect CoverBounds { get; private set; }

        private double Radius => _big ? 8 : 6;

        private void Fill()
        {
            Width = _cellWidth;
            Height = _cellHeight;
            Children.Clear();
            Children.Add(_box);
            Children.Add(_ring);
            if (_item is not { } item)
            {
                _box.IsVisible = false;
                return;
            }
            double aspect = double.IsFinite(item.Aspect) && item.Aspect > 0 ? item.Aspect : 0.75;
            double w = Math.Min(_cellWidth, _cellHeight * aspect), h = w / aspect;
            CoverBounds = new Rect((_cellWidth - w) / 2, _cellHeight - h, w, h);
            _box.IsVisible = true;
            _box.Width = w;
            _box.Height = h;
            _box.Margin = new Thickness(CoverBounds.X, CoverBounds.Y, 0, 0);
            _box.HorizontalAlignment = HorizontalAlignment.Left;
            _box.VerticalAlignment = VerticalAlignment.Top;
            _box.CornerRadius = new CornerRadius(Radius);
            _box[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            _box.Child = item.Picture is { } picture ? new Image { Source = picture, Stretch = Stretch.UniformToFill, IsHitTestVisible = false } : Generated(item, w, h);
            if (item.IsFavourite && PageLayout.Mark(PageMark.FilledHeart, "LunaAccentColor", _big ? 24 : 14) is { } heart)
            {
                heart.HorizontalAlignment = HorizontalAlignment.Right;
                heart.VerticalAlignment = VerticalAlignment.Top;
                heart.Margin = _big ? new Thickness(0, -2, 0, 0) : new Thickness(0, 4, 6, 0);
                heart.Name = "CoverHeart";
                Children.Add(heart);
            }
            AutomationProperties.SetName(this, item.IsFavourite ? $"{item.Title}, favourite" : item.Title);
            FillFocus();
        }

        // The badge with the mark at its full size over the title, which is designed at the reference width and shrunk to the box, so a short box keeps both whole and the mark legible.
        private Control Generated(CoverItem item, double w, double h)
        {
            double reference = _big ? BigReference : DesktopReference, pad = _big ? 8 : 4;
            LunaTypeScale type = LunaTokens.Type(_big ? LunaForm.BigScreen : LunaForm.Desktop);
            var stack = new StackPanel { Spacing = _big ? 8 : 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(pad) };
            double badgeSide = 0;
            if (item.Mark is { Length: > 0 } mark)
            {
                badgeSide = _big ? (h >= 150 ? 48 : 40) : 24;
                var badge = new Border { Name = "CoverMark", MinWidth = badgeSide, Height = badgeSide, CornerRadius = new CornerRadius(_big ? 8 : 4), BorderThickness = new Thickness(_big ? 2 : 1), HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(_big ? 4 : 2, 0) };
                badge[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaMuted");
                var word = new TextBlock { Text = mark, FontFamily = LunaFonts.Condensed, FontSize = _big ? type.Caption : type.Caption, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                word[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
                badge.Child = word;
                stack.Children.Add(badge);
            }
            double scale = Math.Min(1, Math.Min((w - 2 * pad) / (reference - 2 * pad), h / reference));
            var title = new TextBlock
            {
                Name = "CoverTitle", Text = item.Title, FontFamily = LunaFonts.Condensed, FontSize = Math.Max(_big ? MinimumTitle : type.Caption, (_big ? type.Title : type.Strong) * scale),
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.WordEllipsis, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Width = Math.Max(1, w - 2 * pad), MaxHeight = Math.Max(1, h - 2 * pad - badgeSide - stack.Spacing), IsHitTestVisible = false,
            };
            title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaTextStrong");
            stack.Children.Add(title);
            return stack;
        }

        private void FillFocus()
        {
            LunaFocus focus = LunaTokens.FocusMarks;
            double thick = _big ? focus.RingThickness : 2, offset = _big ? focus.RingOffset : 1;
            Rect box = CoverBounds;
            _ring.IsVisible = _focused && _item is not null;
            _ring.Width = box.Width + 2 * (thick + offset);
            _ring.Height = box.Height + 2 * (thick + offset);
            _ring.Margin = new Thickness(box.X - thick - offset, box.Y - thick - offset, 0, 0);
            _ring.HorizontalAlignment = HorizontalAlignment.Left;
            _ring.VerticalAlignment = VerticalAlignment.Top;
            _ring.BorderThickness = new Thickness(thick);
            _ring.CornerRadius = new CornerRadius(Radius + thick + offset);
            _ring[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaAccent");
            bool raised = _focused && _big;
            RenderTransformOrigin = new RelativePoint(box.Center.X / Math.Max(1, _cellWidth), box.Center.Y / Math.Max(1, _cellHeight), RelativeUnit.Relative);
            RenderTransform = raised ? new ScaleTransform(focus.TileScale, focus.TileScale) : null;
            _box.BoxShadow = raised ? new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 24, Color = Color.FromArgb(0x80, 0, 0, 0) }) : default;
            ZIndex = _focused ? 1 : 0;
        }
    }
}
