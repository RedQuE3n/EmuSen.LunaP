using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Controls
{
    /// <summary>Whether a path picker asks the platform for a file or for a directory.</summary>
    public enum PathPickerMode
    {
        /// <summary>Browse for a folder.</summary>
        Folder,
        /// <summary>Browse for an existing file.</summary>
        OpenFile,
    }

    // A read-only path box and a Browse... button, which is all four of the frontends' picker rows were - see docs/LunaP.md §5.4.
    /// <summary>A read-only path box with a Browse button that opens the platform picker.</summary>
    public class PathPickerRow : TemplatedControl
    {
        public static readonly StyledProperty<string> PathProperty =
            AvaloniaProperty.Register<PathPickerRow, string>(nameof(Path), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<PathPickerRow, string>(nameof(Placeholder), string.Empty);

        public static readonly StyledProperty<string> BrowseTitleProperty =
            AvaloniaProperty.Register<PathPickerRow, string>(nameof(BrowseTitle), "Choose");

        public static readonly StyledProperty<PathPickerMode> ModeProperty =
            AvaloniaProperty.Register<PathPickerRow, PathPickerMode>(nameof(Mode));

        public static readonly StyledProperty<bool> IsEditableProperty =
            AvaloniaProperty.Register<PathPickerRow, bool>(nameof(IsEditable));

        public static readonly StyledProperty<bool> IsClearableProperty =
            AvaloniaProperty.Register<PathPickerRow, bool>(nameof(IsClearable));

        private Button? _browse;
        private Button? _clear;
        private TextBox? _box;

        // Blank means unset to the consumer, so a blank box is a pick of the empty string - see docs/LunaP.md §201.
        /// <summary>Whether the path can be returned to blank: a Clear button is shown, enabled while there is a path, and a blank box committed in an editable row is picked as the empty string. False by default.</summary>
        public bool IsClearable
        {
            get => GetValue(IsClearableProperty);
            set => SetValue(IsClearableProperty, value);
        }

        /// <summary>Whether a path can be typed into the box as well as picked, committed on Enter or on leaving the box. False by default, which keeps the box read-only.</summary>
        public bool IsEditable
        {
            get => GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        // Raised only when something is actually picked, never on a cancel.
        /// <summary>Raised when a path is chosen through the browse button or typed into an editable box, with the new path. Not raised when Path is set in code.</summary>
        public event Action<string>? PathPicked;

        /// <summary>The current path. Setting it does not raise PathPicked.</summary>
        public string Path
        {
            get => GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        // Shown when Path is empty - "(not set)" or "(default)", the caller's wording.
        /// <summary>The grey text shown while the box is empty.</summary>
        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        /// <summary>The title of the picker dialog the browse button opens.</summary>
        public string BrowseTitle
        {
            get => GetValue(BrowseTitleProperty);
            set => SetValue(BrowseTitleProperty, value);
        }

        /// <summary>Whether the browse button asks for a folder or an existing file.</summary>
        public PathPickerMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        // BrowseTitle is doing double duty now, and it is the right property for it: it already
        // holds the one thing that distinguishes one picker row from another - "Choose a save
        // folder" rather than "Choose an export folder" - and a settings page full of these had no
        // other way to tell them apart. The template hangs the path box's name and the button's
        // help text off it; see docs/LunaP.md §24.2 for why the button's NAME stays "Browse...".
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group, name: () => BrowseTitle);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_browse is not null) _browse.Click -= OnBrowseClick;
            _browse = e.NameScope.Find<Button>("PART_Browse");
            if (_browse is not null) _browse.Click += OnBrowseClick;

            if (_clear is not null) _clear.Click -= OnClearClick;
            _clear = e.NameScope.Find<Button>("PART_Clear");
            if (_clear is not null) _clear.Click += OnClearClick;
            ShowClear();

            if (_box is not null)
            {
                _box.LostFocus -= OnBoxLeft;
                _box.KeyDown -= OnBoxKey;
            }
            _box = e.NameScope.Find<TextBox>("PART_Path");
            if (_box is not null)
            {
                _box.LostFocus += OnBoxLeft;
                _box.KeyDown += OnBoxKey;
            }
        }

        private void OnBoxLeft(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CommitTyped();

        private void OnBoxKey(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter) CommitTyped();
        }

        // A typed path is a pick like any other; unchanged text is not, nor blank text unless the row is clearable, and the box goes back to the path.
        private void CommitTyped()
        {
            if (!IsEditable || _box is null) return;

            string typed = (_box.Text ?? string.Empty).Trim();
            if ((typed.Length == 0 && !IsClearable) || typed == Path)
            {
                _box.Text = Path;
                return;
            }

            Path = typed;
            PathPicked?.Invoke(typed);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PathProperty || change.Property == IsClearableProperty) ShowClear();
        }

        private void ShowClear()
        {
            if (_clear is null) return;
            _clear.IsVisible = IsClearable;
            _clear.IsEnabled = !string.IsNullOrEmpty(Path);
        }

        private void OnClearClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (Path.Length == 0) return;
            // A disabled button drops the focus, so a pad or keyboard user lands on Browse... instead of nowhere.
            if (_clear?.IsFocused == true) _browse?.Focus(Avalonia.Input.NavigationMethod.Directional);
            Path = string.Empty;
            PathPicked?.Invoke(string.Empty);
        }

        private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string? picked = Mode switch
            {
                PathPickerMode.OpenFile => (await Dialogs.PickFileAsync(this, BrowseTitle, startIn: Path))?.Path,
                _ => await Dialogs.PickFolderAsync(this, BrowseTitle, Path),
            };

            if (picked is null) return;

            Path = picked;
            PathPicked?.Invoke(picked);
        }
    }
}
