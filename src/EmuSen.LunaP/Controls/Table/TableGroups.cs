using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // Group headings that ride on the first row of their group, never rows of their own - see docs/LunaP.md §211.1.
    public partial class LunaTable<T> where T : class
    {
        /// <summary>The style class on a row's heading band.</summary>
        public const string GroupHeadingClass = "group-heading";

        /// <summary>The style class on the part of a grouped row below its heading band, which a look paints as the row.</summary>
        public const string GroupRowClass = "group-row";

        private Func<T, string?>? _groupOf;
        private string?[] _headings = Array.Empty<string?>();
        private readonly Dictionary<object, string> _headingOf = new();

        /// <summary>The group a row is shown under; a heading is drawn above a row whose group differs from the row before it, while the rows shown hold more than one group. Null, the default, draws none.</summary>
        public Func<T, string?>? GroupOf
        {
            get => _groupOf;
            set
            {
                _groupOf = value;
                Rebuild();
                Show();
            }
        }

        /// <summary>The heading drawn above the shown row at an index, or null where none is.</summary>
        /// <param name="index">The row's index in Models.</param>
        /// <returns>The heading's words as drawn, or null.</returns>
        public string? HeadingAt(int index) => index >= 0 && index < _headings.Length ? _headings[index] : null;

        // Worked out once per display, so a prepared container only looks its heading up.
        private void FindHeadings()
        {
            _headingOf.Clear();
            if (_groupOf is null)
            {
                _headings = Array.Empty<string?>();
                return;
            }

            string?[] groups = _view.Select(_groupOf).ToArray();
            bool several = groups.Where(g => !string.IsNullOrEmpty(g)).Distinct(StringComparer.Ordinal).Skip(1).Any();
            _headings = new string?[groups.Length];
            for (int i = 0; i < groups.Length && several; i++)
            {
                if (!string.IsNullOrEmpty(groups[i]) && (i == 0 || groups[i] != groups[i - 1])) _headings[i] = groups[i]!.ToUpperInvariant();
                if (_headings[i] is { } h && Key(_view[i]) is { } key) _headingOf.TryAdd(key, h);
            }
        }

        // The band above the row and the row in its own part, built only for a table that groups.
        private Control Grouped(Control row, T? item)
        {
            if (_groupOf is null) return row;
            string? words = item is not null && Key(item) is { } key && _headingOf.TryGetValue(key, out string? h) ? h : null;
            var heading = new TextBlock { Text = words, IsVisible = words is not null, VerticalAlignment = VerticalAlignment.Top };
            heading.Classes.Add(GroupHeadingClass);
            var body = new Border { Child = row };
            body.Classes.Add(GroupRowClass);
            DockPanel.SetDock(heading, Dock.Top);
            return new DockPanel { LastChildFill = true, Children = { heading, body } };
        }

        // A container prepared for a model shows that model's heading, recycled or not, and is named with it.
        private void ShowHeading(Control container, T model)
        {
            if (_groupOf is null) return;
            string? heading = Key(model) is { } key && _headingOf.TryGetValue(key, out string? h) ? h : null;
            if (container.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains(GroupHeadingClass)) is { } band)
            {
                band.Text = heading;
                band.IsVisible = heading is not null;
            }

            if (heading is not null && AutomationProperties.GetName(container) is { Length: > 0 } name)
                AutomationProperties.SetName(container, $"{heading}: {name}");
        }
    }
}
