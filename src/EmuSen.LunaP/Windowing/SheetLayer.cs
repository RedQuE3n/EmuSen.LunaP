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
using EmuSen.LunaP.Controls;

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

        /// <summary>Set on a chromeless sheet while another chromeless sheet, such as a message box, is presented over it; inherited, so a menu in it can put its help bar away.</summary>
        public static readonly AttachedProperty<bool> IsCoveredProperty =
            AvaloniaProperty.RegisterAttached<SheetLayer, Control, bool>("IsCovered", inherits: true);

        /// <summary>Reads whether an element's sheet is covered by a chromeless sheet presented over it.</summary>
        /// <param name="element">Any element on a sheet.</param>
        /// <returns>True while its sheet is drawn beneath another.</returns>
        public static bool GetIsCovered(Control element) => element.GetValue(IsCoveredProperty);

        /// <summary>Set on a chromeless window, such as a message box, whose sheet leaves the chromeless sheet beneath it drawn, out of reach; without it a chromeless sheet over another hides it, as a submenu replaces a menu - see docs/LunaP.md §182.5.</summary>
        public static readonly AttachedProperty<bool> KeepsBeneathDrawnProperty =
            AvaloniaProperty.RegisterAttached<SheetLayer, Window, bool>("KeepsBeneathDrawn");

        /// <summary>Reads whether a window's sheet leaves the chromeless sheet beneath it drawn.</summary>
        /// <param name="window">The window presented, or to be.</param>
        /// <returns>True for a box drawn over a menu.</returns>
        public static bool GetKeepsBeneathDrawn(Window window) => window.GetValue(KeepsBeneathDrawnProperty);

        /// <summary>Sets whether a window's sheet leaves the chromeless sheet beneath it drawn. Set it before the window is presented.</summary>
        /// <param name="window">The window to be presented.</param>
        /// <param name="value">True for a box drawn over a menu.</param>
        public static void SetKeepsBeneathDrawn(Window window, bool value) => window.SetValue(KeepsBeneathDrawnProperty, value);

        /// <summary>Whether a window's content is presented as it is, filling the layer, for content that draws its own chrome - see docs/LunaP.md §181.5.</summary>
        public static readonly AttachedProperty<bool> ChromelessProperty =
            AvaloniaProperty.RegisterAttached<SheetLayer, Window, bool>("Chromeless");

        /// <summary>Whether a sheet that does not draw its own chrome is framed as a big-screen menu, its content in the menu's look - see docs/LunaP.md §196.</summary>
        public static readonly StyledProperty<bool> MenuLookProperty =
            AvaloniaProperty.Register<SheetLayer, bool>(nameof(MenuLook));

        /// <summary>The pad family whose buttons a menu-framed sheet's help bar draws.</summary>
        public static readonly StyledProperty<PadFamily> HintFamilyProperty =
            AvaloniaProperty.Register<SheetLayer, PadFamily>(nameof(HintFamily));

        /// <summary>Which windows a layer with MenuLook frames as menus; null frames every one. A host keeps a window in the plain frame by answering false, such as one whose own look is still to come.</summary>
        public Func<Window, bool>? MenuFrameFor { get; set; }

        /// <summary>The help bar of a menu-framed sheet whose window names none of its own (MenuLook.Hints); null shows none.</summary>
        public Func<Window, IReadOnlyList<HintEntry>?>? MenuHintsFor { get; set; }

        private readonly List<Sheet> _sheets = new();

        // What had the focus before the first sheet, given back when the last one goes.
        private IInputElement? _focusBefore;

        // The window this layer is in while it presents anything; its Closed closes the sheets, as an owner closes its owned windows - see docs/LunaP.md §90.7.
        private Window? _host;

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

        /// <summary>Whether sheets presented from now on that do not draw their own chrome are framed as a big-screen menu. False by default.</summary>
        public bool MenuLook
        {
            get => GetValue(MenuLookProperty);
            set => SetValue(MenuLookProperty, value);
        }

        /// <summary>The pad family a menu-framed sheet's help bar draws.</summary>
        public PadFamily HintFamily
        {
            get => GetValue(HintFamilyProperty);
            set => SetValue(HintFamilyProperty, value);
        }

        /// <summary>Whether a presented window draws a menu of its own over what is behind it: chromeless, or framed as a menu by this layer.</summary>
        /// <param name="window">A window presented on this layer.</param>
        /// <returns>True for a menu, false for a plain sheet or a window not presented here.</returns>
        public bool DrawsMenu(Window window) => _sheets.FirstOrDefault(s => ReferenceEquals(s.Window, window)) is { } sheet && sheet.OwnChrome;

        /// <summary>A line shown under every sheet, such as which buttons do what. Null shows none.</summary>
        public string? Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        /// <summary>Reads whether a window is presented without a sheet's chrome.</summary>
        /// <param name="window">The window to ask about.</param>
        /// <returns>True when its content fills the layer as it is.</returns>
        public static bool GetChromeless(Window window) => window.GetValue(ChromelessProperty);

        /// <summary>Presents a window's content as it is, filling the layer: no title, hint line, surface, width cap or scale, and no fill behind it. Set before the window is presented.</summary>
        /// <param name="window">A window not yet presented.</param>
        /// <param name="value">True for content that draws its own chrome.</param>
        public static void SetChromeless(Window window, bool value) => window.SetValue(ChromelessProperty, value);

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

            if (_sheets.Count == 0)
            {
                _focusBefore = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                _host = TopLevel.GetTopLevel(this) as Window;
                if (_host is not null) _host.Closed += OnHostClosed;
            }

            var sheet = new Sheet(this, window);
            _sheets.Add(sheet);
            Presenters[window] = this;
            Children.Add(sheet.Root);
            sheet.RefreshHints();
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
            // A message box over a menu leaves the menu drawn and out of reach; any other sheet replaces what is beneath - §182.5.
            int at = _sheets.IndexOf(shown);
            Sheet? beneath = shown.Chromeless && GetKeepsBeneathDrawn(shown.Window) && at > 0 && _sheets[at - 1].OwnChrome ? _sheets[at - 1] : null;
            foreach (Sheet other in _sheets)
            {
                other.Root.IsVisible = ReferenceEquals(other, shown) || ReferenceEquals(other, beneath);
                other.Root.IsHitTestVisible = ReferenceEquals(other, shown);
                other.Root.SetValue(IsCoveredProperty, ReferenceEquals(other, beneath));
            }
            // A chromeless sheet's content draws what is behind it itself.
            if (shown.OwnChrome) Background = null;
            else this[!BackgroundProperty] = new DynamicResourceExtension("LunaHudSurface");
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
                if (_host is not null) _host.Closed -= OnHostClosed;
                _host = null;
            }

            sheet.Done.TrySetResult();
            PresentedChanged?.Invoke();
        }

        // Newest first, as each sheet's own close takes it off the layer.
        private void OnHostClosed(object? sender, EventArgs e)
        {
            foreach (Sheet sheet in Enumerable.Reverse(_sheets.ToList())) sheet.Window.Close();
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
            private readonly LayoutTransformControl? _scaler;
            private readonly TextBlock? _hint;

            public Window Window { get; }
            public Border Root { get; }
            public bool Chromeless { get; }
            public bool MenuFramed { get; }
            public bool OwnChrome => Chromeless || MenuFramed;
            public TaskCompletionSource Done { get; } = new();

            public Sheet(SheetLayer layer, Window window)
            {
                _layer = layer;
                Window = window;

                object? content = window.Content;
                window.Content = null;
                _host = new ContentControl { Content = content, DataContext = window.DataContext };
                Chromeless = GetChromeless(window);

                if (Chromeless)
                {
                    Root = new Border { Child = _host, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                    _host.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    _host.VerticalContentAlignment = VerticalAlignment.Stretch;
                    KeyboardNavigation.SetTabNavigation(Root, KeyboardNavigationMode.Cycle);
                    EmbeddedPopups.SetIsEnabled(Root, true);
                    Root.KeyDown += OnKeyDown;
                    return;
                }

                if (layer.MenuLook && layer.MenuFrameFor?.Invoke(window) != false)
                {
                    MenuFramed = true;
                    Root = MenuFrame(layer, window);
                    KeyboardNavigation.SetTabNavigation(Root, KeyboardNavigationMode.Cycle);
                    EmbeddedPopups.SetIsEnabled(Root, true);
                    Root.KeyDown += OnKeyDown;
                    return;
                }

                var title = new TextBlock { FontSize = 20, FontWeight = FontWeight.SemiBold, Margin = new Thickness(16, 12, 16, 4) };
                title[!TextBlock.TextProperty] = window[!Window.TitleProperty];

                _hint = new TextBlock { Margin = new Thickness(16, 4, 16, 10), TextWrapping = TextWrapping.Wrap };
                _hint[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");

                DockPanel.SetDock(title, Dock.Top);
                DockPanel.SetDock(_hint, Dock.Bottom);
                var dock = new DockPanel { LastChildFill = true };
                dock.Children.Add(title);
                dock.Children.Add(_hint);
                dock.Children.Add(_host);

                // A window laid out for a desk keeps its width, centred, rather than stretching across a television.
                if (double.IsFinite(window.Width) && window.Width > 0) dock.MaxWidth = window.Width;

                _scaler = new LayoutTransformControl { Child = dock };

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

                // A session that shows one window shows no popup window either - §92.5.
                EmbeddedPopups.SetIsEnabled(Root, true);
                Root.KeyDown += OnKeyDown;
                Restyle();
            }

            // The window's content in the menu's look, scaled as the menus are, in a menu panel titled by the window, with the help bar under it - see docs/LunaP.md §196.
            private Border MenuFrame(SheetLayer layer, Window window)
            {
                _host.Padding = new Thickness(MenuSidePadding, MenuTopPadding, MenuSidePadding, 0);
                _host.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                _host.VerticalContentAlignment = VerticalAlignment.Stretch;
                // Avalonia 12.1 shrinks a list's effective viewport by the scale again for a clipping child of a scaled control, so only the scaler clips - §196.4.
                _host.ClipToBounds = false;
                Controls.MenuLook.SetIsOn(_host, true);

                var scaled = new LayoutTransformControl { Child = _host };
                scaled.Bind(LayoutTransformControl.LayoutTransformProperty, scaled.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(u, u)));

                double fraction = Controls.MenuLook.GetWidthFraction(window);
                var panel = new MenuPanel { Name = "SheetMenu", RowPitch = 1, Child = scaled };
                if (double.IsFinite(fraction) && fraction > 0)
                {
                    panel.WidthFraction = fraction;
                    panel.MaxWidthToHeight = fraction * 1.75;
                }
                panel[!MenuPanel.TitleProperty] = window[!Window.TitleProperty];
                panel.FooterSize = MenuFooterSize;
                panel.TitleMinScale = MenuTitleMinScale;
                panel.TitleMaxLines = MenuTitleMaxLines;
                // A window's footer is its own words, a path or an attribution among them, so it keeps their casing.
                panel.FooterLetterCase = Media.LetterCase.None;
                panel.Bind(MenuPanel.FooterProperty, window.GetObservable(Controls.MenuLook.FooterProperty, f => f?.ReplaceLineEndings(" ")));
                void FooterLines() => panel.FooterMaxLines = string.IsNullOrEmpty(Controls.MenuLook.GetFooter(window)) ? 1 : Math.Max(2, Controls.MenuLook.GetFooterLines(window));
                FooterLines();
                window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Controls.MenuLook.FooterProperty || e.Property == Controls.MenuLook.FooterLinesProperty) FooterLines();
                };
                panel[!MenuPanel.HintFamilyProperty] = layer[!HintFamilyProperty];
                _menu = panel;
                window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Controls.MenuLook.HintsProperty) RefreshHints();
                };
                return new Border { Child = panel, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            }

            private MenuPanel? _menu;

            // The window's own help bar, else the layer's for it, read once the sheet is on the layer so the layer's can look at its content.
            public void RefreshHints()
            {
                if (_menu is not null) _menu.Hints = Controls.MenuLook.GetHints(Window) ?? _layer.MenuHintsFor?.Invoke(Window);
            }

            // The content's inset inside a menu frame, in design pixels before the menu's scale.
            private const double MenuSidePadding = 24, MenuTopPadding = 12;

            // A window's footer in a menu frame: its size in design pixels, and the lines kept for it.
            private const double MenuFooterSize = 20;

            // A window's name may be long, a screenshot's or a theme's: it shrinks to two thirds before it is cut.
            private const double MenuTitleMinScale = 0.66;

            // One too long for a line at that size wraps onto a second, never cut (§196.11).
            private const int MenuTitleMaxLines = 2;

            public void Restyle()
            {
                if (_scaler is null || _hint is null) return;
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

            // The default button if there is one, as a dialog starts, else the control nearest the top left that is not a tab header - §90.1.
            public void FocusFirst()
            {
                if (TopLevel.GetTopLevel(Root) is not { } top) return;
                top.UpdateLayout();

                var candidates = Root.GetVisualDescendants().OfType<InputElement>()
                    .Where(e => e.Focusable && e.IsEffectivelyEnabled && e.IsEffectivelyVisible && e is not ScrollViewer)
                    .Select(e => (Element: e, At: e.TranslatePoint(default, Root) ?? default))
                    .OrderBy(c => c.Element is TabItem).ThenBy(c => Math.Round(c.At.Y)).ThenBy(c => c.At.X)
                    .Select(c => c.Element)
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
