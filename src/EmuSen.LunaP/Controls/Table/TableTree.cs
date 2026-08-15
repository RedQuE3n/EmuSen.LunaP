using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmuSen.LunaP.Controls
{
    // THE TABLE GREW A TREE - see docs/LunaP.md §55.
    //
    // WHAT MAKES THIS FILE POSSIBLE IS THAT NOTHING ELSE KNOWS ABOUT IT. Flatten turns the hierarchy
    // into the flat sequence a ListBox shows, and selection, editing, the spoken name, the frozen
    // band and column virtualization all keep working on a flat sequence of T. §73.5 calls that the
    // load-bearing decision of the whole arc; here it is the rule to keep, because a tree that
    // leaked into one of those files would have to leak into all of them.
    //
    // THREE ENTRY POINTS AND NOTHING ELSE: Children turns it on, Flatten is called by Show, and
    // Expander is called by AddCell for one column. A fourth would be a leak.
    //
    // EXPANSION IS THE USER'S STATE AND IS KEYED, so it survives a Refresh that hands back new
    // objects for the same rows - the same reason selection is keyed (§27.6), and it matters more
    // here because a polling window refreshes every second.
    public partial class LunaTable<T> where T : class
    {
        // THE THREE THINGS HIERARCHY NEEDS, and only the first of them is state.
        //
        // _expanded is the USER'S and outlives any particular set of models. _depth and _expandable
        // are derived, rebuilt on every flatten, and exist only so that Row() can draw an indent and
        // a toggle without walking the tree again per row.
        private readonly HashSet<object> _expanded = new();
        private readonly Dictionary<object, int> _depth = new();
        private readonly HashSet<object> _expandable = new();
        private Func<T, IEnumerable<T>>? _children;

        // THE ONE PROPERTY THAT TURNS A LIST INTO A TREE, and null - the default - means it is not
        // one. A table that never sets this runs the same code path it did in 0.7.0: Flatten returns
        // Ordered(_items) unchanged, no depth is recorded, no expander is built, and nothing about a
        // flat table pays for hierarchy existing (§26.13).
        //
        // A PROJECTION, NOT AN INTERFACE, for the §1 reason every other seam here is one: a caller's
        // model needs no base class, no ITreeNode, and no knowledge that LunaP exists. `r => r.Kids`
        // is the whole of it, and a model that stores children in a dictionary elsewhere writes
        // `r => index[r.Id]` instead - which an interface on the model could not express at all.
        /// <summary>How to find a row's children, or null - the default - for a flat table.</summary>
        /// <remarks>
        /// Called during every rebuild, for every visible row and for every row that has to be tested for
        /// children, so it should be cheap. Return an empty sequence for a leaf. Rows are expanded through
        /// <see cref="Expand"/> and start collapsed.
        /// </remarks>
        public Func<T, IEnumerable<T>>? Children
        {
            get => _children;
            set
            {
                _children = value;
                Show();
            }
        }

        // How far one level is indented from the one above it. A property rather than a constant
        // because a table of file paths and a table of two-deep config groups want different
        // amounts, and neither is wrong.
        /// <summary>How many pixels each level of hierarchy is indented. 16 by default.</summary>
        public double IndentSize { get; set; } = 16;

        // WHICH COLUMN CARRIES THE EXPANDER, and it is a choice rather than always the first because
        // the first column is not always the name. A table whose leading column is a checkbox or an
        // icon wants the toggle beside the label instead, and TreeDataGrid makes the same choice by
        // having the caller declare which column is the hierarchical one.
        /// <summary>Which column shows the expander and the indent. The first column by default.</summary>
        public int ExpanderColumn { get; set; }

        /// <summary>Whether a row is currently expanded. Always false when the table is flat.</summary>
        /// <param name="item">The row's model.</param>
        /// <returns>True when the row's children are shown.</returns>
        public bool IsExpanded(T item) => item is not null && _expanded.Contains(KeyOf(item));

        /// <summary>Shows a row's children. Does nothing for a leaf or a flat table.</summary>
        /// <param name="item">The row's model.</param>
        public void Expand(T item)
        {
            if (item is null || _children is null) return;
            if (_expanded.Add(KeyOf(item))) Show();
        }

        /// <summary>Hides a row's children.</summary>
        /// <param name="item">The row's model.</param>
        public void Collapse(T item)
        {
            if (item is null) return;
            if (_expanded.Remove(KeyOf(item))) Show();
        }

        // EXPANDS WHAT IS REACHABLE, NOT WHAT IS EXPANDED. Walking only the currently-visible rows
        // would expand one level per call, which reads as a broken ExpandAll rather than a lazy one -
        // so this walks the whole tree through Children, whatever is open at the time.
        //
        // The cycle guard in Walk protects this too: without it, ExpandAll on a model whose Children
        // eventually returns an ancestor is an immediate stack overflow rather than a slow table.
        /// <summary>Expands every row that has children, at every level.</summary>
        public void ExpandAll()
        {
            if (_children is null) return;

            var seen = new HashSet<object>();
            CollectKeys(_items, seen, new HashSet<object>());
            if (seen.Count == 0) return;

            foreach (object key in seen) _expanded.Add(key);
            Show();
        }

        /// <summary>Collapses every row.</summary>
        public void CollapseAll()
        {
            if (_expanded.Count == 0) return;

            _expanded.Clear();
            Show();
        }

        private void CollectKeys(IEnumerable<T> level, HashSet<object> into, HashSet<object> path)
        {
            foreach (T item in level)
            {
                object key = KeyOf(item);
                if (!path.Add(key)) continue;

                IReadOnlyList<T> kids = ChildrenOf(item);
                if (kids.Count > 0)
                {
                    into.Add(key);
                    CollectKeys(kids, into, path);
                }

                path.Remove(key);
            }
        }

        private IReadOnlyList<T> ChildrenOf(T item)
        {
            if (_children is null) return Array.Empty<T>();

            IEnumerable<T>? kids = _children(item);
            return kids as IReadOnlyList<T> ?? kids?.ToList() ?? (IReadOnlyList<T>)Array.Empty<T>();
        }

        // THE TREE, FLATTENED INTO THE LIST THE ListBox ACTUALLY SHOWS - see docs/LunaP.md §55.
        //
        // A ListBox displays a sequence, so hierarchy has to become one: parents followed by their
        // visible children, each row remembering how deep it is. Everything else about the control -
        // selection by model, editing, the row's spoken name, virtualization - keeps working on a
        // flat sequence of T and never learns that a tree exists.
        //
        // THE FLAT CASE IS THE OLD CASE, EXACTLY. No Children means this returns Ordered(_items) and
        // returns it before touching any of the three dictionaries, so a table that is not a tree
        // does no extra work and allocates nothing new (§26.13).
        private IReadOnlyList<T> Flatten()
        {
            if (_children is null) return Ordered(_items);

            _depth.Clear();
            _expandable.Clear();

            var flat = new List<T>(_items.Count);
            Walk(_items, 0, flat, new HashSet<object>());
            return flat;
        }

        // SORTED AT EVERY LEVEL, WHICH IS THE ONLY READING THAT MAKES SENSE. Sorting the flattened
        // list would interleave children with strangers' parents and destroy the tree; sorting only
        // the roots would leave every child list in arrival order under a header the user just
        // clicked. Each level is ordered among its own siblings.
        //
        // `path` IS A CYCLE GUARD AND NOT AN OPTIMISATION. Children is a caller's delegate and
        // nothing stops it returning an ancestor - a parent index built from a bad file, a symlink
        // loop in a directory walk. Without this, the first such model is a StackOverflowException,
        // which cannot be caught and takes the application with it. With it, the repeat is dropped
        // and the rest of the table still draws.
        private void Walk(IReadOnlyList<T> level, int depth, List<T> into, HashSet<object> path)
        {
            foreach (T item in Ordered(level))
            {
                object key = KeyOf(item);
                if (!path.Add(key)) continue;

                _depth[key] = depth;
                into.Add(item);

                IReadOnlyList<T> kids = ChildrenOf(item);
                if (kids.Count > 0)
                {
                    _expandable.Add(key);
                    if (_expanded.Contains(key)) Walk(kids, depth + 1, into, path);
                }

                path.Remove(key);
            }
        }

        // THE INDENT AND THE TOGGLE, in front of the cell that carries the row's name - §55.
        //
        // A LEAF STILL GETS THE SPACE THE TOGGLE WOULD HAVE TAKEN, which is why the button is made
        // invisible rather than left out. Omitting it would shift a leaf's text left of its
        // siblings' by the width of a glyph, so a list of files under a folder would not line up
        // with each other - the one thing an indent exists to do.
        //
        // The whole thing is a DockPanel and not a Grid: two fixed-width things on the left and one
        // elastic thing filling the rest is exactly what docking is, and a Grid here would need
        // three ColumnDefinitions per row per level for the same picture.
        private Control Expander(T item, Control cell, int column)
        {
            object key = KeyOf(item);
            int depth = _depth.TryGetValue(key, out int found) ? found : 0;
            bool expandable = _expandable.Contains(key);
            bool expanded = _expanded.Contains(key);

            var toggle = new Button
            {
                Content = expanded ? "\u25BE" : "\u25B8",
                IsVisible = expandable,
                Width = 16,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            toggle.Classes.Add("expander");

            // A BUTTON, so it is focusable, invokable and reachable by keyboard for the same reason
            // a sortable heading is one (§27.3) - a tree a mouse can open and a keyboard cannot is a
            // tree half this toolkit's users cannot read. The name says what pressing it does rather
            // than what it is, because "expander" tells a reader nothing about which row.
            //
            // NAMED FROM THE PROJECTION AND NOT OFF THE CELL, since §57. Reading cell.Text was fine
            // while every cell was a TextBlock; an ExpanderColumn that is a checkbox or a template
            // has no text to read, and the projection answers for all three kinds.
            AutomationProperties.SetName(
                toggle,
                $"{(expanded ? "Collapse" : "Expand")} {_columns[column].Text(item) ?? string.Empty}");

            toggle.Click += (_, e) =>
            {
                if (_expanded.Contains(key)) Collapse(item); else Expand(item);
                e.Handled = true;
            };

            var row = new DockPanel();

            row.Children.Add(new Border
            {
                Width = depth * IndentSize,
                [DockPanel.DockProperty] = Dock.Left,
            });

            DockPanel.SetDock(toggle, Dock.Left);
            row.Children.Add(toggle);
            row.Children.Add(cell);

            return row;
        }
    }
}
