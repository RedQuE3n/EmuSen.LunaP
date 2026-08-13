using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // A list that keeps hold of the type it was given - see docs/LunaP.md §22.9.
    //
    // Five places project a model to a string, put the strings in a ListBox, and keep a parallel
    // array to map the selected index back. One of them then parses the label apart again to
    // recover a field it already had:
    //
    //     SystemsList.ItemsSource = systems.Select(s => $"{s.System}  ({s.Count})").ToList();
    //     // The list label carries its own count, so the name has to come back off it
    //
    // Parsing a display string to recover a model field is the shape of a missing control.
    //
    // TAKES A PROJECTION, NOT AN INTERFACE. §1's rule is that a control takes plain data or a
    // delegate - a meter row takes (string, double, string), never a DebugLoadInfo - and a list
    // that demanded an IListItem would be a list only an application that had adopted LunaP's
    // vocabulary could use. Label and Key are Funcs for the same reason.
    public class LunaList<T> : ListBox where T : class
    {
        // ListBox's own theme, not a subclass's: a control's style key defaults to its runtime type,
        // so without this the Fluent ControlTheme never reaches this class and the control renders
        // with no template and no items - §5.5 and §14.1 record that trap twice, the second time in
        // the form where it throws rather than degrading to blank.
        protected override Type StyleKeyOverride => typeof(ListBox);

        // A style key spent is a type a selector can no longer name (§30), so every control
        // that pins one publishes the class that names it instead. Uniform rather than
        // added-when-needed: the class costs nothing, and the day this control gains a style
        // file or a CSS element name the selector already has something to match. Enforced by
        // StyleKeyTests, which is why this cannot be forgotten on the next one.
        public const string StyleClass = "luna-list";

        private readonly Suppressor _filling = new();
        private IReadOnlyList<T> _items = Array.Empty<T>();

        // What each row reads as. Defaults to ToString(), so a list of strings needs no ceremony.
        public Func<T, string> Label { get; set; } = item => item?.ToString() ?? "";

        // What makes two items "the same item" across a refresh. Defaults to reference identity,
        // which is right for a cached model and wrong for rows rebuilt from a database on every
        // poll - those need a real key, and the whole point of Refresh is that it then works.
        public Func<T, object?> Key { get; set; } = item => item;

        // Raised only for a real user choice, never for the selection restored during a refresh -
        // the same distinction Dropdown.Chose draws, and for the same reason.
        public event Action<T?>? Chose;

        public LunaList()
        {
            Classes.Add(StyleClass);
            SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(Selected);
            };
        }

        // NOT `Items`, which would shadow ItemsControl.Items and leave two properties of the same
        // name meaning different things - the rows on one, the models on the other - depending on
        // which type the caller happens to be holding.
        public IReadOnlyList<T> Models => _items;

        // The selected model, not the row. This is the whole point: no shadow array, no index
        // arithmetic, and nothing to parse back out of a label.
        public T? Selected
        {
            get
            {
                int index = SelectedIndex;
                return index >= 0 && index < _items.Count ? _items[index] : null;
            }
        }

        // Replaces the contents AND PUTS THE SELECTION BACK, which is the second half of what the
        // hand-rolled versions were doing:
        //
        //     let chosen = roster.SelectedItem |> ...
        //     roster.ItemsSource <- peers |> Array.map ...
        //     // Losing it every time somebody else signs in would make the Open button unusable
        //
        // Three sites wrote that dance separately (§21.1's A3). It belongs here rather than in a
        // helper beside them, because "rebuild the list" and "keep the selection" are one operation
        // that only looks like two.
        public void Refresh(IEnumerable<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            object? wasSelected = Selected is { } previous ? Key(previous) : null;

            _items = items.ToList();

            using (_filling.Suppress())
            {
                ItemsSource = _items.Select(Label).ToList();

                // -1 when the previously selected item is gone, which is a real answer: the row
                // it named no longer exists, and selecting its neighbour would be a guess.
                SelectedIndex = wasSelected is null
                    ? -1
                    : _items.FindIndex(item => Equals(Key(item), wasSelected));
            }
        }

        // Selects by model rather than by index. Does not raise Chose - a caller setting the
        // selection already knows what it set.
        public void Select(T? item)
        {
            using (_filling.Suppress())
            {
                SelectedIndex = item is null ? -1 : _items.FindIndex(candidate => Equals(Key(candidate), Key(item)));
            }
        }
    }

    internal static class ListExtensions
    {
        // List<T>.FindIndex over an IReadOnlyList, so Refresh does not care what it was handed.
        internal static int FindIndex<T>(this IReadOnlyList<T> items, Func<T, bool> match)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (match(items[i])) return i;
            }

            return -1;
        }
    }
}
