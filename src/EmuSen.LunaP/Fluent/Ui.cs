using System;
using Avalonia.Controls;
using Avalonia.Layout;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Fluent
{
    // Terse constructors for the layouts and kit controls a window is made of - see docs/LunaP.md §9.
    /// <summary>Terse constructors for the layouts and kit controls a window is built from.</summary>
    public static class Ui
    {
        /// <summary>Stacks children vertically with no gap between them.</summary>
        /// <param name="children">The children, top to bottom.</param>
        /// <returns>A vertical StackPanel holding them.</returns>
        public static StackPanel Stack(params Control[] children) => Stack(0, children);

        /// <summary>Stacks children vertically with a gap between them.</summary>
        /// <param name="spacing">The gap between children, not added above the first or below the last.</param>
        /// <param name="children">The children, top to bottom.</param>
        /// <returns>A vertical StackPanel holding them.</returns>
        public static StackPanel Stack(double spacing, params Control[] children)
        {
            var panel = new StackPanel { Spacing = spacing };
            foreach (Control child in children) panel.Children.Add(child);
            return panel;
        }

        /// <summary>Lays children out left to right with no gap between them.</summary>
        /// <param name="children">The children, left to right.</param>
        /// <returns>A horizontal StackPanel holding them.</returns>
        public static StackPanel Row(params Control[] children) => Row(0, children);

        /// <summary>Lays children out left to right with a gap between them.</summary>
        /// <param name="spacing">The gap between children.</param>
        /// <param name="children">The children, left to right.</param>
        /// <returns>A horizontal StackPanel holding them.</returns>
        public static StackPanel Row(double spacing, params Control[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
            foreach (Control child in children) panel.Children.Add(child);
            return panel;
        }

        // Last child fills, as DockPanel already does; the rest carry .Dock(...).
        /// <summary>Docks children to the edges, with the last one filling what is left.</summary>
        /// <param name="children">The children. Give each an edge with the Dock fluent setter; the LAST child takes the remaining space, which is how DockPanel behaves and the usual source of surprise.</param>
        /// <returns>A DockPanel holding them.</returns>
        public static DockPanel Dock(params Control[] children)
        {
            var panel = new DockPanel();
            foreach (Control child in children) panel.Children.Add(child);
            return panel;
        }

        // Columns are assigned by position, so the Grid.SetColumn chore disappears - an explicit .AtColumn() still wins.
        //
        // THE DEFINITIONS ARE ADDED, NOT ASSIGNED, and it is worth a line because the two read as
        // the same thing and are not. Avalonia 12.1.0 registers a definition with its shared size
        // scope on Add and not on assignment, so a grid handed a ready-made ColumnDefinitions shares
        // nothing no matter what group its definitions name. Nothing here is broken by that today -
        // `definitions` is a comma-separated string with no syntax for a SharedSizeGroup, so no
        // caller of Cols is trying to share anything.
        //
        // It is written this way because of where the use would arise. Rows below cites §21.2, "a
        // header-and-body table ends up keeping two column strings in step by hand" - which is the
        // problem shared sizing solves, so the first person to reach for it reaches for it here.
        // They would set a group on the returned grid's definitions and watch it do nothing. §27.7.
        /// <summary>Puts children in a single row of columns.</summary>
        /// <param name="definitions">Avalonia column widths, comma separated - "Auto,*,120" and so on.</param>
        /// <param name="children">The children. Each is placed in the next column by position unless it already carries an explicit column, which lets one child span while the rest fall where they are written.</param>
        /// <returns>A Grid with those columns.</returns>
        public static Grid Cols(string definitions, params Control[] children)
        {
            var grid = new Grid();
            foreach (ColumnDefinition definition in new ColumnDefinitions(definitions))
            {
                grid.ColumnDefinitions.Add(definition);
            }

            for (int i = 0; i < children.Length; i++)
            {
                Control child = children[i];
                if (!child.IsSet(Grid.ColumnProperty)) Grid.SetColumn(child, i);
                grid.Children.Add(child);
            }

            return grid;
        }

        // Rows are assigned by position exactly as Cols assigns columns, and an explicit .AtRow()
        // still wins. Only the column half existed, which is why a header-and-body table ends up
        // keeping two column strings in step by hand instead - see docs/LunaP.md §21.2.
        //
        // Added rather than assigned, for the reason spelled out on Cols above.
        /// <summary>Puts children in a single column of rows.</summary>
        /// <param name="definitions">Avalonia row heights, comma separated - "Auto,*,Auto" and so on.</param>
        /// <param name="children">The children. Each is placed in the next row by position unless it already carries an explicit row.</param>
        /// <returns>A Grid with those rows.</returns>
        public static Grid Rows(string definitions, params Control[] children)
        {
            var grid = new Grid();
            foreach (RowDefinition definition in new RowDefinitions(definitions))
            {
                grid.RowDefinitions.Add(definition);
            }

            for (int i = 0; i < children.Length; i++)
            {
                Control child = children[i];
                if (!child.IsSet(Grid.RowProperty)) Grid.SetRow(child, i);
                grid.Children.Add(child);
            }

            return grid;
        }

        /// <summary>Wraps content in a scroller.</summary>
        /// <param name="content">What to scroll.</param>
        /// <returns>A ScrollViewer around it.</returns>
        public static ScrollViewer Scroll(Control content) => new() { Content = content };

        // A SectionHeader and its content. Spacing defaults to the 8 the dashboards already used between the two.
        /// <summary>A section heading with one block of content under it.</summary>
        /// <param name="header">The heading text.</param>
        /// <param name="content">What goes under the heading.</param>
        /// <param name="spacing">The gap between the heading and the content.</param>
        /// <returns>A vertical StackPanel: the heading, then the content.</returns>
        public static StackPanel Section(string header, Control content, double spacing = 8) =>
            Stack(spacing, new SectionHeader { Text = header }, content);

        // The same, for a section with more than one child. Taking exactly one is why eight places
        // write a bold TextBlock by hand rather than using SectionHeader at all - see §21.2.
        /// <summary>A section heading with several controls under it.</summary>
        /// <param name="header">The heading text.</param>
        /// <param name="content">What goes under the heading, top to bottom.</param>
        /// <returns>A vertical StackPanel: the heading, then the content.</returns>
        public static StackPanel Section(string header, params Control[] content)
        {
            var panel = Stack(8, new SectionHeader { Text = header });
            foreach (Control child in content) panel.Children.Add(child);
            return panel;
        }

        /// <summary>A section heading, in the palette header colour and size.</summary>
        /// <param name="text">The heading text.</param>
        /// <returns>A SectionHeader showing it.</returns>
        public static SectionHeader Header(string text) => new() { Text = text };

        /// <summary>A muted line of explanation, for sitting under the control it describes.</summary>
        /// <param name="text">The hint text.</param>
        /// <returns>A HintText showing it.</returns>
        public static HintText Hint(string text) => new() { Text = text };

        /// <summary>Text in the palette monospaced family, for anything that has to line up in columns.</summary>
        /// <param name="text">The text. Empty by default, for a line filled in later.</param>
        /// <returns>A MonoText showing it.</returns>
        public static MonoText Mono(string text = "") => new() { Text = text };

        /// <summary>A plain line of text.</summary>
        /// <param name="text">The text. Empty by default, for a line filled in later.</param>
        /// <returns>A TextBlock showing it.</returns>
        public static TextBlock Text(string text = "") => new() { Text = text };

        /// <summary>A button that runs a handler when it is pressed.</summary>
        /// <param name="content">The caption.</param>
        /// <param name="onClick">Runs on the UI thread each time the button is pressed. For a command several surfaces share, build a LunaAction instead and let them follow it.</param>
        /// <returns>A Button wired to the handler.</returns>
        public static Button Button(string content, Action onClick)
        {
            var button = new Button { Content = content };
            button.Click += (_, _) => onClick();
            return button;
        }

        // A right-aligned run of buttons, for the bottom of a window.
        /// <summary>A right-aligned run of buttons, for the bottom of a dialog.</summary>
        /// <param name="buttons">The buttons, in reading order. The rightmost is conventionally the accepting one.</param>
        /// <returns>A ButtonBar holding them.</returns>
        public static ButtonBar Buttons(params Button[] buttons) => new() { ItemsSource = buttons };
    }
}
