using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Threading;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE REST OF THE CHECKABLE SUMMARY CLAIMS - defaults, "or null when", identity and counts.
    // See docs/LunaP.md §81.
    //
    // Ninety-two of the 542 published summaries make a claim a test can settle that the earlier
    // passes did not cover. These are the ones whose failure would be silent: a default that drifts,
    // a property that stops being null for a kind it was never meant to serve, a ToggleAction that
    // starts handing back a fresh object. None of them break a build, and none of them show up in a
    // render.
    //
    // TABLES ARE SHOWN IN A WINDOW HERE, NOT MERELY CONSTRUCTED. An untemplated LunaTable cannot
    // select anything at all, so a selection assertion made against one passes without touching the
    // behaviour it names - which is how §81.1 hid, and how this file's first draft nearly recorded
    // it as verified. §5.5 is the same shape in the theme; UiTest.Settle exists because the fill
    // happens during layout (§79.6).
    public class SummaryClaimTests
    {
        private sealed record Row(int N, List<Row>? Kids = null);

        [Fact]
        public void Column_kinds_are_null_for_every_other_kind()
        {
            var text = new LunaColumn<Row>("h", r => "");
            Assert.Equal(LunaCellKind.Text, text.Kind);
            Assert.Null(text.Checked);
            Assert.Null(text.Toggle);
            Assert.Null(text.Build);
            Assert.Null(text.Sort);
            Assert.Null(text.Commit);
            Assert.Null(text.Validate);
            Assert.Null(text.MinWidth);
            Assert.Null(text.MaxWidth);
            Assert.Null(text.Alignment);
            Assert.Null(text.VerticalAlignment);
            Assert.Equal("*", text.Width);

            var check = new LunaColumn<Row>("h", r => true);
            Assert.Equal(LunaCellKind.Check, check.Kind);
            Assert.NotNull(check.Checked);
            Assert.Null(check.Toggle);      // read-only unless a write is given
            Assert.Null(check.Build);
            Assert.Equal("yes", check.Text(new Row(1)));

            var writable = new LunaColumn<Row>("h", r => true, (r, on) => { });
            Assert.NotNull(writable.Toggle);

            var template = new LunaColumn<Row>("h", r => new Button(), r => "said");
            Assert.Equal(LunaCellKind.Template, template.Kind);
            Assert.NotNull(template.Build);
            Assert.Null(template.Checked);
            Assert.Null(template.Toggle);
            Assert.Equal("said", template.Text(new Row(1)));
        }

        [Fact]
        public void Can_execute_changed_moves_only_with_is_enabled()
        {
            var action = new LunaAction("Pause");
            int raised = 0;
            action.CanExecuteChanged += (_, _) => raised++;

            // "not for a label change, which would re-query CanExecute on every keystroke"
            action.Text = "Resume";
            action.HelpText = "and again";
            Assert.Equal(0, raised);

            action.IsEnabled = false;
            Assert.Equal(1, raised);

            // "raised only when IsEnabled MOVES" - setting the same value again is not a move.
            action.IsEnabled = false;
            Assert.Equal(1, raised);
        }

        [Fact]
        public void A_group_keeps_at_most_one_member_checked()
        {
            var group = new ActionGroup();
            LunaAction a = group.Add("a");
            LunaAction b = group.Add("b");
            LunaAction c = group.Add("c");

            Assert.Null(group.Checked);
            Assert.Null(new LunaAction("loner").Group);
            Assert.Same(group, a.Group);

            a.IsChecked = true;
            b.IsChecked = true;
            Assert.Same(b, group.Checked);
            Assert.False(a.IsChecked);
            Assert.False(c.IsChecked);
        }

        [Fact]
        public void A_suppressor_is_true_until_every_scope_is_disposed()
        {
            var s = new Suppressor();
            Assert.False(s.IsSuppressing);

            IDisposable outer = s.Suppress();
            Assert.True(s.IsSuppressing);

            IDisposable inner = s.Suppress();
            Assert.True(s.IsSuppressing);

            inner.Dispose();
            // "False once EVERY scope taken has been disposed" - one of two is not every.
            Assert.True(s.IsSuppressing);

            outer.Dispose();
            Assert.False(s.IsSuppressing);
        }

        [Fact]
        public Task Post_always_defers_even_from_the_ui_thread() => UiTest.Run(() =>
        {
            bool ran = false;
            UiThread.Post(() => ran = true);
            Assert.False(ran);

            // Run, by contrast, is documented to go straight through when already there.
            bool immediate = false;
            UiThread.Run(() => immediate = true);
            Assert.True(immediate);
        });

        [Fact]
        public Task Documented_defaults() => UiTest.Run(() =>
        {
            var image = new RgbaImageView();
            Assert.Equal(Stretch.None, image.Stretch);
            Assert.False(image.IntegerScale);
            Assert.Null(image.Source);

            var table = new LunaTable<Row>();
            Assert.False(table.VirtualizeColumns);
            Assert.Equal(LunaSelectionMode.Single, table.SelectionMode);
            Assert.Equal(LunaSelectionUnit.Row, table.SelectionUnit);
            Assert.Equal(16d, table.IndentSize);
            Assert.Null(table.CanDrop);
            Assert.Null(table.Children);
            Assert.Null(table.Selected);
            Assert.Equal(LunaEditGestures.Default, table.EditGestures);
            Assert.Equal(LunaGridLines.None, table.GridLines);

            Assert.Equal(TimeSpan.FromSeconds(3), IdleCursor.DefaultDelay);
        });

        [Fact]
        public Task Parts_are_null_before_the_template_is_applied() => UiTest.Run(() =>
        {
            var table = new LunaTable<Row>();
            table.Column("n", r => r.N.ToString());

            // The three template parts, and Facet, all promise to be readable/null this early.
            Assert.Null(table.SelectedCell);
            Assert.False(table.IsExpanded(new Row(1)));

            var filter = new FilterBar();
            Assert.Null(filter.Facet);
        });

        [Fact]
        public Task A_flat_table_is_never_expanded_and_a_row_unit_has_no_cell() => UiTest.Run(() =>
        {
            var table = new LunaTable<Row> { SelectionUnit = LunaSelectionUnit.Row };
            table.Column("n", r => r.N.ToString());
            var host = new Window { Content = table, Width = 300, Height = 200 };
            host.Show();
            UiTest.Settle(table);

            var row = new Row(1);
            table.Refresh(new[] { row });
            UiTest.Settle(table);

            table.Expand(row);
            Assert.False(table.IsExpanded(row));

            table.Select(row);
            Assert.Equal(row, table.Selected);
            // "or null when no cell is selected OR THE UNIT IS ROW"
            Assert.Null(table.SelectedCell);
            host.Close();
        });

        [Fact]
        public Task A_side_panel_hands_back_the_same_toggle_every_time() => UiTest.Run(() =>
        {
            var panel = new SidePanel();
            LunaAction first = panel.ToggleAction;
            LunaAction second = panel.ToggleAction;

            Assert.Same(first, second);
            Assert.True(first.IsCheckable);

            // "every surface agrees about whether the panel is open"
            panel.IsOpen = true;
            Assert.True(first.IsChecked);
            panel.IsOpen = false;
            Assert.False(first.IsChecked);
        });

        [Fact]
        public Task A_console_pane_drops_the_oldest_lines() => UiTest.Run(() =>
        {
            var console = new ConsolePane { MaxLines = 3 };
            for (int i = 1; i <= 5; i++) console.AppendLine("line " + i);

            string output = console.OutputText;
            Assert.DoesNotContain("line 1", output);
            Assert.DoesNotContain("line 2", output);
            Assert.Contains("line 5", output);

            console.Clear();
            Assert.Equal("", console.OutputText.Trim());
        });

        [Fact]
        public Task A_field_row_has_an_error_exactly_when_one_was_given() => UiTest.Run(() =>
        {
            var field = new FieldRow();
            Assert.False(field.HasError);
            Assert.False(field.HasHint);

            field.Error = "too long";
            Assert.True(field.HasError);

            field.Error = "";
            Assert.False(field.HasError);

            field.Hint = "up to 8 characters";
            Assert.True(field.HasHint);
        });

    }
}
