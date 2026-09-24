using Avalonia;

namespace EmuSen.LunaP.Windowing
{
    // Popup.ShouldUseOverlayLayer for a whole subtree, through a class the theme's rule selects - see docs/LunaP.md §92.5.
    /// <summary>Draws every popup under an element (dropdown lists, menus, tooltips) in its window's overlay layer rather than as a window of its own.</summary>
    public static class EmbeddedPopups
    {
        /// <summary>The class the element carries while this is on; Theme/Controls/EmbeddedPopups.axaml is the rule that selects it.</summary>
        public const string StyleClass = "luna-embedded-popups";

        /// <summary>True draws the popups under this element inside its window, on any platform; <see cref="LunaApp.EmbedPopups"/> does the whole application on X11.</summary>
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<StyledElement, bool>("IsEnabled", typeof(EmbeddedPopups));

        static EmbeddedPopups() =>
            IsEnabledProperty.Changed.AddClassHandler<StyledElement>((element, e) => element.Classes.Set(StyleClass, e.GetNewValue<bool>()));

        /// <summary>Whether the popups under the element are drawn inside its window.</summary>
        /// <param name="element">The element whose subtree is asked about.</param>
        /// <returns>True when the element asks for its popups inside the window.</returns>
        public static bool GetIsEnabled(StyledElement element) => element.GetValue(IsEnabledProperty);

        /// <summary>Draws the popups under the element inside its window, or stops doing so.</summary>
        /// <param name="element">The element whose subtree's popups are meant.</param>
        /// <param name="value">True to draw them inside the window.</param>
        public static void SetIsEnabled(StyledElement element, bool value) => element.SetValue(IsEnabledProperty, value);
    }
}
