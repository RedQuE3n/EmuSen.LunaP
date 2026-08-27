using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Windowing
{
    // The small modal behind Dialogs.ConfirmAsync/ErrorAsync/MessageAsync, built from the kit rather
    // than a hand-laid grid - see docs/LunaP.md §8.4.
    //
    // IT WAS CALLED MessageWindow UNTIL §84.3, AND THE RENAME IS THE WHOLE OF WHAT CHANGED HERE.
    // LunaPY has a public MessageWindow that is a different control entirely - non-modal, scrollable,
    // selectable, for output too long to be a dialog - so one name meant two unrelated things across
    // the two halves of one toolkit. A consumer porting from the Python side would grep a clone, find
    // this file, and conclude the capability was here. The Python meaning is the one with a public
    // API behind it, so it kept the name and this took a new one; being `internal` is what made that
    // free, since no consumer could have been holding the old spelling.
    internal sealed class DialogWindow : ToolWindow
    {
        private DialogWindow(string title, string message, string acceptText, string? cancelText)
        {
            Title = title;
            SizeToContent = SizeToContent.Height;
            Width = 420;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ClosesOnEscape = true;

            var accept = new Button { Content = acceptText, IsDefault = true };
            accept.Click += (_, _) => Close(true);

            var buttons = new ButtonBar();
            if (cancelText is not null)
            {
                var cancel = new Button { Content = cancelText, IsCancel = true };
                cancel.Click += (_, _) => Close(false);
                buttons.ItemsSource = new[] { accept, cancel };
            }
            else
            {
                buttons.ItemsSource = new[] { accept };
            }

            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new ContentControl { Content = buttons, HorizontalAlignment = HorizontalAlignment.Right },
                },
            };
        }

        // Escape and the window's own close button both mean "no".
        public static DialogWindow Confirm(string title, string message, string acceptText, string cancelText) =>
            new(title, message, acceptText, cancelText);

        // One button. Both ErrorAsync and MessageAsync are this with a different title, which is why
        // adding the second cost a method and no mechanism at all.
        public static DialogWindow Notice(string title, string message, string acceptText) =>
            new(title, message, acceptText, null);
    }
}
