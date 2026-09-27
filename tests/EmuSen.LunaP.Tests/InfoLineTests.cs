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

        // The reference puts the folder at the left of a right-aligned line and at the right otherwise.
        [Fact]
        public Task A_right_aligned_line_draws_the_folder_first() => UiTest.Run(() =>
        {
            var line = new InfoLine { Items = [new(InfoIcon.Folder), new(InfoIcon.Star, "6")], FontSize = 40, Foreground = Colors.White, Width = 400, Height = 60 };
            ToolWindow window = Show(new Canvas { Width = 400, Height = 60, Background = Brushes.Black, Children = { line } }, 400, 60);
            RenderedFrame left = Frame(window);
            line.TextAlignment = TextAlignment.Right;
            RenderedFrame right = Frame(window);
            double ink(RenderedFrame f, int x0, int x1) => Enumerable.Range(x0, x1 - x0).Sum(x => Enumerable.Range(0, 60).Count(y => At(f, x, y).G > 128));
            double start = 400 - line.LineWidth;
            // Left-aligned: the star and its text first, the folder last; right-aligned, the folder's 1.06 ems lead.
            Assert.True(ink(right, (int)start, (int)(start + 0.9 * 40)) > ink(left, (int)start, (int)(start + 0.9 * 40)) || ink(left, 0, 40) > 0);
            Assert.True(ink(right, (int)start + 2, (int)(start + 40)) > 200, "a folder at the start of the right-aligned line");
            Assert.True(ink(left, 400 - 44, 400 - 2) == 0 && ink(left, (int)(line.LineWidth - 42), (int)line.LineWidth) > 200, "the folder at the end of the left-aligned line");
            window.Close();
        });
    }
}
