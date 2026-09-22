using System;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // The two pieces a SourceList is built from in code - see docs/LunaP.md §88.3. Internal because a
    // host reaches them by class (`source-row`, `source-header`, `source-badge`), which is the
    // vocabulary SourceList.axaml styles and the one a host's own styles can name.

    // A group as its own panel rather than a header followed by rows in one flat stack, so the
    // automation tree has the shape a reader expects: a list, then a named group, then its items.
    internal sealed class SourceListGroupView : StackPanel
    {
        internal SourceListGroupView(string header)
        {
            Header = header;
            Classes.Add("source-group");

            // Capitals are drawn, not stored: Avalonia's TextBlock has no text-transform, so the
            // shown string is upper-cased here and the heading is hidden from automation, leaving
            // the group to be announced by the header as the host wrote it rather than letter by
            // letter as an acronym.
            var title = new TextBlock { Text = header.ToUpperInvariant() };
            title.Classes.Add("source-header");
            AutomationProperties.SetAccessibilityView(title, AccessibilityView.Raw);
            Children.Add(title);
        }

        internal string Header { get; }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group, name: () => Header);
    }

    // One row: text on the left, the badge on the right, 24 pixels high from the theme.
    internal sealed class SourceListRow : Border
    {
        internal SourceListRow(SourceListItem item, Func<string?> selectedKey, Action<string> choose, Control list)
        {
            Item = item;
            Classes.Add("source-row");

            var text = new TextBlock
            {
                Text = item.Text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.Classes.Add("source-text");

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(text);

            if (!string.IsNullOrEmpty(item.Badge))
            {
                var count = new TextBlock { Text = item.Badge, VerticalAlignment = VerticalAlignment.Center };
                count.Classes.Add("source-badge-text");

                var badge = new Border { Child = count, VerticalAlignment = VerticalAlignment.Center };
                badge.Classes.Add("source-badge");
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }

            Child = grid;

            _peer = () => new SelectableItemPeer(
                this,
                () => Item.Text,
                () => Item.Key == selectedKey(),
                () => choose(Item.Key),
                () => list,
                status: () => Item.Badge);
        }

        private readonly Func<AutomationPeer> _peer;

        internal SourceListItem Item { get; }

        internal void SetSelected(bool selected) => Classes.Set("selected", selected);

        // A ListItem named by its text, with the badge as its item status: "Nintendo 64, 38" rather
        // than a name that changes every time a game is added.
        protected override AutomationPeer OnCreateAutomationPeer() => _peer();
    }
}
