using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Random;

namespace Relic.Magic.Tests
{
    public class CircleParserTests
    {
        static GlyphRegistry Reg => MagicTestData.Registry;

        static string Messages(SpellCircle c) => string.Join("\n", c.Diagnostics.Select(d => d.ToString()));

        static Drawing Page(string recipe, float radius = 200f) =>
            RecipeDrawer.Draw(SpellRecipe.Parse(recipe), Reg, new Vec2(300, 300), radius);

        // ---------- Pre-written spells round-trip ----------

        static readonly string[] RoundTripRecipes =
        {
            "water | split seek | line",
            "flame | more | ring",
            "stone | lasting | line wall",
            "wind | far | cone",
            "light | lasting | orbit",
            "growth | heavy | delay line return",
            "water | less swift | line",
            "flame | split | ring delay line",
            "stone | more+ far- | cone+ delay- line",
            "light | heavy swift seek | orbit return line",
            "growth | | ",
            "wind | lasting more less | ring line cone",
        };

        [Test]
        public void RecipeToDrawingToProgramRoundTrips()
        {
            var parser = new CircleParser(Reg);
            foreach (var text in RoundTripRecipes)
            {
                var recipe = SpellRecipe.Parse(text);
                var direct = SpellCompiler.Compile(recipe, Reg);
                var circle = parser.Parse(RecipeDrawer.Draw(recipe, Reg, new Vec2(512, 512), 300f));
                Assert.IsTrue(circle.HasCircle && circle.IsClosed, text + "\n" + Messages(circle));
                Assert.AreEqual(recipe.ToString(), circle.ToRecipe().ToString(), "glyphs read back from the drawing");
                Assert.IsEmpty(circle.Unrecognized, text);
                var drawn = SpellCompiler.Compile(circle);
                Assert.AreEqual(direct.Json, drawn.Json, text);
                Assert.AreEqual(direct.Success, drawn.Success, text);
            }
        }

        [Test]
        public void EveryGlyphRoundTripsInItsLayer()
        {
            var parser = new CircleParser(Reg);
            foreach (var g in Reg.All)
            {
                string text = g.Role == GlyphRole.Core ? $"{g.Id} | | line"
                    : g.Role == GlyphRole.Inner ? $"water | {g.Id} | line"
                    : $"water | | {g.Id}";
                var circle = parser.Parse(Page(text));
                Assert.AreEqual(SpellRecipe.Parse(text).ToString(), circle.ToRecipe().ToString(), g.Id + "\n" + Messages(circle));
            }
        }

        // ---------- Geometry ----------

        [Test]
        public void RingIsReadClockwiseFromTheTop()
        {
            // Draw the ring glyphs in reverse order, at right, bottom and left; the
            // reading order is by position, not by drawing order.
            var d = new Drawing();
            var c = new Vec2(0, 0);
            d.Strokes.Add(RecipeDrawer.CircleStroke(c, 100f));
            d.Strokes.AddRange(Reg["water"].Render(c, 40f));
            d.Strokes.AddRange(Reg["cone"].Render(c + new Vec2(-80, 0), 25f));  // left (270°)
            d.Strokes.AddRange(Reg["delay"].Render(c + new Vec2(0, -80), 25f)); // bottom (180°)
            d.Strokes.AddRange(Reg["line"].Render(c + new Vec2(80, 0), 25f));   // right (90°)
            var circle = new CircleParser(Reg).Parse(d);
            CollectionAssert.AreEqual(new[] { "line", "delay", "cone" }, circle.Ring.Select(g => g.Glyph.Id).ToArray(), Messages(circle));
        }

        [Test]
        public void SizeSetsEmphasis()
        {
            var circle = new CircleParser(Reg).Parse(Page("water | split+ seek- | line"));
            Assert.AreEqual(Emphasis.Large, circle.Inner.First(g => g.Glyph.Id == "split").Emphasis);
            Assert.AreEqual(Emphasis.Small, circle.Inner.First(g => g.Glyph.Id == "seek").Emphasis);
            Assert.AreEqual(Emphasis.Normal, circle.Ring[0].Emphasis);
        }

        [Test]
        public void EmptyPageSaysSo()
        {
            var circle = new CircleParser(Reg).Parse(new Drawing());
            Assert.That(circle.Diagnostics.Any(d => d.Severity == Severity.Error && d.Message.Contains("empty")));
        }

