using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A terminal-shaped pane: scrolling output, a prompt, an input box with history recall. Knows nothing about DianaOS - see docs/LunaP.md §5.6.
    /// <summary>A terminal-shaped pane with scrolling output, a prompt, and an input box with history recall.</summary>
    public class ConsolePane : TemplatedControl
    {
        public static readonly StyledProperty<string> PromptProperty =
            AvaloniaProperty.Register<ConsolePane, string>(nameof(Prompt), string.Empty);

        // How many lines to keep. Zero means no limit, which is what this control used to do and
        // is still available for anybody who wants it; the default is a limit because an
        // unbounded one is a leak with a nice name - see docs/LunaP.md §22.6.
        public static readonly StyledProperty<int> MaxLinesProperty =
            AvaloniaProperty.Register<ConsolePane, int>(nameof(MaxLines), 5000);

        private TextBox? _input;
        private SelectableTextBlock? _output;
        private ScrollViewer? _scroll;

        // The output is held here, not in the TextBlock: callers print a welcome banner from their
        // constructor, long before a template exists.
        //
        // A list of lines rather than one accumulating string, and the reason is a defect this
        // replaces: `_text = _text + "\n" + line` allocates and copies the ENTIRE buffer on every
        // line, so printing n lines costs O(n^2) and a long-running console spends progressively
        // more time copying its own history. Appending to a list is O(1), and dropping the oldest
        // line when the cap is reached is O(1) from the front of the window kept below.
        private readonly List<string> _lines = new();

        // The joined form, rebuilt only when the lines change and reused by every read in between.
        // Setting SelectableTextBlock.Text is unavoidably O(total length) - it is one string
        // property - so the cap is what actually bounds the per-line cost, not this.
        private string? _joined;

        // -1 means "not recalling, editing whatever is live in the box" - the algorithm DianaOS's own ConsoleLineReader uses.
        private int _historyIndex = -1;
        private string _pendingInputText = "";

        // Raised on Enter, with the line as typed; the caller decides what running it means.
        /// <summary>Raised when a line is entered. The pane does not echo or interpret it - append whatever should appear.</summary>
        public event Action<string>? Submitted;

        // Oldest-first. A delegate because one caller reads a live core's history and the other its own interpreter's.
        /// <summary>Supplies the lines Up and Down walk through, newest last. Called each time recall starts, so a caller returning a live list needs no notification. Null disables recall.</summary>
        public Func<IReadOnlyList<string>>? HistorySource { get; set; }

        /// <summary>The sigil before the input, such as "&gt; ". Punctuation rather than a label, so it is not what a screen reader announces the box as.</summary>
        public string Prompt
        {
            get => GetValue(PromptProperty);
            set => SetValue(PromptProperty, value);
        }

        // Lines kept before the oldest is dropped. Zero or less means unlimited.
        /// <summary>How many output lines to keep. Older ones are dropped from the top once this is exceeded.</summary>
        public int MaxLines
        {
            get => GetValue(MaxLinesProperty);
            set => SetValue(MaxLinesProperty, value);
        }

        /// <summary>Everything currently in the output, newline separated.</summary>
        public string OutputText => _joined ??= string.Join("\n", _lines);

        // A Group over two named parts, named in the template - "Console output" and "Console
        // input". Prompt is deliberately NOT used as the input's name: it is "> " or "diana$ ",
        // which is a sigil rather than a description and reads as punctuation.
        //
        // WHAT THIS DOES NOT DO, because the honest thing is to say so rather than to appear to.
        // Output is one SelectableTextBlock holding the whole joined buffer, so there is no way to
        // announce a line as it arrives: a live region here would re-read the entire history on
        // every AppendLine, which is worse than silence. Announcing per line would mean output
        // became a list of line items, which is a different control and would give back the O(n)
        // per-line cost §22.6 removed. Recorded as a limitation in docs/LunaP.md §24.4 rather than
        // half-solved.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_input is not null) _input.KeyDown -= OnInputKeyDown;

            _input = e.NameScope.Find<TextBox>("PART_Input");
            _output = e.NameScope.Find<SelectableTextBlock>("PART_Output");
            _scroll = e.NameScope.Find<ScrollViewer>("PART_Scroll");

            if (_input is not null) _input.KeyDown += OnInputKeyDown;
            Flush();
        }

        /// <summary>Adds a line of output, scrolling to it if the view was already at the bottom.</summary>
        /// <param name="text">The line. Not interpreted: a caller echoing input adds it themselves.</param>
        public void AppendLine(string text)
        {
            // Asked BEFORE the text changes. Once the new line is in, the extent has already grown
            // and every position looks like "not at the bottom".
            bool follow = IsScrolledToBottom();

            _lines.Add(text ?? "");

            int cap = MaxLines;
            if (cap > 0 && _lines.Count > cap)
            {
                _lines.RemoveRange(0, _lines.Count - cap);
            }

            _joined = null;
            Flush();

            // ONLY IF THE READER WAS ALREADY AT THE BOTTOM. This used to scroll unconditionally,
            // which meant scrolling back through output to read something was undone by the next
            // line to arrive - on a busy console, that is every few hundred milliseconds, and it
            // makes the history effectively unreadable while anything is still printing. Following
            // the tail is the behaviour you want right up until the moment you start reading.
            if (follow) _scroll?.ScrollToEnd();
        }

        /// <summary>Empties the output. Does not touch what is typed in the input.</summary>
        public void Clear()
        {
            _lines.Clear();
            _joined = null;
            Flush();
        }

        private void Flush()
        {
            if (_output is not null) _output.Text = OutputText;
        }

        // No scroll viewer yet counts as "at the bottom", so output arriving before the template -
        // the welcome banner every console prints - ends up followed rather than stranded at the top.
        private bool IsScrolledToBottom() =>
            _scroll is null || Follows(_scroll.Offset.Y, _scroll.Extent.Height, _scroll.Viewport.Height);

        // Split out from the ScrollViewer so the rule itself is testable without a laid-out scroll
        // viewer, exactly as WindowPlacementStore.IsOnAScreen is split so it is testable without a
        // display (§8.1). That is not tidiness here, it is the only option: under Avalonia.Headless
        // a ScrollViewer reports extent == viewport however much text it holds, so ScrollToEnd is a
        // no-op and the wiring cannot be observed at all - §22.6 records the measurement.
        //
        // "Follows" as in follows the tail: true means new output should scroll into view.
        /// <summary>Whether a scroller at this position counts as being at the bottom, and so should follow new output.</summary>
        /// <param name="offsetY">Current vertical scroll offset.</param>
        /// <param name="extentHeight">Total scrollable height.</param>
        /// <param name="viewportHeight">Visible height.</param>
        /// <returns>True when it is at the bottom within a small tolerance. Public because the tolerance is the whole behaviour, and a test that cannot reach it cannot pin it.</returns>
        public static bool Follows(double offsetY, double extentHeight, double viewportHeight)
        {
            double hidden = extentHeight - viewportHeight;

            // Nothing is hidden, so there is no "away from the bottom" to be at.
            if (hidden <= 0) return true;

            // A pixel of slack: a viewport that does not divide evenly into the extent can leave
            // the offset a fraction short of the bottom while looking pinned to it.
            return offsetY >= hidden - 1.0;
        }

        /// <summary>Puts the keyboard in the input box.</summary>
        public void FocusInput() => _input?.Focus();

        // Resets recall too: after a target swap the old history is gone and a half-finished recall would point at nothing.
        /// <summary>Forgets where Up and Down had walked to, so the next Up starts at the newest line again.</summary>
        public void ResetHistoryRecall() => _historyIndex = -1;

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    string line = _input?.Text ?? "";
                    if (_input is not null) _input.Text = "";
                    _historyIndex = -1;
                    Submitted?.Invoke(line);
                    break;

                case Key.Up:
                    e.Handled = true;
                    RecallHistory(older: true);
                    break;

                case Key.Down:
                    e.Handled = true;
                    RecallHistory(older: false);
                    break;
            }
        }

        private void RecallHistory(bool older)
        {
            IReadOnlyList<string> entries = HistorySource?.Invoke() ?? Array.Empty<string>();
            if (entries.Count == 0 || _input is null) return;

            if (older)
            {
                if (_historyIndex == -1)
                {
                    _pendingInputText = _input.Text ?? "";
                    _historyIndex = entries.Count - 1;
                }
                else if (_historyIndex > 0)
                {
                    _historyIndex--;
                }
            }
            else
            {
                if (_historyIndex == -1) return;

                if (_historyIndex < entries.Count - 1)
                {
                    _historyIndex++;
                }
                else
                {
                    _historyIndex = -1;
                    SetInputText(_pendingInputText);
                    return;
                }
            }

            SetInputText(entries[_historyIndex]);
        }

        private void SetInputText(string text)
        {
            if (_input is null) return;

            _input.Text = text;
            _input.CaretIndex = text.Length;
        }
    }
}
