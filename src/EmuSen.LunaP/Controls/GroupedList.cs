using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // A long list of models under group headings, each row a name, a line of detail and a badge; headings ride on the first row of their group, so they are never rows - see docs/LunaP.md §94.1.
    /// <summary>A virtualised single-selection list of models under group headings, each row showing a name, an optional line of detail and an optional badge.</summary>
    public class GroupedList<T> : ListBox where T : class
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        /// <summary>The style class this control adds to itself, so a selector can reach it despite its style key being ListBox.</summary>
        public const string StyleClass = "luna-grouped-list";

        private readonly Suppressor _filling = new();
        private IReadOnlyList<T> _items = Array.Empty<T>();

        public GroupedList()
        {
            Classes.Add(StyleClass);
            // A long list's bar stays full width, so its thumb can be seen and taken - see docs/LunaP.md §96.
            ScrollViewer.SetAllowAutoHide(this, false);
            ItemTemplate = new FuncDataTemplate<GroupedListEntry>((entry, _) => entry is null ? null : new GroupedListRow(entry), supportsRecycling: false);
            SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(Selected);
            };
        }

        /// <summary>The group a model is shown under. A heading is drawn above each row whose group differs from the row before it, so models should arrive grouped.</summary>
        public Func<T, string> Group { get; set; } = _ => string.Empty;

        /// <summary>The name a row shows, wrapped rather than cut short.</summary>
        public Func<T, string> Label { get; set; } = item => item?.ToString() ?? string.Empty;

        /// <summary>A second, muted line under the name, or null for none.</summary>
        public Func<T, string?> Detail { get; set; } = _ => null;

        /// <summary>Short text in a pill at the row's right, such as "In use", or null for none.</summary>
        public Func<T, string?> Badge { get; set; } = _ => null;

        /// <summary>How a row is matched to a model across a Refresh, so a selection survives a rebuild.</summary>
        /// <remarks>Unless set, the item itself, which is reference identity for a class: give a stable key when the models are rebuilt rather than reused.</remarks>
        public Func<T, object?> Key { get; set; } = item => item;

        /// <summary>Whether a heading shows how many rows its group has in the current list. True by default.</summary>
        public bool ShowGroupCounts { get; set; } = true;

        /// <summary>Raised when the selection changes to a different row, with the model. A selection, not an activation: for double-click or Enter, handle DoubleTapped or KeyDown. Not raised by Refresh or Select.</summary>
        public event Action<T?>? Chose;

        /// <summary>The models currently shown, in order.</summary>
        public IReadOnlyList<T> Models => _items;

        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected
        {
            get
            {
                int index = SelectedIndex;
                return index >= 0 && index < _items.Count ? _items[index] : null;
            }
        }

        /// <summary>Replaces every row, keeping the selection if Key still matches something, and the keyboard focus on that row if a row had it.</summary>
        /// <param name="items">The new models, in display order, grouped. Safe to call before the control has a template.</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
        public void Refresh(IEnumerable<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            bool focused = IsKeyboardFocusWithin;
            object? wasSelected = Selected is { } previous ? Key(previous) : null;
            _items = items.ToList();

            var groups = _items.Select(Group).ToArray();
            var counts = groups.GroupBy(g => g, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            var entries = new GroupedListEntry[_items.Count];
            for (int i = 0; i < _items.Count; i++)
            {
                bool opens = i == 0 || !string.Equals(groups[i], groups[i - 1], StringComparison.Ordinal);
                string? heading = opens && groups[i].Length > 0 ? groups[i] : null;
                string? count = heading is not null && ShowGroupCounts ? counts[groups[i]].ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                entries[i] = new GroupedListEntry(heading, count, Label(_items[i]), Detail(_items[i]), Badge(_items[i]));
            }

            using (_filling.Suppress())
            {
                ItemsSource = entries;
                SelectedIndex = wasSelected is null ? -1 : _items.FindIndex(item => Equals(Key(item), wasSelected));
            }
            if (focused) KeepFocus();
        }

        // The focused row's container went with the old rows, which left the focus nowhere; it goes to the kept row, else the first, unselected - see docs/LunaP.md §94.5.
        private void KeepFocus()
        {
            int index = SelectedIndex >= 0 ? SelectedIndex : _items.Count > 0 ? 0 : -1;
            if (index < 0) { Focus(NavigationMethod.Unspecified); return; }
            ScrollIntoView(index);
            UpdateLayout();
            ContainerFromIndex(index)?.Focus(NavigationMethod.Unspecified);
        }

        /// <summary>Selects a model without raising Chose, and scrolls it into view once there is a view.</summary>
        /// <param name="item">The model to select, matched by Key. Null clears the selection. Safe to call before the control has a template.</param>
        public void Select(T? item)
        {
            using (_filling.Suppress())
            {
                SelectedIndex = item is null ? -1 : _items.FindIndex(candidate => Equals(Key(candidate), Key(item)));
            }
            if (SelectedIndex >= 0) ScrollIntoView(SelectedIndex);
        }

        // A row's accessible name is its label, since its content is an entry and not a string.
        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);
            if (item is GroupedListEntry entry) AutomationProperties.SetName(container, entry.Heading is null ? entry.Text : $"{entry.Heading}: {entry.Text}");
        }
    }

    // What one row draws; ToString is the label, so a row read as text reads as its name.
    internal sealed record GroupedListEntry(string? Heading, string? Count, string Text, string? Detail, string? Badge)
    {
        public override string ToString() => Text;
    }

    // A heading when the row opens a group, then the row itself, painted by class from Theme/Controls/GroupedList.axaml - see docs/LunaP.md §94.1.
    internal sealed class GroupedListRow : StackPanel
    {
        public GroupedListRow(GroupedListEntry entry)
        {
            if (entry.Heading is not null)
            {
                var heading = new DockPanel { LastChildFill = true };
                heading.Classes.Add("grouped-header");
                if (entry.Count is not null)
                {
                    var count = new TextBlock { Text = entry.Count };
                    count.Classes.Add("grouped-count");
                    DockPanel.SetDock(count, Dock.Right);
                    heading.Children.Add(count);
                }
                var title = new TextBlock { Text = entry.Heading.ToUpperInvariant(), TextWrapping = TextWrapping.Wrap };
                title.Classes.Add("grouped-heading");
                heading.Children.Add(title);
                Children.Add(heading);
            }

            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap };
            name.Classes.Add("grouped-text");
            text.Children.Add(name);
            if (!string.IsNullOrEmpty(entry.Detail))
            {
                var detail = new TextBlock { Text = entry.Detail, TextWrapping = TextWrapping.Wrap };
                detail.Classes.Add("grouped-detail");
                text.Children.Add(detail);
            }

            var line = new DockPanel { LastChildFill = true };
            if (!string.IsNullOrEmpty(entry.Badge))
            {
                var badge = new Border { Child = new TextBlock { Text = entry.Badge, Classes = { "grouped-badge-text" } }, VerticalAlignment = VerticalAlignment.Center };
                badge.Classes.Add("grouped-badge");
                DockPanel.SetDock(badge, Dock.Right);
                line.Children.Add(badge);
            }
            line.Children.Add(text);

            var row = new Border { Child = line };
            row.Classes.Add("grouped-row");
            Children.Add(row);
        }
    }
}
