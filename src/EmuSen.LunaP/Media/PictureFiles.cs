using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace EmuSen.LunaP.Media
{
    // One picture file drawn into a rectangle with a tint and saturation, for the controls that draw several - see docs/LunaP.md §98.2.
    internal static class PictureFiles
    {
        internal static Size Intrinsic(string? path) => path is { Length: > 0 } ? ImageFile.Open(path).Intrinsic : default;

        internal static bool Exists(string? path) => path is { Length: > 0 } && ImageFile.Open(path) is { Missing: false, Refused: false };

        // Fits the picture inside the rectangle, centred; returns where it drew, or an empty rectangle.
        internal static Rect Draw(DrawingContext dc, Visual owner, string? path, Rect box, Color tint, double saturation = 1, bool contain = true)
        {
            if (path is not { Length: > 0 } || box.Width <= 0 || box.Height <= 0) return default;
            ImageFile file = ImageFile.Open(path);
            if (file.Missing) return default;
            if (file.Refused)
            {
                Controls.FittedImage.DrawRefusal(dc, box);
                return box;
            }

            Size own = file.Intrinsic;
            if (own.Width <= 0 || own.Height <= 0) return default;
            Rect target = contain ? Controls.FittedImage.Contain(box, own) : box;
            double scaling = TopLevel.GetTopLevel(owner)?.RenderScaling ?? 1;
            var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(target.Width * scaling - 0.001)), Math.Max(1, (int)Math.Ceiling(target.Height * scaling - 0.001)));
            Bitmap? bitmap = ImagePixels.Prepare(path, file, size, new ImageEffects(tint, tint, false, saturation));
            if (bitmap is null) return default;
            dc.DrawImage(bitmap, new Rect(bitmap.Size), target);
            return target;
        }
    }
}
