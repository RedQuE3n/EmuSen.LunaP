using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // THE STANDARD §47.5 CHOSE, MADE INTO AN ASSERTION - see docs/LunaP.md §48.
    //
    // LunaP includes <FluentTheme /> and always will: third-party Avalonia controls assume it is
    // present, and removing it to own the whole look would make this toolkit incompatible with the
    // ecosystem it is trying to be usable inside. The consequence is that every stock control an
    // application reaches for - a TextBox, a CheckBox, a Slider - works, and renders in FLUENT's
    // palette rather than LunaP's. An application built mostly of form controls comes out mostly
    // Fluent, and the join shows in exactly the places a reader's eye goes: accents, borders, and
    // the focus ring.
    //
    // §47.5 rejected "the gallery must contain a plausible office-app screen" as a standard, because
    // "plausible" is judged by whoever is meeting it. This is what replaced it, and the whole reason
    // it is worth having is that it is mechanical: show every form control in a live headless
    // application and require the colours it actually resolves to come from LunaPalette.
    //
    // WHAT "TRACES TO LunaPalette" MEANS HERE, since a loose reading would make this vacuous. Every
    // colour a control resolves for its background, foreground or border must be either fully
    // transparent - a control that paints nothing is not painting Fluent's grey - or a colour that
    // appears in LunaPalette. Reading the RESOLVED brush rather than the style that set it is the
    // point: a Setter that names the right resource and never matches is the §5.5 symptom, and it
    // reads identically to a control that was styled correctly right up until somebody looks.
    public class FormControlTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FormControlTests).GetTypeInfo().Assembly);

        // Every colour LunaPalette declares, by value. Read by reflection rather than listed, so a
        // palette that gains a colour does not need this file edited.
        private static HashSet<Color> Palette() =>
            typeof(LunaPalette)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.GetValue(null))
                .OfType<ISolidColorBrush>()
                .Select(b => b.Color)
                .ToHashSet();

        // The form controls an office application is mostly made of. Named rather than reflected,
        // because the subject is "what a consumer reaches for", which is not a property of any
        // assembly - and because a name here is a decision to support that control.
        //
        // THE ARRAY IS PUBLIC AND THE TheoryData IS BUILT FROM IT, rather than the list living
        // inside the factory. GalleryRenderTests reads this to require that everything swept here is
        // also on screen somewhere, and two hand-kept lists would drift the first time somebody
        // added a control - which is exactly the failure that left eight of these nine out of the
        // gallery for the length of §48.
        public static readonly string[] KindNames =
        {
            nameof(TextBox), nameof(CheckBox), nameof(RadioButton), nameof(Slider),
            nameof(NumericUpDown), nameof(ComboBox), nameof(ProgressBar), nameof(ToggleSwitch),
            nameof(CalendarDatePicker),
        };

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();
            foreach (string name in KindNames) data.Add(name);
            return data;
        }

        private static Control Build(string kind) => kind switch
        {
            nameof(TextBox) => new TextBox { Text = "value" },
            nameof(CheckBox) => new CheckBox { Content = "Enabled", IsChecked = true },
            nameof(RadioButton) => new RadioButton { Content = "First", IsChecked = true },
            nameof(Slider) => new Slider { Minimum = 0, Maximum = 100, Value = 40, Width = 120 },
            nameof(NumericUpDown) => new NumericUpDown { Value = 3, Width = 120 },
            nameof(ComboBox) => new ComboBox { ItemsSource = new[] { "one", "two" }, SelectedIndex = 0 },
            nameof(ProgressBar) => new ProgressBar { Minimum = 0, Maximum = 100, Value = 60, Width = 120 },
            nameof(ToggleSwitch) => new ToggleSwitch { IsChecked = true },
            nameof(CalendarDatePicker) => new CalendarDatePicker { SelectedDate = new DateTime(2026, 8, 13) },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No factory for this control."),
        };

        private static IEnumerable<(Visual Visual, string Property, Color Colour)> Painted(Visual root)
        {
            foreach (Visual visual in root.GetSelfAndVisualDescendants())
            {
                if (visual is not Control control) continue;

                foreach ((string name, IBrush? brush) in new (string, IBrush?)[]
                {
                    ("Background", (control as TemplatedControl)?.Background ?? (control as Panel)?.Background ?? (control as Border)?.Background),
                    ("Foreground", (control as TemplatedControl)?.Foreground ?? (control as TextBlock)?.Foreground),
                    ("BorderBrush", (control as TemplatedControl)?.BorderBrush ?? (control as Border)?.BorderBrush),
                })
                {
                    if (brush is ISolidColorBrush { Color: { A: > 0 } colour }) yield return (visual, name, colour);
                }
            }
        }

        // THE SWEEP. Every colour every form control actually paints with must come from the
        // palette, or the seam §21.2 caught from the other side is still open.
        [Theory]
        [MemberData(nameof(Kinds))]
        public Task A_form_control_paints_only_in_the_palette(string kind) => Session.Dispatch(() =>
        {
            HashSet<Color> palette = Palette();

            Control control = Build(kind);
            var window = new ToolWindow { Width = 420, Height = 260, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);

            (Visual Visual, string Property, Color Colour)[] strays = Painted(control)
                .Where(p => !palette.Contains(p.Colour))
                .ToArray();

            window.Close();

            Assert.True(strays.Length == 0,
                $"{kind} paints {strays.Length} colour(s) that are not in LunaPalette, so it renders in "
                + "FluentTheme's palette rather than this toolkit's:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, strays
                    .Select(p => $"  {p.Visual.GetType().Name}.{p.Property} = {p.Colour}")
                    .Distinct())
                + Environment.NewLine
                + "Style it in Theme/Controls/FormControls.axaml and list that file in LunaTheme.axaml. "
                + "See docs/LunaP.md §48.");
        }, default);

        // The sweep is only worth anything if it has subjects, and a TheoryData that quietly emptied
        // would report a pass. §26.11 caught the same shape of hollow guard.
        [Fact]
        public void The_sweep_has_subjects()
        {
            int count = Kinds().Cast<object>().Count();
            Assert.True(count >= 9, $"Only {count} form controls are swept; there were nine when §48 was written.");
        }
    }
}
