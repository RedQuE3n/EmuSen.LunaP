using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // A keyboard drawn over a window and steered one key at a time, for a pad, a remote or a pointer - see docs/LunaP.md §91.
    /// <summary>An on-screen keyboard that types into a text box, moved key by key from a pad, the arrow keys or a pointer.</summary>
    public class OnScreenKeyboard : Border
    {
        private readonly IReadOnlyList<KeyboardLayout> _layouts;
        private readonly string _original;
        private readonly StackPanel _rows = new() { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBlock _preview = new() { FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 10) };
        private readonly TextBlock _hint = new() { Margin = new Thickness(4, 10, 4, 0), TextWrapping = TextWrapping.Wrap };
        private readonly List<List<Button>> _keys = new();
        private readonly TaskCompletionSource<bool> _done = new();
        private Panel? _host;
        private int _layout, _row, _column;

        /// <summary>Builds a keyboard for a text box; it types nothing until it is shown with <see cref="Show"/>.</summary>
        /// <param name="target">The text box the keys type into, at its caret.</param>
        /// <param name="layouts">The layouts, the first shown first; the Next key moves through them in order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="layouts"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="layouts"/> is empty.</exception>
        public OnScreenKeyboard(TextBox target, IReadOnlyList<KeyboardLayout> layouts)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            _layouts = layouts ?? throw new ArgumentNullException(nameof(layouts));
            if (_layouts.Count == 0) throw new ArgumentException("A keyboard needs at least one layout.", nameof(layouts));
            _original = target.Text ?? string.Empty;

            Focusable = true;
            Padding = new Thickness(16);
            CornerRadius = new CornerRadius(10, 10, 0, 0);
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Bottom;
            this[!BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            this[!BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            BorderThickness = new Thickness(1, 1, 1, 0);
            _hint[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
            AutomationProperties.SetName(this, "On-screen keyboard");

            var dock = new DockPanel();
            DockPanel.SetDock(_preview, Dock.Top);
            DockPanel.SetDock(_hint, Dock.Bottom);
            dock.Children.Add(_preview);
            dock.Children.Add(_hint);
            dock.Children.Add(_rows);
            Child = dock;

            Build();
        }

        /// <summary>The text box the keys type into.</summary>
        public TextBox Target { get; }

        /// <summary>The layout on screen now.</summary>
        public KeyboardLayout Layout => _layouts[_layout];

        /// <summary>The key the highlight is on, as the layout spells it.</summary>
        public string CurrentKey => Layout.Rows[_row][_column].Key;

        /// <summary>Whether the next letter typed is upper case. False when the keyboard opens.</summary>
        public bool Shifted { get; private set; }

        private double _keySize = 44;

        /// <summary>The width of a one-unit key, in points. 44 by default.</summary>
        public double KeySize
        {
            get => _keySize;
            set
            {
                _keySize = value;
                Build();
            }
        }

        /// <summary>A line under the keys, such as which pad buttons do what. Null shows none.</summary>
        public string? Hint
        {
            get => _hint.Text;
            set
            {
                _hint.Text = value;
                _hint.IsVisible = !string.IsNullOrEmpty(value);
            }
        }

        /// <summary>Whether the keyboard is drawn as a big-screen menu's text popup: a titled, rounded panel in the middle of the window, the text on a dark bar, and the keys as tiles in the menu's typeface, sized by the inherited menu scale. Set it before the keyboard is shown; false by default.</summary>
        public bool MenuLook { get; init; }

        /// <summary>The popup's title in the menu look, such as "Enter Name", drawn in upper case; null for none. Ignored otherwise.</summary>
        public string? Title { get; init; }

        // The menu look's design sizes before the menu scale, from the reference's text popup at 800 lines - see docs/LunaP.md §182.10.
        private const double TileWidth = 78, TileHeight = 52, TileGap = 4, TileText = 30, TitleText = 60, FieldText = 32, FieldHeight = 52, PopupPad = 20, HintText = 18;

        private static readonly Color TileColor = Color.FromRgb(0x2A, 0x2A, 0x2D), TileInk = Color.FromRgb(0x9C, 0x9C, 0xA0), CurrentColor = Color.FromRgb(0x05, 0x05, 0x07),
            CurrentInk = Color.FromRgb(0xEE, 0xEE, 0xF0), FieldColor = Color.FromRgb(0x05, 0x05, 0x07);

        private readonly FontText _field = new() { Wrap = false, TextVerticalAlignment = VerticalAlignment.Center };
        private double _keyHeight = 44, _gap = 6, _unit = 1;
        private string? _font;

        // The menu look: the panel, the title, the text on its bar and the keys as tiles, in the typeface and scale the window's menus inherit.
        private void ApplyMenuLook()
        {
            _unit = MenuPanel.GetScale(Target) is var u && double.IsFinite(u) && u > 0 ? u : 1;
            _font = MenuPanel.GetFontPath(Target);
            double k = _unit;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            CornerRadius = new CornerRadius(16 * k);
            Background = new SolidColorBrush(MenuPanel.PanelColorProperty.GetDefaultValue(typeof(MenuPanel)));
            BorderThickness = default;
            FocusAdorner = null;
            Padding = new Thickness(PopupPad * k, PopupPad * k * 0.6, PopupPad * k, PopupPad * k);
            _keySize = TileWidth * k;
            _keyHeight = TileHeight * k;
            _gap = TileGap * k;
            _rows.Spacing = _gap;

            var title = new FontText { Text = Title ?? "", FontPath = _font, FontSize = TitleText * k, LetterCase = Media.LetterCase.Upper, Wrap = false, TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(MenuPanel.TitleColorProperty.GetDefaultValue(typeof(MenuPanel))), Margin = new Thickness(0, 0, 0, 10 * k), IsVisible = !string.IsNullOrEmpty(Title) };
            _field.FontPath = _font;
            _field.FontSize = FieldText * k;
            _field.Foreground = new SolidColorBrush(CurrentInk);
            _field.Padding = new Thickness(10 * k, 0);
            var bar = new Border { Background = new SolidColorBrush(FieldColor), Height = FieldHeight * k, Child = _field, Margin = new Thickness(0, 0, 0, 10 * k) };
            _hint.FontSize = HintText * k;
            (Child as DockPanel)?.Children.Clear();
            Child = null;
            Child = new StackPanel { Children = { title, bar, _rows, _hint } };
            Build();
        }

        // A key drawn as a tile: its words centred in the menu's typeface, its fill and ink set by Refresh.
        private FuncControlTemplate<Button> TileTemplate() => new((b, _) =>
        {
            var text = new FontText { Wrap = false, FontPath = _font, FontSize = TileText * _unit, TextAlignment = TextAlignment.Center, TextVerticalAlignment = VerticalAlignment.Center };
            text.Bind(FontText.TextProperty, b.GetObservable(ContentControl.ContentProperty, c => c as string));
            text.Bind(FontText.ForegroundProperty, b.GetObservable(TemplatedControl.ForegroundProperty));
            var tile = new Border { Child = text, CornerRadius = new CornerRadius(3 * _unit) };
            tile.Bind(Border.BackgroundProperty, b.GetObservable(TemplatedControl.BackgroundProperty));
            return tile;
        });

        /// <summary>Whether the keyboard is on screen.</summary>
        public bool IsOpen => _host is not null;

        /// <summary>Completes when the keyboard closes: true for Done, false when it was cancelled and the text put back.</summary>
        public Task<bool> Closed => _done.Task;

        /// <summary>Shows a keyboard over the window holding a text box, and gives it the keyboard focus.</summary>
        /// <param name="target">The text box to type into. It must be in a window.</param>
        /// <param name="layouts">The layouts, the first shown first.</param>
        /// <param name="hint">A line under the keys, or null for none.</param>
        /// <returns>The keyboard, on screen. Await <see cref="Closed"/> for how it ended.</returns>
        /// <exception cref="InvalidOperationException">The text box is not in a window with an overlay layer.</exception>
        public static OnScreenKeyboard Show(TextBox target, IReadOnlyList<KeyboardLayout> layouts, string? hint = null)
        {
            var keyboard = new OnScreenKeyboard(target, layouts) { Hint = hint };
            keyboard.Open();
            return keyboard;
        }

        /// <summary>Shows a keyboard over the window holding a text box, drawn as a big-screen menu's text popup with a title.</summary>
        /// <param name="target">The text box to type into. It must be in a window.</param>
        /// <param name="layouts">The layouts, the first shown first.</param>
        /// <param name="hint">A line under the keys, or null for none.</param>
        /// <param name="title">The popup's title, such as "Enter Name".</param>
        /// <returns>The keyboard, on screen. Await <see cref="Closed"/> for how it ended.</returns>
        /// <exception cref="InvalidOperationException">The text box is not in a window with an overlay layer.</exception>
        public static OnScreenKeyboard ShowAsMenu(TextBox target, IReadOnlyList<KeyboardLayout> layouts, string? hint, string title)
        {
            var keyboard = new OnScreenKeyboard(target, layouts) { Hint = hint, MenuLook = true, Title = title };
            keyboard.Open();
            return keyboard;
        }

        /// <summary>The keyboard open over the window holding a visual, or null when there is none.</summary>
        /// <param name="visual">Any visual in the window, or the window itself.</param>
        /// <returns>The open keyboard, or null when no keyboard is open over that window.</returns>
        public static OnScreenKeyboard? OpenOver(Visual visual) =>
            OverlayLayer.GetOverlayLayer(visual)?.Children.OfType<Panel>()
                .SelectMany(p => p.Children.OfType<OnScreenKeyboard>()).FirstOrDefault(k => k.IsOpen);

        private void Open()
        {
            OverlayLayer layer = OverlayLayer.GetOverlayLayer(Target) ?? throw new InvalidOperationException("The text box is not in a window with an overlay layer.");

            // Transparent but hit-testable, so a pointer cannot reach the window under an open keyboard.
            _host = new Panel { Background = Brushes.Transparent, Children = { this } };
            _host.Width = layer.Bounds.Width;
            _host.Height = layer.Bounds.Height;
            if (MenuLook)
            {
                ApplyMenuLook();
                // The menu under the popup stays in view, shaded, as a message box's does.
                _host.Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0));
            }
            layer.Children.Add(_host);
            Refresh();
            Focus(NavigationMethod.Directional);
        }

        private void Build()
        {
            _rows.Children.Clear();
            _keys.Clear();
            foreach (IReadOnlyList<(string Key, int Width)> row in Layout.Rows)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _gap, HorizontalAlignment = HorizontalAlignment.Center };
                var buttons = new List<Button>();
                foreach ((string key, int width) in row)
                {
                    var button = new Button
                    {
                        Focusable = false,
                        Width = width * _keySize + (width - 1) * _gap,
                        Height = MenuLook ? _keyHeight : _keySize,
                        FontSize = 18,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Tag = key,
                    };
                    if (MenuLook) button.Template = TileTemplate();
                    string captured = key;
                    button.Click += (_, _) => Type(captured);
                    buttons.Add(button);
                    line.Children.Add(button);
                }
                _keys.Add(buttons);
                _rows.Children.Add(line);
            }

            _row = Math.Min(_row, _keys.Count - 1);
            _column = Math.Min(_column, _keys[_row].Count - 1);
            Refresh();
        }

        // Labels follow the shift, and the key under the highlight is drawn in the accent colour.
        private void Refresh()
        {
            for (int r = 0; r < _keys.Count; r++)
            {
                for (int c = 0; c < _keys[r].Count; c++)
                {
                    Button button = _keys[r][c];
                    string key = (string)button.Tag!;
                    button.Content = key == KeyboardLayout.Next ? _layouts[(_layout + 1) % _layouts.Count].Name : Shifted && key.Length == 1 ? key.ToUpperInvariant() : key;
                    bool current = r == _row && c == _column;
                    button.Classes.Set("luna-key-current", current);
                    if (MenuLook)
                    {
                        button.Background = new SolidColorBrush(current ? CurrentColor : TileColor);
                        button.Foreground = new SolidColorBrush(current ? CurrentInk : TileInk);
                        continue;
                    }
                    if (current) button[!BackgroundProperty] = new DynamicResourceExtension("LunaAccent");
                    else button.ClearValue(BackgroundProperty);
                    if (current) button[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("LunaOnAccent");
                    else button.ClearValue(TemplatedControl.ForegroundProperty);
                }
            }

            string text = Target.Text ?? string.Empty;
            // A password box's preview is drawn in its mask, so the keyboard shows no more than the box does - see docs/LunaP.md §110.
            if (Target.PasswordChar != default && !Target.RevealPassword) text = new string(Target.PasswordChar, text.Length);
            int caret = Math.Clamp(Target.CaretIndex, 0, text.Length);
            _preview.Text = text[..caret] + "|" + text[caret..];
            _field.Text = _preview.Text;
        }

        /// <summary>Moves the highlight. Across a row it wraps; between rows it goes to the key under the middle of the one it left.</summary>
        /// <param name="columns">Keys to the right, negative for the left.</param>
        /// <param name="rows">Rows down, negative for up; the highlight stops at the top and bottom rows.</param>
        public void Move(int columns, int rows)
        {
            if (rows != 0)
            {
                double middle = Middle(_row, _column);
                _row = Math.Clamp(_row + rows, 0, _keys.Count - 1);
                IReadOnlyList<(string Key, int Width)> line = Layout.Rows[_row];
                _column = Enumerable.Range(0, line.Count).OrderBy(c => Math.Abs(Middle(_row, c) - middle)).First();
            }

            if (columns != 0)
            {
                int count = _keys[_row].Count;
                _column = ((_column + columns) % count + count) % count;
            }

            Refresh();
        }

        // Where a key's middle falls, in key units from the row's left edge.
        private double Middle(int row, int column)
        {
            IReadOnlyList<(string Key, int Width)> line = Layout.Rows[row];
            double total = line.Sum(k => k.Width);
            double left = line.Take(column).Sum(k => k.Width);
            return left + line[column].Width / 2.0 - total / 2.0;
        }

        /// <summary>Does what the highlighted key does.</summary>
        public void Press() => Type(CurrentKey);

        /// <summary>Does what a key does: types it, or erases, spaces, shifts, changes layout or finishes.</summary>
        /// <param name="key">A key as a layout spells it, such as "A" or <see cref="KeyboardLayout.Erase"/>.</param>
        public void Type(string key)
        {
            switch (key)
            {
                case KeyboardLayout.Erase: Erase(); return;
                case KeyboardLayout.Space: Insert(" "); return;
                case KeyboardLayout.Shift: Shifted = !Shifted; Refresh(); return;
                case KeyboardLayout.Next: NextLayout(1); return;
                case KeyboardLayout.Done: Finish(); return;
            }

            Insert(Shifted && key.Length == 1 ? key.ToUpperInvariant() : key);
            if (Shifted && Layout.Rows.Any(r => r.Any(k => k.Key == KeyboardLayout.Shift)))
            {
                Shifted = false;
                Refresh();
            }
        }

        /// <summary>Erases the character before the caret, if there is one.</summary>
        /// <returns>False when there was nothing to erase.</returns>
        public bool Erase()
        {
            string text = Target.Text ?? string.Empty;
            int caret = Math.Clamp(Target.CaretIndex, 0, text.Length);
            if (caret == 0) return false;

            Target.Text = text.Remove(caret - 1, 1);
            Target.CaretIndex = caret - 1;
            Refresh();
            return true;
        }

        private void Insert(string characters)
        {
            string text = Target.Text ?? string.Empty;
            int caret = Math.Clamp(Target.CaretIndex, 0, text.Length);
            if (Target.MaxLength > 0 && text.Length + characters.Length > Target.MaxLength) return;

            Target.Text = text.Insert(caret, characters);
            Target.CaretIndex = caret + characters.Length;
            Refresh();
        }

        /// <summary>Shows another of the layouts the keyboard was given, wrapping at either end.</summary>
        /// <param name="by">How many layouts on, negative for back.</param>
        public void NextLayout(int by)
        {
            _layout = ((_layout + by) % _layouts.Count + _layouts.Count) % _layouts.Count;
            Build();
        }

        /// <summary>Closes the keyboard and keeps what was typed.</summary>
        public void Finish() => Close(true);

        /// <summary>Closes the keyboard and puts back the text the box had when it opened.</summary>
        public void Cancel()
        {
            Target.Text = _original;
            Target.CaretIndex = _original.Length;
            Close(false);
        }

        private void Close(bool kept)
        {
            if (_host is null) return;
            (_host.Parent as Panel)?.Children.Remove(_host);
            _host.Children.Remove(this);
            _host = null;
            Target.Focus(NavigationMethod.Directional);
            _done.TrySetResult(kept);
        }

        // The arrows move, Enter and Space press, Backspace erases, Escape cancels; a keyboard user can steer it as a pad does.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up: Move(0, -1); break;
                case Key.Down: Move(0, 1); break;
                case Key.Left: Move(-1, 0); break;
                case Key.Right: Move(1, 0); break;
                case Key.Enter: case Key.Space: Press(); break;
                case Key.Back: Erase(); break;
                case Key.Escape: Cancel(); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
        }
    }
}
