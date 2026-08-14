using System;
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
                Assert.Empty(table.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.Classes.Contains("expander")));
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
    }
}
