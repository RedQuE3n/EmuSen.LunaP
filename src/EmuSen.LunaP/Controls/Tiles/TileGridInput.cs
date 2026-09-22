using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // TileGrid<T>'s keyboard and pointer: how a person changes the selection and opens a tile - see
    // docs/LunaP.md §88.2.
    public partial class TileGrid<T>
    {
        // Two-dimensional, because the grid is: Up and Down move a whole row, which is Columns at
        // the current width - the same number the layout used, so a key never lands on a tile the
        // eye did not expect. Left and Right step through the list and so wrap between rows, as
        // OpenEmu's grid and every file browser's icon view do.
        //
        // Down on the last full row goes to the last tile when a shorter row is below, and stays put
        // when there is none; Up on the first row stays put. An arrow with nothing selected selects
        // the first tile, which is where a keyboard user expects to start.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || _items.Count == 0) return;

            if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space)
            {
                if (Selected is { } open)
                {
                    e.Handled = true;
                    Activated?.Invoke(open);
                }

                return;
            }

            int columns = LastLayout.Columns;
            int last = _items.Count - 1;
            int at = _selectedIndex;
            int page = columns * LastLayout.RowsIn(ViewportHeight);

            int? target = e.Key switch
            {
                _ when at < 0 && e.Key is Avalonia.Input.Key.Left or Avalonia.Input.Key.Right or Avalonia.Input.Key.Up or Avalonia.Input.Key.Down or Avalonia.Input.Key.PageUp or Avalonia.Input.Key.PageDown => 0,
                Avalonia.Input.Key.Left => at > 0 ? at - 1 : at,
                Avalonia.Input.Key.Right => at < last ? at + 1 : at,
                Avalonia.Input.Key.Up => at - columns >= 0 ? at - columns : at,
                Avalonia.Input.Key.Down => at + columns <= last ? at + columns : (last / columns > at / columns ? last : at),
                Avalonia.Input.Key.Home => 0,
                Avalonia.Input.Key.End => last,
                Avalonia.Input.Key.PageUp => System.Math.Max(0, at - page),
                Avalonia.Input.Key.PageDown => System.Math.Min(last, at + page),
                _ => (int?)null,
            };

            if (target is not { } index) return;

            e.Handled = true;
            ChooseByUser(index);
        }

        // Both buttons select. The right one does so FIRST, before the context menu opens on its
        // release, so a host's ContextMenu on the grid reads Selected and finds the tile that was
        // clicked rather than whichever one was selected before. Not marked handled, so the menu and
        // the double-tap gesture both still see the press.
        //
        // A press on the gaps between tiles changes nothing. OpenEmu clears the selection there, and
        // this does not: Chose carries a T, not a T?, so a cleared selection is a change the event
        // could not report, and a host would be left holding a tile that is no longer chosen.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            PointerPointProperties button = e.GetCurrentPoint(this).Properties;
            if (!button.IsLeftButtonPressed && !button.IsRightButtonPressed) return;

            Focus(NavigationMethod.Pointer);

            if (TileUnder(e.Source) is { Index: >= 0 } tile) ChooseByUser(tile.Index);
        }

        private void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (TileUnder(e.Source) is not { Index: >= 0 } tile || tile.Index >= _items.Count) return;

            e.Handled = true;
            Activated?.Invoke(_items[tile.Index]);
        }

        private static TileGridItem? TileUnder(object? source) =>
            (source as Visual)?.FindAncestorOfType<TileGridItem>(includeSelf: true);
    }
}
