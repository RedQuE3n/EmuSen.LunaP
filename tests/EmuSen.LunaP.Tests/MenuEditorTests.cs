using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // An editor built of menu rows: text boxes, switches and hosted controls as rows, a subtitle, a band of buttons, a wrapped footer, and a message box over a menu - see docs/LunaP.md §182.
    public class MenuEditorTests
    {
        private static int CountNear(RenderedFrame f, Rect box, Color colour, int tolerance = 12)
        {
            int n = 0;
            for (int y = (int)box.Top; y < (int)box.Bottom; y++)
                for (int x = (int)box.Left; x < (int)box.Right; x++)
                    if (Near(At(f, x, y), colour, tolerance)) n++;
            return n;
        }

        private static MenuRow RowOf(Control c) => c.GetVisualDescendants().OfType<MenuRow>().First();

        [Fact]
        public Task A_text_box_drawn_as_a_row_shows_its_text_as_the_value_follows_typing_and_takes_the_bar_with_the_focus() => UiTest.Run(() =>
        {
            var box = MenuRows.Apply(new TextBox { Text = "Aurora Drift", Width = 600 }, "Name");
            MenuRows.SetValueLetterCase(box, LetterCase.None);
            MenuRows.SetValueColor(box, Colors.Red);
            var other = new Button { Content = "elsewhere" };
            ToolWindow window = Show(new StackPanel { Children = { box, other } }, 600, 120);
            MenuRow row = RowOf(box);
            Assert.Equal(("Name", "Aurora Drift", MenuRowKind.Submenu), (MenuRows.GetLabel(box), row.Value, row.Kind));
            Assert.Equal(LetterCase.None, row.ValueLetterCase);

            box.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(row.IsHighlighted);
            box.CaretIndex = box.Text!.Length;
            box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "!", Source = box });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Aurora Drift!", row.Value);

            // The value is drawn in its own colour; the label is not.
            RenderedFrame f = Frame(window);
            MenuRowLayout at = row.Layout(row.Bounds.Size);
            Assert.True(CountNear(f, at.Value, Colors.Red) > 20, "the value is not in its colour");
            Assert.Equal(0, CountNear(f, at.Label, Colors.Red));
            // The value as written, not upper-cased: following the row's casing again changes what is drawn.
            MenuRows.SetValueLetterCase(box, null);
            RenderedFrame upper = Frame(window);
            MenuRowLayout upperAt = row.Layout(row.Bounds.Size);
            Assert.True(CountNear(f, at.Value, Colors.Red) != CountNear(upper, upperAt.Value, Colors.Red), "the value's own casing is not drawn");
            MenuRows.SetValueLetterCase(box, LetterCase.None);
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.False(row.IsHighlighted);
            window.Close();
        });

        [Fact]
        public Task A_switch_drawn_as_a_row_shows_its_state_and_is_still_toggled_by_a_click_and_by_code() => UiTest.Run(() =>
        {
            var toggle = MenuRows.Apply(new LunaSwitch { Label = "Completed", Width = 600 }, "Completed");
            ToolWindow window = Show(new StackPanel { Children = { toggle } }, 600, 60);
            MenuRow row = RowOf(toggle);
            Assert.Equal(MenuRowKind.Switch, row.Kind);
            Assert.False(row.IsOn);
            toggle.IsChecked = true;
            Assert.True(row.IsOn);
            toggle.Focus();
            toggle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = toggle });
            Assert.False(toggle.IsChecked);
            Assert.False(row.IsOn);
            Assert.Equal(default, row.Layout(row.Bounds.Size).Value);
            window.Close();
        });

        [Fact]
        public Task A_field_row_puts_its_control_at_the_right_on_the_bar_cuts_its_label_short_of_it_and_is_highlighted_while_it_has_the_focus() => UiTest.Run(() =>
        {
            var stars = new RatingPicker { StarSize = 30, FilledColor = Colors.Lime, UnfilledColor = Colors.Blue, Value = 0.6 };
            var row = new MenuFieldRow { Label = "A label long enough to run under the stars if nothing stopped it", Field = stars, Width = 600 };
            var other = new Button { Content = "elsewhere" };
            ToolWindow window = Show(new StackPanel { Children = { row, other } }, 600, 120);
            Assert.Equal(592, stars.Bounds.Right, 0.5);
            Assert.Equal(row.Row.Layout(row.Bounds.Size).Bar.Center.Y, stars.Bounds.Center.Y, 0.5);
            Assert.True(row.Row.Layout(row.Bounds.Size).Label.Right < stars.Bounds.Left);

            stars.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(row.Row.IsHighlighted);
            // The stars are drawn in the colours given, three of five filled.
            RenderedFrame f = Frame(window);
            Rect filled = new(stars.Bounds.Left, stars.Bounds.Top, stars.Bounds.Width * 0.6, stars.Bounds.Height);
            Rect empty = new(stars.Bounds.Left + stars.Bounds.Width * 0.6, stars.Bounds.Top, stars.Bounds.Width * 0.4, stars.Bounds.Height);
            Assert.True(CountNear(f, filled, Colors.Lime) > 100 && CountNear(f, filled, Colors.Blue) == 0);
            Assert.True(CountNear(f, empty, Colors.Blue) > 50 && CountNear(f, empty, Colors.Lime) == 0);
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.False(row.Row.IsHighlighted);
            window.Close();
        });

        [Fact]
        public Task A_date_drawn_unframed_in_a_font_file_has_no_border_and_its_text_in_its_colour() => UiTest.Run(() =>
        {
            var date = new DateStepper { Value = new DateTime(1990, 3, 1), FontPath = Font("Inter-Regular"), FontSize = 30, IsFramed = false, ForegroundColor = Colors.Red };
            ToolWindow window = Show(new StackPanel { Children = { date }, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left }, 400, 80);
            RenderedFrame f = Frame(window);
            Assert.True(CountNear(f, date.Bounds, Colors.Red) > 50, "no text in its colour");
            // No border: the control's edge is the window's black.
            Assert.True(Near(At(f, date.Bounds.Left + 1, date.Bounds.Center.Y), Colors.Black));
            date.IsFramed = true;
            RenderedFrame framed = Frame(window);
            Assert.False(Near(At(framed, date.Bounds.Left + 1, date.Bounds.Center.Y), Colors.Black));
            window.Close();
        });

        [Fact]
        public Task A_subtitle_deepens_the_title_band_and_buttons_sit_in_a_band_of_their_own_under_the_rows() => UiTest.Run(() =>
        {
            var rows = new StackPanel { Children = { new MenuRow { Label = "One" }, new MenuRow { Label = "Two" } } };
            var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { MenuRows.ApplyButton(new Button { Content = "Save" }) } };
            var panel = new MenuPanel { Title = "Edit Metadata", Subtitle = "Aurora Drift\nAurora Drift.sfc", Child = rows, Buttons = buttons, FooterMaxLines = 2, FooterSize = 22 };
            ToolWindow window = Show(panel, 1280, 800);
            Assert.Equal(154, panel.TitleBounds.Height, 0.5);
            Assert.Equal(panel.TitleBounds.Bottom, rows.Bounds.Top, 0.5);
            Assert.Equal(rows.Bounds.Bottom, panel.ButtonsBounds.Top, 0.5);
            Rect save = new(buttons.TranslatePoint(default, panel)!.Value, buttons.Bounds.Size);
            Assert.True(panel.ButtonsBounds.Contains(save), $"{save} outside {panel.ButtonsBounds}");
            Assert.Equal(panel.PanelBounds.Center.X, save.Center.X, 0.5);
            // The footer's band is two lines tall whether or not it holds any.
            Assert.Equal(2 * 22 * 1.25 + 28, panel.PanelBounds.Bottom - panel.ButtonsBounds.Bottom, 0.5);

            panel.Subtitle = null;
            panel.Title = null;
            panel.ShowsTitleBand = false;
            window.UpdateLayout();
            Assert.Equal(0, panel.TitleBounds.Height);
            Assert.Equal(panel.PanelBounds.Top, rows.Bounds.Top, 0.5);
            window.Close();
        });

        [Fact]
        public Task A_message_box_over_a_menu_leaves_the_menu_drawn_out_of_reach_and_its_help_bar_hidden_and_answers_true_for_its_accepting_button() => UiTest.Run(() =>
        {
            var layer = new SheetLayer { PresentsWindows = true };
            var host = new ToolWindow { Width = 1280, Height = 800, Content = new Grid { Children = { new Border(), layer } } };
            host.Show();
            var under = new MenuPanel { Title = "Edit Metadata", Hints = [new HintEntry("Scrape")], Child = new Button { Content = "Row" } };
            var menu = new ToolWindow { Content = under };
            SheetLayer.SetChromeless(menu, true);
            _ = SheetLayer.Show(menu, host);
            host.UpdateLayout();

            Task<bool> answer = Dialogs.MenuConfirmAsync(host, "Hide this game?", "Hide", "Cancel", PadFamily.Generic, [new HintEntry("Select")]);
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Control menuRoot = layer.SheetOf(menu)!;
            Window box = layer.Current!;
            Assert.True(SheetLayer.GetChromeless(box));
            Assert.True(menuRoot.IsVisible);
            Assert.False(menuRoot.IsHitTestVisible);
            Assert.True(SheetLayer.GetIsCovered(under));
            Assert.Equal(0, under.HelpBar.Opacity);
            MenuPanel boxPanel = layer.SheetOf(box)!.GetVisualDescendants().OfType<MenuPanel>().Single();
            Assert.True(boxPanel.PanelBounds.Width < under.PanelBounds.Width);
            Assert.Equal(0, boxPanel.TitleBounds.Height);
            Button accept = layer.SheetOf(box)!.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Hide");
            Assert.True(accept.IsFocused);

            accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(answer.IsCompleted && answer.Result);
            Assert.True(menuRoot.IsHitTestVisible);
            Assert.False(SheetLayer.GetIsCovered(under));
            Assert.Equal(1, under.HelpBar.Opacity);

            // A plain sheet over the menu still takes the whole layer, as before.
            var plain = new ToolWindow { Title = "Settings", Content = new Border() };
            _ = SheetLayer.Show(plain, host);
            host.UpdateLayout();
            Assert.False(menuRoot.IsVisible);
            plain.Close();
            menu.Close();
            host.Close();
        });
    }
}
