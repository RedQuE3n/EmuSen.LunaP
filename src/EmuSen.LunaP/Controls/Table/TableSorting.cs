using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmuSen.LunaP.Controls
{
    // SORTING - see docs/LunaP.md §27 for the three-state cycle, and §70.3 for sorting without a
    // click.
    //
    // TWO HALVES THAT MUST AGREE: what the rows are ordered by, and what the headings SAY they are
    // ordered by. Every path that changes the first ends in Apply, which updates the second and
    // writes it down - so a programmatic sort and a clicked one cannot end up doing different
    // amounts of work, and neither can leave an arrow pointing at the wrong column.
    //
    // THE SORT PROJECTS RATHER THAN REORDERS. `_items` stays in the order Refresh was given and
    // `_view` is what is on screen. That is the mechanism the third click depends on; Cycle's
    // comment is where the argument for having a third click lives.
    //
    // THE HEADINGS ARE UPDATED IN PLACE AND NEVER REBUILT, which is a keyboard requirement rather
    // than a performance one and is why `_heads` is kept at all. ShowSortState says why.
    public partial class LunaTable<T> where T : class
    {
        // Which column is sorted, and which way. -1 is the third state and the initial one.
        private int _sortColumn = -1;
        private bool _sortDescending;

        // The heading controls, in column order, so that a sort can update what is already on screen
        // rather than building it again - see ShowSortState.
        private readonly List<Head> _heads = new();

        // WHICH COLUMN THE TABLE IS SORTED BY - see docs/LunaP.md §70.3.
        //
        // -1 for unsorted, which is a real state here rather than an absence: §27's heading cycle is
        // ascending, descending, OFF, and the third one returns the rows to the order the caller gave
        // them. A nullable int would say the same thing in a type that invites `.Value`.
        /// <summary>The column the table is sorted by, or -1 when it is in the order it was given.</summary>
        public int SortedColumn => _sortColumn;

        /// <summary>Whether the current sort is descending. False when nothing is sorted.</summary>
        public bool SortedDescending => _sortDescending;

        // Sorting without a click, which is the half of §54.3's "programmatic SortDirection" this
        // control had no way to do. The obvious callers are a "Sort by size" menu item and an
        // application restoring its own default before the user has touched anything.
        //
        // REFUSES A COLUMN WITH NO COMPARISON rather than throwing or sorting by the projected text.
        // A column without a Sort is one the caller declared unsortable, and the reason Sort takes a
        // comparison over the model at all is that sorting the displayed string is wrong in ways that
        // look right - "10" before "9" (§27). Silently falling back to it would reintroduce exactly
        // that bug through a door with no heading to click.
        //
        // Works before the template for §27.6's reason: the fields are the table's own and Show
        // applies them, so a caller sorting from a window's constructor gets a sorted table when it
        // appears. A remembered layout still wins over it - Restore runs after, and what a user
        // dragged and clicked outranks what the application declared (§27.11, §65.4).
        /// <summary>Sorts by one column, as though its heading had been clicked.</summary>
        /// <param name="column">The column index, in the order the columns were added.</param>
        /// <param name="descending">Which way round. Ascending by default.</param>
        /// <remarks>Does nothing when the index is out of range or that column has no Sort comparison.</remarks>
        public void SortBy(int column, bool descending = false)
        {
            if (column < 0 || column >= _columns.Count) return;
            if (_columns[column].Sort is null) return;

            _sortColumn = column;
            _sortDescending = descending;
            Apply();
        }

        /// <summary>Returns the rows to the order they were given in.</summary>
        public void ClearSort()
        {
            if (_sortColumn < 0 && !_sortDescending) return;

            _sortColumn = -1;
            _sortDescending = false;
            Apply();
        }

        // ASCENDING, DESCENDING, THEN BACK TO THE ORDER REFRESH WAS GIVEN.
        //
        // Two states is the commoner convention and this departs from it knowingly. The order a
        // caller hands to Refresh carries meaning in this toolkit far more often than in a database
        // front end - log order, file order, the order a scan found things in - and a two-state
        // cycle makes that order unreachable the moment somebody clicks a header. The cost is a
        // third click that will surprise somebody; the alternative is a table that can lose
        // information the caller deliberately put in it. docs/LunaP.md §27.
        private void Cycle(int index)
        {
            if (_sortColumn != index)
            {
                _sortColumn = index;
                _sortDescending = false;
            }
            else if (!_sortDescending)
            {
                _sortDescending = true;
            }
            else
            {
                _sortColumn = -1;
                _sortDescending = false;
            }

            Apply();
        }

        // What a heading click does after it has decided the new state, shared so a programmatic sort
        // and a clicked one cannot end up doing different amounts of work.
        private void Apply()
        {
            Show();
            ShowSortState();
            Remember();
        }

        // ORDERBY AND NOT List<T>.Sort, and the difference is visible rather than academic.
        // List<T>.Sort is an unstable introsort: rows that compare equal come out in an arbitrary
        // order that changes between runs, so a table sorted by a column with ties reshuffles its
        // equal rows every time it is refreshed. LINQ's OrderBy is documented stable, so ties keep
        // the order the caller gave to Refresh.
        //
        // It also projects rather than reorders: _items stays in arrival order, so the third click
        // on a header has somewhere to return to. Sorting in place would make the unsorted state
        // unreachable, which is most of why §27 chose a three-state cycle at all.
        private IReadOnlyList<T> Ordered(IReadOnlyList<T> level)
        {
            if (_sortColumn < 0 || _sortColumn >= _columns.Count) return level;
            if (_columns[_sortColumn].Sort is not { } comparison) return level;

            var comparer = Comparer<T>.Create(comparison);
            return _sortDescending
                ? level.OrderByDescending(item => item, comparer).ToList()
                : level.OrderBy(item => item, comparer).ToList();
        }

        // A SORTABLE HEADING IS A BUTTON; AN UNSORTABLE ONE STAYS A TEXTBLOCK.
        //
        // The button is not for the look - it is styled flat, and the theme spends more lines taking
        // Fluent's chrome off it than putting anything on. It is there because a heading that only
        // responds to a click is a sort a keyboard user does not have, and §24 is the section about
        // exactly this class of miss. A Button brings focus, Tab, Space and Enter, an invoke peer and
        // a focus adorner, all of which would otherwise be hand-built on a TextBlock and half of
        // which would be forgotten.
        //
        // The converse matters as much: a column with no comparison is left a plain TextBlock rather
        // than made into a button that does nothing. An inert tab stop costs a keyboard user a press
        // and tells them nothing, which is worse than not being a stop at all.
        private Control Heading(int index)
        {
            ColumnSpec column = _columns[index];

            var label = new TextBlock
            {
                Text = column.Header,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // THE HEADING FOLLOWS THE COLUMN, or a right-aligned column of sizes sits under a
            // left-aligned word and the two look like different columns. §70.2.
            //
            // On the label's TextAlignment rather than on the Button, because a sortable heading is
            // a Button stretched across the whole column (§27.3) and moving the button would move
            // its hover fill off the column it belongs to. The label inside it is the thing that has
            // to line up with the values below.
            if (TextAlign(column.Alignment) is { } heading) label.TextAlignment = heading;

            if (column.Sort is null)
            {
                _heads.Add(new Head(label, null));
                return label;
            }

            // Hidden rather than blank in the unsorted state, and never a neutral "sortable" mark.
            // Three states with a glyph in all three reads as three sorts; two glyphs and nothing
            // reads as what it is - two sorted states and off.
            //
            // Raw in the automation tree because a screen reader announcing "black up-pointing
            // triangle" after the column name is noise. The state is carried on the button's own
            // name instead, where a reader will actually meet it.
            var glyph = new TextBlock
            {
                FontWeight = Avalonia.Media.FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsVisible = false,
            };

            glyph.Classes.Add("sort");
            AutomationProperties.SetAccessibilityView(glyph, AccessibilityView.Raw);

            var button = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { label, glyph },
                },
            };

            button.Classes.Add("heading");

            int clicked = index;
            button.Click += (_, _) => Cycle(clicked);

            _heads.Add(new Head(button, glyph));
            return button;
        }

        // UPDATES THE HEADINGS IN PLACE AND NEVER REBUILDS THEM, which is a keyboard requirement
        // rather than a performance one. A user who reached a heading with Tab and pressed Space is
        // focused on that button; replacing it with a new one drops focus to the top of the window
        // and leaves them nowhere, having just used the control exactly as intended.
        private void ShowSortState()
        {
            for (int i = 0; i < _heads.Count && i < _columns.Count; i++)
            {
                if (_heads[i].Glyph is not { } glyph) continue;

                bool sorted = i == _sortColumn;
                glyph.IsVisible = sorted;
                glyph.Text = sorted ? (_sortDescending ? "▼" : "▲") : string.Empty;

                AutomationProperties.SetName(
                    _heads[i].Cell,
                    sorted
                        ? $"{_columns[i].Header}, sorted {(_sortDescending ? "descending" : "ascending")}"
                        : $"{_columns[i].Header}, not sorted");
            }
        }

        // The heading control for a column, and its glyph when it has one. Held so that a sort can
        // update what is already on screen rather than building it again - see ShowSortState.
        private readonly record struct Head(Control Cell, TextBlock? Glyph);
    }
}
