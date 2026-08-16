using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // Column widths a user dragged, and the sort they left a table in - see docs/LunaP.md §27.11.
    //
    // Separate from TableTests because these need a settings store of their own, and a fixture that
    // writes files does not belong bolted to the suite that does not.
    public class TableLayoutTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableLayoutTests).GetTypeInfo().Assembly);

        private readonly string _configDir;

        // Keeps remembered table layout off whoever is running the suite, exactly as ShellTests
        // keeps pane layout off them. §43 is what happens when a suite does not do this.
        public TableLayoutTests()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "lunap-table-" + Guid.NewGuid().ToString("N"));
            LunaSettings.Store = new JsonSettingsStore(_configDir);
        }

        public void Dispose()
        {
            LunaSettings.Store = new JsonSettingsStore(Path.Combine(Path.GetTempPath(), "lunap-unset"));
            if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
        }

        private sealed record Field(string Name, string Type, int Page);

        private static readonly Field[] Fields =
        {
            new("Beta", "text", 20),
            new("Alpha", "text", 30),
            new("Gamma", "text", 10),
        };

        private static LunaTable<Field> Make(string? key)
        {
            var table = new LunaTable<Field>();
            table.Column("name", f => f.Name, "2*")
                 .Column("type", f => f.Type, "Auto")
                 .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                 {
                     Width = "40",
                     Sort = (a, b) => a.Page.CompareTo(b.Page),
                 });
            table.TableKey = key;
            table.Refresh(Fields);
            return table;
        }

        private static void Shown(LunaTable<Field> table, Action<LunaTable<Field>> body)
        {
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            body(table);
            window.Close();
        }

        private static Button Heading(LunaTable<Field> table, string text) =>
            table.FindNamed<Grid>("PART_Header").Children.OfType<Button>()
                .First(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));

        private static void Click(Button heading)
        {
            heading.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // Opt-in, like every other persisted thing in the kit: no key, no file.
        [Fact]
        public Task A_table_with_no_key_is_never_written_down() => Session.Dispatch(() =>
        {
            LunaTable<Field> table = Make(null);
            Shown(table, t =>
            {
                Click(Heading(t, "pg"));
                t.SaveNow();
            });

            Assert.False(File.Exists(Path.Combine(_configDir, TableLayoutStore.FileName)),
                "A table with no TableKey wrote tables.json anyway.");
        }, default);

        [Fact]
        public Task A_sort_the_user_left_comes_back() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Click(Heading(t, "pg"));
                Click(Heading(t, "pg"));
                t.SaveNow();

                Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, t.Models.Select(f => f.Name));
            });

            TableLayout saved = TableLayoutStore.Load("fields")!;
            Assert.Equal("pg", saved.SortedBy);
            Assert.True(saved.Descending);

            Shown(Make("fields"), t =>
                Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, t.Models.Select(f => f.Name)));
        }, default);

        // NOBODY CALLS SaveNow, AND THAT IS THE TEST - see docs/LunaP.md §70.4.
        //
        // Every other test in this file calls it by hand, which is how a defect lived here since
        // §27.11: a heading click never poked the save debounce, and this control never flushed on
        // the way out of the visual tree the way SplitPane does. So the promise on the tin - "columns
        // sort, resize and remember where you left them" - held for a resize and not for a sort, and
        // the suite could not see it because every fixture forced the write.
        //
        // A user clicks a heading and closes the window. That is the whole scenario, and it was the
        // one path not covered.
        [Fact]
        public Task A_sort_survives_a_window_closing_without_anyone_asking_it_to() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t => Click(Heading(t, "pg")));

            TableLayout? saved = TableLayoutStore.Load("fields");

            Assert.NotNull(saved);
            Assert.Equal("pg", saved!.SortedBy);
            Assert.False(saved.Descending);

            Shown(Make("fields"), t =>
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, t.Models.Select(f => f.Name)));
        }, default);

        // The same for a sort nobody clicked, since SortBy is the programmatic door onto the same
        // state (§70.3) and a menu item is as much "where the user left it" as a heading is.
        [Fact]
        public Task A_sort_set_in_code_is_remembered_like_a_clicked_one() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t => t.SortBy(2, descending: true));

            TableLayout? saved = TableLayoutStore.Load("fields");

            Assert.NotNull(saved);
            Assert.Equal("pg", saved!.SortedBy);
            Assert.True(saved.Descending);
        }, default);

        // THE HEADING IS SAVED, NOT THE INDEX, and this is the case that makes the difference: a
        // caller who inserts a column at the front between two runs would otherwise come back sorted
        // by its neighbour, with the arrow pointing confidently at the wrong heading.
        //
        // Here the layout is simply refused, because the column COUNT changed too - which is the
        // outer guard. The header match is what protects a reorder that keeps the count the same.
        [Fact]
        public Task A_layout_whose_columns_no_longer_match_is_ignored() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Click(Heading(t, "pg"));
                t.SaveNow();
            });

            var narrower = new LunaTable<Field>();
            narrower.Column("name", f => f.Name, "2*")
                    .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                    {
                        Width = "40",
                        Sort = (a, b) => a.Page.CompareTo(b.Page),
                    });
            narrower.TableKey = "fields";
            narrower.Refresh(Fields);

            Shown(narrower, t =>
            {
                Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, t.Models.Select(f => f.Name));
                Assert.Equal(2, t.FindNamed<Grid>("PART_Header").ColumnDefinitions.Count);
            });
        }, default);

        // A column that stopped being sortable must not come back with a sort arrow on a heading
        // nobody can click.
        [Fact]
        public Task A_sort_on_a_column_that_is_no_longer_sortable_is_dropped() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Click(Heading(t, "pg"));
                t.SaveNow();
            });

            var plain = new LunaTable<Field>();
            plain.Column("name", f => f.Name, "2*")
                 .Column("type", f => f.Type, "Auto")
                 .Column("pg", f => f.Page.ToString(), "40");
            plain.TableKey = "fields";
            plain.Refresh(Fields);

            Shown(plain, t =>
            {
                Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, t.Models.Select(f => f.Name));
                Assert.Empty(t.FindNamed<Grid>("PART_Header").Children.OfType<Button>());

                // AND THE FILE HEALS RATHER THAN KEEPING THE DEAD ENTRY. This is the assertion the
                // check in Restore actually earns: dropping `c.Sort is not null` from the lookup
                // turned nothing red on display order, because Ordered() refuses to sort a column
                // with no comparison anyway - so the table looked identical either way and the
                // sabotage proved nothing. What differs is what gets written back: a stale index
                // pointing at an unsortable column re-saves that column as the sorted one, and the
                // entry outlives every release that could have used it. §46.3.
                t.SaveNow();
                Assert.Null(TableLayoutStore.Load("fields")!.SortedBy);
            });
        }, default);

        // THE ORDERING TRAP THE EXEMPTION IN TemplateOrderTests PROMISES IS COVERED. There is no
        // rule that puts TableKey after the columns - an object initializer invites the opposite -
        // so both orders have to reach the same table, and both have to work before the template.
        [Fact]
        public Task A_saved_layout_is_restored_whichever_order_it_is_set_in() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Click(Heading(t, "pg"));
                t.SaveNow();
            });

            // Key first, columns after - the object-initializer shape.
            var keyFirst = new LunaTable<Field> { TableKey = "fields" };
            keyFirst.Column("name", f => f.Name, "2*")
                    .Column("type", f => f.Type, "Auto")
                    .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                    {
                        Width = "40",
                        Sort = (a, b) => a.Page.CompareTo(b.Page),
                    });
            keyFirst.Refresh(Fields);

            Shown(keyFirst, t =>
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, t.Models.Select(f => f.Name)));

            // Columns first, key after - what Make does.
            Shown(Make("fields"), t =>
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, t.Models.Select(f => f.Name)));
        }, default);

        // THE CASE THAT MAKES Column's CALL TO Restore NECESSARY, AND THE ONE THE TEST ABOVE DOES
        // NOT REACH.
        //
        // Removing that call turned nothing red, which is how this test came to exist. Both orders
        // above are rescued by OnPartsAttached, which runs Restore once the template arrives and by
        // then has the columns; neither needed Column to do anything. The uncovered case is a caller
        // adding a column AFTER the template - which Column's own summary says is allowed - because
        // OnPartsAttached has already run, found no columns, and returned.
        //
        // §26.11 is the precedent for recording a sabotage that turned nothing red rather than
        // dropping it. This is the same thing one step further on: the sabotage that turned nothing
        // red said the guard was missing, not that the code was.
        [Fact]
        public Task A_saved_layout_reaches_columns_added_after_the_template() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Click(Heading(t, "pg"));
                t.SaveNow();
            });

            var late = new LunaTable<Field> { TableKey = "fields" };
            var window = new ToolWindow { Width = 500, Height = 300, Content = late };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // The template exists and the table has no columns yet. Everything below happens after
            // OnPartsAttached has been and gone.
            late.Column("name", f => f.Name, "2*")
                .Column("type", f => f.Type, "Auto")
                .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                {
                    Width = "40",
                    Sort = (a, b) => a.Page.CompareTo(b.Page),
                });
            late.Refresh(Fields);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);

            Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, late.Models.Select(f => f.Name));

            window.Close();
        }, default);

        // A width the user dragged comes back, and an untouched star column comes back as a star
        // column rather than as the pixels it happened to resolve to in yesterday's window.
        [Fact]
        public Task A_dragged_width_comes_back_and_an_untouched_one_stays_relative() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                Grid header = t.FindNamed<Grid>("PART_Header");
                header.ColumnDefinitions[0].Width = new GridLength(150);

                GridSplitter grip = header.Children.OfType<GridSplitter>().First();
                grip.RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                t.SaveNow();
            });

            TableLayout saved = TableLayoutStore.Load("fields")!;
            Assert.Equal(new[] { "150", "Auto", "40" }, saved.Widths);

            Shown(Make("fields"), t =>
            {
                Grid header = t.FindNamed<Grid>("PART_Header");
                Assert.True(header.ColumnDefinitions[0].Width.IsAbsolute);
                Assert.Equal(150, header.ColumnDefinitions[0].Width.Value);
            });
        }, default);

        // A HAND-EDITED OR TRUNCATED FILE MUST NOT HALF-APPLY. There is no GridLength.TryParse, so a
        // width that does not parse throws, and applying the ones before it would leave a table in a
        // state neither the caller nor the user asked for.
        [Fact]
        public Task A_layout_with_an_unparseable_width_is_refused_whole() => Session.Dispatch(() =>
        {
            TableLayoutStore.Update("fields", layout =>
            {
                layout.Widths = new System.Collections.Generic.List<string> { "150", "not-a-width", "40" };
                layout.SortedBy = "pg";
            });

            Shown(Make("fields"), t =>
            {
                Grid header = t.FindNamed<Grid>("PART_Header");

                Assert.True(header.ColumnDefinitions[0].Width.IsStar,
                    "The first width was applied before the third failed to parse, so the table is half restored.");
                Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, t.Models.Select(f => f.Name));
            });
        }, default);

        // The grip carries a name, for the same reason SplitPane's divider does - §26.11 records the
        // sabotage that reported "Focusable but unnamed: GridSplitter".
        [Fact]
        public Task Every_resize_grip_can_be_found_and_named() => Session.Dispatch(() =>
        {
            Shown(Make("fields"), t =>
            {
                GridSplitter[] grips = t.FindNamed<Grid>("PART_Header").Children.OfType<GridSplitter>().ToArray();

                // Two, not three: no grip after the last column, because nothing is to its right.
                Assert.Equal(2, grips.Length);
                Assert.Equal(new[] { "Resize name", "Resize type" },
                    grips.Select(AutomationProperties.GetName));
                Assert.All(grips, g => Assert.True(g.Focusable));
            });
        }, default);
    }
}
