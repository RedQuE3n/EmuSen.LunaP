using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // THE ASSERTION THAT WAS MISSING WHEN THE ALIGNMENT FIX SHIPPED, AND THE REASON IT COST
        // SOMETHING - see docs/LunaP.md §27.10.
        //
        // Putting every column in a shared size group made the Auto columns line up and made the
        // STAR columns stop filling: a shared size group forces a star column to behave as Auto
        // (Avalonia #19114). Measured at the time on two otherwise identical grids - 360.0 outside a
        // scope, 36.0 inside one - so the table's "name" column shrank to its longest name and the
        // whole table clumped into the left third of its own width.
        //
        // Twenty-six tests in this file said nothing, because every one of them asked where a column
        // STARTS and none asked how wide it ended up. Alignment and fill are different properties
        // and the suite only had the first.
        //
        // Summing the columns rather than checking the star one is deliberate: it needs no arithmetic
        // about what the remainder should be, and it fails the same way for any column type that
        // stops taking its share.
        [Fact]
        public Task The_columns_fill_the_width_they_are_given() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name, "2*")
                     .Column("type", f => f.Type, "Auto")
                     .Column("pg", f => f.Page.ToString(), "40");
                table.Refresh(Fields);
                return table;
            },
            assert: table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                double total = header.ColumnDefinitions.Sum(c => c.ActualWidth);

                Assert.True(header.Bounds.Width > 100,
                    $"The header grid is only {header.Bounds.Width:F1} wide, so this asserts nothing.");

                Assert.True(Math.Abs(total - header.Bounds.Width) < 1.0,
                    $"The columns resolve to {total:F1} in a header grid {header.Bounds.Width:F1} wide, so "
                    + $"{header.Bounds.Width - total:F1} of the table is empty. A star column that stops "
                    + "filling is a star column in a shared size group - see LunaTable.Define.");
            });

        // SORTING - see docs/LunaP.md §27.
        //
        // The fixture arrives in an order that is NEITHER ascending NOR descending, and that is a
        // load-bearing property rather than an aesthetic one (§46.3). Every assertion below about
        // returning to arrival order would also pass against a two-state implementation if the rows
        // happened to arrive sorted, because a two-state cycle's third click lands on ascending. The
        // fixture is part of the guard.
        private static readonly Field[] Unordered =
        {
            new("Beta", "text", 20),
            new("Alpha", "text", 30),
            new("Gamma", "text", 10),
        };

        private static LunaTable<Field> Sortable()
        {
            var table = new LunaTable<Field> { Key = f => f.Name };
            table.Column("name", f => f.Name, "2*")
                 .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                 {
                     Width = "60",
                     Sort = (a, b) => a.Page.CompareTo(b.Page),
                 });
            table.Refresh(Unordered);
            return table;
        }

        private static Button Heading(LunaTable<Field> table, string text) =>
            table.FindNamed<Grid>("PART_Header").Children.OfType<Button>()
                .First(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));

        private static string[] Shown(LunaTable<Field> table) =>
            table.Models.Select(f => f.Name).ToArray();

        // Raises the Click event rather than synthesising a pointer press, which keeps these tests
        // about the sort instead of about Avalonia's hit testing. The real input path is covered
        // once, separately, by A_heading_can_be_sorted_from_the_keyboard_alone - which sends a key
        // and lets Button turn it into a click, so nothing here assumes that step works.
        private static void Click(Button heading)
        {
            heading.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // The whole cycle in one test, because the states are only meaningful against each other.
        [Fact]
        public Task A_header_click_cycles_ascending_then_descending_then_back_to_arrival_order() => Realised(
            make: Sortable,
            assert: table =>
            {
                Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, Shown(table));

                Button pg = Heading(table, "pg");

                Click(pg);
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Shown(table));

                Click(pg);
                Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, Shown(table));

                // The third state, and the one a two-state cycle cannot produce: 20, 30, 10 is
                // neither sorted direction, so nothing but a return to arrival order gives it.
                Click(pg);
                Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, Shown(table));

                Click(pg);
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Shown(table));
            });

        // A sort is a rebuild, and every rebuild in this control keeps the selection (§27).
        [Fact]
        public Task A_sort_keeps_the_selected_row() => Realised(
            make: Sortable,
            assert: table =>
            {
                table.Select(Unordered[1]);
                Assert.Equal("Alpha", table.Selected!.Name);

                Click(Heading(table, "pg"));

                Assert.Equal("Alpha", table.Selected!.Name);
                Assert.Single(table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>(), i => i.IsSelected);
            });

        // The trap that makes a sort worthless in a polling window: new rows arriving under an
        // active sort must land sorted, or the sort lasts until the next poll and no longer.
        [Fact]
        public Task A_sort_survives_a_refresh() => Realised(
            make: Sortable,
            assert: table =>
            {
                Click(Heading(table, "pg"));
                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Shown(table));

                table.Refresh(new[]
                {
                    new Field("Delta", "text", 40),
                    new Field("Epsilon", "text", 5),
                });

                Assert.Equal(new[] { "Epsilon", "Delta" }, Shown(table));
            });

        // Ties keep the order Refresh was given. List<T>.Sort is an unstable introsort and would
        // shuffle them, which is invisible until somebody sorts a column with repeats.
        [Fact]
        public Task Rows_that_compare_equal_keep_the_order_they_arrived_in() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name)
                     .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                     {
                         Sort = (a, b) => a.Page.CompareTo(b.Page),
                     });
                table.Refresh(new[]
                {
                    new Field("first", "text", 1),
                    new Field("second", "text", 1),
                    new Field("third", "text", 1),
                });
                return table;
            },
            assert: table =>
            {
                Click(Heading(table, "pg"));
                Assert.Equal(new[] { "first", "second", "third" }, Shown(table));
            });

        // A column with no comparison is a label, not a button that does nothing. An inert tab stop
        // costs a keyboard user a press and tells them nothing.
        [Fact]
        public Task A_column_with_no_comparison_has_no_button_to_press() => Realised(
            make: Sortable,
            assert: table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");

                Assert.Single(header.Children.OfType<Button>());
                Assert.Single(header.Children.OfType<TextBlock>());
                Assert.Equal("name", header.Children.OfType<TextBlock>().Single().Text);
            });

        // WHAT A READER GETS, since the glyph is explicitly hidden from them. A sighted user sees
        // the triangle; the state has to reach everybody else through the name.
        [Fact]
        public Task A_sorted_heading_says_which_way_it_is_sorted() => Realised(
            make: Sortable,
            assert: table =>
            {
                Button pg = Heading(table, "pg");
                Assert.Equal("pg, not sorted", AutomationProperties.GetName(pg));

                Click(pg);
                Assert.Equal("pg, sorted ascending", AutomationProperties.GetName(pg));

                Click(pg);
                Assert.Equal("pg, sorted descending", AutomationProperties.GetName(pg));

                Click(pg);
                Assert.Equal("pg, not sorted", AutomationProperties.GetName(pg));
            });

        // The glyph is absent in the third state rather than neutral, so the cycle reads as two
        // sorted states and off rather than as three sorts.
        [Fact]
        public Task The_sort_glyph_is_absent_until_the_column_is_sorted() => Realised(
            make: Sortable,
            assert: table =>
            {
                Button pg = Heading(table, "pg");
                TextBlock Glyph() => pg.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("sort"));

                Assert.False(Glyph().IsVisible);

                Click(pg);
                Assert.True(Glyph().IsVisible);
                Assert.Equal("▲", Glyph().Text);

                Click(pg);
                Assert.Equal("▼", Glyph().Text);

                Click(pg);
                Assert.False(Glyph().IsVisible);
            });

        // A heading has to be reachable and operable without a mouse, or the sort is a feature
        // keyboard users do not have. §24 is the section about this class of miss.
        [Fact]
        public Task A_heading_can_be_sorted_from_the_keyboard_alone() => Realised(
            make: Sortable,
            assert: table =>
            {
                Button pg = Heading(table, "pg");

                Assert.True(pg.Focusable, "A sortable heading that is not focusable cannot be reached by Tab.");
                Assert.True(pg.Focus(), "The heading refused focus.");

                // Both halves of the press, because Button's default ClickMode is Release: Space on
                // key DOWN only sets IsPressed, and the click happens on key UP. Sending the down
                // alone passes for a control that never clicks at all.
                pg.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                pg.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Shown(table));
            });

        // THE CLAIM ShowSortState MAKES, WHICH WOULD OTHERWISE BE A COMMENT AND NOTHING ELSE.
        //
        // The headings are updated in place rather than rebuilt, and the reason given is keyboard
        // focus: a user who tabbed to a heading and pressed Space is focused on that button, and
        // replacing it drops them to the top of the window having just used the control correctly.
        // A rebuild would pass every other test in this file, because nothing else looks at focus.
        [Fact]
        public Task Sorting_from_the_keyboard_leaves_the_focus_on_the_heading() => Realised(
            make: Sortable,
            assert: table =>
            {
                Button pg = Heading(table, "pg");
                Assert.True(pg.Focus(), "The heading refused focus.");

                Click(pg);

                Assert.True(pg.IsFocused,
                    "Focus left the heading when it was sorted. The headings are being rebuilt rather "
                    + "than updated - see LunaTable.ShowSortState.");
                Assert.Same(pg, Heading(table, "pg"));
            });

        // Two tables on one page must not sort each other, the same way they already must not size
        // each other's columns.
        [Fact]
        public Task Two_tables_do_not_sort_each_other() => Session.Dispatch(() =>
        {
            LunaTable<Field> left = Sortable();
            LunaTable<Field> right = Sortable();

            var window = new ToolWindow
            {
                Width = 700,
                Height = 400,
                Content = new StackPanel { Children = { left, right } },
            };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Click(Heading(left, "pg"));

            Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, Shown(left));
            Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, Shown(right));

            window.Close();
        }, default);

        // THE TWO DECLARATION FORMS MUST BUILD THE SAME COLUMN. Keeping the terse overload is a
        // convenience (§27); keeping two ways to BUILD a column would be a defect waiting for the
        // day one of them stops being updated. The terse form delegates, and this is what says so.
        [Fact]
        public Task The_terse_column_form_and_the_descriptor_build_the_same_column() => Session.Dispatch(() =>
        {
            static LunaTable<Field> Terse()
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name, "2*").Column("pg", f => f.Page.ToString(), "60");
                table.Refresh(Unordered);
                return table;
            }

            static LunaTable<Field> Descriptor()
            {
                var table = new LunaTable<Field>();
                table.Column(new LunaColumn<Field>("name", f => f.Name) { Width = "2*" })
                     .Column(new LunaColumn<Field>("pg", f => f.Page.ToString()) { Width = "60" });
                table.Refresh(Unordered);
                return table;
            }

            static string Describe(LunaTable<Field> table)
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Grid row = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>().First()
                    .GetVisualDescendants().OfType<Grid>().First();

                return string.Join(" | ",
                    string.Join(",", header.ColumnDefinitions.Select(c => c.Width.ToString())),
                    string.Join(",", header.Children.OfType<TextBlock>().Select(t => t.Text)),
                    string.Join(",", row.Children.OfType<TextBlock>().Select(t => t.Text)),
                    AutomationProperties.GetName(row));
            }

            string terse = "", descriptor = "";

            foreach ((LunaTable<Field> table, bool isTerse) in new[] { (Terse(), true), (Descriptor(), false) })
            {
                var window = new ToolWindow { Width = 500, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                if (isTerse) terse = Describe(table); else descriptor = Describe(table);
                window.Close();
            }

            Assert.False(string.IsNullOrWhiteSpace(terse), "The description was empty, so the comparison asserts nothing.");
            Assert.Equal(terse, descriptor);
        }, default);

        // The wiring, kept as a smaller claim than it used to make. This one localizes a failure -
        // if the names have gone wrong, the positional test above cannot say why - but it is no
        // longer mistaken for evidence that the sharing works.
        //
        // ONLY THE AUTO COLUMN CARRIES A GROUP, and this asserts that rather than "every column has
        // one", which is what it used to say and what cost the table its width (§27.10). A star
        // column in a shared size group behaves as Auto, so a group name on one is a defect and not
        // a formality.
        [Fact]
        public Task Only_auto_columns_share_a_size_group_name_with_their_header() => Realised(
            make: () =>
            {
                var table = new LunaTable<Field>();
                table.Column("name", f => f.Name, "2*")
                     .Column("type", f => f.Type, "Auto")
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

                Assert.True(string.IsNullOrEmpty(header.ColumnDefinitions[0].SharedSizeGroup),
                    "The star column is in a shared size group, which makes it size as Auto.");
                Assert.False(string.IsNullOrEmpty(header.ColumnDefinitions[1].SharedSizeGroup),
                    "The Auto column is in no group, so it sizes independently in the header and the rows.");
                Assert.True(string.IsNullOrEmpty(header.ColumnDefinitions[2].SharedSizeGroup),
                    "The absolute column is in a group it does not need.");

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

        // A DATA GRID SINCE §68, AND THIS TEST USED TO ASSERT THE OPPOSITE.
        //
        // It read: "A Group, deliberately not DataGrid or Table: those UIA types promise
        // IGridProvider and ITableProvider, and this control implements neither. §27.3." The premise
        // is wrong about this framework - Avalonia 12.1.0 has no IGridProvider and no ITableProvider
        // at all, so no control here can promise that navigation with a control type - and
        // TreeDataGrid, which this is at parity with, returns DataGrid from its own peer while
        // implementing exactly the two providers below. §68.1 records the correction.
        //
        // Kept as a guard rather than deleted, with the assertions turned over: the type, and both
        // providers actually being reachable. A control type claimed with nothing behind it is the
        // failure the original test was written to prevent, and it still is.
        [Fact]
        public Task A_table_reports_itself_as_a_data_grid_with_the_patterns_to_match() => Realised(table =>
        {
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(table);

            Assert.True(peer.IsControlElement());
            Assert.Equal(AutomationControlType.DataGrid, peer.GetAutomationControlType());
            Assert.NotNull(peer.GetProvider<Avalonia.Automation.Provider.ISelectionProvider>());
            Assert.NotNull(peer.GetProvider<Avalonia.Automation.Provider.IScrollProvider>());
        });
    }
}