        [Test]
        public void MissingCircleIsAnErrorButGlyphsAreStillRead()
        {
            var d = new Drawing(Reg["flame"].Render(new Vec2(0, 0), 80f));
            var circle = new CircleParser(Reg).Parse(d);
            Assert.IsFalse(circle.HasCircle);
            Assert.That(circle.Diagnostics.Any(x => x.Severity == Severity.Error && x.Message.Contains("large circle")), Messages(circle));
            Assert.AreEqual("flame", circle.Core?.Glyph.Id);
        }

        [Test]
        public void UnclosedCircleIsRejectedAndSaysWhere()
        {
            var d = Page("water | | line");
            // Replace the circle with a 300° arc whose gap is at the left.
            d.Strokes[0] = MagicTestData.Circle(new Vec2(300, 300), 200f, startDeg: 150f, sweepDeg: -300f);
            var circle = new CircleParser(Reg).Parse(d);
            Assert.IsTrue(circle.HasCircle);
            Assert.IsFalse(circle.IsClosed);
            var spell = SpellCompiler.Compile(circle);
            Assert.IsFalse(spell.Success);
            Assert.That(spell.Errors.Any(e => e.Message.Contains("isn't closed") && e.Message.Contains("left")), Messages(circle));
        }

        [Test]
        public void SmallGapStillCountsAsClosed()
        {
            var d = Page("water | | line");
            d.Strokes[0] = MagicTestData.Circle(new Vec2(300, 300), 200f, startDeg: 90f, sweepDeg: -350f);
            var circle = new CircleParser(Reg).Parse(d);
            Assert.IsTrue(circle.IsClosed, Messages(circle));
        }

        [Test]
        public void CircleDrawnInTwoArcsIsClosed()
        {
            var d = Page("water | | line");
            d.Strokes[0] = MagicTestData.Circle(new Vec2(300, 300), 200f, startDeg: 90f, sweepDeg: -200f);
            d.Strokes.Insert(1, MagicTestData.Circle(new Vec2(302, 298), 203f, startDeg: -105f, sweepDeg: -170f));
            var circle = new CircleParser(Reg).Parse(d);
            Assert.IsTrue(circle.IsClosed, Messages(circle));
            Assert.AreEqual(2, circle.CircleStrokeIndices.Count);
            Assert.AreEqual("water | | line", circle.ToRecipe().ToString());
        }

        [Test]
        public void CircleCanBeDrawnLast()
        {
            var d = Page("flame | more | ring");
            var circleStroke = d.Strokes[0];
            d.Strokes.RemoveAt(0);
            d.Strokes.Add(circleStroke);
            var circle = new CircleParser(Reg).Parse(d);
            Assert.AreEqual("flame | more | ring", circle.ToRecipe().ToString(), Messages(circle));
        }

        [Test]
        public void GuideCircleIsNotAGlyph()
        {
            var d = Page("flame | more | ring");
            d.Strokes.Add(MagicTestData.Circle(new Vec2(300, 300), 0.63f * 200f));
            var circle = new CircleParser(Reg).Parse(d);
            Assert.Greater(circle.GuideRadius, 0f);
            Assert.AreEqual("flame | more | ring", circle.ToRecipe().ToString(), Messages(circle));
        }

        [Test]
        public void ScribbleInsideIsReportedAndIgnored()
        {
            var d = Page("water | | line");
            var rng = new Pcg32(4);
            var scribble = new Stroke();
            for (int i = 0; i < 40; i++) scribble.Points.Add(new Vec2(300 + rng.Range(-30f, 30f), 400 + rng.Range(-30f, 30f)));
            d.Strokes.Add(scribble);
            var circle = new CircleParser(Reg).Parse(d);
            Assert.AreEqual("water | | line", circle.ToRecipe().ToString());
            Assert.AreEqual(1, circle.Unrecognized.Count, Messages(circle));
            Assert.That(circle.Diagnostics.Any(x => x.Severity == Severity.Warning && x.Message.Contains("wasn't clear enough")));
        }

        [Test]
        public void InkOutsideTheCircleIsIgnored()
        {
            var d = Page("water | | line");
            d.Strokes.AddRange(Reg["flame"].Render(new Vec2(900, 900), 50f));
            var circle = new CircleParser(Reg).Parse(d);
            Assert.AreEqual("water | | line", circle.ToRecipe().ToString());
            Assert.That(circle.Diagnostics.Any(x => x.Message.Contains("outside the circle")));
        }

