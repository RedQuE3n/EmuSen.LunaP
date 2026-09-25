using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // Icons packed into cells of a grid of lines, in order, aligned as a group - see docs/LunaP.md §101.2.
    /// <summary>A small set of icons, such as a game's favourite or completed marks, packed in order into lines of equal cells and aligned as a group.</summary>
    public class BadgeStrip : Control
    {
        public static readonly StyledProperty<IReadOnlyList<string>?> IconsProperty = AvaloniaProperty.Register<BadgeStrip, IReadOnlyList<string>?>(nameof(Icons));
        public static readonly StyledProperty<Orientation> DirectionProperty = AvaloniaProperty.Register<BadgeStrip, Orientation>(nameof(Direction));
        public static readonly StyledProperty<int> LinesProperty = AvaloniaProperty.Register<BadgeStrip, int>(nameof(Lines), 1);
        public static readonly StyledProperty<int> ItemsPerLineProperty = AvaloniaProperty.Register<BadgeStrip, int>(nameof(ItemsPerLine), 4);
        public static readonly StyledProperty<Size> ItemMarginProperty = AvaloniaProperty.Register<BadgeStrip, Size>(nameof(ItemMargin));
        public static readonly StyledProperty<HorizontalAlignment> ContentHorizontalAlignmentProperty = AvaloniaProperty.Register<BadgeStrip, HorizontalAlignment>(nameof(ContentHorizontalAlignment), HorizontalAlignment.Left);
        public static readonly StyledProperty<VerticalAlignment> ContentVerticalAlignmentProperty = AvaloniaProperty.Register<BadgeStrip, VerticalAlignment>(nameof(ContentVerticalAlignment), VerticalAlignment.Top);
        public static readonly StyledProperty<Color> TintProperty = AvaloniaProperty.Register<BadgeStrip, Color>(nameof(Tint), Colors.White);

        static BadgeStrip() => AffectsRender<BadgeStrip>(IconsProperty, DirectionProperty, LinesProperty, ItemsPerLineProperty, ItemMarginProperty,
            ContentHorizontalAlignmentProperty, ContentVerticalAlignmentProperty, TintProperty);

        /// <summary>The icon files to show, in order; only these are drawn.</summary>
        public IReadOnlyList<string>? Icons { get => GetValue(IconsProperty); set => SetValue(IconsProperty, value); }

        /// <summary>Horizontal fills each line left to right, a line being a row; Vertical fills columns top to bottom. Horizontal by default.</summary>
        public Orientation Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

        /// <summary>How many lines the box is divided into, 1 by default.</summary>
        public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

        /// <summary>How many cells each line holds, 4 by default.</summary>
        public int ItemsPerLine { get => GetValue(ItemsPerLineProperty); set => SetValue(ItemsPerLineProperty, value); }

        /// <summary>The gaps between cells across and down, in pixels.</summary>
        public Size ItemMargin { get => GetValue(ItemMarginProperty); set => SetValue(ItemMarginProperty, value); }

        /// <summary>Where the used cells sit across the box. Left by default.</summary>
        public HorizontalAlignment ContentHorizontalAlignment { get => GetValue(ContentHorizontalAlignmentProperty); set => SetValue(ContentHorizontalAlignmentProperty, value); }

        /// <summary>Where the used cells sit down the box. Top by default.</summary>
        public VerticalAlignment ContentVerticalAlignment { get => GetValue(ContentVerticalAlignmentProperty); set => SetValue(ContentVerticalAlignmentProperty, value); }

        /// <summary>A colour every icon is multiplied by. White by default.</summary>
        public Color Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

        /// <summary>The cell each shown icon is drawn in, in the control's coordinates.</summary>
        /// <param name="size">The control's size to lay the cells out in.</param>
        /// <returns>One rectangle per icon drawn, in order.</returns>
        public IReadOnlyList<Rect> Cells(Size size)
        {
            int count = Math.Min(Icons?.Count ?? 0, Math.Max(1, Lines) * Math.Max(1, ItemsPerLine));
            var cells = new List<Rect>(count);
            if (count == 0) return cells;
            bool rows = Direction == Orientation.Horizontal;
            int lines = Math.Max(1, Lines), per = Math.Max(1, ItemsPerLine);
            int columns = rows ? per : lines, rowCount = rows ? lines : per;
            double cw = (size.Width - (columns - 1) * ItemMargin.Width) / columns;
            double ch = (size.Height - (rowCount - 1) * ItemMargin.Height) / rowCount;
            int usedColumns = rows ? Math.Min(per, count) : (count + per - 1) / per;
            int usedRows = rows ? (count + per - 1) / per : Math.Min(per, count);
            double usedW = usedColumns * cw + (usedColumns - 1) * ItemMargin.Width, usedH = usedRows * ch + (usedRows - 1) * ItemMargin.Height;
            double x0 = ContentHorizontalAlignment switch { HorizontalAlignment.Center => (size.Width - usedW) / 2, HorizontalAlignment.Right => size.Width - usedW, _ => 0 };
            double y0 = ContentVerticalAlignment switch { VerticalAlignment.Center => (size.Height - usedH) / 2, VerticalAlignment.Bottom => size.Height - usedH, _ => 0 };
            for (int i = 0; i < count; i++)
            {
                int line = i / per, within = i % per;
                int col = rows ? within : line, row = rows ? line : within;
                cells.Add(new Rect(x0 + col * (cw + ItemMargin.Width), y0 + row * (ch + ItemMargin.Height), cw, ch));
            }

            return cells;
        }

        public override void Render(DrawingContext context)
        {
            IReadOnlyList<Rect> cells = Cells(Bounds.Size);
            for (int i = 0; i < cells.Count; i++) PictureFiles.Draw(context, this, Icons![i], cells[i], Tint);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Group);
    }
}
