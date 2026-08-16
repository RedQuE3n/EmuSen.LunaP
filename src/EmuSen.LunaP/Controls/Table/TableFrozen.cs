using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // COLUMNS THAT DO NOT SCROLL AWAY - see docs/LunaP.md §61, and §60 for the correction that made
    // this possible after §59.3 concluded it needed a different control.
    //
    // FOUR SECTIONS IN ONE FILE, WHICH IS ONE FEATURE AND NOT FOUR. §61 is the pin, §62 is whether
    // the band is real or only looks right, §63 is the seam that makes it visible before anything has
    // scrolled, and §64 is what it does to scrolling, editing and focus. They were four passes
    // because each one found the next; they are one file because changing any of them changes the
    // others, and splitting them would put ClearFocusFromBand where it could not see the band.
    //
    // EVERYTHING HERE IS RENDER-LEVEL. Clip and RenderTransform affect drawing and not layout, so
    // this feature costs no measure and no arrange, and the shared size groups that line the header
    // up with the rows (§27.10) never learn it happened. A change here that invalidates layout has
    // left the design rather than extended it.
    //
    // THE SCROLL OFFSET IS OWNED BY THIS FILE, which is §64.2 and §64.3 together: it is read from
    // one viewer, resolved by one method, at one moment - layout - and nothing else in the control
    // may cache it. Both of those sections are corrections to code that did.
    public partial class LunaTable<T> where T : class
    {
        // How far the rows have been scrolled sideways, so the header can be moved to match and so
        // an event that reports the same offset twice costs nothing. §59.
        private double _scrolledTo;

        // Zero, the default, is a table that behaves exactly as it did: nothing is transformed,
        // nothing is clipped, and Pin returns before touching a single child (§26.13).
        //
        // Counted in COLUMNS AND NOT IN GRID COLUMNS, like every other number a caller gives this
        // control: FrozenColumns = 2 freezes the first two columns the caller declared, whether or
        // not there is a gutter in front of them and whether or not one of them is hidden. §58.2 is
        // the section about keeping those two indices apart, and this is another place they meet.
        private int _frozenColumns;

        /// <summary>How many leading columns stay put when the table is scrolled sideways. Zero by default.</summary>
        /// <remarks>
        /// Counted in columns as they were added, so a hidden column still takes one. A gutter, when there
        /// is one, is frozen along with them as soon as this is greater than zero. Freezing more columns
        /// than the table has freezes all of them, which simply leaves nothing to scroll.
        /// </remarks>
        public int FrozenColumns
        {
            get => _frozenColumns;
            set
            {
                if (_frozenColumns == value) return;

                _frozenColumns = value;

                // Rebuilt rather than only re-pinned, because the seam is a child of the header and
                // of every row - so turning frozen columns on or off changes what those grids
                // CONTAIN and not merely where their contents sit.
                Rebuild();
                Show();
                Pin();
            }
        }

        // The frozen band expressed in GRID columns, which is what Pin walks. Clamped rather than
        // trusted: a caller who freezes five columns of a three-column table has said something
        // harmless, and the honest reading is "all of them" rather than an exception at layout time.
        //
        // A GUTTER IS ALWAYS FROZEN, EVEN AT FrozenColumns = 0 - see docs/LunaP.md §63. §59.4 pinned
        // the opposite as a decision on the record, because nothing could be frozen then; this is
        // that decision being taken rather than reversed by accident. A row header is how the user
        // refers to a row, and one that scrolls away leaves them reading a line of values with
        // nothing to say which row it is - which is the whole of what a gutter is for.
        private int FrozenGridColumns
        {
            get
            {
                int gutter = _rowHeader is null ? 0 : 1;
                return _frozenColumns <= 0
                    ? gutter
                    : gutter + Math.Min(_frozenColumns, _columns.Count);
            }
        }

        // FROZEN COLUMNS, AND THE TWO RULES THEY ARE - see docs/LunaP.md §61.
        //
        // §59.3 said this needed a different control and §60 records why that was wrong. The whole
        // mechanism is two lines of geometry applied to the row grid's DIRECT CHILDREN:
        //
        //   - a child in a frozen column is translated by +scrollX, which cancels the scroll the
        //     ScrollContentPresenter is applying to everything and leaves it where it started;
        //   - a child in a scrolling column is CLIPPED so that whatever would fall inside the frozen
        //     band is not drawn at all.
        //
        // CLIPPED AND NOT COVERED, which is the part worth understanding. A frozen cell painted over
        // its neighbours would need an opaque backdrop, and the row's backdrop is Fluent's selected
        // and pointer-over fill, which is not reachable without reaching into a template §48 refuses
        // to touch. Removing the neighbour instead means the thing behind both of them - that same
        // Fluent fill - carries on showing through, and nothing has to be matched at all. Measured at
        // 6,399 red pixels and ZERO blue inside the band (§60.1).
        //
        // THE DIRECT CHILDREN ARE THE RIGHT GRANULARITY because every one of them already carries a
        // Grid.Column, so a bare cell, a cell inside an expander panel (§55), a vertical rule (§56.2),
        // a resize grip and an open editor are all handled without one of them being a special case.
        //
        // BOUNDS RATHER THAN COLUMN OFFSETS. The child's own Bounds.X is where it actually sits in
        // the grid, which already accounts for its alignment, its margin and any column span - a
        // GridSplitter is aligned right inside its column and would be placed wrongly by arithmetic
        // over column starts.
        //
        // Render-level, both of them: Clip and RenderTransform affect drawing and not layout, so this
        // costs no measure and no arrange, and the shared size groups that line the header up with
        // the rows (§27.10) never learn it happened.
        private void Pin()
        {
            // THE OFFSET IS READ HERE AND NOWHERE ELSE - see docs/LunaP.md §64.2, which is a
            // correction to §59.2.
            //
            // §59 cached the offset from the ScrollChanged event's own viewer, and that number is
            // not always the one the viewer has settled on. Measured: a scroll caused by
            // BringIntoView - which is what Tab, F2 and Edit all provoke - raises ScrollChanged
            // reporting 0 while the viewer is already at 612, and no later event corrects it. The
            // header stayed put and every heading sat 612 pixels from its own cells, silently,
            // whenever a scroll was caused by anything other than the user dragging the bar.
            //
            // Reading the live offset at layout time cannot be stale, because layout is what happens
            // after the offset settles. The event is kept for promptness and for catching the viewer
            // out of the ListBox's template, and its Offset is now ignored.
            // NOT ANY ScrollViewer UNDER THE ROWS, AND NOT THE EVENT'S EITHER - see §64.3. A TextBox
            // has a ScrollViewer inside its own template, so the moment a cell editor is opened its
            // inner viewer raises ScrollChanged, that event bubbles to PART_Rows, and a handler that
            // trusted e.Source pointed this control at the editor's scroller from then on. Its offset
            // is always zero, so the header stopped following the rows and every pin went stale -
            // measured as a table sitting at offset 600 with _scrolledTo reading 0.
            //
            // The one this control owns is the one that is not inside a row, which is what the filter
            // says. Resolved once and kept, because it comes from the ListBox's template and cannot
            // be found before that template is applied.
            _viewer ??= OwnViewer();

            double offset = _viewer?.Offset.X ?? 0;
            if (Math.Abs(_scrolledTo - offset) > 0.01)
            {
                _scrolledTo = offset;

                // Null at rest, so an unscrolled table has the tree it always had.
                if (HeaderGrid is not null)
                {
                    HeaderGrid.RenderTransform = offset == 0 ? null : new TranslateTransform(-offset, 0);
                }
            }

            // Nothing asked for and nothing ever set: the common table does not pay for this feature
            // existing, and this is the line that makes that true. It tests what the caller ASKED
            // for rather than what was granted, because a refused band still has a seam in every row
            // to hide - and the flag beside it, because turning frozen columns off has to clear what
            // was set once, and then stop.
            int asked = FrozenGridColumns;
            if (asked <= 0 && !_pinned) return;

            int frozen = asked;

            // FREEZING MUST NEVER TAKE SCROLLING AWAY - see docs/LunaP.md §64.
            //
            // A frozen band as wide as the viewport leaves the scrolling columns nowhere to be: they
            // are clipped to nothing at every offset, and the table is back to §59's defect, where
            // columns exist and no scrollbar, wheel or key can reach them. Measured at two frozen
            // columns of 300 in a 400-wide table - band 600, viewport 400, and columns 2 upwards
            // fully clipped at maximum scroll.
            //
            // So a band that does not leave room freezes NOTHING. Freezing is a refinement of
            // scrolling and does not get to remove it; a caller who freezes too much, or a user who
            // drags the window narrow, gets an ordinary scrolling table rather than an unusable one.
            // It comes back by itself when there is room again, because this is recomputed per pass
            // rather than latched.
            if (frozen > 0 && !LeavesRoom(frozen)) frozen = 0;

            _pinned = frozen > 0;

            if (HeaderGrid is not null) Pin(HeaderGrid, frozen);
            if (Rows is null) return;

            foreach (Control container in Rows.GetRealizedContainers())
            {
                if (RowGridIn(container) is { } grid) Pin(grid, frozen);
            }

            if (!_focusMoved) return;

            _focusMoved = false;
            ClearFocusFromBand(frozen);
        }

        // NOTHING MAY TAKE FOCUS WHERE NOBODY CAN SEE IT - see docs/LunaP.md §62.
        //
        // A ScrollViewer brings a newly focused control into view by scrolling the least it can to
        // put that control inside the VIEWPORT, and the viewport's left edge is zero. It knows
        // nothing about a band of frozen columns sitting over the first two hundred pixels of it, so
        // "just visible at the left" means "exactly underneath them". Measured: tabbing to a button
        // in column 1 of a table scrolled to 824 left it focused, at x=0, with a clip of zero width -
        // a control holding the keyboard focus and drawing nothing at all. That is §24's failure in a
        // new place, and it is the reason this pass exists rather than a refinement of pass 1.
        //
        // THE CLIP CANNOT SUPPLY THE CORRECTION, which is worth knowing before someone simplifies
        // this. Pin clamps the hidden amount to the child's own width, because a clip rectangle
        // cannot be wider than what it clips; the amount needed to clear the band is the UNCLAMPED
        // overlap, and for a fully hidden control those two are different numbers. Reading it back
        // off Clip.Bounds.X scrolls a narrow control by its own width and leaves it under the band.
        //
        // Scrolling by exactly the overlap lands the control on the band's edge and no further: it
        // is the smallest move that makes it visible, which is what BringIntoView was trying to do.
        private void ClearFocusFromBand(int frozen)
        {
            if (frozen <= 0 || _viewer is null) return;
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused) return;

            foreach (Visual step in focused.GetSelfAndVisualAncestors())
            {
                if (ReferenceEquals(step, this)) return;

                // The grid child is where a pin lives, so it is the thing whose overlap counts - the
                // focused control itself may be nested inside a template cell.
                if (step is not Control child || child.Parent is not Grid grid) continue;
                if (Grid.GetColumn(child) < frozen) return;

                double overlap = _scrolledTo + BandOf(grid, frozen) - child.Bounds.X;
                if (overlap > 0.5)
                {
                    _viewer.Offset = new Vector(Math.Max(0, _scrolledTo - overlap), _viewer.Offset.Y);
                }

                return;
            }
        }

        // Raised for anything inside the table, including a heading, a cell editor and a caller's own
        // control in a template column. Handled on the next layout rather than here, because the
        // ScrollViewer's own BringIntoView has not run yet at this point and correcting a position it
        // is about to change would be corrected right back.
        private bool _focusMoved;

        // The viewer the rows scroll in, kept from the scroll event because it lives inside the
        // ListBox's template and cannot be found before that template is applied.
        private ScrollViewer? _viewer;

        // Whether the frozen band leaves anything for the rest of the table to be seen in. Measured
        // against the viewport the rows actually scroll in, and falling back to the control's own
        // width before that viewer has been found - which is the state during the first layout, when
        // nothing has scrolled and the answer does not matter yet.
        //
        // A zero-width viewport is "not laid out", not "no room": answering false there would unfreeze
        // every table for one pass on the way up.
        private bool LeavesRoom(int frozen)
        {
            if (HeaderGrid is null) return true;

            double viewport = _viewer?.Viewport.Width ?? Bounds.Width;
            if (viewport <= 0) return true;

            return BandOf(HeaderGrid, frozen) < viewport;
        }

        // The band, in a grid's own coordinates. The frozen columns start at zero by definition, so
        // their total width is also where the band ends on screen.
        private static double BandOf(Grid grid, int frozen)
        {
            double band = 0;
            for (int i = 0; i < frozen && i < grid.ColumnDefinitions.Count; i++)
            {
                band += grid.ColumnDefinitions[i].ActualWidth;
            }

            return band;
        }

        private void Pin(Grid grid, int frozen)
        {
            double band = BandOf(grid, frozen);

            foreach (Control child in grid.Children.OfType<Control>())
            {
                // THE SEAM ONLY EXISTS WHILE THE BOUNDARY DOES. It is built into the header and every
                // row from FrozenColumns (§63.1), which is what the caller asked for - but the band
                // can be refused for want of room, and a line drawn where nothing is pinned is a
                // statement about the layout that is not true.
                if (child.Classes.Contains("frozen-edge")) child.IsVisible = frozen > 0;

                if (frozen <= 0)
                {
                    child.RenderTransform = null;
                    child.Clip = null;
                    continue;
                }

                if (Grid.GetColumn(child) < frozen)
                {
                    child.Clip = null;

                    // Null rather than a zero translation, so an unscrolled table has exactly the
                    // visual tree it had before frozen columns existed. It matters more than tidiness
                    // now that a gutter is frozen by default (§63): every table with a row header
                    // would otherwise carry a transform per cell for a scroll that never happened.
                    child.RenderTransform = _scrolledTo == 0
                        ? null
                        : new TranslateTransform(_scrolledTo, 0);
                    continue;
                }

                child.RenderTransform = null;

                Rect bounds = child.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    // Not arranged yet, so there is no honest rectangle to write. The next layout
                    // pass calls this again with real bounds; guessing one now would clip a cell to
                    // nothing and leave it that way until something else moved.
                    child.Clip = null;
                    continue;
                }

                // How much of this child is inside the band. Degenerates correctly: unscrolled,
                // _scrolledTo is zero and a scrolling column starts at or after the band, so this is
                // zero or negative and no clip is set at all.
                double hidden = Math.Clamp(_scrolledTo + band - bounds.X, 0, bounds.Width);

                child.Clip = hidden <= 0
                    ? null
                    : new RectangleGeometry(new Rect(hidden, 0, bounds.Width - hidden, bounds.Height));
            }
        }

        // Whether anything is currently pinned, so that turning frozen columns off clears what was
        // set and a table that never had any does no work at all.
        private bool _pinned;

        // Puts the seam in the last frozen column of a grid, or leaves the grid alone when nothing is
        // frozen. Both the header and every row go through here, so the two cannot disagree about
        // where the boundary is.
        private void AddFrozenEdge(Grid grid)
        {
            int frozen = FrozenGridColumns;
            if (frozen <= 0) return;

            Control edge = FrozenEdge();
            Grid.SetColumn(edge, frozen - 1);
            grid.Children.Add(edge);
        }

        // WHERE THE PINNED COLUMNS STOP, DRAWN SO SOMEBODY CAN SEE IT - see docs/LunaP.md §63.
        //
        // Without this, frozen columns are invisible until the table is scrolled: the user is given
        // a layout that behaves differently on the left and is told nothing about it until they
        // discover it. So the edge is drawn whether or not anything has been scrolled yet.
        //
        // A SIBLING IN THE LAST FROZEN COLUMN, WHICH IS THE WHOLE IMPLEMENTATION. It is the same
        // shape as a vertical grid rule (§56.2) - a Border in the column, aligned right - and because
        // it sits in a frozen column, Pin translates it with everything else in there. There is no
        // positioning code for the seam at all, and no way for it to drift from the boundary it
        // marks: it IS the right-hand edge of that column.
        //
        // LunaBorder, the same token the grid rules take, rather than a colour of its own. §56.2's
        // argument applies unchanged - it is where one surface stops and the next begins, and it is
        // already held to 3:1 against both. When vertical rules are also on, the two coincide exactly
        // rather than doubling up, because both are one pixel aligned to the same edge.
        private static Control FrozenEdge()
        {
            var edge = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            edge.Classes.Add("frozen-edge");
            return edge;
        }

        // THE ONE ScrollViewer THIS CONTROL OWNS, and the filter is the whole of §64.3: a TextBox has
        // a ScrollViewer inside its own template, so a cell editor puts a second one in this tree and
        // "the first descendant" is whichever the walk reaches first. The one that is not inside a
        // row is this control's.
        //
        // Shared with the automation peer since §68, which is why it is a method rather than the
        // inline lookup Pin used to carry: two callers finding the viewer two ways is exactly how
        // §64.3 happened once already.
        private ScrollViewer? OwnViewer() =>
            Rows?.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault(scroller => !scroller.GetVisualAncestors().OfType<ListBoxItem>().Any());
    }
}
