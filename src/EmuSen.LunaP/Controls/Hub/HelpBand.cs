using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // The help band of a full screen as the frame draws it: 58 high from 702 to the foot with a rule above, its entries in Caption 20, 40 apart, from the safe area's edge - see docs/LunaP.md §211.4.
    internal static class HelpBand
    {
        public const double Top = 702, Height = 58, Foot = 40, Side = 64;

        public static Border Make(HintBar bar, string name)
        {
            PageLayout.StyleBar(bar);
            bar.FontSize = LunaTokens.BigScreenType.Caption;
            bar.EntrySpacing = 40;
            bar.IconTextSpacing = 10;
            bar.IconScale = 1.5;
            bar.VerticalAlignment = VerticalAlignment.Center;
            var band = new Border
            {
                Name = name, Child = bar, Height = Height + Foot, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Top, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(Side, 0, Side, Foot),
            };
            band[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            band[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            return band;
        }
    }
}
