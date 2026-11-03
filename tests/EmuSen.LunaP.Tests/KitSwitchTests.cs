using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Motion;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // The text scale and reduced motion are the process's, so their tests run alone, after every other.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class KitSwitches
    {
        public const string Name = "The kit's switches";
    }

    // Text size and reduced motion as the kit's own switches: the scaled type, the metrics following, and the kit's movements stopping - see docs/LunaP.md §214.
    [Collection(KitSwitches.Name)]
    public class KitSwitchTests(ITestOutputHelper output)
    {
        [Fact]
        public Task The_text_scale_multiplies_every_size_to_a_half_unit_and_is_clamped() => UiTest.Run(() =>
        {
            try
            {
                LunaTypeScale before = LunaTokens.BigScreenType;
                Assert.Equal((26d, 22d, 20d), (before.Row, before.Body, before.Caption));
                int raised = 0;
                void Count() => raised++;
                LunaTokens.Changed += Count;
                LunaTokens.TextScale = 1.15;
                LunaTokens.TextScale = 1.15;
                LunaTokens.Changed -= Count;
                Assert.Equal(1, raised);
                Assert.Equal((30d, 25.5d, 23d, 50.5d), (LunaTokens.BigScreenType.Row, LunaTokens.BigScreenType.Body, LunaTokens.BigScreenType.Caption, LunaTokens.BigScreenType.Display));
                Assert.Equal(15d, LunaTokens.DesktopType.Body);
                LunaTokens.TextScale = 9;
                Assert.Equal(LunaTokens.MaxTextScale, LunaTokens.TextScale);
                Assert.Equal(34d, LunaTokens.BigScreenType.Row);
                LunaTokens.TextScale = 0.5;
                Assert.Equal(1d, LunaTokens.TextScale);
                Assert.Equal(before, LunaTokens.BigScreenType);
            }
            finally { LunaTokens.TextScale = 1; }
        });

        [Fact]
        public Task A_metrics_dictionary_follows_the_text_scale_at_once() => UiTest.Run(() =>
        {
            try
            {
                var big = new LunaMetrics { Form = LunaForm.BigScreen };
                var desk = new LunaMetrics();
                Assert.Equal(22d, big["LunaBodyFontSize"]);
                LunaTokens.TextScale = 1.3;
                Assert.Equal(28.5d, big["LunaBodyFontSize"]);
                Assert.Equal(17d, desk["ControlContentThemeFontSize"]);
            }
            finally { LunaTokens.TextScale = 1; }
        });

        // A switch set off the interface's thread reaches every subscriber on it: an attached overlay bar takes its new fade there, and the setter never throws (docs/LunaP.md §214.1).
        [Fact]
        public Task A_switch_set_off_the_interface_thread_tells_its_controls_on_that_thread() => UiTest.Run(() =>
        {
            var bar = new OverlayBar();
            var window = new Window { Content = bar };
            window.Show();
            Exception? thrown = null;
            try
            {
                var other = new System.Threading.Thread(() =>
                {
                    try
                    {
                        LunaMotion.Reduced = true;
                        LunaTokens.TextScale = 1.15;
                    }
                    catch (Exception e) { thrown = e; }
                });
                other.Start();
                other.Join();
                Assert.Null(thrown);
                Assert.True(LunaMotion.Reduced);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(TimeSpan.FromMilliseconds(120), ((DoubleTransition)bar.Transitions!.Single()).Duration);
            }
            finally
            {
                LunaMotion.Reduced = false;
                LunaTokens.TextScale = 1;
            }
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(TimeSpan.FromMilliseconds(250), ((DoubleTransition)bar.Transitions!.Single()).Duration);
            window.Close();
        });

        [Fact]
        public Task Under_reduced_motion_a_glide_is_at_its_end_a_notice_fades_in_120_ms_and_the_overlay_bar_too() => UiTest.Run(() =>
        {
            var glide = new Glide(0, 10, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            var notice = new NoticeLayer { Duration = TimeSpan.FromSeconds(2) };
            var bar = new OverlayBar();
            var window = new Window { Content = bar };
            window.Show();
            Assert.Equal(5, glide.ValueAt(TimeSpan.FromSeconds(0.5)), 3);
            Assert.Equal(TimeSpan.FromMilliseconds(250), ((DoubleTransition)bar.Transitions!.Single()).Duration);
            int raised = 0;
            void Count() => raised++;
            LunaMotion.Changed += Count;
            try
            {
                LunaMotion.Reduced = true;
                Assert.Equal(1, raised);
                Assert.Equal(10, glide.ValueAt(TimeSpan.FromSeconds(0.5)));
                Assert.True(glide.IsSettledAt(TimeSpan.Zero));
                var frames = ((double Cue, double Opacity)[])typeof(NoticeLayer).GetMethod("Keyframes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(notice, null)!;
                Assert.Equal(0.06, frames[1].Cue, 3);
                Assert.Equal(TimeSpan.FromMilliseconds(120), ((DoubleTransition)bar.Transitions!.Single()).Duration);
            }
            finally
            {
                LunaMotion.Reduced = false;
                LunaMotion.Changed -= Count;
            }
            Assert.Equal(TimeSpan.FromMilliseconds(250), ((DoubleTransition)bar.Transitions!.Single()).Duration);
            window.Close();
        });

        [Theory]
        [InlineData(1280, 720)]
        [InlineData(1280, 800)]
        public Task Settings_at_the_largest_text_scale_fits(int width, int height) => UiTest.Run(() =>
        {
            try
            {
                LunaTokens.TextScale = LunaTokens.MaxTextScale;
                var layout = new SettingsLayout();
                layout.SetSections([new("display", "Display"), new("sound", "Sound")], "display");
                layout.SetRows([
                    new PageRow("text", "Text size") { Kind = PageRowKind.Steps, Status = "Larger" },
                    new PageRow("motion", "Reduced motion") { Kind = PageRowKind.Steps, Status = "System" },
                    new PageRow("art", "Background art on Home") { Kind = PageRowKind.Steps, Status = "On" },
                ]);
                layout.PaneFor = key => new SettingsPane([new SettingsLine("Text size scales every word on the big screen; nothing is cut off at any size.")]);
                layout.HintsFor = key => [new PageHint(PadGlyphButton.South, "Change"), new PageHint(PadGlyphButton.East, "Back")];
                MenuPanel.SetScale(layout, height / SettingsLayout.DesignHeight);
                var window = new Window { Width = width, Height = height, Content = layout };
                window.Show();
                UiTest.Settle(layout);
                var faults = FitAudit.Check(layout, []);
                foreach (string f in faults) output.WriteLine(f);
                Assert.Empty(faults);
                window.Close();
            }
            finally { LunaTokens.TextScale = 1; }
        });
    }
}
