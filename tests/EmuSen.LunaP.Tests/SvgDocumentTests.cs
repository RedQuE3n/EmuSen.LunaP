using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // SvgDocument and SvgPicture - see docs/LunaP.md §99: the subset, one rule per case, and whole-file refusal.
    public class SvgDocumentTests
    {
        private const string Ns = "xmlns='http://www.w3.org/2000/svg'";

        private static RenderedFrame Render(string body, string attributes = "viewBox='0 0 100 100'", int size = 100)
        {
            SvgDocument doc = SvgDocument.Parse($"<svg {Ns} {attributes}>{body}</svg>");
            Assert.False(doc.IsRefused, string.Join("; ", doc.Refusals));
            return Draw(size, size, dc => doc.Draw(dc, new Rect(0, 0, size, size)));
        }

        private static SvgDocument Refused(string body)
        {
            SvgDocument doc = SvgDocument.Parse($"<svg {Ns} viewBox='0 0 10 10'>{body}</svg>");
            Assert.True(doc.IsRefused);
            return doc;
        }

        [Fact]
        public Task Absolute_relative_implicit_and_packed_path_data_draw_the_same_square() => UiTest.Run(() =>
        {
            int a = Covered(Render("<path d='M10 10 H90 V90 H10 Z'/>"));
            int b = Covered(Render("<path d='m10,10l80,0 0,80-80,0z'/>"));
            int c = Covered(Render("<path d='M10 10L90 10L90 90L10 90Z'/>"));
            Assert.InRange(a, 6300, 6500);
            Assert.Equal(a, b);
            Assert.Equal(a, c);
        });

        [Fact]
        public Task Arcs_with_packed_flags_and_curves_draw_their_areas() => UiTest.Run(() =>
        {
            int circle = Covered(Render("<path d='M10 50a40 40 0 1 0 80 0a40 40 0 1 0-80 0z'/>"));
            Assert.InRange(circle, (int)(Math.PI * 1600 * 0.98), (int)(Math.PI * 1600 * 1.02));
            int packed = Covered(Render("<path d='M10 50a40 40 0 10 80 0a40 40 0 10-80 0z'/>"));
            Assert.Equal(circle, packed);
            int shape = Covered(Render("<circle cx='50' cy='50' r='40'/>"));
            Assert.InRange(shape, circle - 40, circle + 40);
            int cubic = Covered(Render("<path d='M10 90 C10 10 90 10 90 90 S 10 170 10 90Z'/>"));
            Assert.InRange(cubic, 3000, 6400);
        });

        [Fact]
        public Task Transforms_compose_with_the_first_written_applied_last() => UiTest.Run(() =>
        {
            RenderedFrame translatedThenScaled = Render("<rect width='10' height='10' fill='red' transform='translate(50 0) scale(2)'/>");
            Assert.Equal(Colors.Red, At(translatedThenScaled, 65, 15));
            Assert.Equal(0, At(translatedThenScaled, 5, 5).A);
            RenderedFrame scaledThenTranslated = Render("<rect width='10' height='10' fill='red' transform='scale(2) translate(10 0)'/>");
            Assert.Equal(Colors.Red, At(scaledThenTranslated, 35, 15));
            Assert.Equal(0, At(scaledThenTranslated, 15, 15).A);
            RenderedFrame rotated = Render("<g transform='rotate(90 50 50)'><rect x='0' y='45' width='50' height='10' fill='blue'/></g>");
            Assert.Equal(Colors.Blue, At(rotated, 50, 20));
            Assert.Equal(0, At(rotated, 20, 50).A);
            RenderedFrame matrix = Render("<rect width='10' height='10' fill='lime' transform='matrix(1 0 0 1 30 40)'/>");
            Assert.Equal(Colors.Lime, At(matrix, 35, 45));
        });

        [Fact]
        public Task Fill_rule_evenodd_leaves_the_hole_nonzero_fills() => UiTest.Run(() =>
        {
            const string d = "M10 10H90V90H10Z M30 30H70V70H30Z";
            Assert.Equal(0, At(Render($"<path fill-rule='evenodd' d='{d}'/>"), 50, 50).A);
            Assert.Equal(255, At(Render($"<path d='{d}'/>"), 50, 50).A);
            Assert.Equal(0, At(Render($"<path style='fill-rule:evenodd' d='{d}'/>"), 50, 50).A);
        });

        [Fact]
        public Task Colours_opacity_and_inheritance() => UiTest.Run(() =>
        {
            RenderedFrame f = Render("<g fill='#0f0'><rect width='50' height='50'/><rect x='50' width='50' height='50' fill='rgb(0,0,255)'/></g>"
                + "<rect y='50' width='50' height='50' fill='#ff0000' fill-opacity='0.5'/><rect x='50' y='50' width='50' height='50' fill='red' opacity='0.25'/>");
            Assert.Equal(Colors.Lime, At(f, 25, 25));
            Assert.Equal(Colors.Blue, At(f, 75, 25));
            Assert.InRange(At(f, 25, 75).A, 126, 129);
            Assert.InRange(At(f, 75, 75).A, 62, 66);
        });

        [Fact]
        public Task Strokes_draw_outside_a_fill_of_none() => UiTest.Run(() =>
        {
            RenderedFrame f = Render("<rect x='20' y='20' width='60' height='60' fill='none' stroke='red' stroke-width='10'/>");
            Assert.Equal(Colors.Red, At(f, 20, 50));
            Assert.Equal(Colors.Red, At(f, 16, 50));
            Assert.Equal(0, At(f, 50, 50).A);
        });

        [Fact]
        public Task Style_sheets_cascade_by_specificity_below_the_style_attribute() => UiTest.Run(() =>
        {
            const string sheet = "<style>rect.a{fill:blue} #b{fill:lime} .c,.d{fill:yellow} .a{fill:red}</style>";
            RenderedFrame f = Render(sheet
                + "<rect class='a' width='50' height='50' fill='black'/>"
                + "<rect id='b' class='a' x='50' width='50' height='50'/>"
                + "<rect class='a' y='50' width='50' height='50' style='fill:white'/>"
                + "<circle class='d' cx='75' cy='75' r='20'/>");
            Assert.Equal(Colors.Blue, At(f, 25, 25));
            Assert.Equal(Colors.Lime, At(f, 75, 25));
            Assert.Equal(Colors.White, At(f, 25, 75));
            Assert.Equal(Colors.Yellow, At(f, 75, 75));
        });

        [Fact]
        public Task Linear_gradients_in_both_units_with_transforms_and_inherited_stops() => UiTest.Run(() =>
        {
            const string defs = "<defs>"
                + "<linearGradient id='g'><stop offset='0' stop-color='#f00'/><stop offset='1' style='stop-color:#00f'/></linearGradient>"
                + "<linearGradient id='v' href='#g' x2='0' y2='1'/>"
                + "<linearGradient id='u' gradientUnits='userSpaceOnUse' x1='0' y1='0' x2='100' y2='0' xlink:href='#g' xmlns:xlink='http://www.w3.org/1999/xlink'/>"
                + "<linearGradient id='t' href='#g' gradientTransform='rotate(90 0.5 0.5)'/>"
                + "</defs>";
            RenderedFrame across = Render(defs + "<rect x='0' y='0' width='100' height='50' fill='url(#g)'/>");
            Assert.True(Near(Colors.Red, At(across, 1, 25), 10));
            Assert.True(Near(Colors.Blue, At(across, 98, 25), 10));
            RenderedFrame down = Render(defs + "<rect width='100' height='100' fill='url(#v)'/>");
            Assert.True(Near(Colors.Red, At(down, 50, 1), 10));
            Assert.True(Near(Colors.Blue, At(down, 50, 98), 10));
            RenderedFrame user = Render(defs + "<rect x='50' width='50' height='50' fill='url(#u)'/>");
            Color mid = At(user, 51, 25);
            Assert.InRange(mid.R, 110, 145);
            RenderedFrame turned = Render(defs + "<rect width='100' height='100' fill='url(#t)'/>");
            Assert.True(Near(Colors.Red, At(turned, 50, 1), 10));
        });

        [Fact]
        public Task Clip_paths_clip_in_the_referencing_elements_user_space() => UiTest.Run(() =>
        {
            RenderedFrame f = Render("<defs><clipPath id='c'><rect width='50' height='100'/></clipPath></defs>"
                + "<rect width='100' height='100' fill='red' clip-path='url(#c)'/>");
            Assert.Equal(Colors.Red, At(f, 25, 50));
            Assert.Equal(0, At(f, 75, 50).A);
        });

        [Fact]
        public Task The_viewBox_maps_by_preserveAspectRatio() => UiTest.Run(() =>
        {
            const string square = "<rect width='10' height='10' fill='red'/>";
            RenderedFrame mid = Render(square, "viewBox='0 0 10 20'");
            Assert.Equal(Colors.Red, At(mid, 50, 25));
            Assert.Equal(0, At(mid, 10, 25).A);
            RenderedFrame left = Render(square, "viewBox='0 0 10 20' preserveAspectRatio='xMinYMin meet'");
            Assert.Equal(Colors.Red, At(left, 10, 25));
            RenderedFrame stretched = Render(square, "viewBox='0 0 10 20' preserveAspectRatio='none'");
            Assert.Equal(Colors.Red, At(stretched, 90, 45));
            SvgDocument sized = SvgDocument.Parse($"<svg {Ns} width='20pt' height='3in' viewBox='-5 -5 10 10'/>");
            Assert.Equal(new Size(20 * 4.0 / 3, 288), sized.Size);
            Assert.Equal(new Rect(-5, -5, 10, 10), sized.ViewBox);
        });

        [Theory]
        [InlineData("<text x='0' y='5'>t</text>", "the element <text>")]
        [InlineData("<image href='x.png' width='5' height='5'/>", "the element <image>")]
        [InlineData("<script>1</script>", "the element <script>")]
        [InlineData("<defs><filter id='f'/></defs>", "the element <filter>")]
        [InlineData("<rect width='5' height='5' filter='url(#f)'/>", "the property filter")]
        [InlineData("<rect width='5' height='5' style='mask:url(#m)'/>", "the property mask")]
        [InlineData("<defs><radialGradient id='r'/></defs><rect width='5' height='5' fill='url(#r)'/>", "the element <radialGradient>")]
        [InlineData("<style>g rect{fill:red}</style>", "style sheet: the selector \"g rect\"")]
        [InlineData("<use href='#x'/>", "the element <use>")]
        [InlineData("<rect width='50%' height='5'/>", "a percentage in width")]
        public Task Anything_outside_the_subset_refuses_the_whole_document(string body, string reason) => UiTest.Run(() =>
        {
            SvgDocument doc = Refused("<rect width='10' height='10' fill='red'/>" + body);
            Assert.Contains(reason, doc.Refusals);
            RenderedFrame f = Draw(20, 20, dc => doc.Draw(dc, new Rect(0, 0, 20, 20)));
            Assert.Equal(0, Covered(f, 1));
        });

        [Fact]
        public Task Unreadable_input_is_a_refused_document_not_an_exception() => UiTest.Run(() =>
        {
            Assert.True(SvgDocument.Parse("<svg").IsRefused);
            Assert.True(SvgDocument.Parse("<html/>").IsRefused);
            Assert.True(SvgDocument.Load("/nonexistent/lunap/icon.svg").IsRefused);
            Assert.False(SvgDocument.Parse($"<svg {Ns}><title>t</title><metadata/><desc/></svg>").IsRefused);
            Assert.False(SvgDocument.Parse($"<svg {Ns} xmlns:sodipodi='http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd'><sodipodi:namedview/></svg>").IsRefused);
        });

        [Fact]
        public Task SvgPicture_draws_the_document_by_its_aspect_or_a_crossed_box() => UiTest.Run(() =>
        {
            var picture = new SvgPicture { Document = SvgDocument.Parse($"<svg {Ns} viewBox='0 0 20 10'><rect width='20' height='10' fill='#00ff00'/></svg>") };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetMaxSize(picture, new Size(1, 1));
            canvas.Children.Add(picture);
            ToolWindow window = Show(canvas, 200, 200);
            Assert.Equal(new Size(200, 100), picture.Bounds.Size);
            Assert.Equal(Colors.Lime, At(Frame(window), 100, 50));

            picture.Document = SvgDocument.Parse($"<svg {Ns}><text>no</text></svg>");
            Assert.Equal(EmuSen.LunaP.Theme.LunaPalette.Error.Color, At(Frame(window), 8, 8));
            window.Close();
        });
    }
}