        [Test]
        public void MisplacedGlyphWarns()
        {
            var d = new Drawing();
            var c = new Vec2(0, 0);
            d.Strokes.Add(RecipeDrawer.CircleStroke(c, 100f));
            d.Strokes.AddRange(Reg["water"].Render(c, 40f));
            d.Strokes.AddRange(Reg["seek"].Render(c + new Vec2(0, 82), 25f)); // an inner glyph on the outer ring
            d.Strokes.AddRange(Reg["line"].Render(c + new Vec2(0, -80), 25f));
            var circle = new CircleParser(Reg).Parse(d);
            Assert.That(circle.Diagnostics.Any(x => x.Severity == Severity.Warning && x.Message.Contains("Seek is an inner glyph")), Messages(circle));
            Assert.AreEqual("water | seek | line", circle.ToRecipe().ToString());
        }

        [Test]
        public void ParsingIsDeterministicAcrossParsers()
        {
            var rng = new Pcg32(21);
            var d = MagicTestData.SloppySpell("growth | heavy | delay line return", ref rng, MagicTestData.Sloppiness.Default, Vec2.Zero, 250f);
            var a = SpellCompiler.Compile(new CircleParser(Reg).Parse(d));
            var b = SpellCompiler.Compile(new CircleParser(Reg).Parse(d));
            Assert.AreEqual(a.Json, b.Json);
            Assert.AreEqual(a.Quality, b.Quality);
            Assert.AreEqual(a.Mana, b.Mana);
        }

        [Test]
        public void CachedReparseAfterNewStrokeIsFast()
        {
            var parser = new CircleParser(Reg);
            var rng = new Pcg32(8);
            var full = MagicTestData.SloppySpell("wind | lasting more less | ring line cone", ref rng, MagicTestData.Sloppiness.Default, Vec2.Zero, 250f);
            var partial = new Drawing(full.Strokes.Take(full.Strokes.Count - 1));
            parser.Parse(partial);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            parser.Parse(full);
            sw.Stop();
            TestContext.WriteLine($"re-parse after one new stroke: {sw.ElapsedMilliseconds} ms ({full.Strokes.Count} strokes)");
            Assert.Less(sw.ElapsedMilliseconds, 500);
        }

        // ---------- Data ----------

        [Test]
        public void BadGlyphDataIsRejectedWithAClearMessage()
        {
            var ex = Assert.Throws<FormatException>(() => GlyphRegistry.FromJson("{\"glyphs\":[{\"id\":\"x\",\"role\":\"Middle\",\"strokes\":[[[0,0],[1,1]]]}]}"));
            StringAssert.Contains("role", ex.Message);
            Assert.Throws<FormatException>(() => GlyphRegistry.FromJson("{\"glyphs\":[{\"id\":\"x\",\"role\":\"Core\",\"strokes\":[[[0,0],[1,1]]]}]}"));
            Assert.Throws<FormatException>(() => GlyphRegistry.FromJson("{\"glyphs\":[{\"id\":\"x\",\"role\":\"Inner\",\"effect\":\"Line\",\"strokes\":[[[0,0],[1,1]]]}]}"));
        }

        [Test]
        public void NewGlyphsCanBeAddedAsData()
        {
            var reg = GlyphRegistry.FromJson(System.IO.File.ReadAllText(MagicTestData.FindGlyphFile()));
            reg.AddJson("{\"glyphs\":[{\"id\":\"tidepool\",\"name\":\"Tidepool\",\"role\":\"Inner\",\"effect\":\"Lasting\"," +
                        "\"strokes\":[[[-1,-1],[1,-1],[1,1],[-1,1],[-1,-1]],[[-0.5,-0.5],[0.5,0.5]]]}]}");
            var s = SpellCompiler.Compile(SpellRecipe.Parse("water | tidepool | line"), reg);
            Assert.IsTrue(s.Success);
            var circle = new CircleParser(reg).Parse(RecipeDrawer.Draw(SpellRecipe.Parse("water | tidepool | line"), reg, Vec2.Zero, 200f));
            Assert.AreEqual("tidepool", circle.Inner[0].Glyph.Id);
        }

        [Test]
        public void RecipeJsonRoundTripsAndRejectsUnknownFields()
        {
            var r = SpellRecipe.Parse("water | split+ seek | line delay- ring");
            var back = SpellRecipe.FromJson(r.ToJson());
            Assert.AreEqual(r.ToString(), back.ToString());
            Assert.Throws<FormatException>(() => SpellRecipe.FromJson("{\"core\":\"water\",\"power\":9000}"));
        }
    }
}
