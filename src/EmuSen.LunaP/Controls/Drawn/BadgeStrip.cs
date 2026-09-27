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
    /// <summary>One badge of a BadgeStrip: an image file when given, else the toolkit's drawing of its kind, with the controller or folder link drawn over it.</summary>
    /// <param name="Kind">Which badge this is, drawn by BadgeGlyph when no image file is given.</param>
    public sealed record BadgeEntry(BadgeKind Kind)
    {
        /// <summary>An image file drawn in place of the toolkit's drawing; null or missing draws the drawing.</summary>
        public string? IconPath { get; init; }

        /// <summary>The controller drawn over a Controller badge; null draws none.</summary>
        public ControllerShape? Controller { get; init; }

        /// <summary>An image file drawn in place of the controller's drawing.</summary>
        public string? ControllerIconPath { get; init; }

        /// <summary>Whether a folder link is drawn over a Folder badge.</summary>
        public bool Linked { get; init; }

        /// <summary>An image file drawn in place of the folder link's drawing.</summary>
        public string? LinkIconPath { get; init; }
    }

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
        public static readonly StyledProperty<IReadOnlyList<BadgeEntry>?> EntriesProperty = AvaloniaProperty.Register<BadgeStrip, IReadOnlyList<BadgeEntry>?>(nameof(Entries));
        public static readonly StyledProperty<Point> ControllerPositionProperty = AvaloniaProperty.Register<BadgeStrip, Point>(nameof(ControllerPosition), new Point(0.5, 0.5));
        public static readonly StyledProperty<double> ControllerSizeProperty = AvaloniaProperty.Register<BadgeStrip, double>(nameof(ControllerSize), 0.5);
        public static readonly StyledProperty<Color> ControllerTintProperty = AvaloniaProperty.Register<BadgeStrip, Color>(nameof(ControllerTint), Colors.White);
        public static readonly StyledProperty<Point> FolderLinkPositionProperty = AvaloniaProperty.Register<BadgeStrip, Point>(nameof(FolderLinkPosition), new Point(0.5, 0.5));
        public static readonly StyledProperty<double> FolderLinkSizeProperty = AvaloniaProperty.Register<BadgeStrip, double>(nameof(FolderLinkSize), 0.5);
        public static readonly StyledProperty<Color> FolderLinkTintProperty = AvaloniaProperty.Register<BadgeStrip, Color>(nameof(FolderLinkTint), Colors.White);

        static BadgeStrip() => AffectsRender<BadgeStrip>(IconsProperty, DirectionProperty, LinesProperty, ItemsPerLineProperty, ItemMarginProperty,
            ContentHorizontalAlignmentProperty, ContentVerticalAlignmentProperty, TintProperty, EntriesProperty, ControllerPositionProperty, ControllerSizeProperty,
            ControllerTintProperty, FolderLinkPositionProperty, FolderLinkSizeProperty, FolderLinkTintProperty);

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

        /// <summary>The badges to show, in order, each an image file or the toolkit's drawing; when set they are drawn instead of Icons.</summary>
        public IReadOnlyList<BadgeEntry>? Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

        /// <summary>Where a controller's centre sits on its badge, as fractions of the badge's width and height. (0.5, 0.5), the centre, by default.</summary>
        public Point ControllerPosition { get => GetValue(ControllerPositionProperty); set => SetValue(ControllerPositionProperty, value); }

        /// <summary>A controller's width as a fraction of its badge's width. 0.5 by default.</summary>
        public double ControllerSize { get => GetValue(ControllerSizeProperty); set => SetValue(ControllerSizeProperty, value); }

        /// <summary>A colour every controller is multiplied by. White by default.</summary>
        public Color ControllerTint { get => GetValue(ControllerTintProperty); set => SetValue(ControllerTintProperty, value); }

        /// <summary>Where a folder link's centre sits on its badge, as fractions of the badge's width and height. (0.5, 0.5), the centre, by default.</summary>
        public Point FolderLinkPosition { get => GetValue(FolderLinkPositionProperty); set => SetValue(FolderLinkPositionProperty, value); }

        /// <summary>A folder link's width as a fraction of its badge's width. 0.5 by default.</summary>
        public double FolderLinkSize { get => GetValue(FolderLinkSizeProperty); set => SetValue(FolderLinkSizeProperty, value); }

        /// <summary>A colour every folder link is multiplied by. White by default.</summary>
        public Color FolderLinkTint { get => GetValue(FolderLinkTintProperty); set => SetValue(FolderLinkTintProperty, value); }

        /// <summary>The cell each shown icon is drawn in, in the control's coordinates.</summary>
        /// <param name="size">The control's size to lay the cells out in.</param>
        /// <returns>One rectangle per icon drawn, in order.</returns>
        public IReadOnlyList<Rect> Cells(Size size)
        {
            int count = Math.Min(Entries?.Count ?? Icons?.Count ?? 0, Math.Max(1, Lines) * Math.Max(1, ItemsPerLine));
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
            for (int i = 0; i < cells.Count; i++)
            {
                if (Entries is not { } entries) PictureFiles.Draw(context, this, Icons![i], cells[i], Tint);
                else Draw(context, entries[i], cells[i]);
            }
        }

        // A badge's file, else its drawing in a square fitted to the cell; then its overlay, sized by the cell's width and centred at its position.
        private void Draw(DrawingContext context, BadgeEntry entry, Rect cell)
        {
            bool linkDrawn = entry.Kind == BadgeKind.Folder && entry.Linked && !PictureFiles.Exists(entry.LinkIconPath);
            if (PictureFiles.Exists(entry.IconPath)) PictureFiles.Draw(context, this, entry.IconPath, cell, Tint);
            else if (!linkDrawn) BadgeGlyphDrawing.Draw(context, Fitted(cell), entry.Kind, Tint);
            else
            {
                // A drawn link over a drawn folder is cut out of it, so the two outlines do not run together (§180.2).
                var ground = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(cell), BadgeGlyphDrawing.LinkGround(Overlay(cell, FolderLinkPosition, FolderLinkSize)));
                using (context.PushGeometryClip(ground)) BadgeGlyphDrawing.Draw(context, Fitted(cell), entry.Kind, Tint);
            }
            if (entry.Kind == BadgeKind.Controller && (entry.Controller is not null || PictureFiles.Exists(entry.ControllerIconPath)))
            {
                Rect box = Overlay(cell, ControllerPosition, ControllerSize);
                if (PictureFiles.Exists(entry.ControllerIconPath)) PictureFiles.Draw(context, this, entry.ControllerIconPath, box, ControllerTint);
                else BadgeGlyphDrawing.Draw(context, box, entry.Controller!.Value, ControllerTint);
            }
            if (entry.Kind == BadgeKind.Folder && entry.Linked)
            {
                Rect box = Overlay(cell, FolderLinkPosition, FolderLinkSize);
                if (PictureFiles.Exists(entry.LinkIconPath)) PictureFiles.Draw(context, this, entry.LinkIconPath, box, FolderLinkTint);
                else BadgeGlyphDrawing.Draw(context, box, BadgeKind.FolderLink, FolderLinkTint);
            }
        }

        private static Rect Fitted(Rect cell)
        {
            double side = Math.Min(cell.Width, cell.Height);
            return new Rect(cell.Center.X - side / 2, cell.Center.Y - side / 2, side, side);
        }

        private static Rect Overlay(Rect cell, Point position, double size)
        {
            double side = Fitted(cell).Width * size;
            Point centre = new(cell.X + position.X * cell.Width, cell.Y + position.Y * cell.Height);
            return new Rect(centre.X - side / 2, centre.Y - side / 2, side, side);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Group);
    }
}
