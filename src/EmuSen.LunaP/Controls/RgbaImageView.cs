using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // Shows a raw RGBA buffer, reusing its bitmap across frames - the one surviving implementation of three - see docs/LunaP.md §5.3.
    /// <summary>Displays a raw RGBA pixel buffer, reusing its bitmap across frames.</summary>
    public class RgbaImageView : TemplatedControl
    {
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<RgbaImageView, Stretch>(nameof(Stretch), Stretch.None);

        public static readonly DirectProperty<RgbaImageView, IImage?> SourceProperty =
            AvaloniaProperty.RegisterDirect<RgbaImageView, IImage?>(nameof(Source), o => o.Source);

        // WHY A WHOLE-NUMBER FACTOR IS A SETTING AND NOT JUST GOOD TASTE - see docs/LunaP.md §53.1.
        //
        // Nearest-neighbour at a fractional scale does not enlarge pixels evenly, it DUPLICATES SOME
        // AND NOT OTHERS. A 160x144 frame in a 667-pixel-wide box is 4.17x, so most source rows land
        // 4 device pixels tall and every sixth one lands 5. On a static image that is a faint
        // irregular banding; on anything that scrolls or moves, the tall rows travel through the
        // picture and it shimmers.
        //
        // Rounding the factor down to 4x and centring what is left is what every emulator frontend
        // ends up doing, and it is why this is opt-in rather than automatic: it trades screen area
        // for evenness, and a tile viewer in a small panel would rather have the area.
        public static readonly StyledProperty<bool> IntegerScaleProperty =
            AvaloniaProperty.Register<RgbaImageView, bool>(nameof(IntegerScale));

        private WriteableBitmap? _bitmap;
        private Image? _image;
        private int _width;
        private int _height;

        // DEFAULTS TO None, WHICH DOES NOT SCALE AT ALL - one bitmap pixel to one layout pixel, and
        // the frame cropped by the control's bounds rather than fitted to them. That is what a
        // pixel-accurate view of a framebuffer wants, and it is why the template pins
        // BitmapInterpolationMode to None beside it: scaling and smoothing are the two ways a
        // framebuffer stops being the thing that was rendered.
        //
        // This summary said "defaults to preserving the aspect ratio" until §52. That describes
        // Uniform, which is a different member and a different picture - it fits the frame to the
        // control and letterboxes it. The value was always right; the sentence named the wrong one,
        // and it is a /// summary, so it was what a consumer's IntelliSense showed.
        /// <summary>How the frame fills the control. Defaults to Stretch.None, which does not scale: one bitmap pixel to one layout pixel, which is what a pixel-accurate view of a framebuffer needs.</summary>
        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        // Only has an effect where there is room to scale, which means a Stretch that scales at all -
        // under the default Stretch.None the frame is already at 1:1 and there is nothing to round.
        /// <summary>Whether the frame is scaled by a whole number of pixels only, rounding down and centring the result. Off by default.</summary>
        /// <remarks>
        /// Prevents the uneven pixel rows nearest-neighbour produces at a fractional scale - a 160x144
        /// frame at 4.17x has most rows 4 device pixels tall and every sixth one 5, which shimmers when
        /// anything moves. Trades some screen area for evenness.
        /// </remarks>
        public bool IntegerScale
        {
            get => GetValue(IntegerScaleProperty);
            set => SetValue(IntegerScaleProperty, value);
        }

        /// <summary>The current frame as an image, or null before one is set. For binding and for tests; the way to change it is SetFrame.</summary>
        public IImage? Source => _bitmap;

        // THE CONTROL DRIVES THE Image RATHER THAN THE TEMPLATE BINDING IT, since §53.1. Integer
        // scaling needs to set an exact size on the Image and a Stretch that fills it, and a
        // TemplateBinding on Stretch would be fighting for the same property - so the template no
        // longer binds it and this is the single place the Image's geometry is decided.
        //
        // Recomputed whenever anything it depends on moves: the frame size, the control's bounds,
        // the Stretch, or the flag itself.
        private void ApplyScale()
        {
            if (_image is null) return;

            if (!IntegerScale || _bitmap is null || _width <= 0 || _height <= 0)
            {
                _image.Stretch = Stretch;
                _image.Width = double.NaN;
                _image.Height = double.NaN;
                _image.HorizontalAlignment = HorizontalAlignment.Stretch;
                _image.VerticalAlignment = VerticalAlignment.Stretch;
                return;
            }

            // Floor, and never below 1: a frame larger than the box still shows at 1:1 and is
            // cropped, which is the honest answer - a "0x" scale is not a picture.
            double room = Math.Min(Bounds.Width / _width, Bounds.Height / _height);
            int factor = room >= 1 ? (int)Math.Floor(room) : 1;

            // Fill an exactly-sized box, so each source pixel covers exactly `factor` device pixels.
            // The template's BitmapInterpolationMode of None then keeps every one of them crisp.
            _image.Stretch = Stretch.Fill;
            _image.Width = (double)_width * factor;
            _image.Height = (double)_height * factor;
            _image.HorizontalAlignment = HorizontalAlignment.Center;
            _image.VerticalAlignment = VerticalAlignment.Center;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty
                || change.Property == StretchProperty
                || change.Property == IntegerScaleProperty)
            {
                ApplyScale();
            }
        }

        // An Image, and unnamed on purpose. This shows a live buffer of pixels - a game frame, a
        // tile viewer - and only the caller knows what is in it. A toolkit-supplied name here would
        // be a guess presented as a description, which is worse for a reader than an image the
        // toolkit admits it cannot describe: a wrong alt text is believed, a missing one is asked
        // about. AutomationProperties.Name is where the caller says.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Image);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _image = e.NameScope.Find<Image>("PART_Image");
            ApplyScale();
        }

        // A 0x0 or short buffer clears the view rather than throwing - a core with no tile memory reports exactly that.
        /// <summary>Shows a frame of raw pixels, reusing the existing bitmap when the size has not changed.</summary>
        /// <param name="rgba">The pixels, four bytes each in R, G, B, A order, row by row from the top. Copied, so the caller may reuse the array immediately.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        public void SetFrame(byte[] rgba, int width, int height) =>
            SetFrame(new ReadOnlySpan<byte>(rgba), width, height);

        // THE OVERLOAD THAT REMOVES A COPY, and the reason it is worth having is arithmetic rather
        // than taste - see docs/LunaP.md §53. A caller whose pixels are already in a managed buffer
        // slices it and hands the slice over; a caller who had to build a byte[] first was paying
        // 8.3 MB per frame at 1080p to do so, which is about 500 MB/s at 60fps for nothing.
        //
        // The array overload above now delegates here, so there is ONE copy path rather than two
        // that can drift.
        /// <summary>Shows a frame of raw pixels without requiring them in an array.</summary>
        /// <param name="rgba">The pixels, four bytes each in R, G, B, A order, row by row from the top. Copied, so the caller may reuse the memory immediately.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        public void SetFrame(ReadOnlySpan<byte> rgba, int width, int height)
        {
            if (width <= 0 || height <= 0 || rgba.Length < width * height * 4)
            {
                Clear();
                return;
            }

            Reserve(width, height);

            unsafe
            {
                fixed (byte* source = rgba)
                {
                    Blit((nint)source, width, height);
                }
            }

            // Pixels changed but the bitmap instance did not, so nothing else would know to repaint.
            _image?.InvalidateVisual();
        }

        // THE ONE ENTRY POINT THAT CANNOT CHECK ITS ARGUMENT, and that is the whole warning. There is
        // no length in a bare address, so `width` and `height` are a promise the caller is making
        // about memory this control is about to read. Get them wrong and it reads past the buffer -
        // which is a crash at best and somebody else's pixels at worst.
        //
        // It exists because the alternative for a caller whose frame is already in native memory - an
        // emulator core's framebuffer, a decoder's output - was to marshal it into a managed array so
        // that this control could copy it straight back out again. Two copies to avoid a pointer.
        /// <summary>Shows a frame of raw pixels straight from unmanaged memory, without copying them into a managed array first.</summary>
        /// <param name="rgba">The address of the first pixel. Four bytes each in R, G, B, A order, row by row from the top. Read immediately and not retained.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <remarks>
        /// UNCHECKED. The caller guarantees that at least <c>width * height * 4</c> bytes are readable at
        /// <paramref name="rgba"/>; nothing here can verify it. A zero address clears the view.
        /// </remarks>
        public void SetFrame(nint rgba, int width, int height)
        {
            if (rgba == 0 || width <= 0 || height <= 0)
            {
                Clear();
                return;
            }

            Reserve(width, height);
            Blit(rgba, width, height);
            _image?.InvalidateVisual();
        }

        /// <summary>Shows a frame given as one tuple, for a producer that hands back all three together.</summary>
        /// <param name="frame">The pixels and their dimensions.</param>
        public void SetFrame((byte[] Rgba, int Width, int Height) frame) => SetFrame(frame.Rgba, frame.Width, frame.Height);

        // Makes sure _bitmap is the right size, rebuilding it only when the frame size actually
        // changed - which is the reuse this control exists for (§5.3).
        private void Reserve(int width, int height)
        {
            if (_bitmap is not null && _width == width && _height == height) return;

            WriteableBitmap? old = _bitmap;
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
            _width = width;
            _height = height;
            RaisePropertyChanged(SourceProperty, old, _bitmap);
            old?.Dispose();
            ApplyScale();
        }

        // ROW BY ROW WHEN THE STRIDE IS PADDED, AND ONE BLOCK WHEN IT IS NOT.
        //
        // ILockedFramebuffer.RowBytes exists because a backend is allowed to align each row - a
        // 163-pixel frame may sit in rows of 656 bytes rather than 652. This method used to copy
        // width*height*4 as a single contiguous block, which assumes RowBytes == width*4 and would
        // have produced a progressively skewed image on any backend that padded.
        //
        // MEASURED, and the measurement is why this was not a visible bug: on Linux with Skia,
        // WriteableBitmap returns RowBytes == width*4 at 160, 161, 163, 256, 257 and 1920 - never
        // padded, including at odd widths. So the assumption held everywhere it was tested and was
        // still an assumption about a platform rather than a guarantee of the API. The fast path
        // below is the same single copy as before, taken whenever the stride is tight.
        private unsafe void Blit(nint source, int width, int height)
        {
            using ILockedFramebuffer fb = _bitmap!.Lock();

            int rowBytes = width * 4;
            var src = (byte*)source;
            var dst = (byte*)fb.Address;

            if (fb.RowBytes == rowBytes)
            {
                Buffer.MemoryCopy(src, dst, (long)fb.RowBytes * height, (long)rowBytes * height);
                return;
            }

            for (int y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(src + ((long)y * rowBytes), dst + ((long)y * fb.RowBytes), fb.RowBytes, rowBytes);
            }
        }

        /// <summary>Drops the current frame, leaving the control empty.</summary>
        public void Clear()
        {
            if (_bitmap is null) return;

            WriteableBitmap old = _bitmap;
            _bitmap = null;
            _width = 0;
            _height = 0;
            RaisePropertyChanged(SourceProperty, old, null);
            old.Dispose();
        }
    }
}
