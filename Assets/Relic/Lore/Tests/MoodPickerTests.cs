using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Random;

namespace Relic.Lore.Tests
{
    public class MoodPickerTests
    {
        static LoreBible B => LoreTestData.Bible;
        const int N = 10000;

        [Test]
        public void BrightShareIsSeventyPercent()
        {
            Assert.AreEqual(0.7, B.Moods.BrightShare, 1e-9, "the Bible's tone rule");
            var picker = new MoodPicker(B.Moods);
            foreach (ulong seed in new ulong[] { 1, 2026, 987654321 })
            {
                var rng = new Pcg32(seed);
                int bright = 0;
                for (int i = 0; i < N; i++) if (picker.Pick(ref rng).Tone == Tone.Bright) bright++;
                double share = bright / (double)N;
                TestContext.WriteLine("seed " + seed + ": bright " + share.ToString("0.000"));
                Assert.That(share, Is.InRange(0.65, 0.75), "seed " + seed);
            }
        }

        [Test]
        public void MoodsWithinAToneFollowTheirWeights()
        {
            var picker = new MoodPicker(B.Moods);
            var rng = new Pcg32(42);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < N; i++)
            {
                var m = picker.PickIn(ref rng, Tone.Bright);
                counts[m.Id] = (counts.TryGetValue(m.Id, out int c) ? c : 0) + 1;
            }
            int total = 0;
            foreach (var m in B.Moods.Moods) if (m.Tone == Tone.Bright) total += m.Weight;
            foreach (var m in B.Moods.Moods)
            {
                if (m.Tone != Tone.Bright) { Assert.IsFalse(counts.ContainsKey(m.Id), m.Id + " is not bright"); continue; }
                double expected = m.Weight / (double)total;
                double actual = (counts.TryGetValue(m.Id, out int c) ? c : 0) / (double)N;
                Assert.AreEqual(expected, actual, 0.03, m.Id);
            }
        }

        [Test]
        public void ExplicitBrightShareIsHonored()
        {
            var picker = new MoodPicker(B.Moods);
            var rng = new Pcg32(7);
            int bright = 0;
            for (int i = 0; i < N; i++) if (picker.Pick(ref rng, 0.3).Tone == Tone.Bright) bright++;
            Assert.AreEqual(0.3, bright / (double)N, 0.05);
        }

        [Test]
        public void PicksAreDeterministic()
        {
            var picker = new MoodPicker(B.Moods);
            for (ulong s = 0; s < 100; s++)
                Assert.AreEqual(picker.Pick(s, "relic").Id, picker.Pick(s, "relic").Id);
        }

        [Test]
        public void MotifsAlwaysCarryTheMoodsTone()
        {
            var picker = new MoodPicker(B.Moods);
            var rng = new Pcg32(99);
            foreach (var c in B.Cultures)
                for (int i = 0; i < 300; i++)
                {
                    var mood = picker.Pick(ref rng);
                    var motif = MoodPicker.MotifFor(ref rng, B, mood, c.Motifs);
                    Assert.IsTrue(motif.Tones.Contains(mood.Tone), motif.Id + " cannot carry " + mood.Id);
                }
        }
    }
}
