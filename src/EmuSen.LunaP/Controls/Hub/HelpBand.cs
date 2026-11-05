using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // The help band of a full screen as the frame draws it: 58 high from 702 to the foot with a rule above, its entries in Caption 20, 40 apart, from the safe area's edge; at night a dark glass flush with the foot - see docs/LunaP.md §211.4, §221.4 and §222.7.
    internal static class HelpBand
    {
        public const double Top = 702, Height = 58, Foot = 40, Side = 64;

        // At night the band sits on the screen's foot, 742 to 800, with no room under it.
        public const double NightTop = 800 - Height;

        public static Border Make(HintBar bar, string name, bool night = false)
        {
            PageLayout.StyleBar(bar);
            bar.FontSize = LunaTokens.BigScreenType.Caption;
            bar.EntrySpacing = 40;
            bar.IconTextSpacing = 10;
            bar.IconScale = 1.5;
            bar.VerticalAlignment = VerticalAlignment.Center;
            var band = new Border
            {
                Name = name, Height = Height + Foot, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Top, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(Side, 0, Side, Foot),
            };
            if (night)
            {
                // The input surface at 0.72 as glass under the entries, from the palette's own colours.
                var glass = new Border { Opacity = 0.72, IsHitTestVisible = false, BorderThickness = new Thickness(0, 1, 0, 0) };
                glass[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaInputSurface");
                glass[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
                bar.Margin = new Thickness(Side, 0, Side, 0);
                band.Height = Height;
                band.Margin = new Thickness(0, NightTop, 0, 0);
                band.Padding = default;
                band.BorderThickness = default;
                band.Child = new Panel { Children = { glass, bar } };
                return band;
            }
            bar.Margin = default;
            band.Child = bar;
            band[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            band[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            return band;
        }
    }
}
