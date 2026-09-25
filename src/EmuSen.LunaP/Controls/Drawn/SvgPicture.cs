using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // An SvgDocument drawn as vectors at whatever size the control is, or a crossed box when the document is refused - see docs/LunaP.md §99.4.
    /// <summary>Shows an SVG document as vectors, sized by its own aspect ratio, or a crossed box when the document is outside the supported subset.</summary>
    public class SvgPicture : Control
    {
        public static readonly StyledProperty<SvgDocument?> DocumentProperty = AvaloniaProperty.Register<SvgPicture, SvgDocument?>(nameof(Document));

        static SvgPicture() => AffectsMeasure<SvgPicture>(DocumentProperty);

        /// <summary>The document to draw. Null draws nothing.</summary>
        public SvgDocument? Document { get => GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

        /// <summary>Reads a file into Document.</summary>
        /// <param name="path">The .svg file.</param>
        public void Load(string path) => Document = SvgDocument.Load(path);

        protected override Size MeasureOverride(Size availableSize)
        {
            Size own = Document is { IsRefused: false } d ? d.Size : new Size(16, 16);
            if (own.Width <= 0 || own.Height <= 0) return default;
            bool wInf = double.IsInfinity(availableSize.Width), hInf = double.IsInfinity(availableSize.Height);
            if (wInf && hInf) return own;
            double s = wInf ? availableSize.Height / own.Height : hInf ? availableSize.Width / own.Width : Math.Min(availableSize.Width / own.Width, availableSize.Height / own.Height);
            return new Size(own.Width * s, own.Height * s);
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            if (Document is not { } document) return;
            if (document.IsRefused) FittedImage.DrawRefusal(context, bounds);
            else document.Draw(context, bounds);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image);
    }
}
