using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // TileStrip<T>'s keyboard and pointer - see docs/LunaP.md §95.2.
    public partial class TileStrip<T>
    {
        // One axis: Left and Right step, Home and End, a page by the whole tiles in view; Enter and Space open - see §95.2.
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

            int page = TilesInView;
            int? by = e.Key switch
            {
                Avalonia.Input.Key.Left => -1,
                Avalonia.Input.Key.Right => 1,
                Avalonia.Input.Key.PageUp => -page,
                Avalonia.Input.Key.PageDown => page,
                Avalonia.Input.Key.Home => int.MinValue / 2,
                Avalonia.Input.Key.End => int.MaxValue / 2,
                _ => null,
            };

            if (by is not { } step) return;
            e.Handled = true;
            Move(step);
        }

        // Either button selects on press, a gap changes nothing - TileGrid's rule (§88.2).
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
