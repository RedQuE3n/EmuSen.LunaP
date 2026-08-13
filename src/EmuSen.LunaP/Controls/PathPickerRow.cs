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

        private Button? _browse;

        // Raised only when the user actually picks something, never on a cancel.
        /// <summary>Raised when a path is chosen through the browse button, with the new path. Not raised when Path is set in code.</summary>
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
        // folder" rather than "Choose a ROM folder" - and a settings page full of these had no
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
