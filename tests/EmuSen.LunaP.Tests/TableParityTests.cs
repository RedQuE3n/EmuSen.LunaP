using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // THE PARITY ARC - see docs/LunaP.md §54.
    //
    // §54 decided LunaTable<T> goes to feature parity with Avalonia.Controls.TreeDataGrid, measured
    // against the 70 public types enumerated from 12.2.0. This file guards the items as they land.
    //
    // The rule every one of them is held to is §26.13: additive and off by default. A table that
    // names no SelectionMode, hides no column and bounds no width has to behave exactly as it did in
    // 0.7.0 - so most of these tests are as much about what did NOT change as about what did.
    public class TableParityTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableParityTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, int size)
            {
                Name = name;
                Size = size;
            }

            public string Name { get; }
            public int Size { get; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("alpha", 10), new Row("bravo", 20), new Row("charlie", 30),
        };

        // A FACTORY AND NOT AN INSTANCE, because a control built on the test thread and then shown
        // on the UI thread throws "a different thread owns it" - Avalonia checks affinity when the
        // logical parent is set. Everything the table is made of has to be made inside the dispatch.
        private static Task Realised(Func<LunaTable<Row>> make, Action<LunaTable<Row>> assert) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = 500, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static LunaTable<Row> Plain(Row[] rows)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name).Column("size", r => r.Size.ToString());
            table.Refresh(rows);
            return table;
        }

        // ---- selection ----

        [Fact]
        public Task A_table_selects_one_row_by_default() => Realised(() => Plain(Rows()), table =>
        {
            Assert.Equal(LunaSelectionMode.Single, table.SelectionMode);
            Assert.Equal(Avalonia.Controls.SelectionMode.Single, table.FindNamed<ListBox>("PART_Rows").SelectionMode);
        });

        [Fact]
        public Task Multiple_rows_can_be_selected_and_come_back_in_display_order() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Plain(rows);
            table.SelectionMode = LunaSelectionMode.Multiple;

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ListBox list = table.FindNamed<ListBox>("PART_Rows");
            list.SelectedItems!.Add(rows[2]);
            list.SelectedItems!.Add(rows[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Display order, not click order - alpha was clicked second and comes first.
            Assert.Equal(new[] { "alpha", "charlie" }, table.SelectedItems.Select(r => r.Name));

            window.Close();
        }, default);

        [Fact]
        public Task Nothing_is_selected_when_the_mode_is_None() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Plain(rows);

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Select(rows[1]);
            Assert.NotNull(table.Selected);

            // Switching to None has to CLEAR what is already selected, not merely refuse the next
            // one - otherwise the mode reads as "no new selections".
            table.SelectionMode = LunaSelectionMode.None;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Null(table.Selected);
            Assert.Empty(table.SelectedItems);

            window.Close();
        }, default);

        [Fact]
        public Task SelectedItems_is_empty_rather_than_null_when_nothing_is_selected() =>
            Realised(() => Plain(Rows()), table => Assert.Empty(table.SelectedItems));

        // A mode set before the template has to survive it, the same way Select does (§27.6).
        [Fact]
        public Task A_selection_mode_set_before_the_template_survives_it() => Session.Dispatch(() =>
        {
            LunaTable<Row> table = Plain(Rows());
            table.SelectionMode = LunaSelectionMode.Multiple;

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(Avalonia.Controls.SelectionMode.Multiple,
                table.FindNamed<ListBox>("PART_Rows").SelectionMode);

            window.Close();
        }, default);

        // ---- column visibility and bounds ----

        private static LunaTable<Row> WithHiddenMiddleColumn(Row[] rows)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name)
                 .Column(new LunaColumn<Row>("size", r => r.Size.ToString()) { IsVisible = false })
                 .Column("kind", _ => "file");
            table.Refresh(rows);
            return table;
        }

        [Fact]
        public Task A_hidden_column_draws_nothing() => Realised(() => WithHiddenMiddleColumn(Rows()), table =>
        {
            Grid row = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>().First()
                .GetVisualDescendants().OfType<Grid>().First();

            string[] shown = row.Children.OfType<TextBlock>().Select(c => c.Text ?? "").ToArray();

            Assert.Equal(new[] { "alpha", "file" }, shown);
        });

        // THE WHOLE REASON HIDING IS NOT REMOVING. Every index after a hidden column has to be
        // unmoved, or a remembered layout, a sort and Edit(item, column) all mean something else.
        [Fact]
        public Task A_hidden_column_keeps_its_index() => Realised(() => WithHiddenMiddleColumn(Rows()), table =>
        {
            Grid row = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>().First()
                .GetVisualDescendants().OfType<Grid>().First();

            Assert.Equal(3, row.ColumnDefinitions.Count);

            TextBlock kind = row.Children.OfType<TextBlock>().Single(c => c.Text == "file");
            Assert.Equal(2, Grid.GetColumn(kind));
        });

        [Fact]
        public Task A_hidden_column_takes_no_width() => Realised(() => WithHiddenMiddleColumn(Rows()), table =>
        {
            Grid header = table.FindNamed<Grid>("PART_Header");

            Assert.Equal(0, header.ColumnDefinitions[1].MaxWidth);
            Assert.Equal(0, header.ColumnDefinitions[1].ActualWidth);
        });

        [Fact]
        public Task A_column_can_be_bounded() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column(new LunaColumn<Row>("name", r => r.Name) { MinWidth = 80, MaxWidth = 120 })
                 .Column("size", r => r.Size.ToString());
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ColumnDefinition first = table.FindNamed<Grid>("PART_Header").ColumnDefinitions[0];
            Assert.Equal(80, first.MinWidth);
            Assert.Equal(120, first.MaxWidth);

            window.Close();
        }, default);

        // Unbounded is what every column did before §54, and the Grid's own defaults say so.
        [Fact]
        public Task A_column_that_names_no_bounds_is_unbounded() => Realised(() => Plain(Rows()), table =>
        {
            ColumnDefinition first = table.FindNamed<Grid>("PART_Header").ColumnDefinitions[0];

            Assert.Equal(0, first.MinWidth);
            Assert.Equal(double.PositiveInfinity, first.MaxWidth);
        });

        // ---- navigation ----

        [Fact]
        public Task A_realised_row_and_cell_can_be_found() => Realised(() => Plain(Rows()), table =>
        {
            Assert.True(table.TryGetRow(table.Models[1], out Control? row));
            Assert.NotNull(row);

            Assert.True(table.TryGetCell(table.Models[1], 0, out Control? cell));
            Assert.Equal("bravo", ((TextBlock)cell!).Text);
        });

        [Fact]
        public Task A_hidden_columns_cell_is_not_found() => Realised(() => WithHiddenMiddleColumn(Rows()), table =>
        {
            Assert.False(table.TryGetCell(table.Models[0], 1, out Control? cell));
            Assert.Null(cell);
        });

        // The exemption in TemplateOrderTests is a claim, and this is the claim: these are queries,
        // answered honestly before the template rather than queued for later.
        [Fact]
        public Task Navigating_before_there_are_rows_is_answered_not_queued() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Plain(rows);

            Assert.False(table.TryGetRow(rows[0], out Control? row));
            Assert.Null(row);
            Assert.False(table.TryGetCell(rows[0], 0, out _));
            table.BringRowIntoView(rows[0]);   // must not throw

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Nothing was replayed: the row is found now because it is realised now.
            Assert.True(table.TryGetRow(rows[0], out _));

            window.Close();
        }, default);

        [Fact]
        public Task A_row_scrolled_away_can_be_brought_back() => Session.Dispatch(() =>
        {
            Row[] many = Enumerable.Range(0, 60).Select(i => new Row($"row{i:D2}", i)).ToArray();
            LunaTable<Row> table = Plain(many);
            table.Height = 90;

            var window = new ToolWindow { Width = 500, Height = 140, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(table.TryGetRow(many[50], out _));

            table.BringRowIntoView(many[50]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.TryGetRow(many[50], out _));

            window.Close();
        }, default);

        // ---- hierarchy (§55) ----

        private sealed class Node
        {
            public Node(string name, params Node[] kids)
            {
                Name = name;
                Kids = kids;
            }

            public string Name { get; }
            public Node[] Kids { get; }
        }

        private static Node[] Tree() => new[]
        {
            new Node("roms",
                new Node("snes", new Node("smw.sfc"), new Node("zelda.sfc")),
                new Node("nes", new Node("metroid.nes"))),
            new Node("saves"),
        };

        private static LunaTable<Node> TreeTable(Node[] roots, bool hierarchical = true)
        {
            var table = new LunaTable<Node> { Key = n => n.Name };
            table.Column("name", n => n.Name);
            if (hierarchical) table.Children = n => n.Kids;
            table.Refresh(roots);
            return table;
        }

        private static Task Tree(Func<LunaTable<Node>> make, Action<LunaTable<Node>> assert) =>
            Session.Dispatch(() =>
            {
                LunaTable<Node> table = make();
                var window = new ToolWindow { Width = 500, Height = 400, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static string[] Visible(LunaTable<Node> table) =>
            table.Models.Select(n => n.Name).ToArray();

        // THE FLAT CASE IS THE OLD CASE. No Children means no expander, no indent, and the same
        // rows in the same order - the §26.13 half of this arc.
        [Fact]
        public Task A_table_with_no_children_projection_is_exactly_a_flat_table() =>
            Tree(() => TreeTable(Tree(), hierarchical: false), table =>
            {
                Assert.Equal(new[] { "roms", "saves" }, Visible(table));
                Assert.DoesNotContain(
                    table.GetVisualDescendants().OfType<Button>(),
                    b => b.Classes.Contains("expander"));
            });

        [Fact]
        public Task A_tree_starts_collapsed_and_shows_only_its_roots() =>
            Tree(() => TreeTable(Tree()), table =>
            {
                Assert.Equal(new[] { "roms", "saves" }, Visible(table));
                Assert.False(table.IsExpanded(table.Models[0]));
            });

        [Fact]
        public Task Expanding_a_row_shows_its_children_under_it() =>
            Tree(() => TreeTable(Tree()), table =>
            {
                table.Expand(table.Models[0]);

                Assert.Equal(new[] { "roms", "snes", "nes", "saves" }, Visible(table));
                Assert.True(table.IsExpanded(table.Models[0]));
            });

        [Fact]
        public Task Expanding_a_child_nests_further() => Tree(() => TreeTable(Tree()), table =>
        {
            table.ExpandAll();

            Assert.Equal(
                new[] { "roms", "snes", "smw.sfc", "zelda.sfc", "nes", "metroid.nes", "saves" },
                Visible(table));
        });

        [Fact]
        public Task Collapsing_hides_the_whole_subtree() => Tree(() => TreeTable(Tree()), table =>
        {
            table.ExpandAll();
            table.Collapse(table.Models.First(n => n.Name == "roms"));

            Assert.Equal(new[] { "roms", "saves" }, Visible(table));
        });

        [Fact]
        public Task CollapseAll_shuts_every_level() => Tree(() => TreeTable(Tree()), table =>
        {
            table.ExpandAll();
            table.CollapseAll();

            Assert.Equal(new[] { "roms", "saves" }, Visible(table));
        });

        // DEPTH IS DRAWN, not merely recorded. The indent is what tells a reader which parent a row
        // belongs to, and a tree that computed depth correctly and drew every row flush left would
        // pass every assertion above.
        [Fact]
        public Task Each_level_is_indented_further_than_the_one_above() =>
            Tree(() => TreeTable(Tree()), table =>
            {
                table.ExpandAll();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                double Indent(string name) => table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Node)?.Name == name)
                    .GetVisualDescendants().OfType<Border>()
                    // The indent spacer is the only Border in a row with an explicit width; every
                    // other one leaves Width as NaN. Naming that is the difference between a lookup
                    // that is right and one that happens to work.
                    .First(b => !double.IsNaN(b.Width)).Width;

                Assert.Equal(0, Indent("roms"));
                Assert.Equal(table.IndentSize, Indent("snes"));
                Assert.Equal(table.IndentSize * 2, Indent("smw.sfc"));
            });

        // SORTED WITHIN EACH LEVEL, which is the only reading that keeps a tree a tree. Sorting the
        // flattened list would interleave children with strangers' parents.
        [Fact]
        public Task A_sort_orders_siblings_and_does_not_flatten_the_tree() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Node> { Key = n => n.Name };
            table.Column(new LunaColumn<Node>("name", n => n.Name)
            {
                Sort = (a, b) => string.CompareOrdinal(a.Name, b.Name),
            });
            table.Children = n => n.Kids;
            table.Refresh(Tree());

            var window = new ToolWindow { Width = 500, Height = 400, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.ExpandAll();
            table.FindNamed<Grid>("PART_Header").GetVisualDescendants().OfType<Button>().First()
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Roots sorted among themselves, children sorted among their own siblings, and every
            // child still directly under its parent.
            Assert.Equal(
                new[] { "roms", "nes", "metroid.nes", "snes", "smw.sfc", "zelda.sfc", "saves" },
                Visible(table));

            window.Close();
        }, default);

        // EXPANSION IS KEYED BY MODEL, so a Refresh that hands back new objects for the same rows -
        // which is every poll in a PollingWindow - does not collapse the tree the user just opened.
        [Fact]
        public Task Expansion_survives_a_refresh_that_rebuilds_the_models() =>
            Tree(() => TreeTable(Tree()), table =>
            {
                table.Expand(table.Models[0]);
                Assert.Equal(new[] { "roms", "snes", "nes", "saves" }, Visible(table));

                table.Refresh(Tree());   // brand new Node objects, same names

                Assert.Equal(new[] { "roms", "snes", "nes", "saves" }, Visible(table));
            });

        // A CYCLE IS A STACK OVERFLOW WITHOUT THE GUARD, and a StackOverflowException cannot be
        // caught - it takes the application down. Children is a caller's delegate and nothing stops
        // it returning an ancestor.
        [Fact]
        public Task A_children_projection_that_loops_does_not_take_the_process_with_it() =>
            Session.Dispatch(() =>
            {
                var a = new Node("a");
                var table = new LunaTable<Node> { Key = n => n.Name };
                table.Column("name", n => n.Name);
                table.Children = _ => new[] { a };   // every row's child is the root
                table.Refresh(new[] { a });

                var window = new ToolWindow { Width = 400, Height = 200, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                table.ExpandAll();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal(new[] { "a" }, Visible(table));

                window.Close();
            }, default);

        // THE EXPANDER IN A COLUMN THAT IS NOT THE FIRST - see docs/LunaP.md §66.
        //
        // Every other test in this file, and both in TableCellKindTests, sets ExpanderColumn to 0.
        // Zero is also Grid.Column's default, so "the column this was put in" and "the column
        // nothing set" were the same number in every fixture there was - and a Grid.SetColumn left
        // on the inner cell while the Grid holds the expander's WRAPPER read as column 0 and looked
        // right. With the expander in column 1, the wrapper landed on top of column 0's cell.
        //
        // Asserted as POSITIONS and not only as attached properties: the property is what was wrong,
        // but two cells sharing an x is what the user saw, and it is the second that says the row is
        // laid out rather than merely annotated (§5.5's shape).
        [Fact]
        public Task An_expander_in_a_later_column_lands_in_that_column() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Node> { Key = n => n.Name, ExpanderColumn = 1 };
            table.Column("size", _ => "12k", "80").Column("name", n => n.Name, "*");
            table.Children = n => n.Kids;
            table.Refresh(Tree());

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Grid grid = table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants()
                .OfType<ListBoxItem>().First()
                .GetVisualDescendants().OfType<Grid>().First();

            Control wrapper = grid.Children.OfType<Control>()
                .Single(c => c.GetVisualDescendants().OfType<Button>().Any(b => b.Classes.Contains("expander")));

            Assert.Equal(1, Grid.GetColumn(wrapper));

            Control first = grid.Children.OfType<Control>().Single(c => Grid.GetColumn(c) == 0);

            Assert.True(wrapper.Bounds.X >= first.Bounds.Right,
                $"the expander column starts at {wrapper.Bounds.X:F0}, inside column 0 which ends at "
                + $"{first.Bounds.Right:F0} - the two cells are drawn on top of each other.");

            window.Close();
        }, default);

        // A leaf keeps the toggle's WIDTH so its text lines up with its siblings', and loses only
        // the glyph. Omitting the button would shift a leaf left of the folders beside it.
        [Fact]
        public Task A_leaf_has_no_toggle_but_still_lines_up() => Tree(() => TreeTable(Tree()), table =>
        {
            table.ExpandAll();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Button Toggle(string name) => table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Node)?.Name == name)
                .GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("expander"));

            Assert.True(Toggle("snes").IsVisible);
            Assert.False(Toggle("smw.sfc").IsVisible);
            Assert.Equal(Toggle("snes").Width, Toggle("smw.sfc").Width);
        });

        // Clicking the toggle is the gesture a mouse user has; it must do what Expand does.
        [Fact]
        public Task The_toggle_opens_and_shuts_the_row() => Tree(() => TreeTable(Tree()), table =>
        {
            Button toggle = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Node)?.Name == "roms")
                .GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("expander"));

            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { "roms", "snes", "nes", "saves" }, Visible(table));
        });

        // WHAT A READER HEARS OF A TREE. "does this have more under it" is part of what the row IS,
        // and a reader that only hears the cells cannot tell a leaf from an unopened folder.
        [Fact]
        public Task A_row_that_can_be_opened_says_so() => Tree(() => TreeTable(Tree()), table =>
        {
            string Heard(string name) => Avalonia.Automation.Peers.ControlAutomationPeer
                .CreatePeerForElement(table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Node)?.Name == name)).GetName() ?? "";

            Assert.Equal("name: roms, collapsed", Heard("roms"));
            Assert.Equal("name: saves", Heard("saves"));   // a leaf says neither

            table.Expand(table.Models[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("name: roms, expanded", Heard("roms"));
        });

        // ---- pass 2: gestures, rules, lifecycle (§56) ----

        private static LunaTable<Node> Editable(Node[] roots, LunaEditGestures gestures)
        {
            var table = new LunaTable<Node> { Key = n => n.Name, EditGestures = gestures };
            table.Column(new LunaColumn<Node>("name", n => n.Name) { Commit = (_, _) => { } });
            table.Refresh(roots);
            return table;
        }

        [Fact]
        public Task Both_gestures_open_an_editor_by_default() => Tree(() => Editable(Tree(), LunaEditGestures.Default), table =>
        {
            table.Select(table.Models[0]);
            table.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.F2,
            });

            Assert.True(table.IsEditing);
        });

        [Fact]
        public Task F2_does_nothing_when_it_is_not_among_the_gestures() =>
            Tree(() => Editable(Tree(), LunaEditGestures.DoubleTap), table =>
            {
                table.Select(table.Models[0]);
                table.RaiseEvent(new Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                    Key = Avalonia.Input.Key.F2,
                });

                Assert.False(table.IsEditing);
            });

        // None still allows Edit(), because an application driving editing from its own menu wants
        // the column editable and the gestures off - and turning the column read-only to get that
        // would lose its validation with it.
        [Fact]
        public Task No_gesture_still_leaves_Edit_working() =>
            Tree(() => Editable(Tree(), LunaEditGestures.None), table =>
            {
                table.Select(table.Models[0]);
                table.RaiseEvent(new Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                    Key = Avalonia.Input.Key.F2,
                });
                Assert.False(table.IsEditing);

                table.Edit(table.Models[0], 0);
                Assert.True(table.IsEditing);
            });

        // ---- grid lines ----

        private static int Rules(LunaTable<Node> table, string cls) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<Border>()
                .Count(b => b.Classes.Contains(cls));

        [Fact]
        public Task A_table_draws_no_rules_by_default() => Tree(() => TreeTable(Tree(), hierarchical: false), table =>
        {
            Assert.Equal(LunaGridLines.None, table.GridLines);
            Assert.Equal(0, Rules(table, "row-rule"));
            Assert.Equal(0, Rules(table, "column-rule"));
        });

        [Fact]
        public Task Horizontal_rules_are_one_per_row() => Session.Dispatch(() =>
        {
            LunaTable<Node> table = TreeTable(Tree(), hierarchical: false);
            table.GridLines = LunaGridLines.Horizontal;

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, Rules(table, "row-rule"));     // two roots
            Assert.Equal(0, Rules(table, "column-rule"));

            window.Close();
        }, default);

        // One per column BOUNDARY, not per column: a rule after the last column would draw on the
        // table's own edge.
        [Fact]
        public Task Vertical_rules_stop_at_the_last_column() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Node> { Key = n => n.Name, GridLines = LunaGridLines.Vertical };
            table.Column("name", n => n.Name)
                 .Column("kind", _ => "folder")
                 .Column("size", _ => "0");
            table.Refresh(new[] { new Node("roms") });

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, Rules(table, "column-rule"));  // three columns, two boundaries
            Assert.Equal(0, Rules(table, "row-rule"));

            window.Close();
        }, default);

        // A vertical rule must not wrap the cell, or BeginEdit cannot find a Panel to put the editor
        // in and editing silently stops working with rules turned on (§55.7).
        [Fact]
        public Task A_cell_can_still_be_edited_with_rules_on() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Node> { Key = n => n.Name, GridLines = LunaGridLines.All };
            table.Column(new LunaColumn<Node>("name", n => n.Name) { Commit = (_, _) => { } })
                 .Column("kind", _ => "folder");
            table.Refresh(new[] { new Node("roms") });

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 0);

            Assert.True(table.IsEditing);
            Assert.Single(table.GetVisualDescendants().OfType<TextBox>());

            window.Close();
        }, default);

        // ---- lifecycle ----

        [Fact]
        public Task A_row_reports_when_a_container_starts_standing_for_it() => Session.Dispatch(() =>
        {
            var prepared = new List<string>();
            LunaTable<Node> table = TreeTable(Tree(), hierarchical: false);
            table.RowPrepared += (model, _) => prepared.Add(model.Name);

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { "roms", "saves" }, prepared);

            window.Close();
        }, default);

        [Fact]
        public Task A_committed_edit_reports_the_model_and_the_column() => Session.Dispatch(() =>
        {
            var changed = new List<string>();
            var node = new Node("roms");
            var table = new LunaTable<Node> { Key = n => n.Name };
            table.Column("kind", _ => "folder")
                 .Column(new LunaColumn<Node>("name", n => n.Name) { Commit = (_, _) => { } });
            table.Refresh(new[] { node });
            table.CellValueChanged += (model, column) => changed.Add($"{model.Name}:{column}");

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(node, 1);
            table.GetVisualDescendants().OfType<TextBox>().Single().RaiseEvent(
                new Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                    Key = Avalonia.Input.Key.Enter,
                });

            Assert.Equal(new[] { "roms:1" }, changed);

            window.Close();
        }, default);

        // A cancelled edit changed nothing, so it must not say it did.
        [Fact]
        public Task A_cancelled_edit_reports_nothing() => Session.Dispatch(() =>
        {
            var changed = new List<string>();
            var node = new Node("roms");
            var table = new LunaTable<Node> { Key = n => n.Name };
            table.Column(new LunaColumn<Node>("name", n => n.Name) { Commit = (_, _) => { } });
            table.Refresh(new[] { node });
            table.CellValueChanged += (model, column) => changed.Add($"{model.Name}:{column}");

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(node, 0);
            table.GetVisualDescendants().OfType<TextBox>().Single().RaiseEvent(
                new Avalonia.Input.KeyEventArgs
                {
                    RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                    Key = Avalonia.Input.Key.Escape,
                });

            Assert.Empty(changed);

            window.Close();
        }, default);
    }
}
