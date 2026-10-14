using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Relic.Core.Random;

namespace Relic.Magic.Tests
{
    public class RecognizerTests
    {
        static GlyphRegistry Reg => MagicTestData.Registry;

        [Test]
        public void GlyphSetV0HasTheBriefsCounts()
        {
            Assert.AreEqual(6, Reg.WithRole(GlyphRole.Core).Count(), "cores");
            Assert.AreEqual(8, Reg.WithRole(GlyphRole.Inner).Count(), "inner modifiers");
            Assert.AreEqual(7, Reg.WithRole(GlyphRole.Ring).Count(), "ring glyphs");
        }

        [Test]
        public void EveryTemplateRecognizesItselfPerfectly()
        {
            foreach (var g in Reg.All)
            {
                var strokes = g.Render(new Vec2(300, 300), 120f);
                var r = Reg.Recognizer.Recognize(strokes);
                Assert.AreSame(g, r.Glyph, $"{g.Id} recognized as {r}");
                Assert.Greater(r.Precision, 0.99f, g.Id);
            }
        }

        [Test]
        public void RecognitionIsDeterministic()
        {
            var rng1 = new Pcg32(5);
            var rng2 = new Pcg32(5);
            var g = Reg["seek"];
            var a = Reg.Recognizer.Recognize(MagicTestData.Sloppy(g, ref rng1, MagicTestData.Sloppiness.Default, Vec2.Zero));
            var b = Reg.Recognizer.Recognize(MagicTestData.Sloppy(g, ref rng2, MagicTestData.Sloppiness.Default, Vec2.Zero));
            Assert.AreEqual(a.Glyph, b.Glyph);
            Assert.AreEqual(a.Distance, b.Distance);
            Assert.AreEqual(a.Precision, b.Precision);
        }

        [Test]
        public void StrokeOrderAndDirectionDoNotMatter()
        {
            var g = Reg["light"];
            var strokes = g.Render(Vec2.Zero, 100f);
            strokes.Reverse();
            foreach (var s in strokes) s.Points.Reverse();
            Assert.AreSame(g, Reg.Recognizer.Recognize(strokes).Glyph);
        }

        [Test]
        public void ScribbleIsRejectedOrImprecise()
        {
            var rng = new Pcg32(11);
            var scribble = new Stroke();
            for (int i = 0; i < 60; i++) scribble.Points.Add(new Vec2(rng.Range(0f, 100f), rng.Range(0f, 100f)));
            var r = Reg.Recognizer.Recognize(new List<Stroke> { scribble });
            Assert.That(!r.IsRecognized || r.Precision < 0.3f, r.ToString());
        }


        [Test]
        public void MostRandomScribblesAreRejected()
        {
            var rng = new Pcg32(Hash.Of("magic.recognizer.scribbles"));
            int rejected = 0;
            const int n = 300;
            for (int k = 0; k < n; k++)
            {
                var s = new Stroke();
                int points = rng.Range(5, 60);
                for (int i = 0; i < points; i++) s.Points.Add(new Vec2(rng.Range(0f, 100f), rng.Range(0f, 100f)));
                if (!Reg.Recognizer.Recognize(new List<Stroke> { s }).IsRecognized) rejected++;
            }
            TestContext.WriteLine($"random scribbles rejected: {rejected}/{n}");
            Assert.Greater(rejected, n * 6 / 10);
        }
        [Test]
        public void SloppierDrawingsScoreLowerPrecision()
        {
            float Mean(MagicTestData.Sloppiness s)
            {
                var rng = new Pcg32(77);
                float sum = 0f;
                int n = 0;
                foreach (var g in Reg.All)
                    for (int i = 0; i < 10; i++)
                    {
                        sum += Reg.Recognizer.Recognize(MagicTestData.Sloppy(g, ref rng, s, Vec2.Zero)).Precision;
                        n++;
                    }
                return sum / n;
            }

            var tidy = new MagicTestData.Sloppiness { PointNoise = 0.005f, Wobble = 0.01f, MaxRotationDegrees = 3f, AspectJitter = 0.03f };
            var messy = new MagicTestData.Sloppiness { PointNoise = 0.03f, Wobble = 0.07f, MaxRotationDegrees = 20f, AspectJitter = 0.2f };
            float tidyP = Mean(tidy), normalP = Mean(MagicTestData.Sloppiness.Default), messyP = Mean(messy);
            TestContext.WriteLine($"mean precision: tidy {tidyP:0.000}, default {normalP:0.000}, messy {messyP:0.000}");
            Assert.Greater(tidyP, normalP);
            Assert.Greater(normalP, messyP);
            Assert.Greater(tidyP, 0.85f, "a careful drawing should count as clean");
        }

        /// <summary>
        /// Acceptance: ≥ 95% on a synthetic set of every glyph drawn with noise,
        /// wobble, rotation (±15°), aspect/scale jitter, point-count jitter and
        /// shuffled stroke order/direction.
        /// </summary>
        [Test]
        public void SyntheticAccuracyIsAtLeast95Percent()
        {
            const int perGlyph = 100;
            var rng = new Pcg32(Hash.Of("magic.recognizer.synthetic"));
            int correct = 0, total = 0;
            var confusions = new Dictionary<string, int>();
            foreach (var g in Reg.All)
            {
                for (int i = 0; i < perGlyph; i++)
                {
                    var strokes = MagicTestData.Sloppy(g, ref rng, MagicTestData.Sloppiness.Default, new Vec2(500, 500));
                    var r = Reg.Recognizer.Recognize(strokes);
                    total++;
                    if (r.Glyph == g) correct++;
                    else
                    {
                        string key = $"{g.Id}->{(r.Glyph == null ? "none" : r.Glyph.Id)}";
                        confusions.TryGetValue(key, out int c);
                        confusions[key] = c + 1;
                    }
                }
            }
            float accuracy = (float)correct / total;
            var sb = new StringBuilder();
            sb.Append($"recognizer accuracy {accuracy:P2} ({correct}/{total})");
            foreach (var kv in confusions.OrderByDescending(k => k.Value)) sb.Append($"\n  {kv.Key}: {kv.Value}");
            TestContext.WriteLine(sb.ToString());
            Assert.GreaterOrEqual(accuracy, 0.95f, sb.ToString());
        }
    }
}
