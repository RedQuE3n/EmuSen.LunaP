using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // OpenEmu's sidebar: groups under small capital headings, rows of text with a count at the
    // right - see docs/LunaP.md §88.3.
    //
    // NOT A ListBox OF MIXED ROWS, which was the first shape considered and is refused for what it
    // does to selection. A ListBox selects containers, so a heading would be a selectable item that
    // every consumer then had to un-select, skip on arrow keys and hide from a reader - three
    // corrections to a control in order to make it not do something. Built from panels, a heading
    // is simply not a row: it has no selected state, the keyboard walks the rows, and the tree a
    // reader walks is list, group, item, which is what the picture is.
    //
    // HELD BY KEY, reported by key. Chose carries the key and not the item, because the key is what
    // a host switches on ("console:n64", "collection:favourites") and the text is what it
    // localises; a sidebar whose selection was a display string would be one a translation broke.
    //
    // The same selection contract as LunaList and TileGrid<T>: Fill never raises Chose, only a
    // person does (§22.9, §78).
    /// <summary>A sidebar of grouped, selectable rows under small capital headings, each row with optional right-aligned badge text such as a count.</summary>
    public class SourceList : TemplatedControl
    {
        private IReadOnlyList<SourceListGroup> _groups = Array.Empty<SourceListGroup>();
        private readonly List<SourceListRow> _rows = new();
        private StackPanel? _host;
        private string? _selectedKey;

        public SourceList()
        {
            Focusable = true;
        }

        /// <summary>The key of the selected row, or null when nothing is selected.</summary>
        public string? SelectedKey => _selectedKey;

        /// <summary>Raised with the row's key when the user selects a different row, by pointer, keyboard or assistive technology. Not raised by Fill.</summary>
        public event Action<string>? Chose;

        /// <summary>Replaces every group and row, and the selection, without raising Chose.</summary>
        /// <param name="groups">The groups in display order. Copied, so later changes to the collections are not seen until the next Fill.</param>
        /// <param name="selectedKey">The key to select. Null, or a key no row has, leaves nothing selected. Safe to call before the control has a template.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="groups"/> is null.</exception>
        public void Fill(IEnumerable<SourceListGroup> groups, string? selectedKey)
        {
            if (groups is null) throw new ArgumentNullException(nameof(groups));

            _groups = groups.Select(g => g with { Items = g.Items.ToArray() }).ToArray();

            // A key that names no row is not kept as a pending wish: SelectedKey would then report a
            // row nobody can see, and the first arrow key would have nowhere to start from.
            _selectedKey = _groups.SelectMany(g => g.Items).Any(i => i.Key == selectedKey) ? selectedKey : null;

            Build();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _host = e.NameScope.Find<StackPanel>("PART_Rows");
            Build();
        }

        private void Build()
        {
            _rows.Clear();
            if (_host is null) return;

            _host.Children.Clear();
            foreach (SourceListGroup group in _groups)
            {
                var view = new SourceListGroupView(group.Header);
                foreach (SourceListItem item in group.Items)
                {
                    var row = new SourceListRow(item, () => _selectedKey, ChooseByUser, this);
                    row.SetSelected(item.Key == _selectedKey);
                    view.Children.Add(row);
                    _rows.Add(row);
                }

                _host.Children.Add(view);
            }
        }

        private void ChooseByUser(string key)
        {
            if (key == _selectedKey || _rows.All(r => r.Item.Key != key)) return;

            _selectedKey = key;
            foreach (SourceListRow row in _rows)
            {
                row.SetSelected(row.Item.Key == key);
                if (row.Item.Key == key) row.BringIntoView();
            }

            Chose?.Invoke(key);
        }

        // Up and Down walk the rows and never land on a heading, because headings are not rows. Home
        // and End go to the first and last row. An arrow with nothing selected starts at the top.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || _rows.Count == 0) return;

            int at = _rows.FindIndex(r => r.Item.Key == _selectedKey);
            int? target = e.Key switch
            {
                Key.Up => at < 0 ? 0 : Math.Max(0, at - 1),
                Key.Down => at < 0 ? 0 : Math.Min(_rows.Count - 1, at + 1),
                Key.Home => 0,
                Key.End => _rows.Count - 1,
                _ => null,
            };

            if (target is not { } index) return;

            e.Handled = true;
            ChooseByUser(_rows[index].Item.Key);
        }

        // Either button, for the reason TileGrid<T> gives: a context menu on the sidebar should find
        // the row it was opened over already selected.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            PointerPointProperties button = e.GetCurrentPoint(this).Properties;
            if (!button.IsLeftButtonPressed && !button.IsRightButtonPressed) return;

            Focus(NavigationMethod.Pointer);
            if ((e.Source as Avalonia.Visual)?.FindAncestorOfType<SourceListRow>(includeSelf: true) is { } row) ChooseByUser(row.Item.Key);
        }

        // A List, since that is what a reader navigates a sidebar as; the groups inside it carry the
        // headings as their names.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SingleSelectionPeer(this, AutomationControlType.List, () =>
                _rows.Where(r => r.Item.Key == _selectedKey)
                     .Select(r => ControlAutomationPeer.CreatePeerForElement(r))
                     .ToArray());
    }
}
