using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Controls
{
    // While shown, blurs another visual with the renderer's own Gaussian image filter and shades it - see docs/LunaP.md §181.4.
    /// <summary>While shown, blurs a target visual with a Gaussian blur effect and draws a shade over its own area; hidden or detached, it takes the blur off again.</summary>
    public class BlurBackdrop : Control
    {
        public static readonly StyledProperty<Visual?> TargetProperty = AvaloniaProperty.Register<BlurBackdrop, Visual?>(nameof(Target));
        public static readonly StyledProperty<double> RadiusProperty = AvaloniaProperty.Register<BlurBackdrop, double>(nameof(Radius), 16);
        public static readonly StyledProperty<Color> ShadeProperty = AvaloniaProperty.Register<BlurBackdrop, Color>(nameof(Shade), Color.FromArgb(0x70, 0, 0, 0));

        // The effect this backdrop put on its target, so only that one is ever taken off.
        private BlurEffect? _applied;
        private Visual? _blurred;

        static BlurBackdrop()
        {
            AffectsRender<BlurBackdrop>(ShadeProperty);
            IsHitTestVisibleProperty.OverrideDefaultValue<BlurBackdrop>(false);
        }

        /// <summary>The visual blurred while this is shown, such as the screen a menu opens over. Null blurs nothing.</summary>
        public Visual? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

        /// <summary>The blur's radius in pixels; 0 or less blurs nothing. 16 by default.</summary>
        public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

        /// <summary>A fill drawn over the backdrop's area to darken what is behind it. #70000000, a translucent black, by default.</summary>
        public Color Shade { get => GetValue(ShadeProperty); set => SetValue(ShadeProperty, value); }

        /// <summary>Whether the target is blurred by this backdrop now.</summary>
        public bool IsBlurring => _applied is not null && _blurred is not null && ReferenceEquals(_blurred.Effect, _applied);

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Update();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            Lift();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty || change.Property == TargetProperty || change.Property == RadiusProperty) Update();
        }

        private void Update()
        {
            Lift();
            if (!IsVisible || TopLevel.GetTopLevel(this) is null || Target is not { } target || Radius <= 0) return;
            if (target.Effect is not null) return;
            _applied = new BlurEffect { Radius = Radius };
            _blurred = target;
            target.Effect = _applied;
        }

        // Only the effect this put there comes off; a target given another since keeps it.
        private void Lift()
        {
            if (_blurred is { } target && ReferenceEquals(target.Effect, _applied)) target.Effect = null;
            _blurred = null;
            _applied = null;
        }

        public override void Render(DrawingContext context)
        {
            if (Shade.A > 0) context.FillRectangle(new ImmutableSolidColorBrush(Shade), new Rect(Bounds.Size));
        }
    }
}
