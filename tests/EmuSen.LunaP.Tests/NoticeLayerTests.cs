using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // NoticeLayer - see docs/LunaP.md §88.5. Real time passes here, by RealTime's method; the one
    // "not yet" below is asserted against a timer due 400 ms after the wait ends, and says so.
    public class NoticeLayerTests
    {
        private static (NoticeLayer Notice, ToolWindow Window) Show(TimeSpan duration)
        {
            var notice = new NoticeLayer { Duration = duration };
            var grid = new Grid();
            grid.Children.Add(new Border { Background = Brushes.Black });
            grid.Children.Add(notice);

            var window = new ToolWindow { Width = 480, Height = 320, Content = grid };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            return (notice, window);
        }

        private static Border Pill(NoticeLayer notice) => notice.FindNamed<Border>("PART_Pill");

        [Fact]
        public Task Nothing_shows_until_the_first_notice() => UiTest.Run(() =>
        {
            (NoticeLayer notice, ToolWindow window) = Show(TimeSpan.FromSeconds(1.75));

            Assert.Null(notice.Current);
            Assert.Equal(0, Pill(notice).Opacity);
            Assert.False(notice.IsHitTestVisible);
            Assert.Equal(HorizontalAlignment.Right, notice.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, notice.VerticalAlignment);

            window.Close();
        });

        [Fact]
        public Task A_notice_shows_its_text_and_is_gone_after_its_duration() => UiTest.Run(() =>
        {
            (NoticeLayer notice, ToolWindow window) = Show(TimeSpan.FromMilliseconds(300));

            notice.Show("Quick Save");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Quick Save", notice.Current);
            Assert.Equal("Quick Save", notice.FindNamed<TextBlock>("PART_Text").Text);

            RealTime.Wait(800);

            Assert.Null(notice.Current);
            window.Close();
        });

        // 0 -> 1 -> 1 -> 0 at 0 / 0.15 / 0.85 / 1: halfway through a two-second notice is the middle
        // of the plateau, 700 ms from either edge, and after it the pill is transparent again.
        [Fact]
        public Task The_pill_holds_full_opacity_through_the_middle_and_fades_out() => UiTest.Run(() =>
        {
            (NoticeLayer notice, ToolWindow window) = Show(TimeSpan.FromSeconds(2));

            notice.Show("Fast Forward");
            RealTime.Wait(1000);
            Assert.Equal(1, Pill(notice).Opacity);

            RealTime.Wait(1400);
            Assert.Equal(0, Pill(notice).Opacity);
            Assert.Null(notice.Current);

            window.Close();
        });

        [Fact]
        public void The_curve_is_openemus() =>
            Assert.Equal(new[] { (0.0, 0.0), (0.15, 1.0), (0.85, 1.0), (1.0, 0.0) }, NoticeCurve());

        // A second notice replaces the first and restarts the clock. The first would have ended at
        // 1000 ms; the second was shown at 600 and ends at 1600. At 1200 it must still be showing - a
        // "not yet" with 400 ms between the wait's end and the timer it is about.
        [Fact]
        public Task A_second_notice_replaces_the_first_and_restarts_it() => UiTest.Run(() =>
        {
            (NoticeLayer notice, ToolWindow window) = Show(TimeSpan.FromMilliseconds(1000));
            var seen = new List<string?>();
            notice.PropertyChanged += (_, e) =>
            {
                if (e.Property == NoticeLayer.CurrentProperty) seen.Add((string?)e.NewValue);
            };

            notice.Show("Quick Save");
            RealTime.Wait(600);
            notice.Show("Quick Load");
            RealTime.Wait(600);

            Assert.Equal("Quick Load", notice.Current);

            RealTime.Wait(1000);
            Assert.Null(notice.Current);
            Assert.Equal(new[] { "Quick Save", "Quick Load", null }, seen);

            window.Close();
        });

        [Fact]
        public Task It_is_a_polite_live_region_that_says_when_it_changes() => UiTest.Run(() =>
        {
            (NoticeLayer notice, ToolWindow window) = Show(TimeSpan.FromSeconds(30));
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(notice);
            var changed = new List<object?>();
            peer.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutomationElementIdentifiers.NameProperty) changed.Add(e.NewValue);
            };

            notice.Show("Screenshot Saved");

            Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
            Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());
            Assert.Equal("Screenshot Saved", peer.GetName());
            Assert.Equal(new object?[] { "Screenshot Saved" }, changed);

            window.Close();
        });

        [Fact]
        public Task Show_refuses_null() => UiTest.Run(() =>
            Assert.Throws<ArgumentNullException>("text", () => new NoticeLayer().Show(null!)));

        private static (double, double)[] NoticeCurve()
        {
            var field = typeof(NoticeLayer).GetField("Curve", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return ((double Cue, double Opacity)[])field.GetValue(null)!;
        }
    }
}
