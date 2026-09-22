using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Windowing
{
    // The modal behind Dialogs.PromptAsync: a sentence, one line of text, and two buttons - see
    // docs/LunaP.md §89.
    //
    // A SEPARATE WINDOW FROM DialogWindow, NOT A FLAG ON IT. DialogWindow answers a bool and this
    // answers a string, and a ShowDialog<T> is typed by its result: one class serving both would
    // have to return object and make every caller cast, which is the untyped surface §22.9 took
    // LunaList out of. What the two share is the layout, and that is four lines.
    //
    // THE ACCEPT BUTTON IS DISABLED WHILE THE TEXT IS BLANK, rather than accepting and returning
    // "". A caller asking for a name has no use for an empty one, and every caller would otherwise
    // write the same check after the await and then have nothing to do but ask again - which is
    // the dialog's job, done badly, one level up. Whitespace counts as blank for the same reason,
    // and the answer comes back trimmed: a collection called " Favourites" is a defect a user
    // cannot see.
    //
    // Escape, the close button and Cancel all answer null, which is Confirm's rule (§8.4): anything
    // that is not a deliberate answer is no answer.
    internal sealed class PromptWindow : ToolWindow
    {
        private readonly TextBox _text;
        private readonly Button _accept;

        public PromptWindow(string title, string message, string initial, string acceptText, string cancelText)
        {
            Title = title;
            SizeToContent = SizeToContent.Height;
            Width = 420;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ClosesOnEscape = true;

            _text = new TextBox { Name = "PART_Answer", Text = initial };
            Avalonia.Automation.AutomationProperties.SetName(_text, message);
            _accept = new Button { Name = "PART_Accept", Content = acceptText, IsDefault = true };
            _accept.Click += (_, _) => Accept();
            var cancel = new Button { Name = "PART_Cancel", Content = cancelText, IsCancel = true };
            cancel.Click += (_, _) => Close(null);

            _text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Sync(); };
            Sync();

            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    _text,
                    new ContentControl { Content = new ButtonBar { ItemsSource = new[] { _accept, cancel } }, HorizontalAlignment = HorizontalAlignment.Right },
                },
            };

            // Typing is the whole point of opening it, so the caret is already in the box with the old text selected.
            Opened += (_, _) =>
            {
                _text.Focus(NavigationMethod.Tab);
                _text.SelectAll();
            };
        }

        private string Answer => (_text.Text ?? string.Empty).Trim();

        private void Sync() => _accept.IsEnabled = Answer.Length > 0;

        private void Accept()
        {
            if (Answer.Length > 0) Close(Answer);
        }
    }
}
