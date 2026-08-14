using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // GETTING PIXELS IN WITHOUT COPYING THEM TWICE, AND SCALING THEM BY A WHOLE NUMBER - see
    // docs/LunaP.md §53.
    //
    // The three SetFrame overloads exist to remove copies, and the only thing that makes them worth
    // having is that they all put the SAME pixels in the bitmap. An overload that quietly wrote a
    // skewed or truncated image would look like an optimisation and be a corruption, so every one of
    // them is checked against the actual bytes rather than against "it did not throw".
    //
    // Read back with Marshal.Copy in the native-to-managed direction, which is safe and needs no
    // unsafe block here - the toolkit allows one for exactly one method and the harness needs none.
    public class FrameBufferTests
    {
        // A frame whose bytes are all different, so a copy that drops, doubles or shifts a row is
        // visible in the comparison. A flat colour would survive most of those.
        private static byte[] Ramp(int width, int height)
        {
            var rgba = new byte[width * height * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7 % 251);
            return rgba;
        }

        private static byte[] ReadBack(RgbaImageView view, int width, int height)
        {
            var bitmap = (WriteableBitmap)view.Source!;
            var got = new byte[width * height * 4];

            using ILockedFramebuffer fb = bitmap.Lock();
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(fb.Address + (y * fb.RowBytes), got, y * width * 4, width * 4);
            }

            return got;
        }

        [Fact]
        public Task An_array_frame_arrives_intact() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            byte[] frame = Ramp(37, 11);

            view.SetFrame(frame, 37, 11);

            Assert.Equal(frame, ReadBack(view, 37, 11));
        });

        // The overload that removes a copy has to produce the identical bitmap, or it is not an
        // optimisation of anything.
        [Fact]
        public Task A_span_frame_arrives_identical_to_the_array_one() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            byte[] frame = Ramp(37, 11);

            view.SetFrame(new ReadOnlySpan<byte>(frame), 37, 11);

            Assert.Equal(frame, ReadBack(view, 37, 11));
        });

        // A SLICE, which is the case the span overload exists for: a caller holding one big buffer
        // of several frames hands over a window into it and copies nothing beforehand.
        [Fact]
        public Task A_span_may_be_a_slice_of_a_larger_buffer() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            byte[] frame = Ramp(16, 4);
            var padded = new byte[frame.Length + 500];
            frame.CopyTo(padded, 300);

            view.SetFrame(new ReadOnlySpan<byte>(padded, 300, frame.Length), 16, 4);

            Assert.Equal(frame, ReadBack(view, 16, 4));
        });

        [Fact]
        public Task A_frame_straight_from_unmanaged_memory_arrives_intact() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            byte[] frame = Ramp(24, 9);

            nint block = Marshal.AllocHGlobal(frame.Length);
            try
            {
                Marshal.Copy(frame, 0, block, frame.Length);
                view.SetFrame(block, 24, 9);
            }
            finally
            {
                Marshal.FreeHGlobal(block);
            }

            Assert.Equal(frame, ReadBack(view, 24, 9));
        });

        // ODD WIDTHS ON PURPOSE. The copy honours ILockedFramebuffer.RowBytes rather than assuming
        // the stride is width*4, and a width that is not a nice round number is where a padding
        // backend would differ. Measured unpadded on this platform (§53), so this is guarding the
        // assumption rather than reproducing a failure - which is the point of not making it.
        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 7)]
        [InlineData(13, 5)]
        [InlineData(163, 3)]
        public Task A_frame_of_any_width_arrives_row_for_row(int width, int height) => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            byte[] frame = Ramp(width, height);

            view.SetFrame(frame, width, height);

            Assert.Equal(frame, ReadBack(view, width, height));
        });

        [Fact]
        public Task A_short_buffer_clears_rather_than_reading_past_it() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            view.SetFrame(Ramp(8, 8), 8, 8);
            Assert.NotNull(view.Source);

            view.SetFrame(new byte[4], 8, 8);

            Assert.Null(view.Source);
        });

        [Fact]
        public Task A_short_span_clears_too() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            view.SetFrame(Ramp(8, 8), 8, 8);

            view.SetFrame(new ReadOnlySpan<byte>(new byte[4]), 8, 8);

            Assert.Null(view.Source);
        });

        // The pointer overload cannot check a length, so a zero address is the one thing it CAN
        // refuse - and refusing it is what stops a null frame from being a segfault.
        [Fact]
        public Task A_zero_address_clears_rather_than_dereferencing_it() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();
            view.SetFrame(Ramp(8, 8), 8, 8);

            view.SetFrame(0, 8, 8);

            Assert.Null(view.Source);
        });

        // The bitmap is reused across frames of the same size, which is the whole reason this
        // control exists rather than an Image (§5.3). The overloads must not have broken it.
        [Fact]
        public Task The_bitmap_is_reused_when_the_size_is_unchanged() => UiTest.Run(() =>
        {
            var view = new RgbaImageView();

            view.SetFrame(Ramp(20, 10), 20, 10);
            object first = view.Source!;

            view.SetFrame(new ReadOnlySpan<byte>(Ramp(20, 10)), 20, 10);
            Assert.Same(first, view.Source);

            view.SetFrame(Ramp(21, 10), 21, 10);
            Assert.NotSame(first, view.Source);
        });

        private static Image Realised(RgbaImageView view, double width, double height, out Window window)
        {
            window = new ToolWindow { Width = width + 40, Height = height + 40, Content = view };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return view.GetVisualDescendants().OfType<Image>().Single();
        }

        // INTEGER SCALING, AND THE NUMBER THAT MADE IT WORTH BUILDING. A 160x144 frame in a 667-wide
        // box is 4.17x, which nearest-neighbour renders with most rows 4 device pixels tall and every
        // sixth one 5. Rounded down to 4x, every row is 4.
        [Fact]
        public Task An_integer_scaled_frame_is_a_whole_number_of_pixels_per_pixel() => UiTest.Run(() =>
        {
            var view = new RgbaImageView { Stretch = Stretch.Uniform, IntegerScale = true, Width = 667, Height = 600 };
            view.SetFrame(Ramp(160, 144), 160, 144);

            Image image = Realised(view, 667, 600, out Window window);

            Assert.Equal(640, image.Width);   // 160 * 4, not 160 * 4.17
            Assert.Equal(576, image.Height);  // 144 * 4
            Assert.Equal(0, image.Width % 160);
            Assert.Equal(0, image.Height % 144);

            window.Close();
        });

        [Fact]
        public Task Integer_scaling_off_leaves_the_image_to_its_stretch() => UiTest.Run(() =>
        {
            var view = new RgbaImageView { Stretch = Stretch.Uniform, Width = 667, Height = 600 };
            view.SetFrame(Ramp(160, 144), 160, 144);

            Image image = Realised(view, 667, 600, out Window window);

            Assert.True(double.IsNaN(image.Width));
            Assert.Equal(Stretch.Uniform, image.Stretch);

            window.Close();
        });

        // A frame bigger than the box still shows at 1:1 rather than vanishing: a zero-times scale
        // is not a picture.
        [Fact]
        public Task A_frame_larger_than_its_box_never_scales_below_one() => UiTest.Run(() =>
        {
            var view = new RgbaImageView { Stretch = Stretch.Uniform, IntegerScale = true, Width = 80, Height = 60 };
            view.SetFrame(Ramp(160, 144), 160, 144);

            Image image = Realised(view, 80, 60, out Window window);

            Assert.Equal(160, image.Width);
            Assert.Equal(144, image.Height);

            window.Close();
        });

        // The factor has to follow the control being resized, or a window the user drags wider keeps
        // whatever scale it happened to open at.
        [Fact]
        public Task The_factor_is_recomputed_when_the_control_is_resized() => UiTest.Run(() =>
        {
            var view = new RgbaImageView { Stretch = Stretch.Uniform, IntegerScale = true, Width = 340, Height = 320 };
            view.SetFrame(Ramp(160, 144), 160, 144);

            Image image = Realised(view, 340, 320, out Window window);
            Assert.Equal(320, image.Width); // 2x

            view.Width = 667;
            view.Height = 600;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(640, image.Width); // 4x

            window.Close();
        });
    }
}
