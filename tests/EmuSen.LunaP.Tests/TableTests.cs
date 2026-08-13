using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

        // Shared size groups are what make an Auto column line up between the header grid and each
        // row grid, which are separate grids that would otherwise size independently.
        [Fact]
        public Task Columns_share_a_size_group_with_their_header() => Realised(table =>
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
