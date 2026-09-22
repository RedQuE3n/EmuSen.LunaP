using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // OverlayBar - see docs/LunaP.md §88.4.
    //
    // THESE TESTS LET REAL TIME PASS, which the rest of the suite does not, and RealTime says why that
    // is sound: a nested dispatcher frame runs timers in due order, so a 100 ms hide delay has always
    // fired by the end of a 500 ms wait. Every assertion is of that shape - "by the end of the wait" -
    // and none says "not yet", which is the half a loaded machine could falsify. The pointer is the
    // headless platform's own (MouseMove), so IsPointerOver and PointerExited are the input system's
    // answers rather than events this file raised by hand.
    public class OverlayBarTests
    {
        private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(100);
        private const int Long = 500;

        private sealed class Stage
        {
            public Stage()
            {
                Game = new Border { Background = Brushes.Black };
                Bar = new OverlayBar { Watch = Game, HideAfter = Short, Content = new Button { Content = "Pause" } };
                RevealedLog = new List<bool>();
                Bar.RevealedChanged += RevealedLog.Add;

                var over = new Grid();
                over.Children.Add(Game);
                over.Children.Add(Bar);

                // A strip above the game that nothing watches, so the pointer has somewhere to go that
                // is neither the game nor the bar.
                var elsewhere = new Border { Height = 60, Background = Brushes.Gray };
                DockPanel.SetDock(elsewhere, Dock.Top);
                var dock = new DockPanel();
                dock.Children.Add(elsewhere);
                dock.Children.Add(over);

                Window = new ToolWindow { Width = 640, Height = 480, Content = dock };
                Window.Show();
                Settle();
            }

            public Border Game { get; }
            public OverlayBar Bar { get; }
            public ToolWindow Window { get; }
            public List<bool> RevealedLog { get; }

            public void Settle()
            {
                Dispatcher.UIThread.RunJobs();
                UiTest.Capture(Window);
            }

            public void MoveTo(Point at)
            {
                Window.MouseMove(at);
                Dispatcher.UIThread.RunJobs();
            }

            public Point OnGame => new(20, 100);

            public Point Elsewhere => new(20, 20);

            public Point OnBar => Bar.TranslatePoint(new Point(Bar.Bounds.Width / 2, Bar.Bounds.Height / 2), Window)!.Value;
        }

        [Fact]
        public Task It_starts_concealed_and_out_of_the_pointers_way() => UiTest.Run(() =>
        {
            var stage = new Stage();

            Assert.False(stage.Bar.IsRevealed);
            Assert.False(stage.Bar.IsHitTestVisible);
            Assert.Equal(0, stage.Bar.Opacity);
            Assert.False(stage.Bar.IsHidePending);

            stage.Window.Close();
        });

        [Fact]
        public Task Moving_over_the_watched_element_reveals_and_stopping_conceals() => UiTest.Run(() =>
        {
            var stage = new Stage();

            stage.MoveTo(stage.OnGame);
            Assert.True(stage.Bar.IsRevealed);
            Assert.True(stage.Bar.IsHitTestVisible);
            Assert.True(stage.Bar.IsHidePending);

            RealTime.Wait(Long);

            Assert.False(stage.Bar.IsRevealed);
            Assert.False(stage.Bar.IsHitTestVisible);
            Assert.Equal(new[] { true, false }, stage.RevealedLog);

            stage.Window.Close();
        });

        // The fade is a real transition: at rest after it, the bar is fully opaque, and after
        // concealing fully transparent. No render tick is forced: inside RealTime's frame the render
        // timer runs as well, measured by writing these reads with a forced tick and removing it
        // (§88.7).
        [Fact]
        public Task It_fades_to_opaque_and_back() => UiTest.Run(() =>
        {
            var stage = new Stage();
            stage.Bar.HideAfter = TimeSpan.FromSeconds(30);

            stage.Bar.Reveal();
            RealTime.Wait(Long);
            Assert.Equal(1, stage.Bar.Opacity);

            stage.Bar.Conceal();
            RealTime.Wait(Long);
            Assert.Equal(0, stage.Bar.Opacity);

            stage.Window.Close();
        });

        // OpenEmu's canFadeOut: the delay passing with the pointer on the bar does not hide it, and
        // the pointer leaving starts the delay again.
        [Fact]
        public Task It_does_not_hide_while_the_pointer_is_over_it() => UiTest.Run(() =>
        {
            var stage = new Stage();

            stage.MoveTo(stage.OnGame);
            stage.MoveTo(stage.OnBar);
            Assert.True(stage.Bar.IsPointerOver);

            RealTime.Wait(Long);
            Assert.True(stage.Bar.IsRevealed);
            Assert.False(stage.Bar.IsHidePending);   // it fired, and declined

            stage.MoveTo(stage.Elsewhere);
            Assert.True(stage.Bar.IsHidePending);
            RealTime.Wait(Long);

            Assert.False(stage.Bar.IsRevealed);
            stage.Window.Close();
        });

        [Fact]
        public Task Keep_open_holds_it_and_clearing_it_restarts_the_delay() => UiTest.Run(() =>
        {
            var stage = new Stage();

            stage.Bar.Reveal();
            stage.Bar.KeepOpen = true;
            RealTime.Wait(Long);
            Assert.True(stage.Bar.IsRevealed);
            Assert.False(stage.Bar.IsHidePending);

            stage.Bar.KeepOpen = false;
            Assert.True(stage.Bar.IsHidePending);
            RealTime.Wait(Long);
            Assert.False(stage.Bar.IsRevealed);

            stage.Window.Close();
        });

        // The host overrules the rule.
        [Fact]
        public Task Conceal_hides_even_with_keep_open() => UiTest.Run(() =>
        {
            var stage = new Stage();
            stage.Bar.Reveal();
            stage.Bar.KeepOpen = true;

            stage.Bar.Conceal();

            Assert.False(stage.Bar.IsRevealed);
            Assert.False(stage.Bar.IsHidePending);
            Assert.Equal(new[] { true, false }, stage.RevealedLog);
            stage.Window.Close();
        });

        [Fact]
        public Task Focus_inside_reveals_it_and_holds_it() => UiTest.Run(() =>
        {
            var stage = new Stage();
            var button = (Button)stage.Bar.Content!;

            button.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(stage.Bar.IsRevealed);

            RealTime.Wait(Long);
            Assert.True(stage.Bar.IsRevealed);

            stage.Window.Close();
        });

        // The plate is the palette's HUD surface and the content is resolved in the Dark palette,
        // whichever variant the application is in - a bar over a game is dark on any desktop.
        [Fact]
        public Task The_plate_is_dark_in_either_variant() => UiTest.Run(() =>
        {
            try
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                var stage = new Stage();
                var button = (Button)stage.Bar.Content!;

                Assert.Equal(Color.Parse("#E61C1C1C"), ((ISolidColorBrush)stage.Bar.Background!).Color);
                Assert.Equal(new CornerRadius(10), stage.Bar.CornerRadius);
                Assert.Equal(new Thickness(10, 5), stage.Bar.Padding);
                Assert.Equal(ThemeVariant.Dark, button.ActualThemeVariant);
                Assert.Equal(ThemeVariant.Light, stage.Game.ActualThemeVariant);

                stage.Window.Close();
            }
            finally
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            }
        });

        [Fact]
        public Task It_is_a_toolbar_to_a_reader_even_while_concealed() => UiTest.Run(() =>
        {
            var stage = new Stage();
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(stage.Bar);

            Assert.Equal(AutomationControlType.ToolBar, peer.GetAutomationControlType());
            Assert.True(peer.IsControlElement());
            stage.Window.Close();
        });
    }
}
