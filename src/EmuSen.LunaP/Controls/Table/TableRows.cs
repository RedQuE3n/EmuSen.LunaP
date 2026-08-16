using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // WHAT A ROW IS MADE OF - see docs/LunaP.md §57 for the three cell kinds, §69 for the one
    // default a template cell gets, §70 for alignment, and §56.2 for the rules between them.
    //
    // ONE PLACE DECIDES WHAT A CELL IS, and everything after it is the same for all three kinds: the
    // index marker, the column, the expander, the rule. A kind that needed a second branch further
    // down would be a kind that had leaked - which is the test to apply before adding a fourth.
    //
    // THERE WILL NOT BE A FOURTH, AND THAT IS §57'S POINT. Template is the escape hatch: everything a
    // column could grow - a progress bar, an icon, a pair of buttons - is a control somebody can
    // build, and a toolkit that added a kind per idea would be maintaining a gallery inside a table.
    //
    // TWO OF THE THREE ARE STOCK AVALONIA CONTROLS ON PURPOSE. A CheckBox already brings focus,
    // Space, a focus adorner and an IToggleProvider peer; a hand-rolled tick would have to reproduce
    // all four and would forget two. The same argument made a sortable heading a Button (§27.3) and a
    // column grip a GridSplitter. Only the text cell is this toolkit's own, and TableCell.cs says why
    // it had to be: Avalonia's TextBlock peer offers no IValueProvider, so a reader could hear a cell
    // and not change it.
    //
    // THE MODEL IS THE TRUTH AND THE CELL IS A VIEW OF IT. Toggled writes and then re-reads rather
    // than trusting the box, which is what makes a Toggle that normalises show what it did and a
    // Toggle that refuses put the tick back, with no separate veto mechanism to build (§57.4).
    public partial class LunaTable<T> where T : class
    {
        // NONE BY DEFAULT, which is what every table drew before §56 - and is also the better
        // default for the instrument panels this toolkit was built for, where a meter list wants to
        // read as a block rather than as a spreadsheet. A table of many narrow columns wants them;
        // a table of three does not.
        //
        // Drawn in LunaBorder, the token that already means "where one surface stops and the next
        // begins" (§26.9), rather than a colour of its own. A rule between cells is exactly that,
        // and it is already held to 3:1 against both surfaces.
        private LunaGridLines _gridLines;

        /// <summary>Which rules to draw between cells. None by default.</summary>
        public LunaGridLines GridLines
        {
            get => _gridLines;
            set
            {
                if (_gridLines == value) return;

                _gridLines = value;
                Show();
            }
        }

        // THE LIFECYCLE, AND WHY IT IS TWO EVENTS AND NOT FIVE. TreeDataGrid raises CellPrepared,
        // CellClearing, RowPrepared, RowClearing and CellValueChanged. The cell pair is not
        // reproducible here and saying so is better than approximating it: this control builds its
        // cells inside the row template rather than realising them independently, so there is no
        // moment at which a cell is prepared that is not simply "its row was prepared". A CellPrepared
        // that fired once per cell during RowPrepared would carry no information the row event does
        // not, while implying a virtualization boundary that is not there. §56.
        //
        // RowPrepared and RowClearing ARE real and are exactly what recycling makes worth having: a
        // caller attaching per-row state - a tooltip, a context menu, a colour from a live source -
        // needs to know when a container starts standing for a different model, and the container
        // is reused, so "when it was created" is the wrong hook and there is no other.
        /// <summary>Raised when a row's container is about to stand for a model, including when a recycled container is reused.</summary>
        public event Action<T, Control>? RowPrepared;

        /// <summary>Raised when a row's container stops standing for its model, before it is reused or dropped.</summary>
        public event Action<T, Control>? RowClearing;

        private Control Row(T? item, string scope)
        {
            var grid = new Grid();
            Define(grid, scope);

            if (item is null) return Ruled(grid);

            // THE GUTTER, AND IT IS NOT A CELL. No column marker goes on it, so it never answers to
            // Cell(item, n), never takes an editor and is not one of the things TryGetCell finds -
            // which is what keeps a caller's column indices meaning what they meant before there was
            // a gutter. Its width and its caption are the table's, not a column's, and it has no
            // resize grip because §27.11's remembered layout is a list of COLUMN widths. §58.
            if (_rowHeader is not null)
            {
                var gutter = new TextBlock
                {
                    Text = _rowHeader(item, PositionOf(item)),
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                gutter.Classes.Add("row-header");

                // Raw, because the row's own sentence already begins with this label and a reader
                // that met both would hear the address twice before reaching the first column. The
                // sort glyph is hidden for the same reason (§27.3), and MeterRow's inner bar for a
                // closely related one (§24.2).
                AutomationProperties.SetAccessibilityView(gutter, AccessibilityView.Raw);

                Grid.SetColumn(gutter, 0);
                grid.Children.Add(gutter);
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                // Nothing is built for a hidden column - not a zero-width control, nothing. A cell
                // that exists and cannot be seen still costs a measure pass per row per frame.
                if (!_columns[i].IsVisible) continue;

                // AND NOTHING IS BUILT FOR A COLUMN THE VIEWPORT CANNOT REACH, which is the same
                // sentence one feature over and is why §72 was cheap: the row already knew how to
                // leave a column out. Realized answers true for every column of every table that has
                // not asked for this, so the loop below is what it always was.
                //
                // NO GUARD FAILS IF THIS LINE GOES, AND THAT IS RECORDED RATHER THAN HIDDEN. The fill
                // would trim the row on the layout pass that follows, so the row is narrow either way
                // by the time anything can look at it - a test was written for this and deleted for
                // passing with the line removed. What it saves is WORK: without it a table scrolled
                // down through two hundred rows builds thirty cells for each and discards twenty-two,
                // which is most of what §72 exists to avoid. §72.5.
                if (!Realized(i)) continue;

                AddCell(grid, item, i, atFront: false);
            }

            AddFrozenEdge(grid);
            NameRow(grid, item);
            return Ruled(grid);
        }

        // ONE PLACE THAT DECIDES WHAT A CELL IS, and everything after it is the same for all three
        // kinds: the index marker, the column, the expander, the rule. A kind that needed a second
        // branch further down would be a kind that had leaked. §57.
        //
        // Called from two places since §72 - once per column while a row is being built, and once per
        // column that scrolls into view afterwards - which is exactly why it is a method rather than
        // the body of Row's loop. A second copy for the fill path is a second copy that would forget
        // the alignment, or the marker, or the rule.
        //
        // atFront IS ABOUT DRAWING ORDER AND NOT ABOUT COLUMNS. A Panel draws its children in the
        // order it holds them, and three things are deliberately drawn ON TOP of the cells: the
        // frozen edge, a cell-selection box and an open editor. Row() appends, and puts those three
        // in afterwards; the fill inserts at the front instead, so a cell arriving by a sideways
        // scroll goes UNDER them rather than over the box marking it selected. The rule follows its
        // own cell either way, so a template cell with a background cannot cover it.
        private void AddCell(Grid grid, T item, int index, bool atFront)
        {
            Control cell = _columns[index].Kind switch
            {
                LunaCellKind.Check => CheckCell(item, index),
                LunaCellKind.Template => TemplateCell(item, index),
                _ => TextCell(item, index),
            };

            NameCell(cell, item, index);

            // The expander column gets the indent and the toggle in front of its text; every other
            // column, and every column of a flat table, gets the bare cell exactly as before.
            Control placed = _children is not null && index == ExpanderColumn
                ? Expander(item, cell, index)
                : cell;

            // THE MARKER GOES ON THE CELL AND THE COLUMN GOES ON WHAT THE GRID HOLDS, and those are
            // two different controls whenever the expander wraps one. Grid reads its attached
            // properties off its own direct children, so a Grid.SetColumn left on the inner cell is a
            // number nothing ever reads - the wrapper takes the default of 0 and the row draws that
            // cell on top of column 0's. §66 has the measurement; it was invisible for as long as it
            // was, because every test set ExpanderColumn to 0 and 0 is the default.
            //
            // The marker stays on the cell on purpose: Cell() walks descendants to find it, so it is
            // reachable through the wrapper, and putting it on the wrapper instead would make a
            // template cell's own children answer for the column.
            Align(cell, index);

            TableCells.SetColumn(cell, index);
            Grid.SetColumn(placed, GridColumn(index));

            // And the OTHER marker, which says this child of the grid is here because of that column
            // and leaves with it. It goes on the wrapper rather than the cell for the same reason
            // Grid.SetColumn does - the wrapper is what the grid holds. §72.2.
            TableCells.SetOwner(placed, index);

            if (atFront) grid.Children.Insert(0, placed); else grid.Children.Add(placed);

            if (!_gridLines.HasFlag(LunaGridLines.Vertical) || index >= LastVisibleColumn) return;

            Control rule = ColumnRule();
            Grid.SetColumn(rule, GridColumn(index));
            TableCells.SetOwner(rule, index);

            if (atFront) grid.Children.Insert(1, rule); else grid.Children.Add(rule);
        }

        // The text cell, which is what every cell was before §57 - unchanged but for being one arm
        // of a switch rather than the whole of the loop.
        private TableCell TextCell(T item, int index)
        {
            var cell = new TableCell
            {
                Text = _columns[index].Text(item) ?? string.Empty,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,

                // Read through the projection so a reader always gets the model's current value,
                // and Write only where the column can be committed to - which is what makes
                // IValueProvider.IsReadOnly answer honestly per column. §50.6.
                Read = () => _columns[index].Text(item) ?? string.Empty,
                Write = _columns[index].IsEditable
                    ? text => SetFromAutomation(item, index, text)
                    : null,
            };

            // DOUBLE-CLICK OPENS THE EDITOR, and the handler goes on the CELL rather than on the
            // row because the cell is the only thing that knows which column was hit. Doing it
            // on the row would mean hit-testing the pointer's x against the column boundaries -
            // arithmetic that has to be kept in step with the Grid, to answer a question the
            // Grid has already answered by delivering the event here.
            //
            // Captured rather than looked up: `cell` and `item` in this closure are the live
            // visual and its model, so a recycled row's handler refers to that row's own cell.
            if (_columns[index].IsEditable)
            {
                cell.DoubleTapped += (_, e) =>
                {
                    if (!EditGestures.HasFlag(LunaEditGestures.DoubleTap)) return;

                    BeginEdit(item, index, cell);
                    e.Handled = true;
                };
            }

            return cell;
        }

        // A STOCK CheckBox AND NOT A CELL TYPE OF THIS TOOLKIT'S OWN, which is the same argument that
        // made a sortable heading a Button (§27.3): Avalonia's CheckBox already brings focus, Space,
        // a focus adorner and an IToggleProvider peer, and a hand-rolled tick would have to reproduce
        // all four and would forget two. The only things added here are the name and the gate.
        //
        // IsEnabled IS THE READ-ONLY MECHANISM, AND IT IS THE ONLY ONE THAT WORKS. Measured on
        // Avalonia 12.1.0: IToggleProvider.Toggle() throws ElementNotEnabledException on a disabled
        // control, and does NOT on one that is merely IsHitTestVisible=false - so the version that
        // kept full contrast by refusing the pointer would have left a read-only cell a screen reader
        // could still flip. That is the §50.6 defect exactly, and it was one design decision away.
        // The contrast cost is paid in FluentBridge.axaml instead. §57.3.
        private Control CheckCell(T item, int index)
        {
            ColumnSpec column = _columns[index];

            var box = new CheckBox
            {
                IsChecked = column.Checked?.Invoke(item) == true,
                IsEnabled = column.Toggle is not null,
                VerticalAlignment = VerticalAlignment.Center,

                // LEFT, NOT STRETCHED, and that is a behaviour rather than a look. A CheckBox
                // defaults to filling its slot, so in a column two hundred pixels wide the whole cell
                // becomes the toggle - and a user aiming at the row to select it ticks a box instead.
                // Left-aligned, the box is the target and the rest of the cell still selects the row.
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0),
            };

            box.Classes.Add("cell-check");

            // The header, because a checkbox with no content has nothing else to say what it is - a
            // reader landing on it would otherwise hear "checkbox, checked" with no column name. The
            // row's own sentence carries the whole row; this is what the cell says on its own.
            AutomationProperties.SetName(box, column.Header);

            box.IsCheckedChanged += (_, _) => Toggled(item, index, box);
            return box;
        }

        // Its own suppressor rather than _filling's, because the two guard different things: _filling
        // says "this selection is not the user's" and gates Chose, and this says "this tick is the
        // table putting the box back" and gates the caller's Toggle. Sharing one would mean a refresh
        // arriving mid-toggle swallowed the other's event.
        private readonly Suppressor _toggling = new();

        // THE MODEL IS THE TRUTH AND THE BOX IS A VIEW OF IT, which is why this writes and then reads
        // back rather than trusting what the user just clicked. Two things fall out of that and both
        // are wanted: a Toggle that normalises - one that turns three flags on together - shows what
        // it actually did, and a Toggle that REFUSES leaves the model alone and the tick returns to
        // where it was, with no separate veto mechanism to build. It is the same rule as Close
        // re-reading a committed cell through the projection instead of keeping the typed text.
        //
        // The suppressor is not optional: putting the box back raises IsCheckedChanged again, and
        // without it a refused toggle calls the caller's delegate forever.
        private void Toggled(T item, int index, CheckBox box)
        {
            if (_toggling.IsSuppressing) return;

            ColumnSpec column = _columns[index];
            if (column.Checked is not { } read) return;

            bool before = read(item);
            column.Toggle?.Invoke(item, box.IsChecked == true);
            bool after = read(item);

            using (_toggling.Suppress()) box.IsChecked = after;

            if (RowGridOf(box) is { } grid)
            {
                NameRow(grid, item);
                NameCells(grid, item);
            }

            // ONLY WHEN THE MODEL ACTUALLY MOVED. CellValueChanged says a value was committed, and a
            // refused toggle committed nothing - raising it anyway would make "changed" mean "was
            // clicked", which is a different event and one nobody asked for.
            if (before != after) CellValueChanged?.Invoke(item, index);
        }

        // WHATEVER THE CALLER BUILT, UNWRAPPED. No ContentControl around it and no Border: the
        // control the caller returned is the control in the row, so its own margins, alignment and
        // automation are what they look like at the call site rather than being negotiated with a
        // host this toolkit put in the way.
        //
        // A null return is an EMPTY cell rather than a throw, which is the same tolerance Text gets
        // two lines up (`?? string.Empty`). A build that has nothing to show for one row - no icon
        // for an unknown kind - should not have to invent a blank control.
        // A caller's own control, with one default applied to it - see docs/LunaP.md §69.
        //
        // DO NOT CENTRE SOMETHING THE CALLER SIZED, which is a narrower rule than the one this
        // started as and the width test is the whole of the difference.
        //
        // Avalonia's Stretch alignment does two different things depending on whether an element has
        // an explicit width. Without one it fills its slot - which is what a progress bar or a
        // coloured background in a cell wants, and is right. With one it CENTRES, so
        // `new Ellipse { Width = 8 }` in a 300px column sits 146 pixels in, beside a text cell and a
        // checkbox that both begin at zero. §57's CheckCell had already met the filling half and
        // written it down; this is the centring half, for the kind that was left out.
        //
        // The first version of this defaulted every template cell to Left and broke eight frozen-band
        // tests at once: their template cells are Borders with no width, which stretch to fill a
        // column and collapsed to nothing. That is a consumer's progress-bar cell vanishing, and the
        // suite caught it rather than a reader. §69.2.
        //
        // IsSet, so the caller still wins. A template column exists to let somebody put their own
        // control in a cell, and a toolkit that silently overruled an alignment they had written
        // would be worse than one that never touched it - their fix would look applied. This only
        // answers where there is no answer, the rule LunaAutomationPeer.GetNameCore follows for names.
        private Control TemplateCell(T item, int index)
        {
            Control cell = _columns[index].Build?.Invoke(item) ?? new Border();

            if (!cell.IsSet(Layoutable.HorizontalAlignmentProperty) && cell.IsSet(Layoutable.WidthProperty))
            {
                cell.HorizontalAlignment = HorizontalAlignment.Left;
            }

            return cell;
        }

        // ONE COLUMN'S ALIGNMENT, SPELLED IN EACH KIND'S OWN TERMS - see docs/LunaP.md §70.
        //
        // A text cell takes TEXT alignment and the other two take LAYOUT alignment, which is one
        // instruction rather than two rules. A TextBlock filling its column and drawing its text
        // right keeps the ellipsis it trims with; one shrunk to its content and pushed right loses
        // it, so the visible result of the "consistent" version would be a right-aligned column that
        // stops trimming (§27.4). Stretch has no meaning for text and is the one value that does
        // nothing here, because filling the cell is what a text cell already does.
        //
        // AFTER EACH KIND'S OWN DEFAULT AND BEFORE NOTHING, which is the precedence §69.2 set up:
        // a control that named its own alignment still wins, because Build ran first and this only
        // assigns what the column actually asked for. Null asks for nothing.
        private void Align(Control cell, int index)
        {
            ColumnSpec column = _columns[index];

            if (column.VerticalAlignment is { } down) cell.VerticalAlignment = down;
            if (column.Alignment is not { } across) return;

            if (cell is TableCell text)
            {
                if (TextAlign(across) is { } spelled) text.TextAlignment = spelled;
                return;
            }

            cell.HorizontalAlignment = across;
        }

        // The same instruction in a TextBlock's terms, and the ONE place that translation lives. The
        // heading needs it too (§70.2), and a rule with two owners is a rule that will disagree with
        // itself - which is §69.1's lesson, applied before it could happen rather than after.
        //
        // Null for Stretch and for a column that said nothing, because both mean "leave it": filling
        // the cell is what a text cell does already, and there is no text alignment that expresses it.
        private static Avalonia.Media.TextAlignment? TextAlign(HorizontalAlignment? across) => across switch
        {
            HorizontalAlignment.Left => Avalonia.Media.TextAlignment.Left,
            HorizontalAlignment.Center => Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment.Right => Avalonia.Media.TextAlignment.Right,
            _ => null,
        };

        // THE HORIZONTAL RULE IS ONE BORDER AROUND THE ROW, not a line per cell, because a rule
        // under a row is a property of the row - drawing it per cell would break wherever a column
        // is hidden and leave the gap unruled. The vertical rules ARE per cell, because that is
        // what a column boundary is.
        //
        // Returns the grid untouched when there are no lines, so a table that draws none has no
        // extra Border in its tree at all rather than a transparent one per row.
        private Control Ruled(Grid grid)
        {
            if (!_gridLines.HasFlag(LunaGridLines.Horizontal)) return grid;

            var ruled = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = grid,
            };

            // Styled by class in LunaTable.axaml rather than given a brush here, so a host theme can
            // restyle a rule the same way it restyles anything else (§12.2).
            ruled.Classes.Add("row-rule");
            return ruled;
        }

        // A SIBLING IN THE COLUMN, NOT A WRAPPER AROUND THE CELL, and that is load-bearing rather
        // than stylistic. Wrapping would make a cell's parent a Border, and Border is a Decorator
        // rather than a Panel - which is exactly what BeginEdit needs the parent to be in order to
        // put the editor in the cell's place (§55.7). A GridSplitter already sits in its column the
        // same way (§27.11), so this is the shape the control was already using.
        private static Control ColumnRule() 
        {
            var rule = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            rule.Classes.Add("column-rule");
            return rule;
        }
    }
}
