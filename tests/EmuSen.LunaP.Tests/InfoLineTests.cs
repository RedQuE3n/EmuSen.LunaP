using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // InfoLine's layout in ems and its pictures - see docs/LunaP.md §193.
    public class InfoLineTests
    {
        private static InfoItem[] Counts(bool folder) =>
            folder ? [new(InfoIcon.Gamepad, "12"), new(InfoIcon.Star, "6"), new(InfoIcon.Folder)] : [new(InfoIcon.Gamepad, "12"), new(InfoIcon.Star, "6")];

        [Fact]
        public Task The_line_measures_its_items_and_one_and_a_half_ems_of_height() => UiTest.Run(() =>
        {
            var line = new InfoLine { Items = Counts(false), FontSize = 40 };
            line.Measure(Size.Infinity);
            Assert.Equal(60, line.DesiredSize.Height, 6);
            double text = line.LineWidth - (1.06 + 0.3 + 0.47 + 0.92 + 0.3) * 40;
            Assert.InRange(text, 20, 80);
            var withFolder = new InfoLine { Items = Counts(true), FontSize = 40 };
            Assert.Equal(line.LineWidth + (0.47 + 1.06) * 40, withFolder.LineWidth, 6);
        });

        // The reference puts the folder at the left of a right-aligned line and at the end otherwise: the tallest ink in the line's first em tells a folder (0.78 ems) from a gamepad (0.58).
        [Fact]
        public Task A_right_aligned_line_draws_the_folder_first() => UiTest.Run(() =>
        {
            var line = new InfoLine { Items = [new(InfoIcon.Gamepad, "1"), new(InfoIcon.Folder)], FontSize = 40, Foreground = Colors.White, Width = 400, Height = 60 };
            ToolWindow window = Show(new Canvas { Width = 400, Height = 60, Background = Brushes.Black, Children = { line } }, 400, 60);
            int Tallest(RenderedFrame f, double x0) =>
                Enumerable.Range((int)x0 + 4, 30).Select(x => Enumerable.Range(0, 60).Count(y => At(f, x, y).G > 128)).Max();
            int gamepad = Tallest(Frame(window), 0);
            line.TextAlignment = TextAlignment.Right;
            int folder = Tallest(Frame(window), 400 - line.LineWidth);
            Assert.InRange(gamepad, 0.5 * 40, 0.66 * 40);
            Assert.InRange(folder, 0.7 * 40, 0.86 * 40);
            window.Close();
        });
    }
}
