using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Tests;

namespace Relic.Magic.Tests
{
    public class SpellCompilerTests
    {
        static GlyphRegistry Reg => MagicTestData.Registry;

        static CompiledSpell Compile(string recipe, float precision = 1f) =>
            SpellCompiler.Compile(SpellRecipe.Parse(recipe), Reg, precision);

        static string Messages(CompiledSpell s) => string.Join("\n", s.Diagnostics.Select(d => d.ToString()));

        // ---------- Acceptance: the example spell ----------

        [Test]
        public void ExampleSpellCompilesToTideSeekers()
        {
            var spell = Compile("water | split seek | line");
            Assert.IsTrue(spell.Success, Messages(spell));
            ProgramAssert.Close(EffectGrammarTests.WaterBolts(), spell.Program);
            Assert.AreEqual("Tide Seekers", spell.Program.Rules[0].Name);
            TestContext.WriteLine(spell.Json);
            TestContext.WriteLine($"power {spell.Power:0.00} pp, cost {spell.Cost:0.00} pp, cooldown {spell.Cooldown}, mana {spell.Mana}; {spell.Flavor.Description}");
        }

        [Test]
        public void CleanlyDrawnExampleAlsoCompilesToTideSeekers()
        {
            var parser = new CircleParser(Reg);
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var rng = new Pcg32(seed);
                var drawing = MagicTestData.SloppySpell("water | split seek | line", ref rng, MagicTestData.Tidy, new Vec2(400, 400), 300f);
                var spell = SpellCompiler.Compile(parser.Parse(drawing));
                Assert.IsTrue(spell.Success, $"seed {seed}: " + Messages(spell));
                ProgramAssert.Close(EffectGrammarTests.WaterBolts(), spell.Program);
            }
        }

        [Test]
        public void HandDrawnSpellsAtNormalSloppinessStillCompile()
        {
            var parser = new CircleParser(Reg);
            int ok = 0, total = 0;
            foreach (var page in PrewrittenSpells.All)
                for (ulong seed = 1; seed <= 4; seed++)
                {
                    var rng = new Pcg32(Hash.Combine(seed, Hash.Of(page.Recipe)));
                    var drawing = MagicTestData.SloppySpell(page.Recipe, ref rng, MagicTestData.Sloppiness.Default, new Vec2(0, 0), 250f);
                    var circle = parser.Parse(drawing);
                    var spell = SpellCompiler.Compile(circle);
                    total++;
                    // Same glyphs read back (emphasis may wobble with hand-drawn sizes).
                    bool same = circle.ToRecipe().ToString().Replace("+", "").Replace("-", "") ==
                                SpellRecipe.Parse(page.Recipe).ToString();
                    if (spell.Success && same) ok++;
                    else TestContext.WriteLine($"{page.Title} seed {seed}: read '{circle.ToRecipe()}'\n{Messages(spell)}");
                }
            TestContext.WriteLine($"hand-drawn spells read correctly: {ok}/{total}");
            Assert.GreaterOrEqual(ok, total * 9 / 10);
        }

        // ---------- Pricing ----------

        [Test]
        public void MoreCostsMoreAndLessCostsLess()
        {
            var plain = Compile("flame | | line");
            var more = Compile("flame | more | line");
            var less = Compile("flame | less | line");
            Assert.Greater(more.Power, plain.Power);
            Assert.Greater(more.Mana, plain.Mana);
            Assert.GreaterOrEqual(more.Cooldown, plain.Cooldown);
            Assert.Less(less.Mana, plain.Mana);
            Assert.LessOrEqual(less.Cooldown, plain.Cooldown);
        }

        [Test]
        public void ManaGrowsFasterThanPower()
        {
            var small = Compile("water | | line");
            var big = Compile("water | more more split | line");
            float powerRatio = big.Power / small.Power;
            float manaRatio = big.Mana / small.Mana;
            Assert.Greater(manaRatio, powerRatio);
        }

        [Test]
        public void SloppyInkCostsMoreAndMisfires()
        {
            var clean = Compile("light | | cone", 1f);
            var shaky = Compile("light | | cone", 0.45f);
            Assert.IsTrue(shaky.Success, Messages(shaky));
            Assert.Greater(shaky.Mana, clean.Mana);
            Assert.Greater(shaky.Cooldown, clean.Cooldown);
            Assert.Greater(shaky.MisfireChance, 0f);
            Assert.AreEqual(0f, clean.MisfireChance);
            Assert.That(shaky.Diagnostics.Any(d => d.Message.Contains("Shaky ink")));
        }

        [Test]
        public void MessyHandDrawingIsPricierThanTidyOne()
        {
            var parser = new CircleParser(Reg);
            var rng = new Pcg32(3);
            var tidy = SpellCompiler.Compile(parser.Parse(MagicTestData.SloppySpell("stone | more | cone", ref rng, MagicTestData.Tidy, Vec2.Zero, 250f)));
            var messy = SpellCompiler.Compile(parser.Parse(MagicTestData.SloppySpell("stone | more | cone", ref rng, MagicTestData.Messy, Vec2.Zero, 250f)));
            TestContext.WriteLine($"tidy quality {tidy.Quality:0.00} mana {tidy.Mana}; messy quality {messy.Quality:0.00} mana {messy.Mana}");
            Assert.Greater(tidy.Quality, messy.Quality);
            if (messy.Success) Assert.GreaterOrEqual(messy.Mana, tidy.Mana);
        }

        // ---------- Ring sequence ----------

