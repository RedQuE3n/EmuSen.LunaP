using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // CELL KINDS - see docs/LunaP.md §57.
    //
    // Pass 3 of §54's parity arc: a cell stops always being a TextBlock. The two things worth
    // knowing before reading the rest, because both are ways this can look finished and be wrong:
    //
    //   - A READ-ONLY CHECKBOX A SCREEN READER CAN STILL TICK. Refusing the pointer is not refusing
    //     the write. Avalonia 12.1.0's IToggleProvider.Toggle() honours IsEnabled and ignores
    //     IsHitTestVisible, which is measured here rather than assumed - and the measurement is what
    //     chose the mechanism (§57.3).
    //   - A CELL THE LOOKUP CANNOT FIND. Edit, TryGetCell and the automation write all locate a cell
    //     by walking a row, and that walk used to filter by type. A CheckBox and a caller's own
    //     control are not that type, so the marker moved to an attached property and these assert
    //     that all three kinds answer to it.
    public class TableCellKindTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableCellKindTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, bool armed)
            {
                Name = name;
                Armed = armed;
            }

            public string Name { get; set; }
            public bool Armed { get; set; }

            // Counts the writes, so a test can tell "the model ended up false" from "the delegate
            // was never called" - two different bugs with the same visible result.
            public int Writes { get; set; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("alpha", false), new Row("bravo", true), new Row("charlie", false),
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

        // name (text, editable), armed (check, writable), kind (template).
        private static LunaTable<Row> Build(Row[] rows, bool writable = true)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
                 {
                     Commit = (r, text) => r.Name = text,
                 })
                 .Column(new LunaColumn<Row>(
                     "armed",
                     r => r.Armed,
                     writable
                         ? (r, on) =>
                         {
                             r.Armed = on;
                             r.Writes++;
                         }
                         : null)
                 {
                     Width = "60",
                 })
                 .Column(new LunaColumn<Row>(
                     "kind",
                     r => new Ellipse { Width = 8, Height = 8, Fill = Brushes.Red },
                     r => r.Armed ? "armed" : "safe")
                 {
                     Width = "30",
                 });

            table.Refresh(rows);
            return table;
        }

        private static CheckBox Box(LunaTable<Row> table, string name) =>
            table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == name)
                .GetVisualDescendants().OfType<CheckBox>().First();

        // ---- the kinds are what they say ----

        [Fact]
        public void A_column_declares_its_kind_from_the_constructor_it_was_given()
        {
            Assert.Equal(LunaCellKind.Text, new LunaColumn<Row>("a", r => r.Name).Kind);
            Assert.Equal(LunaCellKind.Check, new LunaColumn<Row>("b", r => r.Armed).Kind);
            Assert.Equal(
                LunaCellKind.Template,
                new LunaColumn<Row>("c", _ => new Border(), _ => "a border").Kind);
        }

        // THE INIT PROPERTIES STILL APPLY, which is the whole reason these are constructors rather
        // than the static factories the first draft had. A factory hands back a finished object and
        // Width can never be set on it. §57.1.
        [Fact]
        public void A_check_column_still_takes_every_property_a_text_column_does()
        {
            var column = new LunaColumn<Row>("armed", r => r.Armed)
            {
                Width = "60",
                MinWidth = 40,
                IsVisible = false,
                Sort = (a, b) => a.Armed.CompareTo(b.Armed),
            };

            Assert.Equal("60", column.Width);
            Assert.Equal(40, column.MinWidth);
            Assert.False(column.IsVisible);
            Assert.NotNull(column.Sort);
        }

        // A CHECK COLUMN IS NOT EDITABLE, EVEN CARRYING A Commit, and the second half is the whole
        // test. The first version of this asserted against a check column with no Commit, which made
        // it green whether or not IsEditable named the kind - Commit was null either way, so the
        // clause under test contributed nothing and removing it turned nothing red. The §50.4 shape
        // exactly, caught by sabotage rather than by reading.
        //
        // Commit is init-only like every other property, so this declaration compiles and somebody
        // will write it. It is ignored rather than rejected; what must not happen is the column
        // claiming a text editor can be opened on it.
        [Fact]
        public void A_check_column_is_not_editable_even_when_it_carries_a_commit()
        {
            var writable = new LunaColumn<Row>("armed", r => r.Armed, (r, on) => r.Armed = on)
            {
                Commit = (r, text) => r.Name = text,
            };

            Assert.NotNull(writable.Toggle);
            Assert.NotNull(writable.Commit);
            Assert.False(writable.IsEditable);
        }

        [Fact]
        public void A_template_column_refuses_to_be_declared_without_a_sentence()
        {
            // Not "throws for null build" as an afterthought - both arguments are required, and the
            // sentence is required for the reason §57.2 gives.
            Assert.Throws<ArgumentNullException>(() =>
                new LunaColumn<Row>("c", (Func<Row, Control>)null!, _ => "x"));

            Assert.Throws<ArgumentNullException>(() =>
                new LunaColumn<Row>("c", _ => new Border(), (Func<Row, string>)null!));
        }

        [Fact]
        public void A_check_column_refuses_to_be_declared_without_a_projection() =>
            Assert.Throws<ArgumentNullException>(() =>
                new LunaColumn<Row>("b", (Func<Row, bool>)null!));

        // ---- what gets drawn ----

        [Fact]
        public Task Each_kind_draws_the_control_it_promised() => Realised(() => Build(Rows()), table =>
        {
            ListBoxItem row = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == "bravo");

            Assert.Equal("bravo", row.GetVisualDescendants().OfType<TextBlock>().First().Text);
            Assert.True(row.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked);
            Assert.Single(row.GetVisualDescendants().OfType<Ellipse>());
        });

        [Fact]
        public Task A_check_cell_shows_the_models_value() => Realised(() => Build(Rows()), table =>
        {
            Assert.False(Box(table, "alpha").IsChecked);
            Assert.True(Box(table, "bravo").IsChecked);
            Assert.False(Box(table, "charlie").IsChecked);
        });

        // ---- the lookup ----

        [Fact]
        public Task Every_kind_of_cell_can_be_found_by_its_column() => Realised(() => Build(Rows()), table =>
        {
            Assert.True(table.TryGetCell(table.Models[0], 0, out Control? text));
            Assert.True(table.TryGetCell(table.Models[0], 1, out Control? check));
            Assert.True(table.TryGetCell(table.Models[0], 2, out Control? template));

            Assert.IsAssignableFrom<TextBlock>(text!);
            Assert.IsType<CheckBox>(check!);
            Assert.IsType<Ellipse>(template!);
        });

        // A TEMPLATE'S OWN CHILDREN ARE NOT CELLS. The marker is only set on the cell itself, so a
        // caller who returns a panel of three controls does not get one of them answering to
        // column 2. Without the -1 default this would find whichever descendant came first.
        [Fact]
        public Task A_controls_own_children_do_not_answer_as_cells() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name)
                 .Column(new LunaColumn<Row>(
                     "buttons",
                     _ => new StackPanel { Children = { new Button { Content = "a" }, new Button { Content = "b" } } },
                     _ => "two buttons"));
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.TryGetCell(table.Models[0], 1, out Control? cell));
            Assert.IsType<StackPanel>(cell!);

            window.Close();
        }, default);

        // ---- toggling ----

        [Fact]
        public Task Ticking_a_box_writes_to_the_model() => Realised(() => Build(Rows()), table =>
        {
            var alpha = (Row)table.Models[0];
            Assert.False(alpha.Armed);

            Box(table, "alpha").IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(alpha.Armed);
            Assert.Equal(1, alpha.Writes);
        });

        [Fact]
        public Task A_toggle_raises_CellValueChanged_with_its_column() => Realised(() => Build(Rows()), table =>
        {
            Row? changed = null;
            int column = -1;
            table.CellValueChanged += (row, c) =>
            {
                changed = row;
                column = c;
            };

            Box(table, "alpha").IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Same(table.Models[0], changed);
            Assert.Equal(1, column);
        });

        // THE MODEL IS THE TRUTH. A Toggle that declines to write leaves the tick where it was, and
        // that falls out of the table re-reading rather than from a veto mechanism of its own.
        [Fact]
        public Task A_toggle_the_model_refuses_puts_the_tick_back() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name)
                 .Column(new LunaColumn<Row>("armed", r => r.Armed, (r, _) => r.Writes++));
            table.Refresh(rows);

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            bool raised = false;
            table.CellValueChanged += (_, _) => raised = true;

            CheckBox box = Box(table, "bravo");
            Assert.True(box.IsChecked);

            box.IsChecked = false;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // The delegate ran exactly once - the put-back must not call it again - the model never
            // moved, the box went back, and nothing claimed a value had changed.
            Assert.Equal(1, rows[1].Writes);
            Assert.True(rows[1].Armed);
            Assert.True(box.IsChecked);
            Assert.False(raised);

            window.Close();
        }, default);

        // A NORMALISING TOGGLE IS THE SAME MECHANISM SEEN FROM THE OTHER SIDE: the box shows what the
        // model did, not what was clicked. Here unticking is refused and ticking is amplified.
        [Fact]
        public Task A_box_shows_what_the_model_did_and_not_what_was_clicked() => Session.Dispatch(() =>
        {
            Row[] rows = Rows();
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name)
                 .Column(new LunaColumn<Row>("armed", r => r.Armed, (r, _) => r.Armed = true));
            table.Refresh(rows);

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            CheckBox box = Box(table, "alpha");
            box.IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(rows[0].Armed);

            box.IsChecked = false;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(rows[0].Armed);
            Assert.True(box.IsChecked);

            window.Close();
        }, default);

        // ---- read-only ----

        [Fact]
        public Task A_read_only_check_column_is_disabled() =>
            Realised(() => Build(Rows(), writable: false), table =>
            {
                Assert.False(Box(table, "alpha").IsEnabled);
                Assert.True(Box(table, "alpha").IsChecked == false);
            });

        // THE ONE THAT CHOSE THE MECHANISM. IsHitTestVisible=false keeps a checkbox at full contrast
        // and stops a pointer, and a screen reader ticks it anyway - measured on Avalonia 12.1.0,
        // where IToggleProvider.Toggle() throws ElementNotEnabledException for a disabled control and
        // does nothing of the sort for an untouchable one. §57.3.
        //
        // Sabotaged by swapping IsEnabled for IsHitTestVisible in CheckCell: the model changes and
        // this turns red, which is the read-only column being written by an assistive technology.
        [Fact]
        public Task A_read_only_check_column_refuses_a_screen_reader_too() =>
            Realised(() => Build(Rows(), writable: false), table =>
            {
                var rows = table.Models.Cast<Row>().ToArray();
                CheckBox box = Box(table, "bravo");
                Assert.True(box.IsChecked);

                var provider = ControlAutomationPeer.CreatePeerForElement(box).GetProvider<IToggleProvider>();
                Assert.NotNull(provider);

                Assert.Throws<ElementNotEnabledException>(() => provider!.Toggle());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.True(box.IsChecked);
                Assert.True(rows.Single(r => r.Name == "bravo").Armed);
                Assert.Equal(0, rows.Single(r => r.Name == "bravo").Writes);
            });

        // And the writable one does answer, because a reader has to be able to tick a box a person
        // can tick - the same argument that gave an editable text cell an IValueProvider (§50.6).
        [Fact]
        public Task A_writable_check_column_can_be_ticked_by_a_screen_reader() =>
            Realised(() => Build(Rows()), table =>
            {
                var rows = table.Models.Cast<Row>().ToArray();
                CheckBox box = Box(table, "alpha");

                var provider = ControlAutomationPeer.CreatePeerForElement(box).GetProvider<IToggleProvider>();
                provider!.Toggle();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.True(rows.Single(r => r.Name == "alpha").Armed);
                Assert.Equal(ToggleState.On, provider.ToggleState);
            });

        // WHERE A TEMPLATE CELL STARTS - see docs/LunaP.md §69.2.
        //
        // Every other kind of cell begins at the column's left edge. A template cell did not:
        // Avalonia centres an element that has an explicit size and the default Stretch alignment,
        // so a caller's 8px dot in a 120px column sat 56 pixels in, beside a text cell and a
        // checkbox that both started at zero. It cost an hour of §68.5 looking like a scroll defect.
        //
        // Measured as a POSITION rather than as the property, because the property is what was set
        // and the position is what a person sees - and because a future default applied some other
        // way would still have to satisfy this.
        [Fact]
        public Task A_template_cell_starts_where_every_other_cell_starts() =>
            Realised(() => Build(Rows()), table =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 1, out Control? box));
                Assert.True(table.TryGetCell(table.Models[0], 2, out Control? dot));

                double boxOffset = box!.Bounds.X - ColumnLeft(table, box!);
                double dotOffset = dot!.Bounds.X - ColumnLeft(table, dot!);

                Assert.True(Math.Abs(dotOffset - boxOffset) < 1.0,
                    $"the checkbox starts {boxOffset:F1}px into its column and the template cell "
                    + $"{dotOffset:F1}px into its own - a caller's control is being centred.");
            });

        // AND THE CALLER STILL WINS, which is the half that stops this being a toolkit overruling
        // somebody's layout. A control that names its own alignment keeps it.
        [Fact]
        public Task A_template_cell_that_names_its_own_alignment_keeps_it() => Realised(
            () =>
            {
                var table = new LunaTable<Row> { Key = r => r.Name };
                table.Column("name", r => r.Name)
                     .Column(new LunaColumn<Row>(
                         "kind",
                         _ => new Ellipse
                         {
                             Width = 8,
                             Height = 8,
                             HorizontalAlignment = HorizontalAlignment.Right,
                         },
                         _ => "dot")
                     {
                         Width = "120",
                     });
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 1, out Control? dot));

                Assert.Equal(HorizontalAlignment.Right, ((Control)dot!).HorizontalAlignment);
                Assert.True(dot!.Bounds.X - ColumnLeft(table, dot) > 50,
                    "a template cell that asked to be right-aligned was pulled back to the left.");
            });

        // A TEMPLATE CELL WITH NO WIDTH STILL FILLS ITS COLUMN, which is the half the first version of
        // §69.2 broke. Avalonia's Stretch fills when there is no explicit width and centres when
        // there is; defaulting every template cell to Left collapsed the filling ones to nothing, and
        // eight frozen-band tests went red at once because their cells are exactly this shape.
        //
        // That is a consumer's progress-bar or highlight cell disappearing, so it gets a guard of its
        // own rather than being left to be noticed by tests that are about something else.
        [Fact]
        public Task A_template_cell_with_no_width_still_fills_its_column() => Realised(
            () =>
            {
                var table = new LunaTable<Row> { Key = r => r.Name };
                table.Column("name", r => r.Name)
                     .Column(new LunaColumn<Row>(
                         "bar",
                         _ => new Border { Height = 6, Background = Brushes.Red },
                         _ => "a bar")
                     {
                         Width = "120",
                     });
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 1, out Control? bar));

                Assert.True(bar!.Bounds.Width > 100,
                    $"a template cell that named no width came out {bar.Bounds.Width:F1}px wide in a "
                    + "120px column - a cell that filled its column now collapses to its content.");
            });

        // The left edge of the grid column a cell sits in, in the same coordinates as the cell's own
        // bounds - which is what "starts where the column starts" has to be measured against, since
        // the columns are different widths.
        private static double ColumnLeft(LunaTable<Row> table, Control cell)
        {
            Grid grid = cell.GetVisualAncestors().OfType<Grid>().First();
            int column = Grid.GetColumn(
                grid.Children.OfType<Control>().First(c => c == cell || c.GetVisualDescendants().Contains(cell)));

            double left = 0;
            for (int i = 0; i < column; i++) left += grid.ColumnDefinitions[i].ActualWidth;
            return left;
        }

        // ---- what a reader hears ----

        [Fact]
        public Task A_check_cell_carries_its_column_name() => Realised(() => Build(Rows()), table =>
            Assert.Equal("armed", ControlAutomationPeer.CreatePeerForElement(Box(table, "alpha")).GetName()));

        // THE POINT OF `spoken` BEING REQUIRED. The row sentence has to say something for a column
        // whose cell is a red circle, and only the caller can say what.
        [Fact]
        public Task A_rows_sentence_covers_every_kind_including_the_one_it_cannot_see() =>
            Realised(() => Build(Rows()), table =>
            {
                ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>()
                    .First(c => (c.DataContext as Row)?.Name == "bravo");

                Assert.Equal(
                    "name: bravo, armed: yes, kind: armed",
                    ControlAutomationPeer.CreatePeerForElement(container).GetName());
            });

        [Fact]
        public Task A_toggle_renames_the_row_it_changed() => Realised(() => Build(Rows()), table =>
        {
            ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == "alpha");

            Assert.Contains("armed: no", ControlAutomationPeer.CreatePeerForElement(container).GetName());

            Box(table, "alpha").IsChecked = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Contains("armed: yes", ControlAutomationPeer.CreatePeerForElement(container).GetName());
        });

        [Fact]
        public Task A_caller_can_choose_the_words_a_boolean_is_read_as() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column(new LunaColumn<Row>("armed", r => r.Armed, spoken: r => r.Armed ? "live" : "safe"));
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == "bravo");

            Assert.Equal("armed: live", ControlAutomationPeer.CreatePeerForElement(container).GetName());

            window.Close();
        }, default);

        // ---- the rest of the table still works around them ----

        // F2 HAS TO WALK PAST THE CHECK COLUMN. With "armed" declared first and editable, F2 would
        // find it, refuse to open an editor, and the text column behind it would be unreachable from
        // the keyboard. Sabotaged by dropping the Kind test from ColumnSpec.IsEditable.
        [Fact]
        public Task F2_finds_the_first_TEXT_column_and_not_the_first_writable_one() => Session.Dispatch(() =>
        {
            // The check column carries a Commit it can never use, which is what makes this able to
            // fail: without the kind test in IsEditable it is the first "editable" column, F2 stops
            // at it, and the name column behind it is unreachable from the keyboard.
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column(new LunaColumn<Row>("armed", r => r.Armed, (r, on) => r.Armed = on)
                 {
                     Commit = (r, text) => r.Name = text,
                 })
                 .Column(new LunaColumn<Row>("name", r => r.Name) { Commit = (r, t) => r.Name = t });
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Select(table.Models[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.F2,
            });
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.IsEditing);
            Assert.Equal("alpha", table.GetVisualDescendants().OfType<TextBox>().Single().Text);

            window.Close();
        }, default);

        [Fact]
        public Task Edit_refuses_a_check_column_and_a_template_one() =>
            Realised(() => Build(Rows()), table =>
            {
                table.Edit(table.Models[0], 1);
                Assert.False(table.IsEditing);

                table.Edit(table.Models[0], 2);
                Assert.False(table.IsEditing);

                table.Edit(table.Models[0], 0);
                Assert.True(table.IsEditing);
            });

        // A hidden check column builds no checkbox at all, the same as a hidden text one - a cell
        // that exists and cannot be seen still costs a measure pass per row per frame.
        [Fact]
        public Task A_hidden_check_column_draws_nothing() => Session.Dispatch(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("name", r => r.Name)
                 .Column(new LunaColumn<Row>("armed", r => r.Armed) { IsVisible = false });
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Empty(table.GetVisualDescendants().OfType<CheckBox>());

            // And the definition is still there, so column 1 still means "armed".
            Assert.Equal(2, table.FindNamed<Grid>("PART_Header").ColumnDefinitions.Count);

            window.Close();
        }, default);

        // A CHECK COLUMN CAN CARRY THE EXPANDER, which is exactly why ExpanderColumn is a choice
        // rather than always column 0 - and why the toggle's spoken name comes from the projection
        // now rather than from the cell's text, which a checkbox has none of.
        [Fact]
        public Task A_tree_can_indent_a_column_that_is_not_text() => Session.Dispatch(() =>
        {
            var kids = new[] { new Row("kid", true) };
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                Children = r => r.Name == "alpha" ? kids : Array.Empty<Row>(),
                ExpanderColumn = 0,
            };

            table.Column(new LunaColumn<Row>("armed", r => r.Armed, (r, on) => r.Armed = on))
                 .Column("name", r => r.Name);
            table.Refresh(Rows());

            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Button toggle = table.GetVisualDescendants().OfType<Button>()
                .First(b => b.Classes.Contains("expander") && b.IsVisible);

            Assert.Equal("Expand no", Avalonia.Automation.AutomationProperties.GetName(toggle));

            window.Close();
        }, default);

        // ---- the colours a read-only box is legible in ----

        // WCAG 1.4.11 EXEMPTS A DISABLED CONTROL, AND A READ-ONLY CELL IS WHY THAT EXEMPTION DOES NOT
        // APPLY HERE: it assumes you never need to read what you cannot use, and this column exists
        // to be read. FluentBridge overrides three keys for it; this asserts the outcome.
        //
        // COMPOSITED, WHICH IS NOT BOOKKEEPING. Fluent's own disabled colours are translucent white,
        // and comparing two raw ARGB values ignores the alpha entirely - the first version of this
        // did, and under sabotage it reported the dark variant at 1.00:1 because White over White is
        // what an uncomposited #66ffffff looks like. The figure a user meets is the composite, and
        // that is the figure this measures whether or not the value happens to be opaque.
        //
        // Sabotaged by removing the three overrides. LIGHT turns red at 1.78:1 for the tick and
        // 2.80:1 for the empty box; DARK stays green at 4.42:1 and 3.78:1, which is correct and is
        // the point - Fluent's disabled colours were only illegible on the light surface, and a
        // guard that failed both variants would be measuring something other than the defect.
        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public Task A_disabled_checkbox_is_still_legible(string variantName) => Session.Dispatch(() =>
        {
            ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            Color surface = Resolve("LunaSurfaceColor", variant);
            Color fill = Over(Resolve("CheckBoxCheckBackgroundFillCheckedDisabled", variant), surface);
            Color glyph = Over(Resolve("CheckBoxCheckGlyphForegroundCheckedDisabled", variant), fill);
            Color stroke = Over(Resolve("CheckBoxCheckBackgroundStrokeUncheckedDisabled", variant), surface);

            double ticked = Contrast(glyph, fill);
            double box = Contrast(fill, surface);
            double empty = Contrast(stroke, surface);

            Assert.True(ticked >= 3.0, $"{variantName}: a disabled tick sits at {ticked:F2}:1 on its own fill.");
            Assert.True(box >= 3.0, $"{variantName}: a disabled ticked box sits at {box:F2}:1 against the surface.");
            Assert.True(empty >= 3.0, $"{variantName}: a disabled empty box sits at {empty:F2}:1 against the surface.");
        }, default);

        // Source-over, which is what a translucent brush painted on a surface actually does. An
        // opaque colour comes back unchanged, so every case can go through it.
        private static Color Over(Color fg, Color bg)
        {
            double a = fg.A / 255.0;
            return Color.FromRgb(
                (byte)Math.Round((fg.R * a) + (bg.R * (1 - a))),
                (byte)Math.Round((fg.G * a) + (bg.G * (1 - a))),
                (byte)Math.Round((fg.B * a) + (bg.B * (1 - a))));
        }

        private static Color Resolve(string key, ThemeVariant variant)
        {
            Assert.True(Application.Current!.TryGetResource(key, variant, out object? found), key);

            return found switch
            {
                Color colour => colour,
                ISolidColorBrush brush => brush.Color,
                _ => throw new InvalidOperationException($"{key} is not a colour."),
            };
        }

        // WCAG 2.x relative luminance, spelled out for the same reason PaletteVariantTests does: it
        // is nine lines, and a dependency for nine lines is a decision this project would have to
        // justify. These overrides are opaque, so there is no compositing step to do first.
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static double Luminance(Color c) =>
            (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));

        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
