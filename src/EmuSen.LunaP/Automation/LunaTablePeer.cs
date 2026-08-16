using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace EmuSen.LunaP.Automation
{
    // WHAT A LunaTable TELLS A SCREEN READER ABOUT ITSELF - see docs/LunaP.md §68.
    //
    // Until this existed the table was a Group with a name and nothing else: a reader could walk into
    // it, hear each row's sentence (§50.5) and read a cell's value (§50.6), and had no way to ask the
    // two questions a grid exists to answer - WHAT IS SELECTED, and how do I move around something
    // taller and wider than the window.
    //
    // TWO PROVIDERS AND NOT A PEER PER PART, which is where this differs from the control it is at
    // parity with. TreeDataGrid has eight peer types; the ones that carry behaviour are its table
    // peer (ISelectionProvider, IScrollProvider), its row peer (ISelectionItemProvider) and its two
    // cell peers (IValueProvider, IToggleProvider) - enumerated from 12.2.0, not recalled. LunaP
    // already had the last three without writing a peer for any of them: a row is a ListBoxItem and
    // brings its own, a text cell has TableCellPeer, and a check cell is a real CheckBox with
    // Avalonia's own toggle peer (§57.3). What was missing was this one.
    //
    // DELEGATES RATHER THAN A TYPED OWNER, for LunaAutomationPeer's reason one level up: the table is
    // LunaTable<T> and a peer that named the type could not be constructed from the non-generic base
    // that the template, the styles and the tests all go through. Every answer is read when it is
    // asked for, never captured - a selection that changed after the peer was built is the normal
    // case, not the exception.
    internal sealed class LunaTablePeer : LunaAutomationPeer, ISelectionProvider, IScrollProvider
    {
        private readonly Func<bool> _multiple;
        private readonly Func<IReadOnlyList<AutomationPeer>> _selection;
        private readonly Func<ScrollViewer?> _viewer;

        public LunaTablePeer(
            Control owner,
            Func<bool> multiple,
            Func<IReadOnlyList<AutomationPeer>> selection,
            Func<ScrollViewer?> viewer)
            : base(owner, AutomationControlType.DataGrid)
        {
            _multiple = multiple;
            _selection = selection;
            _viewer = viewer;
        }

        public bool CanSelectMultiple => _multiple();

        // False, and it is a statement rather than a default. This table can have nothing selected -
        // Refresh can drop the selected row, SelectionMode.None refuses one outright - and a reader
        // told selection was required would present "nothing selected" as a state that cannot happen.
        public bool IsSelectionRequired => false;

        // THE PEERS OF THE SELECTED CELLS, OR OF THE SELECTED ROWS, according to the unit. Returning
        // peers rather than anything of LunaP's own is what makes this work for all three cell kinds
        // at once: the peer of a check cell is Avalonia's CheckBox peer and the peer of a template
        // cell is whatever the caller's control provides, and a reader gets each one's own name and
        // its own providers without this control having to wrap anything (§68.2).
        public IReadOnlyList<AutomationPeer> GetSelection() => _selection();

        // ---- IScrollProvider, which is the other thing the reference's table peer carries ----
        //
        // All of it reads the one ScrollViewer inside the ListBox - the same one the frozen band
        // follows (§64.3), found the same way and for the same reason. Null before there is a
        // template, and every member below has to answer anyway, so each has a value for "there is
        // nothing to scroll yet" rather than throwing at a reader.

        public bool HorizontallyScrollable =>
            _viewer() is { } viewer && viewer.Extent.Width > viewer.Viewport.Width;

        public bool VerticallyScrollable =>
            _viewer() is { } viewer && viewer.Extent.Height > viewer.Viewport.Height;

        // NoScroll (-1) and not 0 when there is nothing to scroll, because 0 means "at the start"
        // and a reader would announce a table that cannot scroll as one parked at the top left.
        public double HorizontalScrollPercent => Percent(
            _viewer()?.Offset.X, _viewer()?.Extent.Width, _viewer()?.Viewport.Width);

        public double VerticalScrollPercent => Percent(
            _viewer()?.Offset.Y, _viewer()?.Extent.Height, _viewer()?.Viewport.Height);

        public double HorizontalViewSize => ViewSize(_viewer()?.Viewport.Width, _viewer()?.Extent.Width);

        public double VerticalViewSize => ViewSize(_viewer()?.Viewport.Height, _viewer()?.Extent.Height);

        public void SetScrollPercent(double horizontal, double vertical)
        {
            if (_viewer() is not { } viewer) return;

            viewer.Offset = new Avalonia.Vector(
                Place(horizontal, viewer.Offset.X, viewer.Extent.Width, viewer.Viewport.Width),
                Place(vertical, viewer.Offset.Y, viewer.Extent.Height, viewer.Viewport.Height));
        }

        // A page at a time for the large amounts and one line for the small, which is what the names
        // mean everywhere else: LargeIncrement is what Page Down does and SmallIncrement is a wheel
        // notch. The line height is the viewport over sixteen rather than a constant, so a table of
        // tall template rows (§57) does not take forty presses to cross.
        public void Scroll(ScrollAmount horizontal, ScrollAmount vertical)
        {
            if (_viewer() is not { } viewer) return;

            viewer.Offset = new Avalonia.Vector(
                Move(horizontal, viewer.Offset.X, viewer.Extent.Width, viewer.Viewport.Width),
                Move(vertical, viewer.Offset.Y, viewer.Extent.Height, viewer.Viewport.Height));
        }

        private const double NoScroll = -1;

        private static double Percent(double? offset, double? extent, double? viewport)
        {
            if (offset is not { } at || extent is not { } total || viewport is not { } window) return NoScroll;
            if (total <= window) return NoScroll;

            return Math.Clamp(at / (total - window) * 100, 0, 100);
        }

        private static double ViewSize(double? viewport, double? extent)
        {
            if (viewport is not { } window || extent is not { } total || total <= 0) return 100;

            return Math.Clamp(window / total * 100, 0, 100);
        }

        private static double Place(double percent, double current, double extent, double viewport)
        {
            if (percent < 0 || extent <= viewport) return current;

            return Math.Clamp(percent / 100 * (extent - viewport), 0, Math.Max(0, extent - viewport));
        }

        private static double Move(ScrollAmount amount, double current, double extent, double viewport)
        {
            double step = amount switch
            {
                ScrollAmount.LargeIncrement => viewport,
                ScrollAmount.LargeDecrement => -viewport,
                ScrollAmount.SmallIncrement => viewport / 16,
                ScrollAmount.SmallDecrement => -viewport / 16,
                _ => 0,
            };

            return Math.Clamp(current + step, 0, Math.Max(0, extent - viewport));
        }
    }
}
