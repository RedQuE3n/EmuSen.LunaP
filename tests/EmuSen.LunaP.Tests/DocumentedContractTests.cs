using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Threading;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // EVERY PUBLISHED SUMMARY THAT PROMISES SOMETHING WILL NOT HAPPEN - see docs/LunaP.md §80.1.
    //
    // "Does not raise", "does nothing", "cannot", "without running any handler". A promise of
    // ABSENCE is the one kind a normal test never notices going wrong, because the assertion that
    // would catch it is one nobody writes: the feature still works, and the extra call is invisible
    // until it costs something. §80.1 was a re-query of a ROM library and of an on-disk cheat
    // database, fired by an application restoring a saved filter.
    //
    // Twenty-five claims, and one of them was false. The other twenty-four are pinned here so the
    // ratio stays that way.
    public class DocumentedContractTests
    {
        private sealed record Row(int N);

        [Fact]
        public void A_disabled_or_separator_action_does_nothing_at_all()
        {
            int ran = 0;
            var disabled = new LunaAction("x", _ => ran++) { IsEnabled = false, IsCheckable = true };
            disabled.Invoke();

            // "not the handler, not the state flip" - the second half is the one worth pinning.
            Assert.Equal(0, ran);
            Assert.False(disabled.IsChecked);

            LunaAction.Separator().Invoke();
        }

        [Fact]
        public void Setting_a_groups_checked_member_runs_no_handler()
        {
            int ran = 0;
            var group = new ActionGroup();
            LunaAction a = group.Add("a", _ => ran++);
            LunaAction b = group.Add("b", _ => ran++);
            a.Invoked += _ => ran++;
            b.Invoked += _ => ran++;

            group.Checked = b;
            Assert.Equal(0, ran);
            Assert.True(b.IsChecked);
            Assert.False(a.IsChecked);

            // Null unchecks everything, and is still not an invocation.
            group.Checked = null;
            Assert.Equal(0, ran);
            Assert.Null(group.Checked);
        }

        [Fact]
        public void Reporting_with_no_diagnostics_hook_does_nothing()
        {
            Action<string>? previous = LunaSettings.Diagnostics;
            try
            {
                LunaSettings.Diagnostics = null;
                LunaSettings.Report("into the void");
            }
            finally
            {
                LunaSettings.Diagnostics = previous;
            }
        }

        [Fact]
        public void A_theme_that_cannot_be_read_leaves_the_current_one_alone()
        {
            string? before = LunaTheme.Current;
            Assert.False(LunaTheme.Apply("no-such-theme-" + Guid.NewGuid().ToString("N")));
            Assert.Equal(before, LunaTheme.Current);
        }

        [Fact]
        public Task Selection_events_are_not_raised_by_programmatic_writes() => UiTest.Run(() =>
        {
            int chose = 0;
            var drop = new Dropdown();
            drop.Chose += _ => chose++;
            drop.Fill(new[] { "one", "two" }, "two");
            Assert.Equal(0, chose);

            int listChose = 0;
            var list = new LunaList<Row>();
            list.Chose += _ => listChose++;
            list.Refresh(new[] { new Row(1), new Row(2) });
            list.Select(new Row(2));
            Assert.Equal(0, listChose);

            // The half of LunaList.Chose's summary that promises the OPPOSITE, and is equally load
            // bearing: a direct write to SelectedIndex is not a restore, so it does raise.
            list.SelectedIndex = 0;
            Assert.Equal(1, listChose);

            // SHOWN, and not merely constructed. An untemplated LunaTable cannot select at all, so
            // asserting that Select raised nothing would pass without exercising the suppression -
            // the §5.5 shape, arriving in a test written to check something else (§81.2).
            int tableChose = 0;
            var table = new LunaTable<Row>();
            table.Column("n", r => r.N.ToString());
            var host = new Window { Content = table, Width = 300, Height = 200 };
            host.Show();
            UiTest.Settle(table);

            table.Chose += _ => tableChose++;
            var second = new Row(2);
            table.Refresh(new[] { new Row(1), second });
            UiTest.Settle(table);
            table.Select(second);

            Assert.Equal(0, tableChose);
            Assert.Equal(second, table.Selected);   // it really did select
            host.Close();
        });

        [Fact]
        public Task Setting_the_search_text_does_not_raise_changed() => UiTest.Run(() =>
        {
            // §80.1. This raised once against 0.8.0, synchronously, because SearchDelay defaults to
            // zero and the template binding pushed the value into the box, whose PropertyChanged
            // could not tell an echo from a keystroke.
            var filter = new FilterBar();
            var host = new Window { Content = filter };
            host.Show();
            host.UpdateLayout();

            int changed = 0;
            filter.Changed += () => changed++;

            filter.SearchText = "restored";
            Assert.Equal(0, changed);
            Assert.Equal("restored", filter.SearchText);

            // Setting it back, and to the empty string, are the two shapes a caller clearing a saved
            // filter actually uses.
            filter.SearchText = "";
            Assert.Equal(0, changed);

            host.Close();
        });

        [Fact]
        public Task Controls_that_do_nothing_without_a_key_or_a_row() => UiTest.Run(() =>
        {
            new LunaTable<Row>().SaveNow();
            new SplitPane().SaveNow();

            var table = new LunaTable<Row>();
            table.Column("n", r => r.N.ToString());
            table.Refresh(new[] { new Row(1) });
            table.Expand(new Row(1));

            // §81.1, and shown for the reason above: this passed against an untemplated table that
            // could not have selected anything whatever its mode.
            var none = new LunaTable<Row> { SelectionMode = LunaSelectionMode.None };
            none.Column("n", r => r.N.ToString());
            var host = new Window { Content = none, Width = 300, Height = 200 };
            host.Show();
            UiTest.Settle(none);

            var row = new Row(1);
            none.Refresh(new[] { row });
            UiTest.Settle(none);
            none.Select(row);
            Assert.Null(none.Selected);

            // The same table in Single selects it, which is what makes the line above mean anything.
            none.SelectionMode = LunaSelectionMode.Single;
            none.Select(row);
            Assert.Equal(row, none.Selected);

            // And switching back to None clears it, as ApplySelectionMode has always done.
            none.SelectionMode = LunaSelectionMode.None;
            Assert.Null(none.Selected);
            host.Close();
        });

        [Fact]
        public Task An_idle_cursor_hides_and_disposes_idempotently() => UiTest.Run(() =>
        {
            var cursor = new IdleCursor(new Button());

            cursor.Hide();
            Assert.True(cursor.IsHidden);
            cursor.Hide();
            Assert.True(cursor.IsHidden);

            cursor.Dispose();
            cursor.Dispose();
        });

        [Fact]
        public Task A_closed_slot_and_a_cancelled_debounce_do_nothing() => UiTest.Run(() =>
        {
            var slot = new WindowSlot<Window>();
            Assert.False(slot.IsOpen);

            // Throwing rather than counting: a callback that never runs cannot pass by accident.
            slot.RefreshIfOpen(_ => throw new InvalidOperationException("should not run"));
            slot.Close();

            int ran = 0;
            var debounce = new Debounce(TimeSpan.FromMilliseconds(50), () => ran++);

            debounce.Poke();
            debounce.Cancel();
            debounce.Flush();
            Assert.Equal(0, ran);

            debounce.Poke();
            debounce.Flush();
            Assert.Equal(1, ran);

            debounce.Flush();
            Assert.Equal(1, ran);
        });
    }
}
