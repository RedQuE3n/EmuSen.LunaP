using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // A menu's text popup with a real text field and no keys, for a physical keyboard or the device's own on-screen keyboard - see docs/LunaP.md §195.3.
    /// <summary>A big-screen menu's text popup whose bar is a real, focused text box: typed into from a physical keyboard or a system keyboard, Enter keeps the text, Escape puts it back.</summary>
    public class MenuTextPopup : Border
    {
        // Design sizes before the menu scale, the same as the keyboard's text popup - see docs/LunaP.md §182.10.
        private const double TitleText = 60, FieldText = 32, FieldHeight = 52, PopupPad = 20, HintText = 18, PopupWidth = 820;

        private static readonly Color FieldColor = Color.FromRgb(0x05, 0x05, 0x07), FieldInk = Color.FromRgb(0xEE, 0xEE, 0xF0), HintInk = Color.FromRgb(0x86, 0x86, 0x8A);

        private readonly TaskCompletionSource<bool> _done = new();
        private readonly FontText _hint = new() { Wrap = true, TextAlignment = TextAlignment.Center };
        private Panel? _host;

        /// <summary>Builds a popup for a text box; nothing is shown until <see cref="Show"/>.</summary>
        /// <param name="target">The text box the popup edits; its text is copied into the field and written back only on Finish.</param>
        /// <param name="title">The title, such as "Enter Name", drawn in upper case; null for none.</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        public MenuTextPopup(TextBox target, string? title)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Title = title;
            Field = new TextBox
            {
                Name = "MenuTextPopupField",
                Text = target.Text ?? string.Empty,
                MaxLength = target.MaxLength,
                PasswordChar = target.PasswordChar,
                AcceptsReturn = false,
                BorderThickness = default,
                Background = new SolidColorBrush(FieldColor),
                Foreground = new SolidColorBrush(FieldInk),
                CaretBrush = new SolidColorBrush(FieldInk),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Field.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(FieldColor);
            Field.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(FieldColor);
            Field.Resources["TextControlForegroundFocused"] = new SolidColorBrush(FieldInk);
            Field.Resources["TextControlForegroundPointerOver"] = new SolidColorBrush(FieldInk);
            Field.Resources["TextControlBorderBrushFocused"] = Brushes.Transparent;
            AutomationProperties.SetName(Field, title ?? "Text");
            Focusable = false;
            // Enter and Escape are the popup's before the field sees them.
            AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        }

        /// <summary>The text box the popup edits.</summary>
        public TextBox Target { get; }

        /// <summary>The real text box on the popup's bar, which holds the keyboard focus while the popup is open.</summary>
        public TextBox Field { get; }

        /// <summary>The popup's title, as given.</summary>
        public string? Title { get; }

        /// <summary>A line under the field, such as which keys finish; null or empty shows none.</summary>
        public string? Hint
        {
            get => _hint.Text;
            set
            {
                _hint.Text = value ?? "";
                _hint.IsVisible = !string.IsNullOrEmpty(value);
            }
        }

        /// <summary>Whether the popup is on screen.</summary>
        public bool IsOpen => _host is not null;

        /// <summary>Completes when the popup closes: true when the text was kept, false when it was cancelled.</summary>
        public Task<bool> Closed => _done.Task;

        /// <summary>Shows a popup over the window holding a text box, with its field focused and its caret at the end.</summary>
        /// <param name="target">The text box to edit. It must be in a window.</param>
        /// <param name="title">The title, such as "Enter Name".</param>
        /// <param name="hint">A line under the field, or null for none.</param>
        /// <returns>The popup, on screen. Await <see cref="Closed"/> for how it ended.</returns>
        /// <exception cref="InvalidOperationException">The text box is not in a window with an overlay layer.</exception>
        public static MenuTextPopup Show(TextBox target, string? title, string? hint = null)
        {
            var popup = new MenuTextPopup(target, title) { Hint = hint };
            popup.Open();
            return popup;
        }

        /// <summary>The popup open over the window holding a visual, or null when there is none.</summary>
        /// <param name="visual">Any visual in the window, or the window itself.</param>
        /// <returns>The open popup, or null.</returns>
        public static MenuTextPopup? OpenOver(Visual visual) =>
            OverlayLayer.GetOverlayLayer(visual)?.Children.OfType<Panel>()
                .SelectMany(p => p.Children.OfType<MenuTextPopup>()).FirstOrDefault(p => p.IsOpen);

        private void Open()
        {
            OverlayLayer layer = OverlayLayer.GetOverlayLayer(Target) ?? throw new InvalidOperationException("The text box is not in a window with an overlay layer.");
            double k = MenuPanel.GetScale(Target) is var u && double.IsFinite(u) && u > 0 ? u : 1;
            // A layer not laid out yet has no size; the window's client area is what it will have.
            Size area = layer.Bounds.Width > 0 ? layer.Bounds.Size : TopLevel.GetTopLevel(Target)?.ClientSize ?? default;
            string? font = MenuPanel.GetFontPath(Target);

            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            CornerRadius = new CornerRadius(16 * k);
            Background = new SolidColorBrush(MenuPanel.PanelColorProperty.GetDefaultValue(typeof(MenuPanel)));
            Padding = new Thickness(PopupPad * k, PopupPad * k * 0.6, PopupPad * k, PopupPad * k);
            Width = Math.Min(PopupWidth * k, Math.Max(0, area.Width - 32 * k));

            var title = new FontText { Text = Title ?? "", FontPath = font, FontSize = TitleText * k, LetterCase = LetterCase.Upper, Wrap = false, TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(MenuPanel.TitleColorProperty.GetDefaultValue(typeof(MenuPanel))), Margin = new Thickness(0, 0, 0, 10 * k), IsVisible = !string.IsNullOrEmpty(Title) };
            Field.FontSize = FieldText * k * 0.8;
            Field.Height = FieldHeight * k;
            Field.Padding = new Thickness(10 * k, 0);
            Field.CornerRadius = default;
            _hint.FontPath = font;
            _hint.FontSize = HintText * k;
            _hint.Foreground = new SolidColorBrush(HintInk);
            _hint.Margin = new Thickness(0, 10 * k, 0, 0);
            Child = new StackPanel { Children = { title, Field, _hint } };

            // Transparent to nothing: the menu under the popup stays in view, shaded, and a pointer cannot reach it.
            _host = new Panel { Background = new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)), Children = { this } };
            _host.Width = area.Width;
            _host.Height = area.Height;
            layer.Children.Add(_host);
            Field.CaretIndex = Field.Text?.Length ?? 0;
            Field.Focus(NavigationMethod.Directional);
        }

        /// <summary>Closes the popup and writes the field's text to the target.</summary>
        public void Finish()
        {
            if (_host is null) return;
            Target.Text = Field.Text ?? string.Empty;
            Target.CaretIndex = Target.Text.Length;
            Close(true);
        }

        /// <summary>Closes the popup and leaves the target's text as it was.</summary>
        public void Cancel() => Close(false);

        private void Close(bool kept)
        {
            if (_host is null) return;
            (_host.Parent as Panel)?.Children.Remove(_host);
            _host.Children.Remove(this);
            _host = null;
            Target.Focus(NavigationMethod.Directional);
            _done.TrySetResult(kept);
        }

        private void OnKey(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter: Finish(); break;
                case Key.Escape: Cancel(); break;
                default: return;
            }
            e.Handled = true;
        }
    }
}
