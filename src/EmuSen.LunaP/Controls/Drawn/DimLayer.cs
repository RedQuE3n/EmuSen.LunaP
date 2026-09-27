using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace EmuSen.LunaP.Controls
{
    // Desaturates and darkens whatever is drawn beneath it by blending over it, so nothing is captured or resampled - see docs/LunaP.md §184.1.
    /// <summary>Over its own area, turns what is drawn beneath it toward grey by Saturation and toward black by Brightness; at 1 and 1 it draws nothing.</summary>
    public class DimLayer : Control
    {
        public static readonly StyledProperty<double> SaturationProperty = AvaloniaProperty.Register<DimLayer, double>(nameof(Saturation), 1);
        public static readonly StyledProperty<double> BrightnessProperty = AvaloniaProperty.Register<DimLayer, double>(nameof(Brightness), 1);

        private static WriteableBitmap? _grey;

        static DimLayer()
        {
            AffectsRender<DimLayer>(SaturationProperty, BrightnessProperty);
            IsHitTestVisibleProperty.OverrideDefaultValue<DimLayer>(false);
        }

        /// <summary>1 keeps the colours beneath, 0 makes them the grey of their own luminance. 1 by default; clamped to [0, 1].</summary>
        public double Saturation { get => GetValue(SaturationProperty); set => SetValue(SaturationProperty, value); }

        /// <summary>The fraction of the light beneath that is kept: 1 keeps it, 0 is black. 1 by default; clamped to [0, 1].</summary>
        public double Brightness { get => GetValue(BrightnessProperty); set => SetValue(BrightnessProperty, value); }

        /// <summary>Whether the layer changes what is beneath it at its current values.</summary>
        public bool IsDimming => Math.Clamp(Saturation, 0, 1) < 1 || Math.Clamp(Brightness, 0, 1) < 1;

        // The saturation blend takes only the saturation of what it draws, which for any grey is 0; the renderer applies blend modes to images alone.
        private static WriteableBitmap Grey
        {
            get
            {
                if (_grey is not null) return _grey;
                var bitmap = new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (ILockedFramebuffer fb = bitmap.Lock())
                    for (int y = 0; y < 2; y++) Marshal.Copy(new byte[] { 128, 128, 128, 255, 128, 128, 128, 255 }, 0, fb.Address + y * fb.RowBytes, 8);
                return _grey = bitmap;
            }
        }

        public override void Render(DrawingContext context)
        {
            var area = new Rect(Bounds.Size);
            if (area.Width <= 0 || area.Height <= 0) return;
            double grey = 1 - Math.Clamp(Saturation, 0, 1);
            if (grey > 0)
            {
                using DrawingContext.PushedState blend = context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = BitmapBlendingMode.Saturation });
                using DrawingContext.PushedState opacity = context.PushOpacity(grey);
                context.DrawImage(Grey, new Rect(0, 0, 2, 2), area);
            }

            double dark = 1 - Math.Clamp(Brightness, 0, 1);
            if (dark > 0) context.FillRectangle(new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(255 * dark), 0, 0, 0)), area);
        }
    }
}
