using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // OpenEmu's in-game notification - "Quick Save", "Fast Forward" - a small pill over the picture
    // that fades in, holds and fades out - see docs/LunaP.md §88.5.
    //
    // THE CURVE IS OpenEmu's, read from OEGameLayerNotificationView.swift: an opacity keyframe
    // animation over 1.75 s with values 0, 1, 1, 0 at key times 0, 0.15, 0.85, 1. A second notice
    // replaces the first and starts the curve again, which is what the original does by removing
    // the old animation before adding the new one - two pills stacked would say two things at once
    // about one keypress.
    //
    // TWO CLOCKS, AND WHY. The fade is an Avalonia Animation, which advances with the render loop;
    // Current - the fact a host or a test asks about - is ended by a DispatcherTimer of the same
    // Duration. They agree to within a frame on a running application. Current is kept off the
    // render clock because it is a fact about time and not about pixels: nothing guarantees a
    // window that is not being drawn keeps ticking its animations, and a notice that could only end
    // by being seen to fade would outlive its Duration in exactly that window. That is reasoning,
    // not a measurement - §88.7 says which half was measured.
    /// <summary>A small rounded notice laid over other content that fades in, holds and fades out; a second Show replaces the first.</summary>
    public class NoticeLayer : TemplatedControl
    {
        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<NoticeLayer, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(1.75));

        public static readonly DirectProperty<NoticeLayer, string?> CurrentProperty =
            AvaloniaProperty.RegisterDirect<NoticeLayer, string?>(nameof(Current), o => o.Current);

        // The keyframes as fractions of Duration, and the opacity at each. Public-facing numbers live
        // in the docs; this is the one copy the animation is built from.
        internal static readonly (double Cue, double Opacity)[] Curve =
        {
            (0.0, 0.0), (0.15, 1.0), (0.85, 1.0), (1.0, 0.0),
        };

        private string? _current;
        private Border? _pill;
        private Debounce? _end;
        private TimeSpan _endDelay;
        private CancellationTokenSource? _fading;

        /// <summary>How long one notice lasts, from fading in to gone. 1.75 seconds by default, OpenEmu's own.</summary>
        public TimeSpan Duration
        {
            get => GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        /// <summary>The text showing now, or null when no notice is showing because none was shown or the last one has run its Duration.</summary>
        public string? Current => _current;

        /// <summary>Shows a notice, replacing any notice already showing and starting its fade from the beginning.</summary>
        /// <param name="text">What the notice says - a few words. Safe to call before the control has a template; the fade starts when it has one.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="text"/> is null.</exception>
        public void Show(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            SetCurrent(text);
            End.Poke();
            Fade();
        }

        private Debounce End
        {
            get
            {
                if (_end is null || _endDelay != Duration)
                {
                    _end?.Cancel();
                    _endDelay = Duration;
                    TimeSpan interval = Duration > TimeSpan.Zero ? Duration : TimeSpan.FromMilliseconds(1);
                    _end = new Debounce(interval, () => SetCurrent(null));
                }

                return _end;
            }
        }

        private void SetCurrent(string? text)
        {
            string? old = _current;
            if (old == text) return;

            _current = text;
            RaisePropertyChanged(CurrentProperty, old, text);

            // A live region is only heard when something says it changed: the name is read from
            // Current each time, so without this event a reader would never know to read it again.
            if (ControlAutomationPeer.FromElement(this) is { } peer)
            {
                peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, old, text);
            }
        }

        private void Fade()
        {
            _fading?.Cancel();
            _fading = null;

            if (_pill is null || _current is null) return;

            var animation = new Animation
            {
                Duration = Duration,
                FillMode = FillMode.Forward,
            };

            foreach ((double cue, double opacity) in Curve)
            {
                animation.Children.Add(new KeyFrame
                {
                    Cue = new Cue(cue),
                    Setters = { new Setter(Visual.OpacityProperty, opacity) },
                });
            }

            _fading = new CancellationTokenSource();
            _ = animation.RunAsync(_pill, _fading.Token);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _pill = e.NameScope.Find<Border>("PART_Pill");

            // A notice shown from a constructor fades in once there is a pill to fade, rather than
            // being lost - the §28.2 rule for every method that can run before the template.
            Fade();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _end?.Cancel();
            _fading?.Cancel();
            SetCurrent(null);
            base.OnDetachedFromVisualTree(e);
        }

        // Text, named by the notice itself, and polite: set in NoticeLayer.axaml as a style so a host
        // that finds its notices too chatty can turn the live region off the standard Avalonia way.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Text, name: () => Current);
    }
}
