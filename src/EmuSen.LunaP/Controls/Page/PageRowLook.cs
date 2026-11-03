using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // One owner for a page's row as the big screen draws it, shared by PageLayout and SettingsLayout - see docs/LunaP.md §213.3.
    internal static class PageRowLook
    {
        public const double RowHeight = 54;

        public static MenuRow Big(PageRow row, bool focused, string namePrefix, Action pressed)
        {
            var m = new MenuRow
            {
                Name = namePrefix + row.Key,
                Label = row.Label,
                Value = row.Status ?? (row.Kind == PageRowKind.Asks ? "…" : null),
                Kind = row.Kind switch { PageRowKind.Opens or PageRowKind.Text => MenuRowKind.Submenu, PageRowKind.Steps => MenuRowKind.Option, _ => MenuRowKind.Action },
                IsHighlighted = focused,
                LetterCase = LetterCase.None,
                FontPath = LunaFonts.BodyFile,
                TextSize = LunaTokens.BigScreenType.Row,
                RowHeight = RowHeight,
                Inset = 24,
                Scale = 1,
            };
            m.Bind(MenuRow.TextColorProperty, m.GetResourceObservable(row.IsEnabled ? "LunaTextColor" : "LunaMutedColor", c => c is Color colour ? colour : m.TextColor));
            m.Bind(MenuRow.HighlightTextColorProperty, m.GetResourceObservable("LunaTextStrongColor", c => c is Color colour ? colour : m.HighlightTextColor));
            m.Bind(MenuRow.BarColorProperty, m.GetResourceObservable("LunaSurfaceRaisedColor", c => c is Color colour ? colour : m.BarColor));
            m.Bind(MenuRow.RuleColorProperty, m.GetResourceObservable("LunaRuleColor", c => c is Color colour ? colour : m.RuleColor));
            m.Bind(MenuRow.AccentColorProperty, m.GetResourceObservable("LunaAccentColor", c => c is Color colour ? colour : m.AccentColor));
            if (!focused) m.Bind(MenuRow.ValueColorProperty, m.GetResourceObservable("LunaMutedColor", c => c is Color colour ? colour : (Color?)null));
            m.PointerPressed += (_, _) => pressed();
            return m;
        }
    }
}
