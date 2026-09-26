using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;

namespace EmuSen.LunaP.Tests
{
    // A focused switch's outline is drawn inside its bounds, so the label must end short of the right edge or the outline covers its last letter - see docs/LunaP.md §111.
    public class SwitchLabelClearanceTests
    {
        private const double OutlineThickness = 2;

        [Theory]
        [InlineData("Use ScreenScraper")]
        [InlineData("On")]
        [InlineData("Show the frame rate in the status bar")]
        public Task A_focused_switch_s_outline_does_not_reach_its_label(string label) => UiTest.Run(() =>
        {
            var toggle = new LunaSwitch { Label = label };
            var window = new Window { Width = 700, Height = 160, Content = new StackPanel { Margin = new Thickness(24), Children = { toggle } } };
            window.Show();
            toggle.Focus(NavigationMethod.Tab);
            UiTest.Redraw(window);

            TextBlock[] texts = toggle.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == label).ToArray();
            Assert.NotEmpty(texts);
            foreach (TextBlock text in texts)
            {
                double right = text.TranslatePoint(new Point(text.Bounds.Width, 0), toggle)!.Value.X;
                Assert.True(toggle.Bounds.Width - right >= OutlineThickness + 2, $"'{label}' ends at {right:0.#} of {toggle.Bounds.Width:0.#}: under the focus outline");
            }
        });
    }
}
