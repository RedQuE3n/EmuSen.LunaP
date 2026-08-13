using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // A FIELD THAT IS WRONG, AND SAYS SO - see docs/LunaP.md §49.
    //
    // This toolkit had no error state anywhere until §49, which is the one thing an office
    // application cannot fake: a settings window whose fields can only be right is a settings window
    // that silently keeps a bad value. §48.5 named it as the piece left open when the form controls
    // were themed.
    //
    // The assertions here are about the two things that are easy to get wrong and impossible to see
    // afterwards. The MESSAGE IS THE STATE - there is no separate IsValid flag that could disagree
    // with the text - and the error must not destroy the hint, because the hint is the explanation
    // and an invalid field is exactly when somebody needs it.
    public class ValidationTests
    {
        private static FieldRow Shown(out Window window, string error = "")
        {
            var box = new TextBox { Text = "/not/a/real/path" };
            var row = new FieldRow
            {
                Label = "ROM Directory",
                Hint = "Default folder for Open ROM... and the ROM list.",
                Error = error,
                Content = box,
            };

            window = new ToolWindow { Width = 420, Height = 240, Content = row };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return row;
        }

        [Fact]
        public Task A_field_with_no_error_shows_no_message() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window);

            Assert.False(row.HasError);
            Assert.False(row.FindNamed<ErrorText>("PART_Error").IsVisible);

            window.Close();
        });

        [Fact]
        public Task A_field_given_an_error_shows_it() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window, "That folder does not exist.");

            Assert.True(row.HasError);
            ErrorText message = row.FindNamed<ErrorText>("PART_Error");
            Assert.True(message.IsVisible);
            Assert.Equal("That folder does not exist.", message.Text);

            window.Close();
        });

        // THE MESSAGE IS THE STATE. Clearing the text is the only way to become valid again, so the
        // two can never disagree - a control carrying both a bool and a string would eventually be
        // set invalid with an empty message, and the user would be blocked by a blank line.
        [Fact]
        public Task Clearing_the_message_is_what_makes_the_field_valid_again() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window, "That folder does not exist.");
            Assert.True(row.HasError);

            row.Error = string.Empty;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(row.HasError);
            Assert.False(row.FindNamed<ErrorText>("PART_Error").IsVisible);

            window.Close();
        });

        // The hint is advice and is true whether or not anything is wrong. Folding the error into
        // HelpText would have destroyed the explanation at the moment it is most useful, which is
        // why the error goes to ItemStatus instead - see the comment on FieldRow.OnCreateAutomationPeer
        // for the enumeration that showed Avalonia has no "invalid" state at all.
        [Fact]
        public Task An_invalid_field_announces_the_error_without_losing_its_hint() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window, "That folder does not exist.");

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(row);

            Assert.Equal("ROM Directory", peer.GetName());
            Assert.Equal("That folder does not exist.", peer.GetItemStatus());
            Assert.Equal("Default folder for Open ROM... and the ROM list.", peer.GetHelpText());

            window.Close();
        });

        [Fact]
        public Task A_valid_field_announces_no_status() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window);

            Assert.True(string.IsNullOrEmpty(ControlAutomationPeer.CreatePeerForElement(row).GetItemStatus()));

            window.Close();
        });

        // THE LAYOUT CLAIM, ASSERTED RATHER THAN WRITTEN DOWN. The message sits below the field so
        // that a field going invalid does not shove the control the user is typing in downwards. A
        // template edit that moved it above would be invisible to every other test here.
        [Fact]
        public Task The_message_sits_below_the_field_it_is_about() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window, "That folder does not exist.");

            ErrorText message = row.FindNamed<ErrorText>("PART_Error");
            ContentPresenter content = row.FindPart<ContentPresenter>()!;

            Point messageTop = message.TranslatePoint(default, row)!.Value;
            Point contentTop = content.TranslatePoint(default, row)!.Value;

            Assert.True(messageTop.Y > contentTop.Y,
                $"The error message is at y={messageTop.Y} and the field at y={contentTop.Y}, so the "
                + "message is above the control it is about - which shoves that control down the "
                + "page at the moment the user is told they got it wrong. See docs/LunaP.md §49.");

            window.Close();
        });

        // The colour IS the signal that this is an error rather than a hint, so it is worth an
        // assertion: an ErrorText that resolved to the muted grey would read as advice.
        [Fact]
        public Task The_message_is_painted_in_the_error_colour() => UiTest.Run(() =>
        {
            FieldRow row = Shown(out Window window, "That folder does not exist.");

            ErrorText message = row.FindNamed<ErrorText>("PART_Error");

            Assert.Equal(
                ((ISolidColorBrush)LunaPalette.Error).Color,
                ((ISolidColorBrush)message.Foreground!).Color);

            window.Close();
        });
    }
}
