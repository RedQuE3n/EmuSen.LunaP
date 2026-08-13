using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // LunaTable<T> - see docs/LunaP.md §27.
    //
    // The control exists for one shape: a flat list with columns, which is what the only counted
    // evidence asks for. These assertions go through real template parts and real rows rather than
    // through the properties that fed them, for the reason §5.5 has recorded twice and §26.11
    // proved again this month - a property assertion passes for a control that draws nothing.
    public class TableTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableTests).GetTypeInfo().Assembly);

        private sealed record Field(string Name, string Type, int Page);

        private static readonly Field[] Fields =
        {
            new("Site", "text", 1),
            new("Technician", "text", 1),
            new("Approved", "checkbox", 2),
        };

        private static Task Realised(Action<LunaTable<Field>> assert, Func<LunaTable<Field>>? make = null) =>
            Session.Dispatch(() =>
            {
                LunaTable<Field> table = make?.Invoke() ?? Build();
                var window = new ToolWindow { Width = 500, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static LunaTable<Field> Build()
        {
            var table = new LunaTable<Field> { Key = f => f.Name };
            table.Column("name", f => f.Name, "2*")
                 .Column("type", f => f.Type)
                 .Column("pg", f => f.Page.ToString(), "40");
            table.Refresh(Fields);
            return table;
        }

        [Fact]
        public Task A_table_puts_its_headers_in_a_real_header_row() => Realised(table =>
        {
            Grid header = table.FindNamed<Grid>("PART_Header");

            Assert.Equal(3, header.ColumnDefinitions.Count);
            Assert.Equal(new[] { "name", "type", "pg" },
                header.Children.OfType<TextBlock>().Select(t => t.Text));
        });

        // The widths a caller asked for, on the header. The rows get the same definitions object
        // shape, which is what makes a column line up with its own heading.
        [Fact]
        public Task Column_widths_are_what_the_caller_wrote() => Realised(table =>
        {
            Grid header = table.FindNamed<Grid>("PART_Header");

            Assert.True(header.ColumnDefinitions[0].Width.IsStar);
            Assert.Equal(2, header.ColumnDefinitions[0].Width.Value);
            Assert.True(header.ColumnDefinitions[2].Width.IsAbsolute);
            Assert.Equal(40, header.ColumnDefinitions[2].Width.Value);
        });

        // The rows are real, and each cell holds its own projection rather than one joined string.
        [Fact]
        public Task Every_row_is_rendered_as_cells() => Realised(table =>
        {
            ListBox rows = table.FindNamed<ListBox>("PART_Rows");
            ListBoxItem[] containers = rows.GetVisualDescendants().OfType<ListBoxItem>().ToArray();

            Assert.Equal(3, containers.Length);

            string[] first = containers[0].GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text!).ToArray();
            Assert.Equal(new[] { "Site", "text", "1" }, first);
        });

        // THE TEST THIS REPLACES COULD NOT FAIL, AND DID NOT, FOR THE WHOLE LIFE OF THE CONTROL.
        //
        // It asserted that the SharedSizeGroup NAMES matched between the header grid and a row
        // grid, and that every name was non-empty. Both were true the entire time the columns were
        // not sharing a size at all: Avalonia registers a definition with its scope on Add and not
        // on assignment, so the names read back perfectly while the grids sized independently. See
        // LunaTable.Define for the mechanism and the upstream issue.
        //
        // It had a second hole, and it is the more instructive one. The comment it was guarding
        // says "AUTO IS ACCEPTED AND MADE TO WORK" - and no test in this file had ever used an Auto
        // column. Star and absolute columns resolve identically in both grids without sharing
        // anything, so every existing assertion passed on data that could not have exposed the
        // defect even if it had been measuring the right thing.
        //
        // So this measures where the text actually lands, in the table's own coordinates, with an
        // Auto column whose heading is deliberately wider than its cells. Made to fail on purpose
        // by reverting Define to an assignment, which puts the middle column six pixels out. §22.5.
        [Fact]
        public Task An_auto_column_lines_up_with_its_own_heading() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();

                // "classification" is far wider than any of its cells, so an Auto column that is
                // not sharing sizes to 13 characters in the header and 8 in the rows.
                table.Column("name", f => f.Name, "2*")
                     .Column("classification", f => f.Type, "Auto")
                     .Column("pg", f => f.Page.ToString(), "40");
                table.Refresh(Fields);
                return table;
            },
            assert: table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Grid row = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>().First()
                    .GetVisualDescendants().OfType<Grid>().First();

                TextBlock[] headings = header.Children.OfType<TextBlock>().ToArray();
                TextBlock[] cells = row.Children.OfType<TextBlock>().ToArray();

                Assert.Equal(3, headings.Length);
                Assert.Equal(3, cells.Length);

                for (int i = 0; i < headings.Length; i++)
                {
                    double heading = headings[i].TranslatePoint(default, table)!.Value.X;
                    double cell = cells[i].TranslatePoint(default, table)!.Value.X;

                    Assert.True(Math.Abs(heading - cell) < 0.5,
                        $"Column {i} ({headings[i].Text}) heading starts at x={heading:F1} but its cell "
                        + $"starts at x={cell:F1}. The column is not sharing a size with its header - see "
                        + "LunaTable.Define.");
                }
            });

        // The wiring, kept as a smaller claim than it used to make. This one localizes a failure -
        // if the names have gone wrong, the positional test above cannot say why - but it is no
        // longer mistaken for evidence that the sharing works.
        [Fact]
        public Task Columns_share_a_size_group_name_with_their_header() => Realised(table =>
        {
            Grid header = table.FindNamed<Grid>("PART_Header");
            Grid row = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>().First()
                .GetVisualDescendants().OfType<Grid>().First();

            Assert.All(header.ColumnDefinitions, c => Assert.False(string.IsNullOrEmpty(c.SharedSizeGroup)));
            Assert.Equal(
                header.ColumnDefinitions.Select(c => c.SharedSizeGroup),
                row.ColumnDefinitions.Select(c => c.SharedSizeGroup));
        });

        // The assumption the whole "no cell virtualization needed" argument rests on: rows are
        // virtualized by the ListBox under PART_Rows, and because cells are built by the row's data
        // template, only realized rows have any cells at all.
        //
        // Asserted rather than assumed because a change of items panel - in this theme or in a
        // consumer's - would turn a 10,000-row table into 10,000 realized grids and 30,000
        // TextBlocks with nothing to announce it but a slow window. §7 of the table plan.
        [Fact]
        public Task A_long_table_realizes_only_the_rows_that_are_visible() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name, "2*")
                     .Column("type", f => f.Type)
                     .Column("pg", f => f.Page.ToString(), "40");
                table.Refresh(Enumerable.Range(0, 10_000).Select(i => new Field("row " + i, "text", i)).ToArray());
                return table;
            },
            assert: table =>
            {
                ListBox rows = table.FindNamed<ListBox>("PART_Rows");
                int realized = rows.GetVisualDescendants().OfType<ListBoxItem>().Count();

                Assert.Equal(10_000, table.Models.Count);
                Assert.True(realized is > 0 and < 100,
                    $"{realized} of 10,000 rows were realized. Under 100 means the ListBox is virtualizing; "
                    + "10,000 means it is not, and every cell of every row exists.");
            });

        // A CANARY ON UPSTREAM, not a claim about LunaTable. It pins the Avalonia behaviour that
        // LunaTable.Define works around: a definition ADDED to a grid's own collection joins the
        // shared size scope, and an identical one ASSIGNED as a ready-made collection does not.
        //
        // AvaloniaUI/Avalonia#21848 fixes this on main, merged after 12.1.0 shipped. When a version
        // carrying the fix is taken, THIS TEST FAILS - which is the intended outcome. It is the
        // notice that Define's comment has become history rather than a live hazard, and that the
        // workaround is now a choice rather than a requirement. Do not delete it to make a bump
        // green; read it, then decide what Define should say.
        [Fact]
        public Task Avalonia_still_ignores_an_assigned_definition_collection() => Session.Dispatch(() =>
        {
            static Grid Grid2(string text, bool assign, string group)
            {
                var grid = new Grid();
                var definitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = group },
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                };

                if (assign)
                {
                    grid.ColumnDefinitions = definitions;
                }
                else
                {
                    foreach (ColumnDefinition definition in definitions) grid.ColumnDefinitions.Add(definition);
                }

                var wide = new TextBlock { Text = text };
                var filler = new TextBlock { Text = "x" };
                Grid.SetColumn(filler, 1);
                grid.Children.Add(wide);
                grid.Children.Add(filler);
                return grid;
            }

            static double Spread(bool assign, string group)
            {
                Grid wide = Grid2("a much wider heading", assign, group);
                Grid narrow = Grid2("narrow", assign, group);
                var scope = new StackPanel { Children = { wide, narrow } };
                Grid.SetIsSharedSizeScope(scope, true);

                var window = new ToolWindow { Width = 600, Height = 400, Content = scope };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                double spread = Math.Abs(wide.ColumnDefinitions[0].ActualWidth - narrow.ColumnDefinitions[0].ActualWidth);
                window.Close();
                return spread;
            }

            Assert.True(Spread(assign: false, "canaryAdded") < 0.5,
                "An ADDED definition no longer shares its size. That is not the defect this pins - "
                + "LunaTable.Define depends on adding working, so this is a real regression.");

            Assert.True(Spread(assign: true, "canaryAssigned") > 0.5,
                "An ASSIGNED definition now shares its size, so AvaloniaUI/Avalonia#21848 has arrived in "
                + "the referenced version. Read LunaTable.Define and decide what it should say now.");
        }, default);

        [Fact]
        public Task A_table_hands_back_the_model_not_the_row() => Realised(table =>
        {
            table.Select(Fields[1]);

            Assert.Equal("Technician", table.Selected!.Name);
            Assert.Equal(1, table.Selected.Page);
        });

        // The same dance LunaList does, for the same reason: rows rebuilt from disk are new
        // objects every time, so reference identity would lose the selection on every poll.
        [Fact]
        public Task A_table_keeps_the_selection_across_a_refresh() => Realised(table =>
        {
            table.Select(Fields[1]);

            table.Refresh(new[]
            {
                new Field("Site", "text", 1),
                new Field("Technician", "checkbox", 3),
                new Field("Approved", "checkbox", 2),
            });

            Assert.Equal("Technician", table.Selected!.Name);
            Assert.Equal(3, table.Selected.Page);
        });

        [Fact]
        public Task A_table_clears_the_selection_when_the_row_disappears() => Realised(table =>
        {
            table.Select(Fields[1]);

            table.Refresh(new[] { new Field("Site", "text", 1) });

            Assert.Null(table.Selected);
        });

        [Fact]
        public Task A_table_does_not_raise_chose_for_a_restored_selection() => Realised(table =>
        {
            table.Select(Fields[1]);

            int chose = 0;
            table.Chose += _ => chose++;
            table.Refresh(Fields);

            Assert.Equal(0, chose);
            Assert.Equal("Technician", table.Selected!.Name);
        });

        // A caller that filled the table before it had a template - which is how every window in
        // this toolkit is built - still gets its rows.
        [Fact]
        public Task Rows_given_before_the_template_existed_still_appear() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name);
                table.Refresh(Fields);
                return table;
            },
            assert: table => Assert.Equal(3,
                table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>().Count()));

        // THE DEFECT A RENDER FOUND AND NO TEST DID. A window here is built in its constructor, so
        // a caller fills the table and selects a row before anything is on screen; an early Select
        // that quietly did nothing left a table with no row highlighted and nothing to explain it.
        [Fact]
        public Task A_row_selected_before_the_template_existed_is_still_selected() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field> { Key = f => f.Name };
                table.Column("name", f => f.Name);
                table.Refresh(Fields);
                table.Select(Fields[2]);
                return table;
            },
            assert: table =>
            {
                Assert.Equal("Approved", table.Selected!.Name);
                Assert.Single(table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>(), i => i.IsSelected);
            });

        // WHAT A READER HEARS. Three bare TextBlocks in a grid announce as three values with
        // nothing to say which column each came from; pairing each with its header is the
        // information the column layout carries visually. §27.3.
        [Fact]
        public Task A_row_announces_its_cells_with_the_column_they_are_in() => Realised(table =>
        {
            Grid row = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>().First()
                .GetVisualDescendants().OfType<Grid>().First();

            Assert.Equal("name: Site, type: text, pg: 1", AutomationProperties.GetName(row));
        });

        // A Group, deliberately not DataGrid or Table: those UIA types promise IGridProvider and
        // ITableProvider, and this control implements neither. §27.3.
        [Fact]
        public Task A_table_is_in_the_control_view_without_promising_grid_navigation() => Realised(table =>
        {
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(table);

            Assert.True(peer.IsControlElement());
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Null(peer.GetProvider<Avalonia.Automation.Provider.ISelectionProvider>());
        });
    }
}