        [Test]
        public void DelayHoldsBackEverythingAfterItClockwise()
        {
            var s = Compile("flame | | ring delay line");
            Assert.IsTrue(s.Success, Messages(s));
            var actions = s.Program.Rules[0].Actions;
            Assert.AreEqual(EmitShape.Ring, actions[0].Emitter.Shape);
            Assert.AreEqual(0f, actions[0].Emitter.Delay);
            Assert.AreEqual(EmitShape.Single, actions[1].Emitter.Shape);
            Assert.AreEqual(SpellTuning.DelaySeconds, actions[1].Emitter.Delay, 1e-4f);
        }

        [Test]
        public void ReturnAppliesToTheShapeBeforeIt()
        {
            var s = Compile("growth | | line return cone");
            Assert.IsTrue(s.Success, Messages(s));
            var actions = s.Program.Rules[0].Actions;
            Assert.IsTrue(actions[0].Emitter.Returning);
            Assert.IsFalse(actions[1].Emitter.Returning);
        }

        [Test]
        public void WallWithLineMakesABarrierThatAppliesTheCore()
        {
            var s = Compile("stone | lasting | line wall");
            Assert.IsTrue(s.Success, Messages(s));
            var actions = s.Program.Rules[0].Actions;
            Assert.AreEqual(ActionKind.CreateBarrier, actions[0].Kind);
            Assert.AreEqual(AreaShape.Wall, actions[0].Area.Shape);
            Assert.AreEqual(ActionKind.CreateArea, actions[1].Kind);
            Assert.AreEqual(TargetKind.EnemiesInArea, actions[1].Payload[0].Target);
            Assert.That(actions[1].Payload.Any(p => p.Status == StatusId.Staggered));
        }

        [Test]
        public void NoShapeBurstsWhereYouAim()
        {
            var s = Compile("flame | more |");
            Assert.IsTrue(s.Success, Messages(s));
            Assert.AreEqual(ActionKind.CreateArea, s.Program.Rules[0].Actions[0].Kind);
            Assert.AreEqual(AreaShape.Circle, s.Program.Rules[0].Actions[0].Area.Shape);
        }

        [Test]
        public void EmphasisChangesStrength()
        {
            var small = Compile("water | split- | line");
            var normal = Compile("water | split | line");
            var large = Compile("water | split+ | line");
            Assert.AreEqual(2, small.Program.Rules[0].Actions[0].Emitter.Count);
            Assert.AreEqual(3, normal.Program.Rules[0].Actions[0].Emitter.Count);
            Assert.AreEqual(4, large.Program.Rules[0].Actions[0].Emitter.Count);
        }

        // ---------- Plain-language errors ----------

        static void AssertError(string recipe, string fragment)
        {
            var s = Compile(recipe);
            Assert.IsFalse(s.Success, recipe + " should not compile");
            Assert.IsNull(s.Program);
            Assert.That(s.Errors.Any(e => e.Message.Contains(fragment)), $"expected an error containing '{fragment}', got:\n{Messages(s)}");
        }

        [Test] public void NoCoreIsRejected() => AssertError(" | more | line", "nothing at the center");
        [Test]
        public void TwoCoresAreRejected()
        {
            var circle = SpellCircle.FromRecipe(SpellRecipe.Parse("water | | line"), Reg);
            circle.Cores.Add(new GlyphInstance { Glyph = Reg["flame"] });
            var s = SpellCompiler.Compile(circle);
            Assert.IsFalse(s.Success);
            Assert.That(s.Errors.Any(e => e.Message.Contains("Only one core sigil") && e.Message.Contains("Water and Flame")), Messages(s));
        }
        [Test] public void WallNeedsALine() => AssertError("stone | | wall", "A Wall needs a Line");
        [Test] public void WallNextToAConeNeedsALine() => AssertError("stone | | cone wall", "A Wall needs a Line");
        [Test] public void ReturnAloneIsRejected() => AssertError("wind | | return", "Return needs something");
        [Test] public void TooManyProjectilesIsRejected() => AssertError("flame | split+ split+ split+ | ring+", "projectiles");
        [Test] public void TooManyShapesIsRejected() => AssertError("flame | | line line line line line", "too much at once");
        [Test] public void UnknownGlyphIsRejected() => AssertError("water | sparkle | line", "nobody here knows");

        [Test]
        public void ProjectileModifiersWithoutProjectilesWarn()
        {
            var s = Compile("water | seek | line wall");
            Assert.IsTrue(s.Success, Messages(s));
            Assert.That(s.Diagnostics.Any(d => d.Severity == Severity.Warning && d.Message.Contains("only changes projectiles")));
        }

        // ---------- Every pre-written spell ----------

        [Test]
        public void PrewrittenSpellsAreValidAndDeterministic()
        {
            foreach (var page in PrewrittenSpells.All)
            {
                var a = Compile(page.Recipe);
                var b = Compile(page.Recipe);
                Assert.IsTrue(a.Success, page.Title + ":\n" + Messages(a));
                Assert.AreEqual(a.Json, b.Json, page.Title);
                var v = EffectValidator.Validate(a.Program, a.Budget);
                Assert.IsTrue(v.IsValid, page.Title + ": " + v);
                // Survives the JSON contract (what saves and the LLM use).
                var back = EffectJson.Deserialize(EffectJson.Serialize(a.Program));
                Assert.AreEqual(EffectJson.Serialize(a.Program), EffectJson.Serialize(back));
                TestContext.WriteLine($"{a.Flavor.Name,-24} {page.Recipe,-36} power {a.Power,5:0.0}  mana {a.Mana,3}  cd {a.Cooldown,4:0.0}s  ink {a.Ink,2}  | {a.Flavor.Description}");
            }
        }
    }
}
