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
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // THE GUTTER DOWN THE LEFT - see docs/LunaP.md §58.
    //
    // Half of pass 4 of §54's parity arc. The thing that can quietly go wrong here is not the gutter
    // itself but everything BEHIND it: a row header shifts every column one place right in the grid
    // while leaving every column INDEX where it was, and those two indices are used by different
    // parts of the control. A remembered layout, a sort, Edit(item, 2) and TryGetCell all speak in
    // column indices; Grid.SetColumn and ColumnDefinitions speak in grid indices. Conflating them
    // moves a dragged width onto its neighbour and saves that to disk.
    //
    // So most of this file is about columns rather than about the gutter.
    public class TableRowHeaderTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableRowHeaderTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, int size)
            {
                Name = name;
                Size = size;
            }

            public string Name { get; set; }
            public int Size { get; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("charlie", 30), new Row("alpha", 10), new Row("bravo", 20),
        };

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

        private static LunaTable<Row> Numbered(Row[] rows, bool sortable = false)
        {
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                RowHeader = (_, i) => (i + 1).ToString(),
                RowHeaderCaption = "#",
            };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
                 {
                     Width = "2*",
                     Sort = sortable ? (a, b) => string.CompareOrdinal(a.Name, b.Name) : null,
                     Commit = (r, text) => r.Name = text,
                 })
                 .Column("size", r => r.Size.ToString());

            table.Refresh(rows);
            return table;
        }

        private static Grid RowGrid(LunaTable<Row> table, string name) =>
            table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == name)
                .GetVisualDescendants().OfType<Grid>().First();

        private static string Gutter(LunaTable<Row> table, string name) =>
            RowGrid(table, name).Children.OfType<TextBlock>()
                .First(t => t.Classes.Contains("row-header")).Text ?? string.Empty;

        // ---- it is there, and it is off by default ----

        [Fact]
        public Task A_table_has_no_gutter_unless_it_is_asked_for() => Realised(
            () =>
            {
                var table = new LunaTable<Row> { Key = r => r.Name };
                table.Column("name", r => r.Name).Column("size", r => r.Size.ToString());
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                Assert.Null(table.RowHeader);
                Assert.Equal(2, table.FindNamed<Grid>("PART_Header").ColumnDefinitions.Count);
                Assert.Empty(table.GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.Classes.Contains("row-header")));
            });

        [Fact]
        public Task A_gutter_adds_one_definition_in_front_of_the_columns() =>
            Realised(() => Numbered(Rows()), table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Assert.Equal(3, header.ColumnDefinitions.Count);
                Assert.Equal(3, RowGrid(table, "alpha").ColumnDefinitions.Count);
            });

        [Fact]
        public Task The_gutter_numbers_rows_down_the_screen() =>
            Realised(() => Numbered(Rows()), table =>
            {
                Assert.Equal("1", Gutter(table, "charlie"));
                Assert.Equal("2", Gutter(table, "alpha"));
                Assert.Equal("3", Gutter(table, "bravo"));
            });

        // THE INDEX IS THE DISPLAYED ONE, which is the whole reason RowHeader takes one rather than
        // the caller closing over a counter. Under a sort the rows move and the numbers do not: row 1
        // is whatever is at the top now, not whatever was first in the list handed to Refresh.
        [Fact]
        public Task Sorting_renumbers_the_gutter_rather_than_carrying_numbers_with_rows() =>
            Realised(() => Numbered(Rows(), sortable: true), table =>
            {
                Assert.Equal("1", Gutter(table, "charlie"));

                Button heading = table.FindNamed<Grid>("PART_Header")
                    .GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("heading"));
                heading.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal("alpha", table.Models[0].Name);
                Assert.Equal("1", Gutter(table, "alpha"));
                Assert.Equal("3", Gutter(table, "charlie"));
            });

        [Fact]
        public Task A_gutter_can_label_from_the_model_instead_of_counting() => Realised(
            () =>
            {
                var table = new LunaTable<Row>
                {
                    Key = r => r.Name,
                    RowHeader = (row, _) => row.Size.ToString("X4"),
                    RowHeaderCaption = "addr",
                };
                table.Column("name", r => r.Name);
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                Assert.Equal("001E", Gutter(table, "charlie"));
                Assert.Equal("000A", Gutter(table, "alpha"));
            });

        [Fact]
        public Task The_caption_sits_above_the_gutter_and_is_not_a_sort_button() =>
            Realised(() => Numbered(Rows(), sortable: true), table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                TextBlock corner = header.Children.OfType<TextBlock>()
                    .First(t => t.Classes.Contains("row-header"));

                Assert.Equal("#", corner.Text);
                Assert.Equal(0, Grid.GetColumn(corner));
                Assert.IsNotType<Button>(corner);
            });

        // ---- the indices behind it ----

        // THE ONE THAT MATTERS. Column 0 is still "name" with a gutter in front of it, so an editor
        // opens on the name and not on the gutter or on "size". Sabotaged by dropping GridColumn from
        // the editor's placement, which puts the caret one column left of the text it is editing.
        [Fact]
        public Task A_column_index_still_means_the_same_column() =>
            Realised(() => Numbered(Rows()), table =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 0, out Control? name));
                Assert.Equal("charlie", Assert.IsAssignableFrom<TextBlock>(name!).Text);

                table.Edit(table.Models[0], 0);
                Assert.True(table.IsEditing);

                TextBox editor = table.GetVisualDescendants().OfType<TextBox>().Single();
                Assert.Equal("charlie", editor.Text);

                // Grid column 1, because the gutter is grid column 0 - and column index 0 all the same.
                Assert.Equal(1, Grid.GetColumn(editor));
            });

        [Fact]
        public Task The_cells_sit_one_grid_column_right_of_their_index() =>
            Realised(() => Numbered(Rows()), table =>
            {
                Grid row = RowGrid(table, "alpha");

                Assert.True(table.TryGetCell(table.Models[1], 0, out Control? first));
                Assert.True(table.TryGetCell(table.Models[1], 1, out Control? second));

                Assert.Equal(1, Grid.GetColumn(first!));
                Assert.Equal(2, Grid.GetColumn(second!));
                Assert.Equal(0, Grid.GetColumn(row.Children.OfType<TextBlock>()
                    .First(t => t.Classes.Contains("row-header"))));
            });

        // A DRAGGED WIDTH LANDS ON THE COLUMN THAT WAS DRAGGED. Resized() reads the HEADER's
        // definitions by grid index and writes the SPECS by column index; getting that pairing wrong
        // moves every width one column left and then saves it. Sabotaged by dropping GridColumn from
        // Resized, which puts the gutter's width onto column 0.
        [Fact]
        public Task A_resize_writes_the_width_onto_the_column_that_was_dragged() =>
            Realised(() => Numbered(Rows()), table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");

                // Column 0 is grid column 1. Drive the header the way a GridSplitter does.
                header.ColumnDefinitions[1].Width = new GridLength(150);
                GridSplitter grip = table.GetVisualDescendants().OfType<GridSplitter>().First();
                grip.RaiseEvent(new Avalonia.Input.VectorEventArgs
                {
                    RoutedEvent = Avalonia.Controls.Primitives.Thumb.DragCompletedEvent,
                });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();

                // Every realised row now agrees, at grid column 1 rather than 0.
                Grid row = RowGrid(table, "alpha");
                Assert.Equal(150, row.ColumnDefinitions[1].Width.Value);
                Assert.True(row.ColumnDefinitions[0].Width.IsAuto, "the gutter kept its own width");
            });

        [Fact]
        public Task The_resize_grip_sits_in_the_column_it_resizes() =>
            Realised(() => Numbered(Rows()), table =>
            {
                GridSplitter grip = table.GetVisualDescendants().OfType<GridSplitter>().Single();

                // One grip - between the two columns - and it belongs to column 0, at grid column 1.
                Assert.Equal(1, Grid.GetColumn(grip));
                Assert.Equal("Resize name", AutomationProperties.GetName(grip));
            });

        // ---- what a reader hears ----

        // THE GUTTER GOES IN FRONT OF THE SENTENCE, because it is how a user refers to the row. A
        // reader that heard it last would have to hold the whole row to find out which one it was.
        [Fact]
        public Task A_reader_hears_the_gutter_before_the_cells() =>
            Realised(() => Numbered(Rows()), table =>
            {
                ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Row)?.Name == "alpha");

                Assert.Equal(
                    "# 2: name: alpha, size: 10",
                    ControlAutomationPeer.CreatePeerForElement(container).GetName());
            });

        [Fact]
        public Task An_uncaptioned_gutter_is_read_bare() => Realised(
            () =>
            {
                var table = new LunaTable<Row> { Key = r => r.Name, RowHeader = (_, i) => (i + 1).ToString() };
                table.Column("name", r => r.Name);
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Row)?.Name == "alpha");

                Assert.Equal("2: name: alpha", ControlAutomationPeer.CreatePeerForElement(container).GetName());
            });

        // AND IS NOT HEARD TWICE. The row's sentence already carries the label, so the TextBlock
        // itself is out of the control view - the same choice the sort glyph makes (§27.3).
        [Fact]
        public Task The_gutter_itself_is_not_in_the_control_view() =>
            Realised(() => Numbered(Rows()), table =>
            {
                TextBlock gutter = RowGrid(table, "alpha").Children.OfType<TextBlock>()
                    .First(t => t.Classes.Contains("row-header"));

                Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(gutter));
            });
    }
}
