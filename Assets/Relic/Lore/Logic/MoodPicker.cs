using System;
using System.Collections.Generic;
using Relic.Core.Random;

namespace Relic.Lore
{
    /// <summary>
    /// Picks moods with the Bible's tone weights: first the tone (Bright with probability
    /// <see cref="MoodTable.BrightShare"/>, about 70%), then a mood within that tone by weight.
    /// Callers that need a darker place (a ruin of the Quarrel, a villain's lair) pass a lower
    /// bright share explicitly; the default always honors the 70/30 rule.
    /// </summary>
    public sealed class MoodPicker
    {
        readonly MoodTable _table;
        readonly List<MoodDef> _bright = new List<MoodDef>();
        readonly List<MoodDef> _somber = new List<MoodDef>();

        public MoodPicker(MoodTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            foreach (var m in table.Moods)
                (m.Tone == Tone.Bright ? _bright : _somber).Add(m);
            if (_bright.Count == 0 || _somber.Count == 0)
                throw new ArgumentException("MoodPicker needs at least one Bright and one Somber mood");
        }

        public double BrightShare => _table.BrightShare;

        public Tone PickTone(ref Pcg32 rng) => PickTone(ref rng, _table.BrightShare);

        public static Tone PickTone(ref Pcg32 rng, double brightShare) =>
            rng.NextDouble() < brightShare ? Tone.Bright : Tone.Somber;

        public MoodDef Pick(ref Pcg32 rng) => Pick(ref rng, _table.BrightShare);

        public MoodDef Pick(ref Pcg32 rng, double brightShare) => PickIn(ref rng, PickTone(ref rng, brightShare));

        /// <summary>A mood of the given tone, by weight.</summary>
        public MoodDef PickIn(ref Pcg32 rng, Tone tone) => Weighted(ref rng, tone == Tone.Bright ? _bright : _somber);

        /// <summary>Deterministic pick for a seed and purpose (e.g. an item id).</summary>
        public MoodDef Pick(ulong seed, string purpose)
        {
            var rng = new Pcg32(Hash.Combine(seed, Hash.Of("lore.mood:" + purpose)));
            return Pick(ref rng);
        }

        static MoodDef Weighted(ref Pcg32 rng, List<MoodDef> list)
        {
            int total = 0;
            foreach (var m in list) total += Math.Max(0, m.Weight);
            int r = rng.Range(0, Math.Max(1, total));
            foreach (var m in list)
            {
                r -= Math.Max(0, m.Weight);
                if (r < 0) return m;
            }
            return list[0];
        }

        /// <summary>
        /// A motif that fits the mood: motifs listing this mood are three times as likely, and
        /// a motif whose tones exclude the mood's tone is never chosen. When <paramref name="allowed"/>
        /// is given (a culture's motif ids), only those are considered if any of them fit.
        /// </summary>
        public static MotifDef MotifFor(ref Pcg32 rng, LoreBible bible, MoodDef mood, IList<string> allowed = null)
        {
            var pool = new List<MotifDef>();
            var weights = new List<int>();
            for (int pass = 0; pass < 2 && pool.Count == 0; pass++)
            {
                foreach (var m in bible.Motifs)
                {
                    if (!m.Tones.Contains(mood.Tone)) continue;
                    if (pass == 0 && allowed != null && !allowed.Contains(m.Id)) continue;
                    pool.Add(m);
                    weights.Add(m.Moods.Contains(mood.Id) ? 3 : 1);
                }
            }
            if (pool.Count == 0) return bible.Motifs[0];
            int total = 0;
            foreach (int w in weights) total += w;
            int r = rng.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                r -= weights[i];
                if (r < 0) return pool[i];
            }
            return pool[0];
        }
    }
}
