using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Tests
{
    // The control kit, driven through a real (headless) window so template application and styling are exercised, not assumed - see docs/LunaP.md §5.
    public class LunaPControlTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(LunaPControlTests).GetTypeInfo().Assembly);

        // Takes a factory, not a control: anything built on the test thread belongs to the wrong dispatcher the moment a window adopts it.
        private static Task Realised<T>(Func<T> make, Action<T> assert) where T : Control => Session.Dispatch(() =>
        {
            T control = make();
            var window = new Window { Width = 400, Height = 300, Content = control };
            window.Show();
            assert(control);
        }, default);

        [Fact]
        public Task Section_headers_take_the_palette_from_the_stylesheet() =>
            Realised(() => new SectionHeader { Text = "Load" }, header =>
            {
                Assert.Equal(LunaPalette.SectionHeader.Color, Assert.IsAssignableFrom<ISolidColorBrush>(header.Foreground).Color);
                Assert.Equal(FontWeight.Bold, header.FontWeight);
            });

        [Fact]
        public Task Hint_text_is_muted_small_and_wrapping() =>
            Realised(() => new HintText { Text = "explanation" }, hint =>
            {
                Assert.Equal(LunaPalette.Muted.Color, Assert.IsAssignableFrom<ISolidColorBrush>(hint.Foreground).Color);
                Assert.Equal(LunaPalette.HintFontSize, hint.FontSize);
                Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
            });

        [Fact]
        public Task Mono_text_uses_the_shared_stack() =>
            Realised(() => new MonoText { Text = "A=0x00" }, mono =>
            {
                Assert.Equal(LunaPalette.Text.Color, Assert.IsAssignableFrom<ISolidColorBrush>(mono.Foreground).Color);
                Assert.Equal(LunaPalette.MonoFont.Name, mono.FontFamily.Name);
            });

        [Theory]
        [InlineData(10, "#32CD32")]
        [InlineData(70, "#FFD700")]
        [InlineData(95, "#FF4500")]
        // Asserted on the rendered bar rather than a computed property: the ramp is a pseudo-class and a style now, so a theme can reach it.
        public Task A_meter_row_colours_its_bar_from_the_ramp(double percent, string expected) =>
            Realised(() => new MeterRow { Label = "S-CPU", Percent = percent, ValueText = $"{percent}%" },
                row => Assert.Equal(Color.Parse(expected),
                    Assert.IsAssignableFrom<ISolidColorBrush>(row.FindPart<ProgressBar>()!.Foreground).Color));

        [Fact]
        public Task A_meter_row_builds_its_template() =>
            Realised(() => new MeterRow { Label = "S-CPU", Percent = 40, ValueText = "40.0%" }, row =>
            {
                ProgressBar? bar = row.FindPart<ProgressBar>();
                Assert.NotNull(bar);
                Assert.Equal(40, bar!.Value);
            });

        [Fact]
        public Task A_meter_list_makes_one_row_per_entry() =>
            Realised(() => new MeterList
            {
                Meters = new List<MeterEntry>
                {
                    new("CPU", 12, "12.0%"),
                    new("Machine memory", 44, "44.0%"),
                    new("Heap fragmentation", 3, "3.0%"),
                },
            }, list => Assert.Equal(3, list.CountParts<MeterRow>()));

        [Fact]
        public Task A_meter_list_rebuilds_when_its_entries_change() =>
            Realised(() => new MeterList { Meters = new List<MeterEntry> { new("CPU", 1, "1%") } }, list =>
            {
                Assert.Equal(1, list.CountParts<MeterRow>());

                list.Meters = new List<MeterEntry> { new("CPU", 1, "1%"), new("GPU", 2, "2%") };

                Assert.Equal(2, list.CountParts<MeterRow>());
            });

        // The flaw the three hand-written copies shared: a fresh bitmap every tick.
        [Fact]
        public Task An_image_view_reuses_its_bitmap_while_the_size_holds() =>
            Realised(() => new RgbaImageView(), view =>
            {
                view.SetFrame(new byte[8 * 8 * 4], 8, 8);
                object? first = view.Source;
                Assert.NotNull(first);

                view.SetFrame(new byte[8 * 8 * 4], 8, 8);
                Assert.Same(first, view.Source);

                view.SetFrame(new byte[16 * 16 * 4], 16, 16);
                Assert.NotSame(first, view.Source);
            });

        [Fact]
        public Task An_image_view_clears_on_an_empty_or_short_buffer() =>
            Realised(() => new RgbaImageView(), view =>
            {
                view.SetFrame(new byte[4 * 4 * 4], 4, 4);
                Assert.NotNull(view.Source);

                // What a core with no tile memory reports.
                view.SetFrame(Array.Empty<byte>(), 0, 0);
                Assert.Null(view.Source);

                // A buffer too short for its claimed size must not be copied out of.
                view.SetFrame(new byte[4], 16, 16);
                Assert.Null(view.Source);
            });

        [Fact]
        public Task A_field_row_hides_an_empty_hint() =>
            Realised(() => new FieldRow { Label = "Log Directory", Content = new TextBox() }, field =>
            {
                Assert.False(field.HasHint);
                Assert.False(field.FindPart<HintText>()!.IsVisible);

                field.Hint = "Where per-session log files are written.";

                Assert.True(field.HasHint);
                Assert.True(field.FindPart<HintText>()!.IsVisible);
            });

        // The XAML shape - children written between the tags rather than handed over as ItemsSource. Four windows use it and nothing covered it.
        [Fact]
        public Task A_button_bar_realises_buttons_declared_as_its_children() =>
            Realised(() =>
            {
                var bar = new ButtonBar();
                bar.Items.Add(new Button { Content = "Reset to Defaults" });
                bar.Items.Add(new Button { Content = "Close" });
                return bar;
            }, bar =>
            {
                Assert.Equal(2, bar.CountParts<Button>());
                Assert.Contains(bar.FindParts<TextBlock>(), t => t.Text == "Close");
            });

        [Fact]
        public Task A_status_bar_shows_its_status_and_its_buttons() =>
            Realised(() => new StatusBar
            {
                Status = "Ready.",
                Content = new ButtonBar { ItemsSource = new[] { new Button { Content = "Close" } } },
            }, bar =>
            {
                Assert.Contains(bar.FindParts<TextBlock>(), t => t.Text == "Ready.");
                Assert.NotNull(bar.FindPart<Button>());
            });

        [Fact]
        public Task A_console_pane_appends_output_and_raises_what_was_typed() =>
            Realised(() => new ConsolePane { Prompt = "DianaOS #: " }, pane =>
            {
                string? submitted = null;
                pane.Submitted += line => submitted = line;

                pane.AppendLine("first");
                pane.AppendLine("second");
                Assert.Equal("first\nsecond", pane.OutputText);

                TextBox input = pane.FindPart<TextBox>()!;
                input.Text = "status";
                Press(input, Key.Enter);

                Assert.Equal("status", submitted);
                Assert.Equal("", input.Text);

                pane.Clear();
                Assert.Equal("", pane.OutputText);
            });

        // Both console windows print a welcome banner from their constructor, before any template exists.
        [Fact]
        public Task A_console_pane_keeps_output_written_before_it_was_shown() => Session.Dispatch(() =>
        {
            var pane = new ConsolePane();
            pane.AppendLine("welcome");
            pane.AppendLine("no ROM loaded yet");

            var window = new Window { Width = 400, Height = 300, Content = pane };
            window.Show();

            Assert.Equal("welcome\nno ROM loaded yet", pane.OutputText);
            Assert.Equal("welcome\nno ROM loaded yet", pane.FindPart<SelectableTextBlock>()!.Text);
        }, default);

        // The recall algorithm both console windows hand-wrote, including the "back to the half-typed line" case.
        [Fact]
        public Task A_console_pane_walks_history_and_returns_to_the_live_line() =>
            Realised(() => new ConsolePane(), pane =>
            {
                pane.HistorySource = () => new[] { "older", "newer" };

                TextBox input = pane.FindPart<TextBox>()!;
                input.Text = "half-typed";

                Press(input, Key.Up);
                Assert.Equal("newer", input.Text);

                Press(input, Key.Up);
                Assert.Equal("older", input.Text);

                // Already at the oldest entry - stays put rather than wrapping.
                Press(input, Key.Up);
                Assert.Equal("older", input.Text);

                Press(input, Key.Down);
                Assert.Equal("newer", input.Text);

                Press(input, Key.Down);
                Assert.Equal("half-typed", input.Text);

                Press(input, Key.Down);
                Assert.Equal("half-typed", input.Text);
            });

        [Fact]
        public Task A_console_pane_with_no_history_ignores_the_arrows() =>
            Realised(() => new ConsolePane(), pane =>
            {
                TextBox input = pane.FindPart<TextBox>()!;
                input.Text = "typing";

                Press(input, Key.Up);
                Press(input, Key.Down);

                Assert.Equal("typing", input.Text);
            });

        private static void Press(TextBox input, Key key) =>
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

        // The two defects §21.4 recorded in this control, now pinned - see §22.6.

        [Fact]
        public Task A_console_pane_drops_its_oldest_lines_at_the_cap() =>
            Realised(() => new ConsolePane { MaxLines = 3 }, pane =>
            {
                for (int i = 1; i <= 6; i++) pane.AppendLine($"line {i}");

                // Bounded, and bounded from the FRONT: a console keeps what just happened, not
                // what happened first. Unbounded is a leak with a nice name.
                Assert.Equal("line 4\nline 5\nline 6", pane.OutputText);
            });

        [Fact]
        public Task A_console_pane_with_no_cap_keeps_everything() =>
            Realised(() => new ConsolePane { MaxLines = 0 }, pane =>
            {
                for (int i = 1; i <= 200; i++) pane.AppendLine($"line {i}");

                Assert.Equal(200, pane.OutputText.Split('\n').Length);
            });

        // The behaviour that made scrolling back useless: any new line yanked the reader to the
        // bottom. The wiring cannot be tested here - under Avalonia.Headless a ScrollViewer reports
        // extent == viewport however much text it holds, so ScrollToEnd does nothing and a test
        // over Offset passes whatever the control does. That was found by writing such a test,
        // sabotaging the fix, and watching it stay green (§22.6).
        //
        // So the RULE is tested instead, which is the §8.1 pattern: the decision is a pure function
        // of three numbers and does not need a laid-out scroll viewer to be pinned.
        [Theory]
        [InlineData(0, 100, 100, true)]     // nothing hidden - a short console follows
        [InlineData(900, 1000, 100, true)]  // pinned to the bottom - follow
        [InlineData(899.5, 1000, 100, true)] // half a pixel short, still pinned in practice
        [InlineData(0, 1000, 100, false)]   // scrolled to the top - do not yank them back
        [InlineData(450, 1000, 100, false)] // reading the middle - leave them there
        public void A_console_pane_follows_the_tail_only_from_the_bottom(
            double offsetY, double extentHeight, double viewportHeight, bool expected) =>
            Assert.Equal(expected, ConsolePane.Follows(offsetY, extentHeight, viewportHeight));

    }
}
