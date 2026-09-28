using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // A big-screen menu's look for stock controls, scoped to one element and everything under it - see docs/LunaP.md §196.
    /// <summary>The look of a big-screen menu given to stock controls: its colours, a typeface a host names by resource key, rounded corners and a full-width bar on a list's chosen row, applied to one element and everything under it.</summary>
    public static partial class MenuLook
    {
        /// <summary>The style class the look puts on the element it is applied to.</summary>
        public const string ClassName = "menu-look";

        /// <summary>The resource key of the typeface the look draws its text in; a host puts a FontFamily under it, and without one the application's default typeface is used.</summary>
        public const string FontFamilyKey = "LunaMenuFontFamily";

        /// <summary>Whether an element and everything under it is drawn in the look. False by default.</summary>
        public static readonly AttachedProperty<bool> IsOnProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsOn", typeof(MenuLook));

        /// <summary>The help bar a window shows on a sheet drawn in the look; null for the layer's own.</summary>
        public static readonly AttachedProperty<IReadOnlyList<HintEntry>?> HintsProperty =
            AvaloniaProperty.RegisterAttached<Window, IReadOnlyList<HintEntry>?>("Hints", typeof(MenuLook));

        /// <summary>The share of the screen's width a window's panel takes on a sheet drawn in the look; NaN for the menu's own.</summary>
        public static readonly AttachedProperty<double> WidthFractionProperty =
            AvaloniaProperty.RegisterAttached<Window, double>("WidthFraction", typeof(MenuLook), double.NaN);

        /// <summary>Words a window shows in the footer of its panel on a sheet in the look, such as an attribution that must stay in view; null shows none.</summary>
        public static readonly AttachedProperty<string?> FooterProperty =
            AvaloniaProperty.RegisterAttached<Window, string?>("Footer", typeof(MenuLook));

        /// <summary>Reads the footer a window asks for on a sheet in the look.</summary>
        /// <param name="window">The window that may name a footer.</param>
        /// <returns>The words, or null.</returns>
        public static string? GetFooter(Window window) => window.GetValue(FooterProperty);

        /// <summary>Sets the footer a window shows on a sheet in the look, up to three lines under its content.</summary>
        /// <param name="window">The window whose footer this is.</param>
        /// <param name="value">The words, or null for none.</param>
        public static void SetFooter(Window window, string? value) => window.SetValue(FooterProperty, value);

        /// <summary>How many lines a window's footer is given on a sheet in the look. 3 by default.</summary>
        public static readonly AttachedProperty<int> FooterLinesProperty =
            AvaloniaProperty.RegisterAttached<Window, int>("FooterLines", typeof(MenuLook), 3);

        /// <summary>Reads how many lines a window's footer is given in the look.</summary>
        /// <param name="window">The window whose footer this is.</param>
        /// <returns>The lines, 3 unless the window asks for more.</returns>
        public static int GetFooterLines(Window window) => window.GetValue(FooterLinesProperty);

        /// <summary>Gives a window's footer more or fewer lines in the look, for words that must stay whole, such as a licence's attribution.</summary>
        /// <param name="window">The window whose footer this is.</param>
        /// <param name="value">The lines, at least two.</param>
        public static void SetFooterLines(Window window, int value) => window.SetValue(FooterLinesProperty, value);

        // The styles an element was given, so that turning the look off takes away exactly those.
        private static readonly AttachedProperty<Styles?> AppliedProperty =
            AvaloniaProperty.RegisterAttached<Control, Styles?>("Applied", typeof(MenuLook));

        static MenuLook()
        {
            IsOnProperty.Changed.AddClassHandler<Control>((control, e) => Update(control, e.GetNewValue<bool>()));
            // An open dropdown's item taking the focus asks the page around the dropdown to scroll to it; under the look the page stays where it is (§196.9).
            Control.RequestBringIntoViewEvent.AddClassHandler<ComboBox>((combo, e) =>
            {
                if (!ReferenceEquals(e.Source, combo) && combo.IsDropDownOpen && Covers(combo)) e.Handled = true;
            });
            SetUpEdges();
        }

        /// <summary>Reads whether an element is drawn in the look.</summary>
        /// <param name="element">The element.</param>
        /// <returns>True when the look was applied to it.</returns>
        public static bool GetIsOn(Control element) => element.GetValue(IsOnProperty);

        /// <summary>Draws an element and everything under it in the look, or gives it back its own.</summary>
        /// <param name="element">The element, such as a sheet's content or a window's.</param>
        /// <param name="value">True for the look.</param>
        public static void SetIsOn(Control element, bool value) => element.SetValue(IsOnProperty, value);

        /// <summary>Reads the help bar a window asks for on a sheet in the look.</summary>
        /// <param name="window">The window that may name its own help.</param>
        /// <returns>Its entries, or null for the layer's.</returns>
        public static IReadOnlyList<HintEntry>? GetHints(Window window) => window.GetValue(HintsProperty);

        /// <summary>Sets the help bar a window shows on a sheet in the look. Set it before the window is presented, or again to change it.</summary>
        /// <param name="window">The window whose help bar this is.</param>
        /// <param name="value">Its entries, or null for the layer's.</param>
        public static void SetHints(Window window, IReadOnlyList<HintEntry>? value) => window.SetValue(HintsProperty, value);

        /// <summary>Reads the share of the width a window's panel takes in the look.</summary>
        /// <param name="window">The window that may ask for more room.</param>
        /// <returns>A fraction, or NaN for the menu's own.</returns>
        public static double GetWidthFraction(Window window) => window.GetValue(WidthFractionProperty);

        /// <summary>Sets the share of the width a window's panel takes in the look, for a window whose layout needs more room than a menu's.</summary>
        /// <param name="window">The window, before it is presented.</param>
        /// <param name="value">A fraction of the screen's width.</param>
        public static void SetWidthFraction(Window window, double value) => window.SetValue(WidthFractionProperty, value);

        /// <summary>A fresh copy of the look's styles and palette, for an element's Styles.</summary>
        /// <returns>The styles; a Styles has one owner, so each element takes its own copy.</returns>
        public static Styles CreateStyles() => (Styles)AvaloniaXamlLoader.Load(new Uri("avares://EmuSen.LunaP/Theme/MenuLook.axaml"));

        /// <summary>Whether an element is drawn in the look: the look is on for it or for an element above it.</summary>
        /// <param name="element">The element.</param>
        /// <returns>True under an element the look was applied to.</returns>
        public static bool Covers(Control element)
        {
            for (StyledElement? at = element; at is not null; at = at.Parent)
                if (at is Control c && GetIsOn(c)) return true;
            return false;
        }

        /// <summary>Runs an action the first time an element is shown where the look is on, for what the look's styles cannot reach, such as a size set on a control itself.</summary>
        /// <param name="element">The element, typically a window's content or one of its controls.</param>
        /// <param name="action">What to change; it runs at most once.</param>
        public static void WhenApplied(Control element, Action action)
        {
            void Attached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                if (!Covers(element)) return;
                element.AttachedToVisualTree -= Attached;
                action();
            }
            element.AttachedToVisualTree += Attached;
        }

        private static void Update(Control element, bool on)
        {
            if (element.GetValue(AppliedProperty) is { } applied)
            {
                element.Styles.Remove(applied);
                element.ClearValue(AppliedProperty);
            }
            element.Classes.Set(ClassName, on);
            if (!on) return;
            Styles styles = CreateStyles();
            element.Styles.Add(styles);
            element.SetValue(AppliedProperty, styles);
        }

        /// <summary>Upper-cases a string and passes anything else through, for the words a button, a tab or a column heading shows.</summary>
        public static readonly IValueConverter UpperCase = new FuncValueConverter<object?, object?>(v => v is string s ? FontLayout.Cased(s, LetterCase.Upper) : v);

        /// <summary>Draws a string content in upper case, and a control content as itself.</summary>
        public static readonly IDataTemplate UpperCaseText = new UpperCaseTemplate();

        // A presenter given a content template uses it for a control content too, so a control is handed back as it is (§196.9).
        private sealed class UpperCaseTemplate : IDataTemplate
        {
            public bool Match(object? data) => data is string or Control;

            public Control? Build(object? param) => param switch
            {
                Control control => control,
                string words => new TextBlock { Text = FontLayout.Cased(words, LetterCase.Upper) },
                _ => new TextBlock { Text = param?.ToString() },
            };
        }
    }
}
