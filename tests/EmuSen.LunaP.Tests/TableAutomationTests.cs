using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // WHAT A SCREEN READER GETS FROM A TABLE - see docs/LunaP.md §68.
    //
    // Pass 2 of finishing §54.3's parity: the automation row. Before it, a reader could walk into
    // the table, hear each row's sentence and read a text cell's value, and had no way to ask what
    // is selected or to move a table taller and wider than the window.
    //
    // The trap this file is mostly about: EVERY ASSERTION HERE MUST GO THROUGH A PEER. Reading the
    // attached property straight back off the control is the §50.5 shape - a test that passes while
    // the effect is absent, because the property being set was never the question. GetName() on the
    // peer is what a reader actually calls.
    public class TableAutomationTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableAutomationTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, bool armed)
            {
                Name = name;
                Armed = armed;
            }

            public string Name { get; set; }
            public bool Armed { get; set; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("alpha", false), new Row("bravo", true), new Row("charlie", false),
        };

        // All three cell kinds, because the whole argument of §68.2 and §68.3 is that the answer has
        // to be the same for a cell this control built and a cell a caller did.
        private static LunaTable<Row> Table(
            LunaSelectionUnit unit = LunaSelectionUnit.Cell,
            LunaSelectionMode mode = LunaSelectionMode.Multiple,
            string width = "120")
        {
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                SelectionUnit = unit,
                SelectionMode = mode,
            };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
                  {
                      Width = width,
                      Commit = (r, text) => r.Name = text,
                  })
                 .Column(new LunaColumn<Row>("armed", r => r.Armed, (r, on) => r.Armed = on)
                  {
                      Width = width,
                  })
                 .Column(new LunaColumn<Row>(
                     "kind",
                     r => new Ellipse
                     {
                         Width = 8,
                         Height = 8,
                         VerticalAlignment = VerticalAlignment.Center,

                         // Stated rather than relied on. §69.2 makes this the default, and the
                         // alignment guards for it live in TableCellKindTests - a header-alignment
                         // measurement that silently depended on that default would be measuring two
                         // things and reporting one. §68.5 is what it cost the first time.
                         HorizontalAlignment = HorizontalAlignment.Left,
                     },
                     // READS BOTH FIELDS, so this column goes stale when the checkbox beside it is
                     // toggled AND when the name column is committed. A projection of one field can
                     // only catch one of the two paths that refresh it (§68.4).
                     r => $"{r.Name} {(r.Armed ? "armed" : "safe")}")
                  {
                      Width = width,
                  });

            table.Refresh(Rows());
            return table;
        }

        private static Task Realised(Func<LunaTable<Row>> make, Action<LunaTable<Row>> assert, double width = 500) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = width, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static AutomationPeer Peer(Control control) =>
            ControlAutomationPeer.CreatePeerForElement(control);

        private static Control CellOf(LunaTable<Row> table, int row, int column)
        {
            Assert.True(table.TryGetCell(table.Models[row], column, out Control? cell));
            return cell!;
        }

        // ---- the name the caller set, and where it has to reach ----

        // §78.5. A caller names the control they declared; the template puts a ListBox under it, and
        // a reader walking the tree used to find that list anonymous inside a named table. Asserted
        // through the peer rather than off the attached property, per this file's own rule.
        [Fact]
        public Task The_tables_name_reaches_the_list_inside_it() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                AutomationProperties.SetName(table, "Cheats for this console");
                return table;
            },
            table =>
            {
                var list = table.GetVisualDescendants().OfType<ListBox>().First();
                Assert.Equal("Cheats for this console", Peer(list).GetName());
            });

        // A caller who named the inner list themselves keeps what they set.
        [Fact]
        public Task A_list_that_already_has_a_name_keeps_it() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                AutomationProperties.SetName(table, "outer");
                return table;
            },
            table =>
            {
                var list = table.GetVisualDescendants().OfType<ListBox>().First();
                AutomationProperties.SetName(list, "inner");
                Assert.Equal("inner", Peer(list).GetName());
            });

        // The normal case for a window built in a constructor: the name is assigned after the
        // template has already been applied, so a one-shot forward at attach time would miss it.
        [Fact]
        public Task A_name_set_after_the_template_still_reaches_the_list() => Realised(
            () => Table(),
            table =>
            {
                AutomationProperties.SetName(table, "named later");

                var list = table.GetVisualDescendants().OfType<ListBox>().First();
                Assert.Equal("named later", Peer(list).GetName());
            });

        // ---- what the table says it is ----

        [Fact]
        public Task The_selection_provider_says_whether_more_than_one_can_be_picked() =>
            Session.Dispatch(() =>
            {
                Assert.True(Multiple(LunaSelectionMode.Multiple));
                Assert.False(Multiple(LunaSelectionMode.Single));
            }, default);

        private static bool Multiple(LunaSelectionMode mode)
        {
            LunaTable<Row> table = Table(mode: mode);
            var window = new ToolWindow { Width = 500, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            bool answer = Peer(table).GetProvider<ISelectionProvider>()!.CanSelectMultiple;
            window.Close();
            return answer;
        }

        // Nothing selected is a real state - Refresh can drop the selected row, None refuses one
        // outright - so a reader must not be told a selection is required.
        [Fact]
        public Task A_selection_is_never_required() => Realised(() => Table(), table =>
            Assert.False(Peer(table).GetProvider<ISelectionProvider>()!.IsSelectionRequired));

        // ---- what is selected ----

        // THE POINT OF THE WHOLE PASS. §67.7 recorded this as a hazard: a reader met the row and had
        // nothing to say which cell inside it had the keyboard. This is the question having an answer.
        [Fact]
        public Task A_reader_can_ask_which_cell_is_selected() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 0);

            var selection = Peer(table).GetProvider<ISelectionProvider>()!.GetSelection();

            Assert.Single(selection);
            Assert.Equal("name", selection[0].GetName());
        });

        // ALL THREE KINDS, which is §68.2's argument stated as an assertion. The peers come back from
        // the cells themselves, so a check cell brings Avalonia's CheckBox peer and a template cell
        // brings whatever the caller's control provides - and both are named because the name is an
        // attached property rather than something a peer of ours had to supply.
        [Fact]
        public Task Every_kind_of_selected_cell_comes_back_named() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 0);
            Key(table, Avalonia.Input.Key.Right, Avalonia.Input.KeyModifiers.Shift);
            Key(table, Avalonia.Input.Key.Right, Avalonia.Input.KeyModifiers.Shift);

            var selection = Peer(table).GetProvider<ISelectionProvider>()!.GetSelection();

            Assert.Equal(
                new[] { "name", "armed", "kind" },
                selection.Select(p => p.GetName()).ToArray());
        });

        // And the providers survive the trip: a reader that found the check cell in the selection can
        // still toggle it, because what came back is the CheckBox's own peer and not a wrapper.
        [Fact]
        public Task A_selected_check_cell_still_carries_its_toggle() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 1);

            var selection = Peer(table).GetProvider<ISelectionProvider>()!.GetSelection();
            var toggle = selection.Single().GetProvider<IToggleProvider>();

            Assert.NotNull(toggle);
            Assert.Equal(ToggleState.Off, toggle!.ToggleState);

            toggle.Toggle();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.Models[0].Armed);
        });

        // In a row unit the same question answers with rows, because that is what is selected. A
        // third notion of "what is selected" reachable only through automation would be a third thing
        // to keep in step.
        [Fact]
        public Task In_a_row_unit_the_selection_is_rows() =>
            Realised(() => Table(unit: LunaSelectionUnit.Row), table =>
            {
                table.Select(table.Models[2]);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                var selection = Peer(table).GetProvider<ISelectionProvider>()!.GetSelection();

                Assert.Single(selection);
                Assert.Equal("name: charlie, armed: no, kind: charlie safe", selection[0].GetName());
            });

        [Fact]
        public Task Nothing_selected_is_an_empty_selection_and_not_a_failure() =>
            Realised(() => Table(), table =>
                Assert.Empty(Peer(table).GetProvider<ISelectionProvider>()!.GetSelection()));

        // ---- what one cell says ----

        // THE NAME IS THE HEADER AND THE VALUE COMES FROM THE PATTERN, which is §57's rule for a
        // check cell applied to all three. Folding the value into the name would say it twice, and
        // would mean the name of a thing changed every time the model did.
        [Fact]
        public Task A_text_cell_is_named_for_its_column_and_values_through_its_pattern() =>
            Realised(() => Table(), table =>
            {
                AutomationPeer peer = Peer(CellOf(table, 1, 0));

                Assert.Equal("name", peer.GetName());
                Assert.Equal("bravo", peer.GetProvider<IValueProvider>()!.Value);
            });

        [Fact]
        public Task A_check_cell_is_named_for_its_column_and_states_through_its_pattern() =>
            Realised(() => Table(), table =>
            {
                AutomationPeer peer = Peer(CellOf(table, 1, 1));

                Assert.Equal("armed", peer.GetName());
                Assert.Equal(ToggleState.On, peer.GetProvider<IToggleProvider>()!.ToggleState);
            });

        // THE KIND WITH NO PATTERN AT ALL, and the gap §57.2 only half closed: it required `spoken`
        // so the ROW's sentence could describe a coloured dot, and the cell itself stayed anonymous.
        // A reader landing on it heard the name of a shape class or nothing.
        [Fact]
        public Task A_template_cell_says_what_it_means_because_nothing_else_can() =>
            Realised(() => Table(), table =>
            {
                AutomationPeer peer = Peer(CellOf(table, 1, 2));

                Assert.Equal("kind", peer.GetName());
                Assert.Equal("bravo armed", peer.GetItemStatus());
            });

        // A committed edit changes a value that appears in two sentences. §50 fixed the row's; this
        // is the same defect one level down, and a reader would have heard the old value for as long
        // as the row stayed realised.
        [Fact]
        public Task A_committed_edit_updates_what_the_cell_says() => Realised(() => Table(), table =>
        {
            table.Edit(table.Models[1], 0);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            TextBox editor = table.GetVisualDescendants().OfType<TextBox>().First();
            editor.Text = "delta";
            editor.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Enter,
            });

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("delta", Peer(CellOf(table, 1, 0)).GetProvider<IValueProvider>()!.Value);

            // AND THE TEMPLATE CELL THAT READS THE SAME FIELD, which is the half a value assertion
            // cannot reach: IValueProvider.Value goes through the projection and is never stale, so
            // it is true whether or not anything was refreshed. The dot's status is only right if the
            // commit path re-read the whole row (§68.4).
            Assert.Equal("delta armed", Peer(CellOf(table, 1, 2)).GetItemStatus());
        });

        // A CHANGE IN ONE COLUMN GOES STALE IN ANOTHER, which is the whole of §68.4 and the reason
        // NameCells walks the row instead of taking the column that changed. The "kind" column here
        // projects the same field the "armed" checkbox writes, so ticking the box must change what
        // the dot beside it says - and nothing about the dot's own control changed.
        //
        // The first draft of this test asserted the CheckBox's own ToggleState after toggling it,
        // which is true whatever the rest of the row does. Sabotage said so by turning nothing red.
        [Fact]
        public Task A_toggle_updates_what_the_template_cell_beside_it_says() =>
            Realised(() => Table(), table =>
            {
                Control dot = CellOf(table, 0, 2);
                Assert.Equal("alpha safe", Peer(dot).GetItemStatus());

                Peer((CheckBox)CellOf(table, 0, 1)).GetProvider<IToggleProvider>()!.Toggle();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal("alpha armed", Peer(CellOf(table, 0, 2)).GetItemStatus());
            });

        // THE THIRD WRITE PATH, and the one §68.4 missed. A screen reader setting a value goes
        // through SetFromAutomation rather than through an editor, so it is a third place the row can
        // be left half re-read - and it was, in the pass whose whole subject was automation. The two
        // paths §68.4 fixed were the two its sabotages happened to cross. §69.1.
        [Fact]
        public Task A_value_set_by_a_reader_refreshes_the_rest_of_the_row() =>
            Realised(() => Table(), table =>
            {
                Peer(CellOf(table, 1, 0)).GetProvider<IValueProvider>()!.SetValue("delta");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal("delta armed", Peer(CellOf(table, 1, 2)).GetItemStatus());
            });

        // And a refused write must leave everything exactly as it was, or a reader would hear a value
        // the model does not hold.
        [Fact]
        public Task A_reader_write_that_validation_refuses_changes_nothing() =>
            Realised(() => Refusing(), table =>
            {
                Peer(CellOf(table, 1, 0)).GetProvider<IValueProvider>()!.SetValue("delta");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal("bravo armed", Peer(CellOf(table, 1, 2)).GetItemStatus());
                Assert.Equal("bravo", Peer(CellOf(table, 1, 0)).GetProvider<IValueProvider>()!.Value);
            });

        // A table whose name column refuses everything, so the write above is rejected at the same
        // gate a typist meets (§50.6) rather than by SetFromAutomation being wired differently.
        private static LunaTable<Row> Refusing()
        {
            var table = new LunaTable<Row> { Key = r => r.Name };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
                  {
                      Width = "120",
                      Commit = (r, text) => r.Name = text,
                      Validate = (_, _) => "no",
                  })
                 .Column(new LunaColumn<Row>("armed", r => r.Armed, (r, on) => r.Armed = on) { Width = "120" })
                 .Column(new LunaColumn<Row>(
                     "kind",
                     _ => new Ellipse { Width = 8, Height = 8 },
                     r => $"{r.Name} {(r.Armed ? "armed" : "safe")}")
                  {
                      Width = "120",
                  });

            table.Refresh(Rows());
            return table;
        }

        // ---- moving a table bigger than its window ----

        // NoScroll and not 0, because 0 means "at the start" and a reader would announce a table that
        // cannot scroll as one parked at the top left.
        [Fact]
        public Task A_table_that_fits_reports_that_it_does_not_scroll() =>
            Realised(() => Table(width: "60"), table =>
            {
                var scroll = Peer(table).GetProvider<IScrollProvider>()!;

                Assert.False(scroll.HorizontallyScrollable);
                Assert.Equal(-1, scroll.HorizontalScrollPercent);
            });

        [Fact]
        public Task A_table_wider_than_its_window_can_be_scrolled_by_a_reader() =>
            Realised(() => Table(width: "300"), table =>
            {
                var scroll = Peer(table).GetProvider<IScrollProvider>()!;

                Assert.True(scroll.HorizontallyScrollable);
                Assert.Equal(0, scroll.HorizontalScrollPercent);
                Assert.True(scroll.HorizontalViewSize < 100,
                    $"a table showing part of itself reported a view size of {scroll.HorizontalViewSize}.");

                scroll.SetScrollPercent(100, -1);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();

                Assert.Equal(100, scroll.HorizontalScrollPercent, 0);
            }, width: 400);

        // The header has to follow a scroll a READER caused, exactly as it follows one a pointer
        // caused - §64.2 is the same defect arriving through a different door.
        [Fact]
        public Task The_header_follows_a_scroll_a_reader_asked_for() =>
            Realised(() => Table(width: "300"), table =>
            {
                Peer(table).GetProvider<IScrollProvider>()!.SetScrollPercent(100, -1);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Grid header = table.FindNamed<Grid>("PART_Header");
                Control heading = header.Children.OfType<Control>()
                    .First(c => c is TextBlock && Grid.GetColumn(c) == 2);

                double dx = Math.Abs(
                    (heading.TranslatePoint(new Point(0, 0), table)?.X ?? double.NaN)
                    - (CellOf(table, 0, 2).TranslatePoint(new Point(0, 0), table)?.X ?? double.NaN));

                Assert.True(dx < 1.0, $"after a reader scrolled, heading 2 is {dx:F1}px from its own cells.");
            }, width: 400);

        [Fact]
        public Task Scroll_moves_by_a_page_for_the_large_amount() =>
            Realised(() => Table(width: "300"), table =>
            {
                var scroll = Peer(table).GetProvider<IScrollProvider>()!;

                scroll.Scroll(ScrollAmount.LargeIncrement, ScrollAmount.NoAmount);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();

                Assert.True(scroll.HorizontalScrollPercent > 0,
                    "a page-right left the table where it was.");
            }, width: 400);

        private static void Key(LunaTable<Row> table, Avalonia.Input.Key key, Avalonia.Input.KeyModifiers modifiers)
        {
            table.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = key,
                KeyModifiers = modifiers,
            });

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }
}
