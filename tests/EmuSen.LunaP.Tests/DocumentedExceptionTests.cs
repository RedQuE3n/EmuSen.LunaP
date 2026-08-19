using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Threading;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // EVERY <exception> TAG THE TWO PACKAGES PUBLISH, TAKEN LITERALLY - see docs/LunaP.md §80.2.
    //
    // A tag is a promise about a type AND a parameter name, and the name is the half that rots
    // quietly: a constructor that forwards an argument into a differently-named private one keeps
    // throwing the right exception while naming a parameter the caller never typed. That is what
    // §80.2 was, and reading the tags could not have found it - only calling them could.
    //
    // Delegated checks are pinned here on purpose even though no throw appears in the method body.
    // IdleCursor's delay is validated by the Debounce it builds, and reads correctly only because
    // that constructor's parameter is called `delay` too; renaming it there would break a promise
    // made in a file that does not mention it.
    public class DocumentedExceptionTests
    {
        private sealed record Row(int N);

        private static void Names<TEx>(string expected, Action body) where TEx : ArgumentException
        {
            TEx ex = Assert.Throws<TEx>(body);
            Assert.Equal(expected, ex.ParamName);
        }

        [Fact]
        public void Off_the_ui_thread()
        {
            Names<ArgumentNullException>("action", () => UiThread.Run(null!));
            Names<ArgumentNullException>("action", () => UiThread.Post(null!));

            Names<ArgumentNullException>("key", () => TableLayoutStore.Update(null!, _ => { }));
            Names<ArgumentNullException>("edit", () => TableLayoutStore.Update("k", null!));
            Names<ArgumentNullException>("key", () => PaneLayoutStore.Update(null!, _ => { }));
            Names<ArgumentNullException>("edit", () => PaneLayoutStore.Update("k", null!));

            Names<ArgumentNullException>("extra", () => LunaHeadless.BuildApp(null!));

            Names<ArgumentNullException>("present", () => new Latest<string>(null!));
            Names<ArgumentNullException>("value", () => new Latest<string>(_ => { }).Offer(null!));

            Names<ArgumentNullException>("title", () => new LunaMenu(null!, Array.Empty<LunaAction>()));
            Names<ArgumentNullException>("text", () => new LunaAction(null!));
            Names<ArgumentNullException>("value", () => new LunaAction("x").Text = null!);

            Names<ArgumentNullException>("menu", () => Menus.Items((LunaMenu)null!));
            Names<ArgumentNullException>("actions", () => Menus.Items((IEnumerable<LunaAction>)null!));
        }

        [Fact]
        public void Column_constructors_name_the_argument_the_caller_passed()
        {
            Names<ArgumentNullException>("header",
                () => new LunaColumn<Row>(null!, (Func<Row, string>)(r => "")));
            Names<ArgumentNullException>("text",
                () => new LunaColumn<Row>("h", (Func<Row, string>)null!));

            Names<ArgumentNullException>("header",
                () => new LunaColumn<Row>(null!, (Func<Row, bool>)(r => true)));
            Names<ArgumentNullException>("read",
                () => new LunaColumn<Row>("h", (Func<Row, bool>)null!));

            Names<ArgumentNullException>("header",
                () => new LunaColumn<Row>(null!, (Func<Row, Control>)(r => new Button()), r => ""));
            Names<ArgumentNullException>("build",
                () => new LunaColumn<Row>("h", (Func<Row, Control>)null!, r => ""));

            // §80.2: this named "text" - the private constructor's parameter - until Named was added
            // beside Says. It is the one of the six that reported an argument this overload has not
            // got, and the comment above Says had already written down why that must not happen.
            Names<ArgumentNullException>("spoken",
                () => new LunaColumn<Row>("h", (Func<Row, Control>)(r => new Button()), null!));
        }

        [Fact]
        public void Groups_refuse_a_member_they_do_not_own()
        {
            var group = new ActionGroup();
            group.Add("one");

            Assert.Throws<ArgumentException>(() => group.Checked = new LunaAction("elsewhere"));

            // Documented in §80.5. An action belongs to one group or none.
            LunaAction owned = group.Add("two");
            Assert.Throws<InvalidOperationException>(() => new ActionGroup().Add(owned));

            // The same group twice is not the same thing, and stays harmless.
            Assert.Same(owned, group.Add(owned));
        }

        [Fact]
        public void A_theme_that_is_not_css_throws_rather_than_warning()
        {
            // §80.4. Parse's summary said it collected rather than throwing, which described the
            // semantic half only. LunaTheme.Read catches these, so no application has ever seen one.
            Assert.Throws<FormatException>(() => CssTheme.Parse("/* unterminated"));
            Assert.Throws<FormatException>(() => CssTheme.Parse("button"));
            Assert.Throws<FormatException>(() => CssTheme.Parse("@media print { }"));
            Assert.Throws<FormatException>(() => CssTheme.Parse("{ color: red; }"));

            // The semantic half, which really does collect: an unknown property is a warning and
            // the file still loads. This is the distinction the summary used to lose.
            Assert.NotEmpty(CssTheme.Parse("button { nonsense-property: 3px; }").Warnings);
        }

        [Fact]
        public Task On_the_ui_thread() => UiTest.Run(() =>
        {
            Names<ArgumentNullException>("action", () => new ActionMenuItem(null!));
            Names<ArgumentNullException>("action", () => new ActionButton(null!));
            Names<ArgumentNullException>("action", () => new ActionToggle(null!));

            Names<ArgumentNullException>("actions",
                () => new ToolBar().SetActions((IEnumerable<LunaAction>)null!));
            Names<ArgumentNullException>("menus",
                () => new MenuBar().SetMenus((IEnumerable<LunaMenu>)null!));

            Names<ArgumentNullException>("items", () => new LunaList<Row>().Refresh(null!));
            Names<ArgumentNullException>("items", () => new LunaTable<Row>().Refresh(null!));

            Names<ArgumentNullException>("header", () => new LunaTable<Row>().Column(null!, r => ""));
            Names<ArgumentNullException>("text", () => new LunaTable<Row>().Column("h", null!));
            Names<ArgumentNullException>("column",
                () => new LunaTable<Row>().Column((LunaColumn<Row>)null!));

            Names<ArgumentOutOfRangeException>("delay", () => new Debounce(TimeSpan.Zero, () => { }));
            Names<ArgumentNullException>("action", () => new Debounce(TimeSpan.FromSeconds(1), null!));

            Names<ArgumentNullException>("panel", () => new AppWindow().AddPanel(null!));

            Names<ArgumentNullException>("target", () => new IdleCursor(null!));

            // Delegated to Debounce, and correct only because its parameter is named `delay` too.
            Names<ArgumentOutOfRangeException>("delay",
                () => new IdleCursor(new Button(), TimeSpan.Zero));

            Names<ArgumentNullException>("target", () => new FileDrop(null!, _ => { }));
            Names<ArgumentNullException>("dropped", () => new FileDrop(new Button(), null!));

            Names<ArgumentNullException>("control", () => UiTest.Settle(null!));

            Names<ArgumentNullException>("target",
                () => Menus.BindShortcuts(null!, Array.Empty<LunaAction>()));
            Names<ArgumentNullException>("actions",
                () => Menus.BindShortcuts(new Button(), (IEnumerable<LunaAction>)null!));
            Names<ArgumentNullException>("menus",
                () => Menus.BindShortcuts(new Button(), (IEnumerable<LunaMenu>)null!));
            Names<ArgumentNullException>("target",
                () => Menus.Unbind(null!, Array.Empty<Avalonia.Input.KeyBinding>()));

            // Null bindings is ignored rather than refused, which the parameter now says.
            Menus.Unbind(new Button(), null!);

            // A window that was never shown has no frame to capture.
            var never = new Window();
            Assert.Throws<InvalidOperationException>(() => UiTest.Capture(never));
            Assert.Throws<InvalidOperationException>(() => UiTest.Redraw(never));

            var shown = new Window { Content = new Button { Name = "here" } };
            shown.Show();
            shown.UpdateLayout();
            Assert.Throws<InvalidOperationException>(() => shown.FindNamed<Button>("absent"));
            shown.Close();
        });
    }
}
