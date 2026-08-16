using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // EDITING A CELL, AND THE FOUR TRAPS PLAN-table.md §6 NAMED BEFORE ANY OF IT WAS BUILT - see
    // docs/LunaP.md §50.
    //
    // Each of those traps is a way this feature can look finished and be broken, and every one is a
    // test here rather than a paragraph. The two worth knowing before reading the rest:
    //
    //   - A ROW THAT LEAPS. The table re-applies its sort whenever the view is rebuilt, so an edit
    //     that rebuilt the view would move the row being edited to wherever its new value sorts,
    //     with the caret still in it. That is why the editor is placed into the existing row grid
    //     and why nothing in the commit path calls Show().
    //   - A RECYCLED EDITOR. The list recycles containers, so the row that scrolls back into view
    //     is built from a different model in the same visual. An editor left behind would appear on
    //     a row nobody opened, attached to a model nobody edited.
    //
    // The model here is a mutable class rather than TableTests' record, because Commit writes to it
    // and a positional record has nothing to write to.
    public class TableEditingTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableEditingTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, int size)
            {
                Name = name;
                Size = size;
            }

            public string Name { get; set; }
            public int Size { get; set; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("charlie", 30),
            new Row("alpha", 10),
            new Row("bravo", 20),
        };

        // "name" is editable and validated; "size" is editable and unvalidated; there is no
        // read-only column here except by omission, which one test below relies on.
        private static LunaTable<Row> Build(Row[] rows, bool sortable = false)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
            {
                Width = "2*",
                Sort = sortable ? (a, b) => string.CompareOrdinal(a.Name, b.Name) : null,
                Commit = (r, text) => r.Name = text,
                Validate = (_, text) => string.IsNullOrWhiteSpace(text) ? "A name is required." : null,
            });

            table.Column(new LunaColumn<Row>("size", r => r.Size.ToString())
            {
                Commit = (r, text) => r.Size = int.Parse(text),
                Validate = (_, text) => int.TryParse(text, out int _) ? null : "Not a number.",
            });

            // A third column with no Commit at all, which is the default and the read-only case.
            table.Column("kind", _ => "file");

            table.Refresh(rows);
            return table;
        }

        private static Task Realised(Row[] rows, Action<LunaTable<Row>, Window> assert, bool sortable = false) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = Build(rows, sortable);
                var window = new ToolWindow { Width = 500, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table, window);
                window.Close();
            }, default);

        private static TextBox Editor(LunaTable<Row> table) =>
            table.GetVisualDescendants().OfType<TextBox>().Single();

        private static Grid RowGrid(LunaTable<Row> table, string name) =>
            table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == name)
                .GetVisualDescendants().OfType<Grid>().First();

        [Fact]
        public Task Opening_a_cell_puts_an_editor_in_it_carrying_the_current_value() =>
            Realised(Rows(), (table, _) =>
            {
                table.Edit(table.Models[0], 0);

                Assert.True(table.IsEditing);
                Assert.Equal("charlie", Editor(table).Text);
            });

        [Fact]
        public Task A_read_only_column_does_not_open() => Realised(Rows(), (table, _) =>
        {
            table.Edit(table.Models[0], 2);

            Assert.False(table.IsEditing);
            Assert.Empty(table.GetVisualDescendants().OfType<TextBox>());
        });

        [Fact]
        public Task Enter_writes_the_value_through_Commit() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 0);
            Editor(table).Text = "delta";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("delta", rows[0].Name);
            Assert.False(table.IsEditing);

            window.Close();
        }, default);

        // Escape restores by never having written: the model is untouched until Commit runs, so
        // there is nothing to roll back and no copy of the old value to keep in step.
        [Fact]
        public Task Escape_leaves_the_model_alone() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 0);
            Editor(table).Text = "delta";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            Assert.Equal("charlie", rows[0].Name);
            Assert.False(table.IsEditing);

            window.Close();
        }, default);

        // A REJECTED EDIT KEEPS THE CARET. Closing the editor would throw away what was typed and
        // then show a message about a value that is no longer on screen.
        [Fact]
        public Task A_value_Validate_refuses_is_not_written_and_the_editor_stays_open() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 0);
            Editor(table).Text = "   ";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.True(table.IsEditing);
            Assert.Equal("charlie", rows[0].Name);

            ErrorText message = table.FindNamed<ErrorText>("PART_Error");
            Assert.True(message.IsVisible);
            Assert.Equal("A name is required.", message.Text);

            window.Close();
        }, default);

        [Fact]
        public Task A_message_goes_away_once_the_value_is_acceptable() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 1);
            Editor(table).Text = "not a number";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.True(table.FindNamed<ErrorText>("PART_Error").IsVisible);

            Editor(table).Text = "42";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.False(table.FindNamed<ErrorText>("PART_Error").IsVisible);
            Assert.Equal(42, rows[0].Size);

            window.Close();
        }, default);

        // TRAP 2. The row being edited must not move. Sorting is by name, the edit changes the name
        // to one that sorts elsewhere, and the row has to stay where the caret is.
        [Fact]
        public Task An_edit_to_the_sorted_column_does_not_move_its_row() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows, sortable: true);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.FindNamed<Grid>("PART_Header").GetVisualDescendants().OfType<Button>().First()
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { "alpha", "bravo", "charlie" }, table.Models.Select(r => r.Name));

            Row edited = table.Models[0];
            table.Edit(edited, 0);
            Editor(table).Text = "zulu";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            // "zulu" sorts last, and the row has NOT moved: the view is untouched until something
            // asks for it to be rebuilt.
            Assert.Equal("zulu", edited.Name);
            Assert.Equal(0, table.Models.ToList().IndexOf(edited));

            // And the next Refresh does re-sort it, so the sort is deferred rather than lost.
            table.Refresh(rows);
            Assert.Equal(2, table.Models.ToList().IndexOf(edited));

            window.Close();
        }, default);

        // TRAP 3. A row's accessible name is built from its cells. Rebuilt after a commit, or a
        // reader announces the old value for as long as the row stays on screen.
        [Fact]
        public Task A_committed_edit_rebuilds_what_a_reader_hears() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("name: charlie, size: 30, kind: file", AutomationProperties.GetName(RowGrid(table, "charlie")));

            table.Edit(table.Models[0], 0);
            Editor(table).Text = "delta";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("name: delta, size: 30, kind: file", AutomationProperties.GetName(RowGrid(table, "delta")));

            window.Close();
        }, default);

        // The cell re-reads through the projection rather than echoing what was typed, so a Commit
        // that normalises a value cannot leave the display and the model disagreeing.
        [Fact]
        public Task The_cell_shows_what_the_model_holds_and_not_what_was_typed() => Session.Dispatch(() =>
        {
            var rows = new[] { new Row("charlie", 30) };
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column(new LunaColumn<Row>("name", r => r.Name)
            {
                Commit = (r, text) => r.Name = text.Trim().ToUpperInvariant(),
            });
            table.Refresh(rows);

            var window = new ToolWindow { Width = 400, Height = 200, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(rows[0], 0);
            Editor(table).Text = "  delta  ";
            Editor(table).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("DELTA", rows[0].Name);
            TextBlock cell = RowGrid(table, "DELTA").Children.OfType<TextBlock>().First();
            Assert.Equal("DELTA", cell.Text);

            window.Close();
        }, default);

        // F2 is the keyboard route, and the reason it exists is that a table whose cells only open
        // to a pointer is a table half this toolkit's users cannot edit (§24).
        [Fact]
        public Task F2_opens_the_first_editable_cell_of_the_selected_row() => Realised(Rows(), (table, _) =>
        {
            table.Select(table.Models[1]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F2 });

            Assert.True(table.IsEditing);
            Assert.Equal("alpha", Editor(table).Text);
        });

        [Fact]
        public Task F2_with_nothing_selected_opens_nothing() => Realised(Rows(), (table, _) =>
        {
            table.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F2 });

            Assert.False(table.IsEditing);
        });

        // THE EXEMPTION IN TemplateOrderTests IS A CLAIM, AND THIS IS THE CLAIM. Edit does not queue
        // the way Select does: a caret belongs to somebody who is looking at the table, and a queued
        // one would open an editor nobody asked for when the window appeared.
        [Fact]
        public Task Editing_before_there_is_a_row_does_nothing() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);

            table.Edit(rows[0], 0);
            Assert.False(table.IsEditing);

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Still nothing: the call was dropped rather than deferred.
            Assert.False(table.IsEditing);
            Assert.Empty(table.GetVisualDescendants().OfType<TextBox>());

            window.Close();
        }, default);

        // TRAP 1. The list recycles containers, so a row scrolled out and back is the same visual
        // rebuilt from a different model. An editor surviving that would sit on a row nobody opened.
        [Fact]
        public Task A_row_scrolled_out_of_view_does_not_come_back_carrying_an_editor() => Session.Dispatch(() =>
        {
            Row[] many = Enumerable.Range(0, 60).Select(i => new Row($"row{i:D2}", i)).ToArray();
            LunaTable<Row> table = Build(many);
            table.Height = 90;

            var window = new ToolWindow { Width = 500, Height = 140, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(many[0], 0);
            Assert.True(table.IsEditing);

            // TYPED, AND THAT IS THE WHOLE OF WHAT MAKES THIS TEST ABLE TO FAIL. An earlier version
            // opened the editor and scrolled without changing anything, so committing and cancelling
            // produced the identical model and the assertion below could not tell them apart -
            // removing the mechanism under test left it green. §50.4.
            Editor(table).Text = "SCROLLED AWAY";

            ScrollViewer scroller = table.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new Vector(0, 800);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            scroller.Offset = new Vector(0, 0);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(table.IsEditing);
            Assert.Empty(table.GetVisualDescendants().OfType<TextBox>());
            Assert.Equal("row00", many[0].Name);

            window.Close();
        }, default);

        // PASS H - THE AUTOMATION DEPTH, and a defect found while measuring for it.
        //
        // PLAN-table.md §2.1 said the accessibility gap was two providers LunaP did not use. Half of
        // that turned out to be already closed and half turned out to hide something worse - see
        // docs/LunaP.md §50.5 and §50.6.

        // ALREADY PROVIDED, AND WORTH A TEST ANYWAY. ISelectionItemProvider comes from Avalonia's own
        // ListItemAutomationPeer because a row IS a ListBoxItem, so this was never work - but a
        // future change that stopped using a ListBox would take it away silently.
        [Fact]
        public Task A_row_can_be_selected_by_a_screen_reader() => Realised(Rows(), (table, _) =>
        {
            ListBoxItem container = table.GetVisualDescendants().OfType<ListBoxItem>().First();
            var provider = ControlAutomationPeer.CreatePeerForElement(container)
                .GetProvider<Avalonia.Automation.Provider.ISelectionItemProvider>();

            Assert.NotNull(provider);

            provider!.Select();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(provider.IsSelected);
            Assert.Equal("charlie", table.Selected!.Name);
        });

        // THE DEFECT §50.5 RECORDS. The row's spoken sentence was set on the row Grid, whose peer is
        // a NoneAutomationPeer and is not in the control view at all - so a reader heard the
        // CONTAINER's name, and a ListBoxItem with no name falls back to its DataContext.ToString().
        //
        // ASKS THE PEER, NOT THE ATTACHED PROPERTY, which is the whole reason the old guard could
        // not catch this: reading AutomationProperties.GetName straight back off the Grid asserts
        // that a value was stored, not that anybody can hear it.
        [Fact]
        public Task What_a_reader_hears_for_a_row_is_the_cells_and_not_the_model_type() =>
            Realised(Rows(), (table, _) =>
            {
                ListBoxItem container = table.GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Row)?.Name == "charlie");

                string? heard = ControlAutomationPeer.CreatePeerForElement(container).GetName();

                Assert.Equal("name: charlie, size: 30, kind: file", heard);
                Assert.DoesNotContain("Row", heard ?? string.Empty, StringComparison.Ordinal);
            });

        [Fact]
        public Task A_recycled_row_is_renamed_for_the_model_it_now_holds() => Session.Dispatch(() =>
        {
            Row[] many = Enumerable.Range(0, 60).Select(i => new Row($"row{i:D2}", i)).ToArray();
            LunaTable<Row> table = Build(many);
            table.Height = 90;

            var window = new ToolWindow { Width = 500, Height = 140, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollViewer scroller = table.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new Vector(0, 800);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            foreach (ListBoxItem container in table.GetVisualDescendants().OfType<ListBoxItem>())
            {
                var model = (Row)container.DataContext!;
                Assert.Equal($"name: {model.Name}, size: {model.Size}, kind: file",
                    ControlAutomationPeer.CreatePeerForElement(container).GetName());
            }

            window.Close();
        }, default);

        // IValueProvider, which Avalonia's TextBlockAutomationPeer does not offer - measured before
        // this was built, not assumed.
        [Fact]
        public Task An_editable_cell_offers_its_value_to_a_reader() => Realised(Rows(), (table, _) =>
        {
            var provider = CellProvider(table, "charlie", 0);

            Assert.NotNull(provider);
            Assert.False(provider!.IsReadOnly);
            Assert.Equal("charlie", provider.Value);
        });

        [Fact]
        public Task A_read_only_column_says_so_rather_than_hiding_its_value() => Realised(Rows(), (table, _) =>
        {
            var provider = CellProvider(table, "charlie", 2);

            Assert.NotNull(provider);
            Assert.True(provider!.IsReadOnly);
            Assert.Equal("file", provider.Value);
        });

        [Fact]
        public Task A_reader_writing_a_cell_goes_through_Commit() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            CellProvider(table, "charlie", 0)!.SetValue("delta");

            Assert.Equal("delta", rows[0].Name);
            Assert.Equal("name: delta, size: 30, kind: file",
                ControlAutomationPeer.CreatePeerForElement(
                    table.GetVisualDescendants().OfType<ListBoxItem>()
                        .First(c => (c.DataContext as Row)?.Name == "delta")).GetName());

            window.Close();
        }, default);

        // A READER CANNOT WRITE WHAT A TYPIST COULD NOT. The same Validate gate, or an assistive
        // technology becomes a way around the rules the control enforces for everybody else.
        [Fact]
        public Task A_reader_cannot_write_a_value_Validate_refuses() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            LunaTable<Row> table = Build(rows);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            CellProvider(table, "charlie", 1)!.SetValue("not a number");

            Assert.Equal(30, rows[0].Size);
            Assert.True(table.FindNamed<ErrorText>("PART_Error").IsVisible);

            window.Close();
        }, default);

        private static Avalonia.Automation.Provider.IValueProvider? CellProvider(
            LunaTable<Row> table, string name, int column)
        {
            Control cell = RowGrid(table, name).Children
                .OfType<Control>()
                .First(c => Grid.GetColumn(c) == column);

            return ControlAutomationPeer.CreatePeerForElement(cell)
                .GetProvider<Avalonia.Automation.Provider.IValueProvider>();
        }
    }
}
