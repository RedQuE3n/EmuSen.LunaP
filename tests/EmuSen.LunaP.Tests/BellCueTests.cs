using System;
using System.Collections.Generic;
using System.Linq;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // The navigation sounds as Moonrise's bells - see docs/LunaP.md §220.
    public class BellCueTests
    {
        private static readonly SplashChime Chime = SplashTimeline.Moonrise.Chime;

        public static IEnumerable<object[]> Designs()
        {
            foreach (LunaSoundCue cue in LunaSounds.Cues) yield return [cue.ToString(), LunaSounds.BellsOf(cue), LunaSounds.Samples(cue)];
            yield return ["QuickSelect", LunaSounds.QuickSelect, LunaSounds.QuickSelectSamples()];
        }

        private static float At(byte[] bytes, int frame) => BitConverter.ToSingle(bytes, frame * 8);

        // The power at one pitch over a stretch, by Goertzel's recurrence.
        private static double Power(byte[] bytes, double from, double to, double hz)
        {
            double c = 2 * Math.Cos(2 * Math.PI * hz / LunaSounds.Rate), s1 = 0, s2 = 0;
            int end = Math.Min(bytes.Length / 8, (int)(to * LunaSounds.Rate));
            for (int i = (int)Math.Max(0, from * LunaSounds.Rate); i < end; i++) (s2, s1) = (s1, At(bytes, i) + c * s1 - s2);
            return s1 * s1 + s2 * s2 - c * s1 * s2;
        }

        private static double Peak(byte[] bytes) => Enumerable.Range(0, bytes.Length / 8).Max(i => Math.Abs(At(bytes, i)));

        [Fact]
        public void There_are_ten_designs_and_every_note_is_on_the_ladder()
        {
            Assert.Equal(9, LunaSounds.Cues.Count);
            Assert.Equal(10, Designs().Count());
            foreach (object[] d in Designs())
                Assert.All((IReadOnlyList<LunaBell>)d[1], b => Assert.InRange(b.Note, 0, 12));
            Assert.Equal([5, 8], LunaSounds.BellsOf(LunaSoundCue.Choose).Select(b => b.Note));
            Assert.Equal([8, 5], LunaSounds.BellsOf(LunaSoundCue.Back).Select(b => b.Note));
            Assert.Equal([0, 1, 2, 3, 4, 5], LunaSounds.BellsOf(LunaSoundCue.Launch).Select(b => b.Note));
            Assert.Equal(900, LunaSounds.BellsOf(LunaSoundCue.Refused)[0].Lowpass);
            Assert.Equal(2400, LunaSounds.BellsOf(LunaSoundCue.Move)[0].Lowpass);
        }

        [Theory]
        [MemberData(nameof(Designs))]
        public void Each_notes_pitch_is_present_from_its_strike_time(string name, IReadOnlyList<LunaBell> bells, byte[] samples)
        {
            foreach (LunaBell b in bells)
            {
                double hz = Chime.NoteOf(b.Note);
                double after = Power(samples, b.Start, b.Start + 0.05, hz), before = Power(samples, b.Start - 0.05, b.Start, hz);
                Assert.True(after > 0 && after > 10 * before, $"{name}: note {b.Note} at {b.Start} s");
            }
        }

        [Theory]
        [MemberData(nameof(Designs))]
        public void Two_renders_are_identical_and_the_peak_is_in_its_band(string name, IReadOnlyList<LunaBell> bells, byte[] samples)
        {
            Assert.Equal(samples, LunaSounds.RenderBells(Chime, bells));
            Assert.Equal(At(samples, 3000), BitConverter.ToSingle(samples, 3000 * 8 + 4));
            Assert.InRange(Peak(samples), 0.05, 0.6);
            Assert.True(Peak(samples) <= bells.Max(b => b.Gain) * 3, name);
        }

        [Fact]
        public void A_single_bell_is_at_the_requested_pitch()
        {
            for (int rung = 0; rung <= 12; rung += 4)
            {
                byte[] bell = LunaSounds.BellSamples(rung);
                double on = Power(bell, 0.02, 0.2, Chime.NoteOf(rung));
                Assert.True(on > 20 * Power(bell, 0.02, 0.2, Chime.NoteOf(rung) * 1.12), $"rung {rung} above");
                Assert.True(on > 20 * Power(bell, 0.02, 0.2, Chime.NoteOf(rung) / 1.12), $"rung {rung} below");
                Assert.Same(bell, LunaSounds.BellSamples(rung));
            }
            Assert.InRange(Peak(LunaSounds.BellSamples(5)), 0.05, 0.6);
        }
    }
}
