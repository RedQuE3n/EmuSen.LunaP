using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // The CSS theme format: what it compiles to, and what it refuses - see docs/LunaP.md §12.2.
    public class CssThemeTests : IDisposable
    {
        private readonly string _configDir;

        // What LunaP reported, captured through the hook a host normally owns.
        private string? _reported;

        public CssThemeTests()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "lunap-css-" + Guid.NewGuid().ToString("N"));
            LunaSettings.Store = new JsonSettingsStore(_configDir);
            _reported = null;
            LunaSettings.Diagnostics = m => _reported = m;
        }

        // The applied theme is global to the headless application, so every test here has to put it back.
        public void Dispose()
        {
            UiTest.Run(() => LunaTheme.Apply(LunaTheme.BuiltIn)).GetAwaiter().GetResult();
            LunaSettings.Store = new JsonSettingsStore(Path.Combine(Path.GetTempPath(), "lunap-unset"));
            if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
        }

        private static void WriteCss(string name, string body)
        {
            Directory.CreateDirectory(LunaTheme.Directory);
            File.WriteAllText(Path.Combine(LunaTheme.Directory, name + LunaTheme.CssExtension), body);
        }

        [Fact]
        public Task A_root_token_becomes_both_halves_of_the_palette_key_it_names() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse(":root { --luna-section-header: #7AA2F7; }");

            Assert.Empty(css.Warnings);

            // Palette.axaml spells every colour as a Color and as a brush, and a theme that set only one would half-apply.
            Assert.Equal(Color.Parse("#7AA2F7"), Assert.IsType<Color>(css.Resources["LunaSectionHeaderColor"]));
            Assert.Equal(Color.Parse("#7AA2F7"), ((ISolidColorBrush)css.Resources["LunaSectionHeader"]!).Color);
        });

        // Naming the Color key rather than the brush is the same declaration, not a second one.
        [Fact]
        public Task Either_spelling_of_a_colour_key_defines_both() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse(":root { --luna-surface-color: #101010; }");

            Assert.Empty(css.Warnings);
            Assert.Equal(Color.Parse("#101010"), Assert.IsType<Color>(css.Resources["LunaSurfaceColor"]));
            Assert.Equal(Color.Parse("#101010"), ((ISolidColorBrush)css.Resources["LunaSurface"]!).Color);
        });

        // The key's suffix decides the type, not the value's shape: a font family and a colour name look alike.
        [Fact]
        public Task A_font_and_a_size_are_told_apart_by_their_key() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse(
                ":root { --luna-mono-font: \"Fira Code\", monospace; --luna-hint-font-size: 13px; }");

            Assert.Empty(css.Warnings);
            Assert.Equal(new[] { "Fira Code", "monospace" },
                Assert.IsType<FontFamily>(css.Resources["LunaMonoFont"]).FamilyNames);
            Assert.Equal(13d, Assert.IsType<double>(css.Resources["LunaHintFontSize"]));
        });

        [Theory]
        [InlineData("#7AA2F7", "#FF7AA2F7")]
        [InlineData("#abc", "#FFAABBCC")]
        [InlineData("rgb(122, 162, 247)", "#FF7AA2F7")]
        [InlineData("rgba(122, 162, 247, 0.5)", "#807AA2F7")]
        [InlineData("gainsboro", "#FFDCDCDC")]
        // Alpha last, the CSS order - Avalonia's own #AARRGGBB would read this backwards.
        [InlineData("#7AA2F780", "#807AA2F7")]
        public Task A_colour_may_be_written_in_any_css_spelling(string written, string expected) => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse(":root { --luna-hot: " + written + "; }");

            Assert.Empty(css.Warnings);
            Assert.Equal(Color.Parse(expected), ((ISolidColorBrush)css.Resources["LunaHot"]!).Color);
        });

        [Fact]
        public Task A_rule_block_becomes_a_style_that_really_applies() => UiTest.Run(() =>
        {
            WriteCss("Nocturne", "section-header { color: #7AA2F7; font-size: 20; }");

            var header = new SectionHeader { Text = "Load" };
            var window = new ToolWindow { Width = 300, Height = 200, Content = header };
            window.Show();

            Assert.Equal(Color.Parse("#9CDCFE"), Brush(header));

            Assert.True(LunaTheme.Apply("Nocturne"));

            Assert.Equal(Color.Parse("#7AA2F7"), Brush(header));
            Assert.Equal(20d, header.FontSize);

            window.Close();
        });

        // The one selector shape that needs to know a template: the load ramp lives on MeterRow's bar, not on MeterRow.
        [Fact]
        public Task A_rule_can_reach_a_state_and_a_template_part() => UiTest.Run(() =>
        {
            WriteCss("Hotter", "meter-row.hot .bar { color: #FF00FF; }");

            var row = new MeterRow { Label = "S-CPU", Percent = 95, ValueText = "95%" };
            var window = new ToolWindow { Width = 400, Height = 100, Content = row };
            window.Show();

            Assert.Equal(Color.Parse("#FF4500"), ((ISolidColorBrush)row.FindPart<ProgressBar>()!.Foreground!).Color);

            Assert.True(LunaTheme.Apply("Hotter"));
            Assert.Equal(Color.Parse("#FF00FF"), ((ISolidColorBrush)row.FindPart<ProgressBar>()!.Foreground!).Color);

            window.Close();
        });

        // A rule that restated a colour could not follow the palette, which is the whole failure §12.1 records.
        [Fact]
        public Task A_var_reference_follows_the_token_it_names() => UiTest.Run(() =>
        {
            WriteCss("Linked", ":root { --luna-warning: #00FF00; } mono-text { color: var(--luna-warning); }");

            var text = new MonoText { Text = "$2100" };
            var window = new ToolWindow { Width = 300, Height = 200, Content = text };
            window.Show();

            Assert.True(LunaTheme.Apply("Linked"));
            Assert.Equal(Color.Parse("#00FF00"), Brush(text));

            window.Close();
        });

        [Fact]
        public Task A_selector_list_applies_to_every_element_in_it() => UiTest.Run(() =>
        {
            WriteCss("Both", "section-header, hint-text { color: #123456; }");

            var header = new SectionHeader { Text = "Load" };
            var hint = new HintText { Text = "explanation" };
            var window = new ToolWindow { Width = 300, Height = 200, Content = new StackPanel { Children = { header, hint } } };
            window.Show();

            Assert.True(LunaTheme.Apply("Both"));
            Assert.Equal(Color.Parse("#123456"), Brush(header));
            Assert.Equal(Color.Parse("#123456"), Brush(hint));

            window.Close();
        });

        // A theme written against a newer LunaP has to keep working, so an unknown name is reported rather than fatal.
        [Fact]
        public Task An_unknown_selector_or_property_is_reported_and_the_rest_still_applies() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse(
                "sprite-viewer { color: #FF0000; } section-header { border-radius: 4; color: #7AA2F7; }");

            Assert.Contains(css.Warnings, w => w.Contains("sprite-viewer"));
            Assert.Contains(css.Warnings, w => w.Contains("border-radius"));

            Style style = Assert.Single(css.Styles.OfType<Style>());
            Assert.Single(style.Setters);
        });

        [Fact]
        public Task An_unknown_state_or_part_is_reported_rather_than_matching_nothing() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse("meter-row.melting { color: #FF0000; } meter-row .knob { color: #FF0000; }");

            Assert.Empty(css.Styles);
            Assert.Contains(css.Warnings, w => w.Contains("melting"));
            Assert.Contains(css.Warnings, w => w.Contains("knob"));
        });

        // Line numbers are the point of reporting at all, and a comment is where a naive scanner loses them.
        [Fact]
        public Task A_comment_does_not_shift_the_reported_line() => UiTest.Run(() =>
        {
            CssThemeResult css = CssTheme.Parse("/* a theme\n   over two lines */\n:root { --luna-nonsense: notacolour; }");

            Assert.Contains("line 3", Assert.Single(css.Warnings));
        });

        [Theory]
        [InlineData("section-header { color: #FF0000;", "'}'")]
        [InlineData("section-header color: #FF0000; }", "'{'")]
        [InlineData("section-header { color #FF0000; }", "declaration")]
        [InlineData("@media screen { section-header { color: #FF0000; } }", "at-rules")]
        [InlineData("section-header { hint-text { color: #FF0000; } }", "nested")]
        [InlineData("/* never closed\nsection-header { color: #FF0000; }", "comment")]
        // A syntax error refuses the file, the way a malformed .axaml theme already does.
        public Task A_malformed_file_is_refused_whole(string css, string mentions) => UiTest.Run(() =>
        {
            var thrown = Assert.Throws<FormatException>(() => CssTheme.Parse(css));

            Assert.Contains(mentions, thrown.Message);
        });

        [Fact]
        public Task A_malformed_css_theme_leaves_the_previous_one_in_force() => UiTest.Run(() =>
        {
            WriteCss("Broken", "section-header { color: #FF0000;");

            var header = new SectionHeader { Text = "Load" };
            var window = new Window { Width = 300, Height = 200, Content = header };
            window.Show();

            Assert.False(LunaTheme.Apply("Broken"));
            Assert.Equal(Color.Parse("#9CDCFE"), Brush(header));
            Assert.Contains("Broken", _reported ?? "");

            window.Close();
        });

        // The .axaml path only ever added resources; a theme's styles have to come off again the same way.
        [Fact]
        public Task Switching_away_from_a_css_theme_removes_its_styles_too() => UiTest.Run(() =>
        {
            WriteCss("Nocturne", "section-header { color: #7AA2F7; }");

            var header = new SectionHeader { Text = "Load" };
            var window = new ToolWindow { Width = 300, Height = 200, Content = header };
            window.Show();

            LunaTheme.Apply("Nocturne");
            Assert.Equal(Color.Parse("#7AA2F7"), Brush(header));

            LunaTheme.Apply(LunaTheme.BuiltIn);
            Assert.Equal(Color.Parse("#9CDCFE"), Brush(header));

            window.Close();
        });

        // The hazard ToolWindow's restyle hook exists for, pinned so nobody removes the hook as redundant.
        [Fact]
        public Task A_window_that_never_restyles_loses_its_styling_when_one_arrives() => UiTest.Run(() =>
        {
            WriteCss("Nocturne", "section-header { color: #7AA2F7; }");

            var header = new SectionHeader { Text = "Load" };
            var window = new Window { Width = 300, Height = 200, Content = header };
            window.Show();
            Assert.Equal(Color.Parse("#9CDCFE"), Brush(header));

            // Mutating Application.Styles strips a realized control of every style it had, including LunaP's own.
            LunaTheme.Apply("Nocturne");
            Assert.NotEqual(Color.Parse("#7AA2F7"), Brush(header));

            LunaTheme.Restyle(window);
            Assert.Equal(Color.Parse("#7AA2F7"), Brush(header));

            window.Close();
        });

        // A palette-only theme changes no styles, so it must not cost an open window a restyle it does not need.
        [Fact]
        public Task A_palette_only_theme_repaints_without_a_restyle() => UiTest.Run(() =>
        {
            WriteCss("Sparse", ":root { --luna-section-header: #7AA2F7; }");

            var header = new SectionHeader { Text = "Load" };
            var window = new Window { Width = 300, Height = 200, Content = header };
            window.Show();

            int restyles = 0;
            LunaTheme.StylesChanged += Count;
            try
            {
                Assert.True(LunaTheme.Apply("Sparse"));
                Assert.Equal(Color.Parse("#7AA2F7"), Brush(header));
                Assert.Equal(0, restyles);
            }
            finally
            {
                LunaTheme.StylesChanged -= Count;
            }

            window.Close();
            void Count() => restyles++;
        });

        [Fact]
        public Task The_catalog_lists_a_css_theme_beside_an_axaml_one() => UiTest.Run(() =>
        {
            WriteCss("Zebra", ":root { }");
            Directory.CreateDirectory(LunaTheme.Directory);
            File.WriteAllText(Path.Combine(LunaTheme.Directory, "Amber" + LunaTheme.Extension),
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" />");

            Assert.Equal(new[] { LunaTheme.BuiltIn, "Amber", "Zebra" }, LunaTheme.Available());
        });

        // One name is one theme however many formats spell it, and .axaml is the one that wins.
        [Fact]
        public Task A_name_spelled_in_both_formats_is_listed_once_and_resolves_to_the_axaml() => UiTest.Run(() =>
        {
            WriteCss("Nocturne", ":root { --luna-section-header: #00FF00; }");
            Directory.CreateDirectory(LunaTheme.Directory);
            File.WriteAllText(Path.Combine(LunaTheme.Directory, "Nocturne" + LunaTheme.Extension),
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + "<SolidColorBrush x:Key=\"LunaSectionHeader\" Color=\"#7AA2F7\" /></ResourceDictionary>");

            Assert.Equal(new[] { LunaTheme.BuiltIn, "Nocturne" }, LunaTheme.Available());

            var header = new SectionHeader { Text = "Load" };
            var window = new Window { Width = 300, Height = 200, Content = header };
            window.Show();

            Assert.True(LunaTheme.Apply("Nocturne"));
            Assert.Equal(Color.Parse("#7AA2F7"), Brush(header));

            window.Close();
        });

        [Fact]
        public Task A_chosen_css_theme_survives_a_restart() => UiTest.Run(() =>
        {
            WriteCss("Nocturne", ":root { --luna-section-header: #7AA2F7; }");
            LunaTheme.Apply("Nocturne");

            Assert.Equal("Nocturne", LunaTheme.Saved);

            LunaTheme.ApplySaved();
            Assert.Equal("Nocturne", LunaTheme.Current);
        });

        private static Color Brush(TextBlock text) => ((ISolidColorBrush)text.Foreground!).Color;

        // ------------------------------------------------------------ the vocabulary really works

        // THE §30 GUARD, and the failure it exists for is the worst one this format has: a rule that
        // parses, warns about nothing, and styles nothing. A refused rule tells the theme author
        // something. A rule that is accepted and silently does nothing tells them their CSS is fine
        // and their eyes are wrong.
        //
        // Four of the twenty-one elements were in exactly that state - menu-bar, luna-switch,
        // dropdown and tabs, every one of them a control that pins StyleKeyOverride and is therefore
        // styled by Avalonia AS the stock control it borrows from. `CssThemeTests` had a test that a
        // rule "really applies", and it passed throughout, because it was written against
        // section-header: a TextBlock, with no overridden style key, which is the case that works.
        //
        // So this sweep takes its subjects from CssTheme.ElementNames rather than from a list
        // somebody keeps. A new element is in it the moment it is published, and there is no way to
        // add a vocabulary entry without also proving it reaches something.
        public static TheoryData<string> Vocabulary()
        {
            var data = new TheoryData<string>();
            foreach (string name in CssTheme.ElementNames) data.Add(name);
            return data;
        }

        // EVERY PART, FOUND THE SAME WAY THE ELEMENTS ARE. §30.5 named this as the hole left in the
        // sweep above: it proved each *element* reaches its control, and said nothing about
        // `card .header` or `console-pane .output`. A part selector is strictly more fragile than an
        // element one - it adds Template(), a type, and a PART_ name, and any of the three can be
        // wrong on its own - so the case with more ways to fail was the one going unchecked.
        //
        // Fourteen parts across seven elements. Exactly one of them, `meter-row .bar`, had a test.
        //
        // SWEPT FOR BOTH COLOURS, because one is not a proxy for the other: a part whose template
        // sets Foreground and not Background fails `color` while passing `background`, and the first
        // draft of this sweep tested only `background` and would have said so. The pairs are
        // generated by ASKING THE PARSER rather than by a table - it already refuses a property a
        // part cannot take ("'color' does not apply to 'split-pane .rule'", since a Border has no
        // Foreground), so a combination it rejects is not a defect and is never emitted as a case.
        // That also means the vocabulary's own validation is what decides the subjects, and a part
        // that gains a property gains its case here on the same commit.
        private static readonly string[] Colours = { "background", "color" };

        // ONE EXEMPTION, AND IT EXPIRES. §28.2's Exempt table is the model: a case that cannot pass
        // gets an entry with the reason, never a quiet deletion from the sweep.
        //
        // This one is a real limit rather than a defect. MeterRow's bar takes its colour from three
        // state styles - :nominal, :busy, :hot - and a selector carrying a pseudo-class binds at
        // BindingPriority.StyleTrigger, which outranks Style. Measured: after applying
        // `meter-row .bar { color: … }` the bar is LimeGreen at priority StyleTrigger. No stateless
        // rule can win, and it should not - a meter whose colour stopped tracking its load would be
        // a worse control. A host that wants other colours names the states, which
        // A_rule_can_reach_a_state_and_a_template_part proves works.
        private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
        {
            ["meter-row .bar|color"] =
                "the bar's colour comes from the :nominal/:busy/:hot styles, which bind at "
                + "StyleTrigger priority and outrank any stateless rule. Name the state instead: "
                + "`meter-row.busy .bar { color: … }`.",
        };

        public static TheoryData<string, string> VocabularyParts()
        {
            var data = new TheoryData<string, string>();
            foreach (string element in CssTheme.ElementNames)
            {
                foreach (string part in CssTheme.PartsOf(element))
                {
                    string selector = $"{element} .{part}";
                    foreach (string property in Colours)
                    {
                        if (Exempt.ContainsKey($"{selector}|{property}")) continue;
                        if (CssTheme.Parse($"{selector} {{ {property}: #FF00FF; }}").Styles.Count > 0)
                            data.Add(selector, property);
                    }
                }
            }

            return data;
        }

        public static TheoryData<string, string> Exemptions()
        {
            var data = new TheoryData<string, string>();
            foreach (string key in Exempt.Keys)
            {
                int bar = key.IndexOf('|');
                data.Add(key[..bar], key[(bar + 1)..]);
            }

            return data;
        }

        // AN EXEMPTION THAT NO LONGER APPLIES IS A LIE IN A TABLE, and the only thing worse than an
        // untested case is one everybody believes is untestable. So each exemption asserts that its
        // limitation is STILL REAL: make the stateless rule win somehow and this turns red saying
        // the entry should be deleted, which is the opposite of how an exemption list usually rots.
        [Theory]
        [MemberData(nameof(Exemptions))]
        public Task An_exemption_is_still_needed(string selector, string property) => UiTest.Run(() =>
        {
            string element = selector[..selector.IndexOf(' ')];
            var control = (Control)Activator.CreateInstance(ControlFor(element))!;
            Configure(control);

            WriteCss("Exempt", $"{selector} {{ {property}: #FF00FF; }}");

            var window = new ToolWindow { Width = 400, Height = 240, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(LunaTheme.Apply("Exempt"));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            string avalonia = property == "background" ? "Background" : "Foreground";
            bool reached = control.GetSelfAndVisualDescendants().Any(v => IsSentinel(v, avalonia));

            Assert.False(reached,
                $"`{selector} {{ {property}: … }}` NOW WORKS, and is listed as exempt in CssThemeTests "
                + $"with this reason: {Exempt[$"{selector}|{property}"]}\n"
                + "Delete the entry - the sweep will pick the case up on its own. See docs/LunaP.md §39.4.");

            window.Close();
        });

        // A [Theory] with no cases is a pass, so the sweep is required to have subjects - the same
        // trap StyleKeyTests.The_sweep_has_subjects covers (§30.6).
        [Fact]
        public void The_part_sweep_has_subjects() =>
            Assert.True(VocabularyParts().Count >= 24,
                $"The part sweep found only {VocabularyParts().Count} cases. There were 26 when it was "
                + "written - 14 parts times two colours, less `split-pane .rule { color }` which the "
                + "parser refuses because a Border has no Foreground, and less the one exemption. If "
                + "parts were removed that is fine and this number moves; if the vocabulary query "
                + "broke, this is the only thing that would say so.");

        [Theory]
        [MemberData(nameof(VocabularyParts))]
        public Task Every_part_in_the_vocabulary_can_actually_be_styled(string selector, string property) => UiTest.Run(() =>
        {
            string element = selector[..selector.IndexOf(' ')];
            var control = (Control)Activator.CreateInstance(ControlFor(element))!;
            Configure(control);

            WriteCss("Parts", $"{selector} {{ {property}: #FF00FF; }}");

            var window = new ToolWindow { Width = 400, Height = 240, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(LunaTheme.Apply("Parts"), $"the theme naming '{selector}' did not load.");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // ASKED OF THE TREE RATHER THAN OF A NAMED PART, deliberately. Looking up PART_Header and
            // reading it back would prove the test can find that part, not that the SELECTOR can -
            // and a selector naming the wrong type or the wrong name is exactly the defect being
            // swept for. So the question is the one the rule actually claims: after applying it,
            // does anything inside this control carry the colour?
            string avalonia = property == "background" ? "Background" : "Foreground";
            bool reached = control.GetSelfAndVisualDescendants().Any(v => IsSentinel(v, avalonia));

            Assert.True(reached,
                $"`{selector} {{ {property}: #FF00FF; }}` compiled without a warning and coloured nothing "
                + $"inside {control.GetType().Name}. Two ways that happens, and they need different fixes: "
                + "the selector reaches nothing (Template() + a type + a PART_ name, and the type in the "
                + $"vocabulary must be the part's REAL type), or it reaches the part and loses - a {avalonia} "
                + "written as an attribute inside a ControlTemplate is at priority Template, which outranks "
                + "Style, so it must be a Setter in a style targeting the part instead. See docs/LunaP.md §39.");

            window.Close();
        });

        // Only what a part needs in order to exist at all. Card collapses its header row when there
        // is no header (§26.9), so `card .header` has nothing to colour until it is given one - that
        // is the control behaving correctly, not the selector failing, and the sweep would otherwise
        // report it as a defect.
        private static void Configure(Control control)
        {
            switch (control)
            {
                case Card card:
                    card.Header = "Header";
                    card.Content = new TextBlock { Text = "Body" };
                    break;
                case SidePanel panel:
                    panel.Title = "Panel";
                    panel.Content = new TextBlock { Text = "Body" };
                    break;
                case SplitPane pane:
                    pane.First = new TextBlock { Text = "First" };
                    pane.Second = new TextBlock { Text = "Second" };
                    break;
                case MeterRow row:
                    row.Label = "S-CPU";
                    row.Percent = 50;
                    break;
            }
        }

        private static bool IsSentinel(Visual visual, string propertyName)
        {
            AvaloniaProperty? property = AvaloniaPropertyRegistry.Instance.FindRegistered(visual, propertyName);
            return property is not null
                && visual.GetValue(property) is ISolidColorBrush brush
                && brush.Color == Color.Parse("#FF00FF");
        }

        // The same spelling CssTheme uses to name an element after its control, so the two halves
        // are matched by the rule rather than by a table that could disagree with it.
        private static string Kebab(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0) sb.Append('-');
                sb.Append(char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }

        private static Type ControlFor(string element) =>
            typeof(SectionHeader).Assembly.GetTypes()
                .Where(t => t.Namespace == "EmuSen.LunaP.Controls" && t.IsPublic && !t.IsAbstract)
                .Where(t => !t.IsGenericTypeDefinition)
                .FirstOrDefault(t => Kebab(t.Name) == element)
            ?? throw new InvalidOperationException(
                $"'{element}' is published in CssTheme.ElementNames but names no control in the kit.");

        [Theory]
        [MemberData(nameof(Vocabulary))]
        public Task Every_element_in_the_vocabulary_can_actually_be_styled(string element) => UiTest.Run(() =>
        {
            Type type = ControlFor(element);
            var control = (Control)Activator.CreateInstance(type)!;

            WriteCss("Vocab", element + " { color: #FF00FF; }");

            var window = new ToolWindow { Width = 400, Height = 200, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Resolved the way CssTheme resolves it, so a control whose Foreground comes from
            // TextElement rather than TemplatedControl is read through the same lookup that set it.
            AvaloniaProperty foreground = AvaloniaPropertyRegistry.Instance.FindRegistered(control, "Foreground")!;

            Assert.True(LunaTheme.Apply("Vocab"), $"the theme naming '{element}' did not load.");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var brush = control.GetValue(foreground) as ISolidColorBrush;
            Assert.True(brush is not null && brush.Color == Color.Parse("#FF00FF"),
                $"`{element} {{ color: #FF00FF; }}` compiled without a warning and changed nothing on "
                + $"{type.Name}, whose Foreground is still {control.GetValue(foreground)}. A rule that is "
                + "accepted and does nothing is worse than one that is refused. If this control pins "
                + "StyleKeyOverride, its vocabulary entry needs the style key AND the class it adds to "
                + "itself - a type selector matches the style key, not the runtime type. See docs/LunaP.md §30.");

            window.Close();
        });
    }
}
