using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // The header row's facts with a warning in Attention, and the chosen row's action at the desktop header's right - see docs/LunaP.md §215.
    public class HeaderRowTests
    {
        public sealed record Row(string Name, bool Waits);

        private static readonly Row[] Rows = { new("PCEE2", true), new("Diamond", false) };

        private const string Facts = "17 cores · 6 installed · ⚠ 1 to approve · catalogue read 31 Oct 2026";

        private static (ManagerLayout<Row> Frame, Window Window) Build(AdaptiveForm form)
        {
            var m = new ManagerLayout<Row> { Title = "Cores", Form = form, RowKey = r => r.Name, DetailPlacement = AdaptiveDetailPlacement.Beside, ContentShare = 0.6 };
            m.Column(new LunaColumn<Row>("Core", r => r.Name), bigScreen: true);
            m.Header = new AdaptiveHeader("CORES", Facts: Facts);
            m.ActionFor = r => r.Waits ? new LunaAction("Approve…", () => { }) : null;
            m.DetailFor = r => new TextBlock { Text = r?.Name ?? "" };
            m.SetToolActions(Array.Empty<LunaAction>());
            m.SetRows(Rows);
            var window = new Window { Width = 1280, Height = 800, Content = m };
            window.Show();
            UiTest.Settle(m);
            (form == AdaptiveForm.BigScreen ? m.BigScreenTable : m.Table).Select(Rows[0]);
            UiTest.Settle(m);
            return (m, window);
        }

        private static Control Header(ManagerLayout<Row> m) => m.HostOf(AdaptiveRole.Header)!;

        private static TextBlock[] FactTexts(Control header) =>
            header.GetVisualDescendants().OfType<StackPanel>().First(p => p.Name == AdaptiveHeaderRow.FactsName).Children.OfType<TextBlock>().ToArray();

        private static Color ColourOf(TextBlock t) => ((ISolidColorBrush)t.Foreground!).Color;

        private static Color Token(Control c, string key) => c.TryFindResource(key, c.ActualThemeVariant, out object? v) && v is ISolidColorBrush b ? b.Color : throw new InvalidOperationException(key);

        private static Rect InWindow(Control control, Window window) => new(control.TranslatePoint(default, window) ?? default, control.Bounds.Size);

        [Theory]
        [InlineData(AdaptiveForm.BigScreen)]
        [InlineData(AdaptiveForm.Desktop)]
        public Task A_warning_fact_is_drawn_in_attention_and_the_rest_muted(AdaptiveForm form) => UiTest.Run(() =>
        {
            (ManagerLayout<Row> m, Window w) = Build(form);
            TextBlock[] texts = FactTexts(Header(m));
            Assert.Equal(Facts, string.Concat(texts.Select(t => t.Text)));
            foreach (TextBlock t in texts)
                Assert.Equal(Token(t, t.Text!.StartsWith('⚠') ? "LunaAttention" : "LunaMuted"), ColourOf(t));
            Assert.Contains(texts, t => t.Text == "⚠ 1 to approve");
            w.Close();
        });

        [Fact]
        public Task On_the_desktop_the_facts_follow_the_name_and_the_rows_action_stands_at_the_right() => UiTest.Run(() =>
        {
            (ManagerLayout<Row> m, Window w) = Build(AdaptiveForm.Desktop);
            Control header = Header(m);
            Button action = header.GetVisualDescendants().OfType<Button>().Single(b => b.Name == AdaptiveHeaderRow.ActionName);
            Assert.Equal("Approve…", action.Content);
            Rect facts = InWindow(header.GetVisualDescendants().OfType<StackPanel>().First(p => p.Name == AdaptiveHeaderRow.FactsName), w);
            TextBlock name = header.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "CORES");
            Rect title = new(InWindow(name, w).Position, name.DesiredSize);
            Rect button = InWindow(action, w);
            Assert.True(facts.Left >= title.Right && facts.Left - title.Right < 20, $"the facts start at {facts.Left}, the name ends at {title.Right}");
            Assert.True(facts.Right <= button.Left, "the facts end before the action");
            Assert.True(button.Right > w.Bounds.Width - 40, $"the action ends at {button.Right}");

            m.Table.Select(Rows[1]);
            UiTest.Settle(m);
            Assert.Null(m.HeaderAction);
            Assert.DoesNotContain(Header(m).GetVisualDescendants().OfType<Button>(), b => b.Name == AdaptiveHeaderRow.ActionName);
            m.Table.Select(Rows[0]);
            UiTest.Settle(m);
            Assert.Equal("Approve…", m.HeaderAction?.Text);
            w.Close();
        });

        [Fact]
        public Task The_big_screen_draws_no_action_in_its_header_and_its_facts_stand_at_the_right() => UiTest.Run(() =>
        {
            (ManagerLayout<Row> m, Window w) = Build(AdaptiveForm.BigScreen);
            Control header = Header(m);
            Assert.Equal("Approve…", m.HeaderAction?.Text);
            Assert.DoesNotContain(header.GetVisualDescendants().OfType<Button>(), b => b.Name == AdaptiveHeaderRow.ActionName);
            Rect facts = InWindow(header.GetVisualDescendants().OfType<StackPanel>().First(p => p.Name == AdaptiveHeaderRow.FactsName), w);
            Assert.True(facts.Right > InWindow(header, w).Right - 40, $"the facts end at {facts.Right}");
            w.Close();
        });
    }
}
