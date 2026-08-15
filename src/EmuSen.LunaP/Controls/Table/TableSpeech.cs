using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // WHAT A SCREEN READER GETS FROM A TABLE - see docs/LunaP.md §68.
    //
    // FOUR THINGS GET A NAME AND THEY ARE NOT THE SAME NAME, which is the whole of why this is a
    // file rather than a method. A ROW says every value paired with its header, because a row of
    // bare TextBlocks announces as concatenated text with nothing to say which column each value
    // came from. A CELL says its column HEADER and never its value, because a name is how you refer
    // to a thing and what it currently says is its value - the two are different questions, and the
    // pattern (IValueProvider, IToggleProvider) carries the second one. A TEMPLATE cell is the
    // exception that made §68.3 necessary at all: a caller's own control has no pattern this control
    // can put a value into, so its sentence goes in ItemStatus. And the CONTROL reports what is
    // selected, in whichever unit the table is in.
    //
    // THE RENAMING IS THE PART THAT ROTS, and §68.4 is the record of it: a cell's name never
    // changes, but a template cell's STATUS is a projection, so one column's edit or toggle can
    // stale a different column's cell. Every write path therefore ends in NameRow + NameCells, and
    // the paths are the commit, the toggle, and the automation setter - three, not two, which is
    // what §68.4's sabotage missed the first time.
    public partial class LunaTable<T> where T : class
    {
        // WHAT A READER HEARS, and the reason it is built here rather than left to Avalonia. A row
        // of bare TextBlocks in a Grid announces as its concatenated text at best - "Site text 1" -
        // which is three values with nothing to say which column each came from. Pairing every value
        // with its header turns that into "name: Site, type: text, pg: 1", which is the information
        // a column layout is carrying visually. §27.3.
        //
        // ITS OWN METHOD SINCE §50, because a committed edit changes a value this sentence contains.
        // Built once at row construction, a reader would announce the old value for as long as the
        // row stayed realised - which is the whole of trap 3 in PLAN-table.md §6.
        //
        // Takes no grid, so ContainerPrepared can build one before the row has been laid out.
        private string Spoken(T item)
        {
            string cells = string.Join(", ", _columns
                .Where(c => c.IsVisible)
                .Select(c => $"{c.Header}: {c.Text(item) ?? string.Empty}"));

            // THE GUTTER GOES IN FRONT, because that is what a gutter is FOR: it is how the user
            // refers to the row - "line 12", "address 8040" - and a reader that heard it last, after
            // every cell, would have to hold the whole sentence to find out which row it was about.
            // Prefixed with the caption when there is one, so "addr 8040: op: LDA" says what the
            // number is; bare when there is not, because "1: name: alpha" is already unambiguous.
            if (_rowHeader is not null)
            {
                string label = _rowHeader(item, PositionOf(item)) ?? string.Empty;
                string prefix = string.IsNullOrEmpty(RowHeaderCaption)
                    ? label
                    : $"{RowHeaderCaption} {label}";

                cells = string.IsNullOrEmpty(cells) ? prefix : $"{prefix}: {cells}";
            }

            // A ROW THAT CAN BE OPENED SAYS SO, because in a tree "does this have more under it"
            // is part of what the row IS, and a reader that only hears the cells cannot tell a leaf
            // from a folder nobody has opened. Only for rows that actually have children - saying
            // "collapsed" about a leaf would be worse than saying nothing. §55.
            if (_children is null) return cells;

            object key = KeyOf(item);
            if (!_expandable.Contains(key)) return cells;

            return cells + (_expanded.Contains(key) ? ", expanded" : ", collapsed");
        }

        // SET ON THE CONTAINER AND NOT ONLY ON THE GRID, and that correction is §50.5.
        //
        // Until §50 this name went onto the row Grid alone. The Grid's peer is a NoneAutomationPeer
        // with IsControlElement = false - it is not in the view a screen reader navigates - so the
        // sentence was never reachable. What a reader actually got was the CONTAINER's name, and a
        // ListBoxItem with no name of its own falls back to its DataContext's ToString(): a reader
        // on the gallery's table heard "EmuSen.LunaP.Gallery.GalleryWindow+Field" three times.
        //
        // The old guard could not have caught it, because it read the attached property straight
        // back off the Grid rather than asking the peer - the §5.5 shape, an assertion about wiring
        // that passes while the effect is absent. It now asks the peer.
        //
        // The Grid keeps its name too. It costs nothing, it is what the existing test reads, and if
        // Avalonia ever puts row content into the control view the sentence is already there.
        private void NameRow(Grid grid, T item)
        {
            string sentence = Spoken(item);
            AutomationProperties.SetName(grid, sentence);

            // After a commit the grid IS parented, so the container can be renamed from here. On the
            // first build it is not, and ContainerPrepared does it instead.
            if (grid.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() is { } container)
            {
                AutomationProperties.SetName(container, sentence);
            }
        }

        // WHAT ONE CELL IS CALLED - see docs/LunaP.md §68.3.
        //
        // THE NAME IS THE HEADER AND NEVER THE VALUE, which is §57's rule for a check cell applied to
        // all three kinds rather than a new idea. A name is how you REFER to a thing; what it
        // currently says is its value, and the two are different questions - the same split
        // LunaAutomationPeer makes between GetNameCore and GetItemStatusCore, and for the reason
        // written there: a name that changed every time the model did would not be a name.
        //
        // So a reader landing on a cell hears "armed" and then the state from the pattern:
        // IToggleProvider.ToggleState for a check cell, IValueProvider.Value for a text one (§50.6).
        // Folding the value into the name would say it twice.
        //
        // A TEMPLATE CELL IS THE ONE THAT CANNOT DO THAT, and it is why this method exists at all. A
        // caller's own control carries whatever peer its type provides and there is no pattern this
        // control can put a value into - so the sentence §57.2 made mandatory goes in ItemStatus,
        // which is exactly the field for state that is not the value and not the name. Without it a
        // coloured dot is a cell a reader can land on and learn nothing from, which is the thing
        // §57.2 required `spoken` to prevent and only half delivered: it reached the row's sentence
        // and never the cell.
        //
        // ON THE CONTROL AND NOT IN A PEER, which is what makes it work for all three kinds. A check
        // cell is a stock CheckBox and a template cell is a caller's own control - neither can be
        // given a peer by this control - but these are attached properties, which is Avalonia's own
        // answer to annotating something you did not write. The same technique §57.5 used for the
        // column marker, two properties along.
        private void NameCell(Control cell, T item, int column)
        {
            ColumnSpec spec = _columns[column];
            AutomationProperties.SetName(cell, spec.Header);

            if (spec.Kind != LunaCellKind.Template) return;

            AutomationProperties.SetItemStatus(cell, spec.Text(item) ?? string.Empty);
        }

        // EVERY CELL OF ONE ROW, RE-READ - the cell-level half of what NameRow does, and it exists
        // because of what the naming rule above implies. A cell's NAME is its column header and never
        // moves, so nothing needs renaming after a change; a template cell's STATUS is a projection of
        // the model, and one column's edit or toggle can change what a different column projects.
        //
        // Found by sabotage: taking the rename out of the commit path turned nothing red, because
        // there was nothing there worth doing. What was missing was this - a check column and a
        // template column reading the same field, where ticking the box left the dot beside it
        // describing the value it used to have. §68.4.
        //
        // Walks the grid rather than taking a column, because the cell that goes stale is not the one
        // that changed.
        private void NameCells(Grid grid, T item)
        {
            foreach (Control child in grid.GetVisualDescendants().OfType<Control>())
            {
                int column = TableCells.GetColumn(child);
                if (column >= 0 && column < _columns.Count) NameCell(child, item, column);
            }
        }

        // WHAT A READER IS TOLD IS SELECTED - see docs/LunaP.md §68.
        //
        // Peers of the selected CELLS in a cell unit and of the selected ROWS in a row unit, which is
        // the same answer SelectedCells and SelectedItems give in each - a third notion of "what is
        // selected" reachable only through automation would be a third thing to keep in step.
        //
        // Returning the control's OWN peer is what makes this work for all three cell kinds without
        // wrapping anything: a check cell hands back Avalonia's CheckBox peer with its IToggleProvider
        // intact, and a template cell hands back whatever the caller's control provides (§68.2).
        private IReadOnlyList<AutomationPeer> SelectionPeers()
        {
            var peers = new List<AutomationPeer>();

            if (_selectionUnit == LunaSelectionUnit.Cell)
            {
                foreach (LunaCell<T> at in SelectedCells)
                {
                    if (Cell(at.Row, at.Column) is { } cell) peers.Add(ControlAutomationPeer.CreatePeerForElement(cell));
                }

                return peers;
            }

            foreach (T row in SelectedItems)
            {
                if (TryGetRow(row, out Control? container) && container is not null)
                {
                    peers.Add(ControlAutomationPeer.CreatePeerForElement(container));
                }
            }

            return peers;
        }

        // A DataGrid since §68, where the base is still a Group. The type is claimed here rather than
        // there because it is only true once there are columns to be a grid of - and it is claimed at
        // all because §27.3's reason for refusing it does not hold in this framework: Avalonia has no
        // IGridProvider and no ITableProvider for it to be a false promise about, and TreeDataGrid -
        // the control this is at parity with - returns DataGrid from its own peer with exactly the two
        // providers below. §68.1.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaTablePeer(
                this,
                () => _selectionMode == LunaSelectionMode.Multiple,
                SelectionPeers,
                OwnViewer);
    }
}
