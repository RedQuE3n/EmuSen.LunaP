using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Windowing
{
    // A window's content drawn on a sheet inside another window, for a session that shows one window at a time - see docs/LunaP.md §90.
    /// <summary>Shows windows' content as sheets laid over a host window's own content, for a session that shows one window at a time.</summary>
    public class SheetLayer : Panel
    {
        public static readonly StyledProperty<bool> PresentsWindowsProperty =
            AvaloniaProperty.Register<SheetLayer, bool>(nameof(PresentsWindows));

        public static readonly StyledProperty<double> ScaleProperty =
            AvaloniaProperty.Register<SheetLayer, double>(nameof(Scale), 1.0);

        public static readonly StyledProperty<string?> HintProperty =
            AvaloniaProperty.Register<SheetLayer, string?>(nameof(Hint));

        private readonly List<Sheet> _sheets = new();

        // What had the focus before the first sheet, given back when the last one goes.
        private IInputElement? _focusBefore;

        /// <summary>Hidden until something is presented.</summary>
        public SheetLayer()
        {
            IsVisible = false;
            ClipToBounds = true;
            this[!BackgroundProperty] = new DynamicResourceExtension("LunaHudSurface");
        }

        /// <summary>Whether <see cref="Show"/> puts windows owned by this layer's window on sheets here. False by default, which shows them as ordinary windows.</summary>
        public bool PresentsWindows
        {
            get => GetValue(PresentsWindowsProperty);
            set => SetValue(PresentsWindowsProperty, value);
        }

        /// <summary>How much a sheet's content is enlarged, 1 for none. A layout scale, so text stays sharp.</summary>
        public double Scale
        {
            get => GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        /// <summary>A line shown under every sheet, such as which buttons do what. Null shows none.</summary>
        public string? Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        /// <summary>The windows presented here, oldest first. The last is the one on screen.</summary>
        public IReadOnlyList<Window> Presented => _sheets.Select(s => s.Window).ToList();

        /// <summary>The window whose sheet is on screen, or null when nothing is presented.</summary>
        public Window? Current => _sheets.Count == 0 ? null : _sheets[^1].Window;

        /// <summary>Whether any window is presented.</summary>
        public bool IsPresenting => _sheets.Count > 0;

        /// <summary>Raised after a sheet is added, brought forward or taken away.</summary>
        public event Action? PresentedChanged;

        /// <summary>The root of the sheet showing a presented window's content, or null if the window is not presented here.</summary>
        /// <param name="window">A window presented on this layer.</param>
        /// <returns>The sheet's root control, which holds the window's content, or null.</returns>
        public Control? SheetOf(Window window) => _sheets.FirstOrDefault(s => ReferenceEquals(s.Window, window))?.Root;

        /// <summary>Takes a window's content onto a sheet over this layer's host, until the window closes.</summary>
        /// <param name="window">A window that has not been shown. Its Content moves here and is let go when it closes.</param>
        /// <returns>A task that completes when the window has closed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="window"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The window is already shown, or already presented.</exception>
        public Task Present(Window window)
        {
            if (window is null) throw new ArgumentNullException(nameof(window));
            if (window.IsVisible) throw new InvalidOperationException("A shown window cannot also be presented on a sheet.");
            if (PresenterOf(window) is not null) throw new InvalidOperationException("That window is already presented.");

            if (_sheets.Count == 0) _focusBefore = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

            var sheet = new Sheet(this, window);
            _sheets.Add(sheet);
            Presenters[window] = this;
            Children.Add(sheet.Root);
            ShowOnly(sheet);
            IsVisible = true;

            window.Closed += (_, _) => Remove(sheet);
            sheet.FocusFirst();
            PresentedChanged?.Invoke();
            return sheet.Done.Task;
        }

        /// <summary>Brings a presented window's sheet back to the top. Does nothing for a window not presented here.</summary>
        /// <param name="window">The window to bring forward.</param>
        public void BringToFront(Window window)
        {
            Sheet? sheet = _sheets.FirstOrDefault(s => ReferenceEquals(s.Window, window));
            if (sheet is null) return;

            _sheets.Remove(sheet);
            _sheets.Add(sheet);
            ShowOnly(sheet);
            sheet.FocusFirst();
            PresentedChanged?.Invoke();
        }

        private void ShowOnly(Sheet shown)
        {
            foreach (Sheet other in _sheets) other.Root.IsVisible = ReferenceEquals(other, shown);
        }

        private void Remove(Sheet sheet)
        {
            if (!_sheets.Remove(sheet)) return;

            Presenters.Remove(sheet.Window);
            Children.Remove(sheet.Root);
            sheet.Release();

            if (_sheets.Count > 0)
            {
                ShowOnly(_sheets[^1]);
                _sheets[^1].FocusFirst();
            }
            else
            {
                IsVisible = false;
                if (_focusBefore is InputElement before && TopLevel.GetTopLevel(before) is not null) before.Focus();
                _focusBefore = null;
            }

            sheet.Done.TrySetResult();
            PresentedChanged?.Invoke();
        }

        // Which layer presents a window, so a presented window's own children land on the same layer.
        private static readonly Dictionary<Window, SheetLayer> Presenters = new(ReferenceEqualityComparer.Instance);

        /// <summary>The layer presenting a window, or null when it is not presented anywhere.</summary>
        /// <param name="window">The window to look up.</param>
        /// <returns>The presenting layer, or null for a window that is shown as a window or not at all.</returns>
        public static SheetLayer? PresenterOf(Window window) =>
            window is not null && Presenters.TryGetValue(window, out SheetLayer? layer) ? layer : null;

        /// <summary>The layer an owner's children would be presented on: the owner's own presenter, else a layer in the owner that presents windows, else null.</summary>
        /// <param name="owner">The window that would own the child.</param>
        /// <returns>The layer a child of this owner is presented on, or null when it would be shown as a window.</returns>
        public static SheetLayer? LayerFor(Window? owner) =>
            owner is null ? null
            : PresenterOf(owner) ?? owner.GetVisualDescendants().OfType<SheetLayer>().FirstOrDefault(l => l.PresentsWindows);

        /// <summary>Shows a window owned by another: on a sheet if the owner is presented or hosts a layer that presents windows, else as an ordinary owned window.</summary>
        /// <param name="window">The window to show.</param>
        /// <param name="owner">Its owner, or null for an ownerless window, which is always shown as a window.</param>
        /// <returns>A task that completes when the window has closed.</returns>
        public static Task Show(Window window, Window? owner)
        {
            if (LayerFor(owner) is { } layer) return layer.Present(window);

            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            if (owner is null) window.Show();
            else window.Show(owner);
            return closed.Task;
        }

        /// <summary>Shows a dialog modally over its owner, on a sheet where <see cref="Show"/> would put one, and answers what it closed with.</summary>
        /// <typeparam name="TResult">The type the dialog closes with.</typeparam>
        /// <param name="dialog">The dialog. On a sheet its answer is read from <see cref="ToolWindow.DialogResult"/>, so it must close with <see cref="ToolWindow.Close(object?)"/>.</param>
        /// <param name="owner">The window it is modal to.</param>
        /// <returns>What the dialog closed with, or the default when it closed with nothing or with another type.</returns>
        public static async Task<TResult?> ShowDialog<TResult>(ToolWindow dialog, Window owner)
        {
            if (LayerFor(owner) is not { } layer) return await dialog.ShowDialog<TResult?>(owner);

            await layer.Present(dialog);
            return dialog.DialogResult is TResult result ? result : default;
        }

        /// <summary>Brings a window forward whether it is on a sheet or on screen as a window.</summary>
        /// <param name="window">The window to bring forward.</param>
        public static void Activate(Window window)
        {
            if (PresenterOf(window) is { } layer) layer.BringToFront(window);
            else window.Activate();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ScaleProperty || change.Property == HintProperty)
            {
                foreach (Sheet sheet in _sheets) sheet.Restyle();
            }
        }

        // One presented window: the sheet it is drawn on, and its content to give back.
        private sealed class Sheet
        {
            private readonly SheetLayer _layer;
            private readonly ContentControl _host;
            private readonly DockPanel _dock;
            private readonly LayoutTransformControl _scaler;
            private readonly TextBlock _hint;

            public Window Window { get; }
            public Border Root { get; }
            public TaskCompletionSource Done { get; } = new();

            public Sheet(SheetLayer layer, Window window)
            {
                _layer = layer;
                Window = window;

                object? content = window.Content;
                window.Content = null;
                _host = new ContentControl { Content = content, DataContext = window.DataContext };

                var title = new TextBlock { FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(16, 12, 16, 4) };
                title[!TextBlock.TextProperty] = window[!Window.TitleProperty];

                _hint = new TextBlock { Margin = new Thickness(16, 4, 16, 10), TextWrapping = TextWrapping.Wrap };
                _hint[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");

                DockPanel.SetDock(title, Dock.Top);
                DockPanel.SetDock(_hint, Dock.Bottom);
                _dock = new DockPanel { LastChildFill = true };
                _dock.Children.Add(title);
                _dock.Children.Add(_hint);
                _dock.Children.Add(_host);

                // A window laid out for a desk keeps its width, centred, rather than stretching across a television.
                if (double.IsFinite(window.Width) && window.Width > 0) _dock.MaxWidth = window.Width;

                _scaler = new LayoutTransformControl { Child = _dock };

                Root = new Border
                {
                    Child = _scaler,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(24),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };
                Root[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");

                // Tab stays on the sheet: the host's controls underneath are not what anybody is working on.
                KeyboardNavigation.SetTabNavigation(Root, KeyboardNavigationMode.Cycle);
                Root.KeyDown += OnKeyDown;
                Restyle();
            }

            public void Restyle()
            {
                double scale = double.IsFinite(_layer.Scale) && _layer.Scale > 0 ? _layer.Scale : 1.0;
                _scaler.LayoutTransform = new ScaleTransform(scale, scale);
                _hint.Text = _layer.Hint;
                _hint.IsVisible = !string.IsNullOrEmpty(_layer.Hint);
            }

            // The window's own OnKeyDown no longer sees keys typed on its content, so Escape is honoured here.
            private void OnKeyDown(object? sender, KeyEventArgs e)
            {
                if (e.Handled || e.Key != Key.Escape) return;
                if (Window is not ToolWindow { ClosesOnEscape: true }) return;

                e.Handled = true;
                Window.Close();
            }

            // The default button if there is one, as a dialog starts, else the first control that takes focus.
            public void FocusFirst()
            {
                if (TopLevel.GetTopLevel(Root) is not { } top) return;
                top.UpdateLayout();

                var candidates = Root.GetVisualDescendants().OfType<InputElement>()
                    .Where(e => e.Focusable && e.IsEffectivelyEnabled && e.IsEffectivelyVisible)
                    .ToList();
                InputElement? first = candidates.OfType<Button>().FirstOrDefault(b => b.IsDefault) ?? candidates.FirstOrDefault();
                first?.Focus(NavigationMethod.Directional);
            }

            // The content is let go so the sheet does not keep it alive; the window is closed and will not show it again.
            public void Release()
            {
                Root.KeyDown -= OnKeyDown;
                _host.Content = null;
            }
        }
    }
}
