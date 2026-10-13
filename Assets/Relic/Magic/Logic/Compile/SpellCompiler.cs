using System;
using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Magic
{
    public sealed class SpellCompileOptions
    {
        /// <summary>Power budget (pp) the validator checks against: the caster's skill. Default: Rare at tier 0.</summary>
        public float Budget = PowerBudget.For(Rarity.Rare, 0);
        public ISpellNamer Namer = new StubSpellNamer();
    }

    /// <summary>The result of compiling a spell circle.</summary>
    public sealed class CompiledSpell
    {
        /// <summary>The castable program, or null when there are errors.</summary>
        public EffectProgram Program;
        /// <summary>What the program would be, even with errors (for showing the player what they made).</summary>
        public EffectProgram Draft;
        public SpellCircle Circle;
        public SpellFlavor Flavor;

        /// <summary>Intrinsic power (pp): the actions priced at a 4 s baseline, before mana and cooldown.</summary>
        public float Power;
        /// <summary>PowerBudget.Cost of the final program (what EffectValidator compares to the budget).</summary>
        public float Cost;
        public float Budget;
        public float Cooldown;
        public float Mana;
        public float MisfireChance;
        /// <summary>0–1 weighted drawing precision.</summary>
        public float Quality;
        /// <summary>Ink needed to write it on a page: one per glyph plus one per pp.</summary>
        public int Ink;

        public readonly List<SpellDiagnostic> Diagnostics = new List<SpellDiagnostic>();

        public bool Success => Program != null;

        public IEnumerable<SpellDiagnostic> Errors
        {
            get
            {
                foreach (var d in Diagnostics)
                    if (d.Severity == Severity.Error)
                        yield return d;
            }
        }

        public string Json => (Program ?? Draft) == null ? "" : EffectJson.Serialize(Program ?? Draft, indented: true);
    }

    /// <summary>
    /// Compiles a spell circle into an EffectProgram with one Active rule:
    /// core → payload, inner glyphs → magnitudes and emitter flags, ring glyphs
    /// (clockwise from the top) → a sequence of shapes. Cooldown and mana follow
    /// from the program's power (PowerBudget), and drawing precision scales how
    /// efficiently that power is paid for.
    ///
    /// Ring reading:
    /// - Line, Cone, Ring, Orbit each add a shape (one action) in order.
    /// - Delay makes every shape after it wait.
    /// - Return makes the shape before it (or after, if first) come back.
    /// - Wall turns the Line next to it into a standing wall.
    /// - No shape at all: the spell bursts where you aim.
    /// </summary>
    public static class SpellCompiler
    {
        sealed class Beat
        {
            public GlyphEffect Shape; // Line/Cone/Ring/Orbit, or None for a burst
            public float Strength = 1f;
            public float Delay;
            public bool Returning;
            public bool Wall;
            public float WallStrength = 1f;
            public bool IsProjectile => Shape != GlyphEffect.None && !Wall;
        }

        sealed class Mods
        {
            public float Damage = 1f, Status = 1f, Knockback = 1f, Range = 1f, Radius = 1f, AreaDuration = 1f, Size = 1f, Speed = 1f;
            public int CountBonus, SplitCount;
            public bool Seek;
            public readonly List<GlyphInstance> ProjectileOnly = new List<GlyphInstance>();
        }

        public static CompiledSpell Compile(SpellCircle circle, SpellCompileOptions options = null)
        {
            options = options ?? new SpellCompileOptions();
            var result = new CompiledSpell { Circle = circle, Budget = options.Budget, Quality = circle.Quality };
            result.Diagnostics.AddRange(circle.Diagnostics);
            var diag = result.Diagnostics;

            // ---- Core ----
            CoreSubstance core = null;
            if (circle.Cores.Count == 0)
            {
                diag.Add(new SpellDiagnostic(Severity.Error,
                    "There's nothing at the center. Draw a core sigil (what the spell is made of) in the middle of the circle."));
            }
            else
            {
                core = circle.Cores[0].Glyph.Core;
                if (circle.Cores.Count > 1)
                {
                    var names = new List<string>();
                    foreach (var g in circle.Cores) names.Add(g.Glyph.Name);
                    diag.Add(new SpellDiagnostic(Severity.Error,
                        $"Only one core sigil fits at the center, but this circle has {JoinAnd(names)}.", circle.Cores[1]));
                }
            }

            var mods = ReadModifiers(circle, diag);
            var beats = ReadRing(circle, diag);

            if (core == null)
            {
                result.Draft = null;
                return result;
            }

            // ---- Actions ----
            var actions = new List<ActionSpec>();
            int projectiles = 0;
            bool anyProjectiles = false;
            foreach (var b in beats)
            {
                if (b.IsProjectile)
                {
                    anyProjectiles = true;
                    var emit = BuildEmit(b, core, mods);
                    projectiles = Math.Max(projectiles, emit.Emitter.Count * (1 + emit.Emitter.SplitCount));
                    actions.Add(emit);
                }
                else if (b.Wall) BuildWall(b, core, mods, actions);
                else actions.Add(BuildBurst(b, core, mods));
            }

            if (!anyProjectiles)
                foreach (var g in mods.ProjectileOnly)
                    diag.Add(new SpellDiagnostic(Severity.Warning, $"{g.Glyph.Name} only changes projectiles, and this spell has none.", g));

            if (actions.Count > EffectValidator.MaxActionsPerList)
                diag.Add(new SpellDiagnostic(Severity.Error,
                    $"This circle tries to do too much at once: {actions.Count} effects, and a circle holds {EffectValidator.MaxActionsPerList}. Remove a shape from the ring (a Wall counts as two)."));
            if (projectiles > EffectValidator.MaxProjectiles)
                diag.Add(new SpellDiagnostic(Severity.Error,
                    $"That's {projectiles} projectiles from one shape, and a spell can hold at most {EffectValidator.MaxProjectiles}. Remove a Split or use a smaller shape."));

            var rule = new EffectRule
            {
                Trigger = new TriggerSpec { Kind = TriggerKind.Active },
                CostResource = ResourceId.Mana,
                Actions = actions,
            };
            var program = new EffectProgram();
            program.Rules.Add(rule);

            // ---- Price ----
            float power = PowerBudget.SumActions(actions);
            result.Power = power;
            if (power > SpellTuning.MaxPower)
                diag.Add(new SpellDiagnostic(Severity.Error,
                    $"Too much power for one circle ({F(power)} of {F(SpellTuning.MaxPower)}). Remove a glyph, or add Less."));

            float cooldown = SpellTuning.CooldownPerSqrtPower * (float)Math.Sqrt(Math.Max(0f, power));
            float mana = SpellTuning.ManaScale * (float)Math.Pow(Math.Max(0f, power), SpellTuning.ManaExponent);

            float sloppy = Clamp01((SpellTuning.CleanQuality - result.Quality) / (SpellTuning.CleanQuality - SpellTuning.WorstQuality));
            cooldown *= 1f + SpellTuning.SloppyCooldown * sloppy;
            mana *= 1f + SpellTuning.SloppyMana * sloppy;
            float misfire = SpellTuning.SloppyMisfire * sloppy;

            rule.Cooldown = Math.Min(SpellTuning.MaxCooldown, Math.Max(SpellTuning.MinCooldown, Round(cooldown, 0.1)));
            rule.CostAmount = Math.Max(1f, (float)Math.Round(mana));
            rule.Chance = Math.Max(0.05f, Round(1f - misfire, 0.01));
            result.Cooldown = rule.Cooldown;
            result.Mana = rule.CostAmount;
            result.MisfireChance = 1f - rule.Chance;
            if (sloppy > 0f)
                diag.Add(new SpellDiagnostic(Severity.Info,
                    $"Shaky ink (precision {result.Quality:P0}): this spell costs {sloppy * SpellTuning.SloppyMana:P0} more mana, recharges {sloppy * SpellTuning.SloppyCooldown:P0} slower and misfires {result.MisfireChance:P0} of the time. Cleaner glyphs fix that."));

            int glyphs = 0;
            foreach (var _ in circle.AllGlyphs) glyphs++;
            result.Ink = glyphs + (int)Math.Ceiling(power);

            // ---- Name ----
            var flavor = (options.Namer ?? new StubSpellNamer()).Name(circle, program);
            result.Flavor = flavor;
            rule.Name = flavor?.Name;

            // ---- Validate ----
            var v = EffectValidator.Validate(program, options.Budget);
            result.Cost = v.Cost;
            foreach (var e in v.Errors) diag.Add(new SpellDiagnostic(Severity.Error, Translate(e)));
            if (v.OverBudget)
                diag.Add(new SpellDiagnostic(Severity.Error,
                    $"This spell is beyond your skill: it needs {F(v.Cost)} power and you can hold {F(v.Budget)}. Remove a glyph or add Less."));

            result.Draft = program;
            bool hasErrors = false;
            foreach (var d in diag) if (d.Severity == Severity.Error) hasErrors = true;
            result.Program = hasErrors ? null : program;
            return result;
        }

        /// <summary>Compiles a recipe as if its glyphs were placed with the palette (or copied from a page).</summary>
        public static CompiledSpell Compile(SpellRecipe recipe, GlyphRegistry registry, float precision = 1f, SpellCompileOptions options = null) =>
            Compile(SpellCircle.FromRecipe(recipe, registry, precision), options);

        // ---------------- Reading ----------------

        static Mods ReadModifiers(SpellCircle circle, List<SpellDiagnostic> diag)
        {
            var m = new Mods();
            bool firstSplit = true;
            foreach (var g in circle.Inner)
            {
                float e = g.Strength;
                switch (g.Glyph.Effect)
                {
                    case GlyphEffect.More:
                        m.Damage *= 1f + SpellTuning.MoreDamage * e;
                        m.Knockback *= 1f + SpellTuning.MoreKnockback * e;
                        break;
                    case GlyphEffect.Less:
                        m.Damage *= Math.Max(0.1f, 1f - SpellTuning.LessDamage * e);
                        m.Status *= Math.Max(0.1f, 1f - SpellTuning.LessDuration * e);
                        m.Knockback *= Math.Max(0.1f, 1f - SpellTuning.LessDuration * e);
                        break;
                    case GlyphEffect.Far:
                        m.Range *= 1f + SpellTuning.FarRange * e;
                        m.Radius *= 1f + SpellTuning.FarRadius * e;
                        break;
                    case GlyphEffect.Lasting:
                        m.Status *= 1f + SpellTuning.LastingDuration * e;
                        m.AreaDuration *= 1f + SpellTuning.LastingDuration * e;
                        break;
                    case GlyphEffect.Split:
                        int n = SpellTuning.SplitAmount[(int)g.Emphasis];
                        if (firstSplit) m.CountBonus += n; else m.SplitCount += n;
                        firstSplit = false;
                        m.ProjectileOnly.Add(g);
                        break;
                    case GlyphEffect.Seek:
                        if (m.Seek) diag.Add(new SpellDiagnostic(Severity.Warning, "Seek only needs to be drawn once; the second one does nothing.", g));
                        m.Seek = true;
                        m.ProjectileOnly.Add(g);
                        break;
                    case GlyphEffect.Heavy:
                        m.Size *= 1f + SpellTuning.HeavySize * e;
                        m.Speed *= Math.Max(0.2f, 1f - SpellTuning.HeavySlow * e);
                        m.Damage *= 1f + SpellTuning.HeavyDamage * e;
                        m.Radius *= 1f + SpellTuning.HeavyRadius * e;
                        break;
                    case GlyphEffect.Swift:
                        m.Speed *= 1f + SpellTuning.SwiftSpeed * e;
                        m.ProjectileOnly.Add(g);
                        break;
                }
            }
            return m;
        }

        static List<Beat> ReadRing(SpellCircle circle, List<SpellDiagnostic> diag)
        {
            var beats = new List<Beat>();
            float delay = 0f;
            GlyphInstance pendingReturn = null, pendingWall = null, lastDelay = null;
            bool delayUsed = true;

            foreach (var g in circle.Ring)
            {
                var effect = g.Glyph.Effect;
                switch (effect)
                {
                    case GlyphEffect.Line:
                    case GlyphEffect.Cone:
                    case GlyphEffect.Ring:
                    case GlyphEffect.Orbit:
                    {
                        var b = new Beat { Shape = effect, Strength = g.Strength, Delay = delay };
                        delayUsed = true;
                        if (pendingWall != null)
                        {
                            if (effect == GlyphEffect.Line) { b.Wall = true; b.WallStrength = pendingWall.Strength; }
                            else diag.Add(WallNeedsLine(pendingWall));
                            pendingWall = null;
                        }
                        if (pendingReturn != null)
                        {
                            ApplyReturn(b, pendingReturn, diag);
                            pendingReturn = null;
                        }
                        beats.Add(b);
                        break;
                    }
                    case GlyphEffect.Delay:
                        delay += SpellTuning.DelaySeconds * g.Strength;
                        delayUsed = false;
                        lastDelay = g;
                        break;
                    case GlyphEffect.Return:
                        if (beats.Count > 0 && !beats[beats.Count - 1].Returning && beats[beats.Count - 1].IsProjectile) ApplyReturn(beats[beats.Count - 1], g, diag);
                        else if (pendingReturn == null) pendingReturn = g;
                        else diag.Add(new SpellDiagnostic(Severity.Warning, "Two Returns in a row: the second has nothing to bring back.", g));
                        break;
                    case GlyphEffect.Wall:
                        var last = beats.Count > 0 ? beats[beats.Count - 1] : null;
                        if (last != null && last.Shape == GlyphEffect.Line && !last.Wall && !last.Returning)
                        {
                            last.Wall = true;
                            last.WallStrength = g.Strength;
                        }
                        else if (pendingWall == null) pendingWall = g;
                        else diag.Add(WallNeedsLine(g));
                        break;
                }
            }

            if (pendingWall != null) diag.Add(WallNeedsLine(pendingWall));
            if (pendingReturn != null)
                diag.Add(new SpellDiagnostic(Severity.Error,
                    "Return needs something to bring back: put it right after a Line, Cone or Ring on the ring.", pendingReturn));
            if (!delayUsed && lastDelay != null)
                diag.Add(new SpellDiagnostic(Severity.Warning,
                    "The last Delay on the ring has nothing after it to wait for. Move it before the shape it should hold back (clockwise from the top).", lastDelay));

            if (beats.Count == 0)
            {
                beats.Add(new Beat { Shape = GlyphEffect.None, Delay = delay });
                diag.Add(new SpellDiagnostic(Severity.Info, "No shape on the ring, so the spell bursts where you aim. Add a Line, Cone, Ring or Orbit to shape it."));
            }
            return beats;
        }

        static void ApplyReturn(Beat b, GlyphInstance g, List<SpellDiagnostic> diag)
        {
            if (b.Shape == GlyphEffect.Orbit)
                diag.Add(new SpellDiagnostic(Severity.Warning, "An Orbit already circles you, so Return does nothing to it.", g));
            else if (b.Wall)
                diag.Add(new SpellDiagnostic(Severity.Error, "A Wall stands still, so there's nothing for Return to bring back.", g));
            else b.Returning = true;
        }

        static SpellDiagnostic WallNeedsLine(GlyphInstance g) =>
            new SpellDiagnostic(Severity.Error, "A Wall needs a Line to shape it: draw a Line right before or after it on the ring.", g);

        // ---------------- Building ----------------

        static List<ActionSpec> Payload(CoreSubstance core, Mods m, bool inArea)
        {
            var target = inArea ? TargetKind.EnemiesInArea : TargetKind.Target;
            var list = new List<ActionSpec>();
            float damage = Round(core.Damage * m.Damage, 0.1);
            if (damage > 0f) list.Add(new ActionSpec { Kind = ActionKind.Damage, Target = target, Amount = damage, DamageKind = core.DamageKind });
            if (core.Status != StatusId.None && core.StatusDuration > 0f)
            {
                float dur = Clamp(Round(core.StatusDuration * m.Status, 0.1), 0.1f, EffectValidator.MaxStatusDuration);
                list.Add(new ActionSpec { Kind = ActionKind.ApplyStatus, Target = target, Status = core.Status, Duration = dur });
            }
            if (core.Knockback > 0f)
            {
                float k = Clamp(Round(core.Knockback * m.Knockback, 0.1), 0.1f, 15f);
                list.Add(new ActionSpec { Kind = ActionKind.Knockback, Target = target, Amount = k });
            }
            return list;
        }

        static ActionSpec BuildEmit(Beat b, CoreSubstance core, Mods m)
        {
            var e = new EmitterSpec { Size = SpellTuning.ProjectileSize };
            float range;
            switch (b.Shape)
            {
                case GlyphEffect.Line:
                    e.Count = 1 + m.CountBonus;
                    e.Speed = SpellTuning.LineSpeed;
                    range = SpellTuning.LineRange * b.Strength;
                    if (e.Count > 1)
                    {
                        e.Shape = EmitShape.Cone;
                        e.SpreadDegrees = Math.Min(SpellTuning.LineFanMax, SpellTuning.LineFanStep * (e.Count - 1));
                    }
                    else e.Shape = EmitShape.Single;
                    break;
                case GlyphEffect.Cone:
                    e.Shape = EmitShape.Cone;
                    e.Count = Math.Max(2, (int)Math.Round(SpellTuning.ConeCount * b.Strength)) + m.CountBonus;
                    e.SpreadDegrees = Math.Min(120f, Round(SpellTuning.ConeSpread * b.Strength, 1.0));
                    e.Speed = SpellTuning.ConeSpeed;
                    range = SpellTuning.ConeRange;
                    break;
                case GlyphEffect.Ring:
                    e.Shape = EmitShape.Ring;
                    e.Count = Math.Max(3, (int)Math.Round(SpellTuning.RingCount * b.Strength)) + m.CountBonus;
                    e.SpreadDegrees = 360f;
                    e.Speed = SpellTuning.RingSpeed;
                    range = SpellTuning.RingRange;
                    break;
                default: // Orbit: Range is the lifetime in seconds
                    e.Shape = EmitShape.Orbit;
                    e.Count = Math.Max(1, (int)Math.Round(SpellTuning.OrbitCount * b.Strength)) + m.CountBonus;
                    e.Speed = SpellTuning.OrbitSpeed;
                    e.Size = SpellTuning.OrbitSize;
                    range = SpellTuning.OrbitLifetime * m.AreaDuration;
                    break;
            }
            if (b.Shape != GlyphEffect.Orbit) range *= m.Range;
            e.Range = Clamp(Round(range, 0.1), 1f, EffectValidator.MaxRange);
            e.Speed = Clamp(Round(e.Speed * m.Speed, 0.1), EffectValidator.MinSpeed, EffectValidator.MaxSpeed);
            e.Size = Clamp(Round(e.Size * m.Size, 0.01), 0.05f, 3f);
            e.Seeking = m.Seek;
            e.Returning = b.Returning;
            e.SplitCount = m.SplitCount;
            e.Delay = Clamp(Round(b.Delay, 0.1), 0f, 5f);
            return new ActionSpec { Kind = ActionKind.Emit, Emitter = e, Payload = Payload(core, m, inArea: false) };
        }

        static void BuildWall(Beat b, CoreSubstance core, Mods m, List<ActionSpec> into)
        {
            float length = Clamp(Round(SpellTuning.WallLength * b.Strength * b.WallStrength * m.Radius, 0.1), 1f, 20f);
            float duration = Clamp(Round(SpellTuning.WallDuration * m.AreaDuration, 0.1), 0.5f, EffectValidator.MaxAreaDuration);
            float delay = Clamp(Round(b.Delay, 0.1), 0f, 5f);
            into.Add(new ActionSpec
            {
                Kind = ActionKind.CreateBarrier,
                Area = new AreaSpec { Shape = AreaShape.Wall, Length = length, Width = SpellTuning.WallWidth, Duration = duration, Delay = delay },
            });
            // Touching the wall applies the core's substance.
            into.Add(new ActionSpec
            {
                Kind = ActionKind.CreateArea,
                Area = new AreaSpec
                {
                    Shape = AreaShape.Line, Length = length, Width = SpellTuning.WallTouchWidth, Duration = duration,
                    TickInterval = SpellTuning.WallTick, Delay = delay,
                },
                Payload = Payload(core, m, inArea: true),
            });
        }

        static ActionSpec BuildBurst(Beat b, CoreSubstance core, Mods m)
        {
            return new ActionSpec
            {
                Kind = ActionKind.CreateArea,
                Area = new AreaSpec
                {
                    Shape = AreaShape.Circle,
                    Radius = Clamp(Round(SpellTuning.BurstRadius * m.Radius, 0.1), 0.5f, EffectValidator.MaxAreaRadius),
                    Duration = Clamp(Round(SpellTuning.BurstDuration * m.AreaDuration, 0.1), 0.1f, EffectValidator.MaxAreaDuration),
                    Delay = Clamp(Round(b.Delay, 0.1), 0f, 5f),
                },
                Payload = Payload(core, m, inArea: true),
            };
        }

        // ---------------- Helpers ----------------

        /// <summary>Validator messages are for programmers; this makes them readable at a desk.</summary>
        static string Translate(string error)
        {
            if (error.Contains("projectiles exceeds")) return "Too many projectiles for one spell. Remove a Split.";
            if (error.Contains("speed must be")) return "The projectiles are too fast or too slow to hold together.";
            if (error.Contains("range must be")) return "The spell reaches too far to hold together.";
            if (error.Contains("too many actions")) return "This circle tries to do too much at once.";
            return "The spell doesn't hold together (" + error + ").";
        }

        static string JoinAnd(List<string> items)
        {
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        static float Round(float v, double step) => (float)(Math.Round(v / step) * step);
        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
