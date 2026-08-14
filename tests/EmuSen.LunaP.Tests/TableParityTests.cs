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
    }
}
