using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Fluent;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Windowing
{
    // A body of text in a window, for output too long to be a dialog - see docs/LunaP.md §84.3.
    //
    // A MODAL IS THE WRONG SHAPE PAST A FEW LINES, and that is the whole argument. Build output, a
    // validation report and an exception trace are all things somebody reads WHILE looking at what
    // produced them: a dialog has to be dismissed before the thing it describes can be touched, it
    // scrolls badly if it scrolls at all, and it cannot be left open beside a second one for
    // comparison. Dialogs.ErrorAsync is right for a sentence and wrong for forty lines, and until
    // now forty lines had no answer here at all.
    //
    // SELECTABLE, because the first thing anybody does with an error is copy it somewhere - into a
    // bug report, a mail, a search box. A read-only TextBox would also be selectable and was
    // refused: it draws a focusable input with a caret, which invites typing into output, and it
    // takes the theme's input styling with it. SelectableTextBlock is Avalonia's own answer to
    // exactly this and is what the kit uses.
    //
    // MONOSPACE BY DEFAULT, which is a judgement about what arrives here rather than a preference.
    // Everything named above is output from something else - a compiler, a linter, a stack trace -
    // and all of it is written to line up in columns. Proportional text silently destroys that.
    // The flag exists because prose does arrive here sometimes and prose reads worse in mono.
    //
    // THIS IS THE PYTHON HALF'S CONTROL, ported rather than invented. LunaPY has had a public
    // MessageWindow since it was written, with the same argument in its docstring; LunaP had a
    // private class of the same name doing something unrelated, which is §84.3's actual finding.
    /// <summary>A window showing a body of read-only, selectable text, for output too long to belong in a dialog.</summary>
    public class MessageWindow : ToolWindow
    {
        private readonly SelectableTextBlock _body;

        /// <summary>A window for a body of output.</summary>
        /// <param name="title">The window title, which is also what a screen reader calls the text. Empty falls back to "Message", because an unnamed body of text is a region a reader cannot announce.</param>
        /// <param name="body">The text to show. Can be replaced later through Body, or grown through AppendLine.</param>
        /// <param name="monospace">Whether to use the palette's monospaced face, which is what output written to line up in columns needs. True by default.</param>
        public MessageWindow(string title = "", string body = "", bool monospace = true)
        {
            Title = title;

            // Set explicitly rather than relied on: ToolWindow leaves this false, whatever its
            // summary said before §84.4. Escape closing THIS window is right for the same reason it
            // is wrong for a main one - there is nothing here to cancel and nothing to lose.
            ClosesOnEscape = true;

            Width = 640;
            Height = 420;

            _body = new SelectableTextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12),
            };

            if (monospace) _body.FontFamily = LunaPalette.MonoFont;

            // Named for the window, because a reader landing on the text needs to know which output
            // it is - a build log and a validation report are the same shape and different things.
            _body.AccessibleName(string.IsNullOrEmpty(title) ? "Message" : title);

            Content = Ui.Scroll(_body);
        }

        /// <summary>The text on show. Replacing it discards whatever was there.</summary>
        public string Body
        {
            get => _body.Text ?? string.Empty;
            set => _body.Text = value;
        }

        // Appending rather than making the caller concatenate, because the case this exists for is
        // a process writing lines as it goes and the naive `Body += line` is quadratic over a build
        // log. It does not scroll to the end: a reader who has scrolled up to look at the first
        // error is reading, and yanking them to the bottom on the next line is the behaviour
        // ConsolePane deliberately avoids too (§5.6).
        /// <summary>Adds a line to the end of the text, without scrolling to it.</summary>
        /// <param name="line">The line to add. A newline is inserted before it unless the body is still empty.</param>
        public void AppendLine(string line)
        {
            _body.Text = _body.Text is not { Length: > 0 } existing ? line : existing + "\n" + line;
        }
    }
}
