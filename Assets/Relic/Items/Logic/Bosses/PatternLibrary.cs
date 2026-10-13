using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Items.Bosses
{
    /// <summary>
    /// Pattern building blocks as EffectPrograms, sized by tier and built to pass
    /// the readability rules by construction (the checker verifies independently).
    /// Farther tiers get more projectiles, faster shots and shorter (never under
    /// 0.4 s) wind-ups, per 03-combat "harder through behavior".
    /// </summary>
    public static class PatternLibrary
    {
        static readonly string[] BlockTells =
        {
            "plants its feet and spreads its arms",                 // Ring
            "starts to turn on the spot, slowly at first",          // Spiral
            "fixes on one player and draws back",                   // AimedBurst
            "sweeps an arm back across its body",                   // Cone
            "a line scorches across the floor toward a player",     // Laser
            "the ground under a player cracks and starts to glow",  // AreaDenial
            "hurls its arms skyward; shadows bloom on the floor",   // Lobbed
            "slams the ground and a barrier grinds up",             // Wall
        };

        public static float HitDamage(int tier) => (float)Math.Round(10f * (1f + 0.4f * Clamp(tier)));

        static int Clamp(int t) => Math.Max(0, Math.Min(10, t));

        static float WindUp(PatternBlock b, int tier)
        {
            float baseline;
            switch (b)
            {
                case PatternBlock.Laser: baseline = 1.4f; break;
                case PatternBlock.AreaDenial: baseline = 1.2f; break;
                case PatternBlock.AimedBurst: baseline = 0.8f; break;
                default: baseline = 1.0f; break;
            }
            return (float)Math.Round(Math.Max(Readability.MinWindUp + 0.1f, baseline - 0.06f * Clamp(tier)), 2);
        }

        static List<ActionSpec> HitPayload(BossArchetype arch, int tier, ref Pcg32 rng, float damageScale = 1f, TargetKind target = TargetKind.Target)
        {
            var list = new List<ActionSpec>
            {
                new ActionSpec { Kind = ActionKind.Damage, Amount = (float)Math.Round(HitDamage(tier) * damageScale), DamageKind = arch.DamageKind, Target = target },
            };
            if (rng.Chance(0.6f))
            {
                var st = arch.Statuses[rng.Range(0, arch.Statuses.Length)];
                list.Add(new ActionSpec { Kind = ActionKind.ApplyStatus, Status = st, Duration = (float)Math.Round(Math.Min(15f, 2f + 0.2f * tier), 1), Stacks = 1, Target = target });
            }
            return list;
        }

        static EffectRule ActiveRule(string name) => new EffectRule { Name = name, Trigger = new TriggerSpec { Kind = TriggerKind.Active }, Cooldown = 1f };

        public static BossPattern Build(BossArchetype arch, PatternBlock block, int tier, ref Pcg32 rng)
        {
            tier = Clamp(tier);
            string name = arch.PatternNames[(int)block];
            var rule = ActiveRule(name);
            float wind = WindUp(block, tier);
            bool airborne = false;
            switch (block)
            {
                case PatternBlock.Ring:
                {
                    float size = 0.4f;
                    int count = Math.Min(Readability.MaxRingCount(tier, size), 8 + tier + rng.Range(0, 3));
                    rule.Actions.Add(Emit(EmitShape.Ring, count, 6f + 0.4f * tier, 18f, size, wind, HitPayload(arch, tier, ref rng)));
                    break;
                }
                case PatternBlock.Spiral:
                {
                    float size = 0.35f;
                    int count = Math.Min(Readability.MaxRingCount(tier, size), 10 + tier + rng.Range(0, 4));
                    rule.Actions.Add(Emit(EmitShape.Spiral, count, 5f + 0.3f * tier, 20f, size, wind, HitPayload(arch, tier, ref rng)));
                    break;
                }
                case PatternBlock.AimedBurst:
                {
                    var e = Emit(EmitShape.Burst, 3 + tier / 3, 11f + 0.6f * tier, 22f, 0.3f, wind, HitPayload(arch, tier, ref rng, 0.8f));
                    e.Emitter.SpreadDegrees = 15f + rng.Range(0, 3) * 5f;
                    rule.Actions.Add(e);
                    break;
                }
                case PatternBlock.Cone:
                {
                    float size = 0.35f, speed = 9f + 0.3f * tier;
                    float spread = 50f + rng.Range(0, 4) * 10f;
                    int count = tier <= Readability.LowTierMax ? Readability.MaxConeCount(tier, spread, size) : 5 + tier / 2;
                    if (tier > Readability.LowTierMax)
                        while (spread > 20f && Readability.ConeGap(count, spread, size) < Readability.RequiredGap(tier)
                               && Readability.SidestepNeeded(spread, size) > Readability.SidestepAvailable(wind, speed))
                            spread -= 5f;
                    var e = Emit(EmitShape.Cone, Math.Max(2, count), speed, 16f, size, wind, HitPayload(arch, tier, ref rng));
                    e.Emitter.SpreadDegrees = spread;
                    rule.Actions.Add(e);
                    break;
                }
                case PatternBlock.Laser:
                {
                    var area = new AreaSpec { Shape = AreaShape.Line, Length = 18f, Width = Math.Min(2.5f, 1.5f + 0.1f * tier), Duration = 1f, TickInterval = 0.25f, Delay = wind, FollowsSelf = true };
                    rule.Actions.Add(new ActionSpec { Kind = ActionKind.CreateArea, Area = area, Payload = HitPayload(arch, tier, ref rng, 0.5f, TargetKind.EnemiesInArea) });
                    break;
                }
                case PatternBlock.AreaDenial:
                {
                    var area = new AreaSpec { Shape = AreaShape.Circle, Radius = Math.Min(4.5f, 2.5f + 0.2f * tier), Duration = 4f + 0.3f * tier, TickInterval = 0.5f, Delay = wind };
                    rule.Actions.Add(new ActionSpec { Kind = ActionKind.CreateArea, Area = area, Payload = HitPayload(arch, tier, ref rng, 0.35f, TargetKind.EnemiesInArea) });
                    break;
                }
                case PatternBlock.Lobbed:
                {
                    var payload = HitPayload(arch, tier, ref rng);
                    payload.Add(Puddle(arch, tier, ref rng));
                    var e = Emit(EmitShape.Burst, Math.Min(7, 3 + tier / 3), 9f, 14f, 0.6f, wind, payload);
                    e.Emitter.SpreadDegrees = 70f;
                    e.Emitter.Airborne = true;
                    airborne = true;
                    rule.Actions.Add(e);
                    break;
                }
                case PatternBlock.Wall:
                {
                    rule.Actions.Add(new ActionSpec { Kind = ActionKind.CreateBarrier, Area = new AreaSpec { Shape = AreaShape.Wall, Length = 6f, Width = 1f, Duration = 5f } });
                    float size = 0.4f;
                    int count = Math.Min(Readability.MaxRingCount(tier, size), 10 + tier);
                    rule.Actions.Add(Emit(EmitShape.Ring, count, 7f + 0.3f * tier, 20f, size, Math.Min(5f, wind + 0.8f), HitPayload(arch, tier, ref rng)));
                    break;
                }
            }
            var p = new BossPattern
            {
                Name = name,
                Blocks = { block },
                Tell = Cap(BlockTells[(int)block]) + "; " + arch.TellFlourish + ".",
                Airborne = airborne,
                Program = new EffectProgram { Rules = { rule } },
            };
            Finish(p);
            return p;
        }

        static ActionSpec Emit(EmitShape shape, int count, float speed, float range, float size, float delay, List<ActionSpec> payload) =>
            new ActionSpec
            {
                Kind = ActionKind.Emit,
                Emitter = new EmitterSpec { Shape = shape, Count = count, Speed = (float)Math.Round(speed, 1), Range = range, Size = size, Delay = delay },
                Payload = payload,
            };

        static ActionSpec Puddle(BossArchetype arch, int tier, ref Pcg32 rng) =>
            new ActionSpec
            {
                Kind = ActionKind.CreateArea,
                Area = new AreaSpec { Shape = AreaShape.Circle, Radius = 1.5f, Duration = 3f, TickInterval = 0.5f, Delay = Readability.MinWindUp },
                Payload = HitPayload(arch, tier, ref rng, 0.3f, TargetKind.EnemiesInArea),
            };

        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Fills WindUp and Duration from the program, and sets the rule cooldown to the pattern's length.</summary>
        public static void Finish(BossPattern p)
        {
            float wind = float.MaxValue, end = 0f;
            foreach (var r in p.Program.Rules)
                foreach (var a in r.Actions)
                {
                    float delay = a.Emitter?.Delay ?? a.Area?.Delay ?? 0f;
                    float active = a.Emitter != null ? a.Emitter.Range / Math.Max(0.1f, a.Emitter.Speed) : a.Area?.Duration ?? 0.5f;
                    if (Readability.ContainsDamage(a.Payload)) wind = Math.Min(wind, delay);
                    end = Math.Max(end, delay + active);
                }
            p.WindUp = wind == float.MaxValue ? 0f : (float)Math.Round(wind, 2);
            p.Duration = (float)Math.Round(end, 1);
            foreach (var r in p.Program.Rules) r.Cooldown = Math.Max(1f, p.Duration);
        }

        // ---------- remixes: later phases change structure, not just speed ----------

        public static readonly string[] RemixOps = { "fused", "lingering", "returning", "shattering", "doubled" };

        /// <summary>True when the op changes this pattern's structure and stays within grammar limits.</summary>
        public static bool CanRemix(string op, BossPattern src, BossPattern other)
        {
            var rule = src.Program.Rules[0];
            var emit = rule.Actions.Find(a => a.Kind == ActionKind.Emit);
            switch (op)
            {
                case "fused":
                    return other != null && other != src && rule.Actions.Count + other.Program.Rules[0].Actions.Count <= EffectValidator.MaxActionsPerList;
                case "lingering":
                    return emit != null && !emit.Payload.Exists(x => x.Kind == ActionKind.CreateArea) && emit.Payload.Count < EffectValidator.MaxActionsPerList;
                case "returning":
                    return emit != null && !emit.Emitter.Returning && !emit.Emitter.Airborne;
                case "shattering":
                    return emit != null && emit.Emitter.SplitCount == 0 && !emit.Emitter.Airborne &&
                           (emit.Emitter.Shape == EmitShape.Burst || emit.Emitter.Shape == EmitShape.Cone || emit.Emitter.Shape == EmitShape.Single);
                case "doubled":
                    return rule.Actions.Count < EffectValidator.MaxActionsPerList &&
                           rule.Actions.Exists(a => (a.Kind == ActionKind.Emit || a.Kind == ActionKind.CreateArea) && Readability.ContainsDamage(a.Payload));
            }
            return false;
        }

        public static BossPattern Remix(string op, BossPattern src, BossPattern other, BossArchetype arch, int tier, ref Pcg32 rng)
        {
            var p = ItemJson.Clone(src);
            p.Remix = op;
            p.RemixOf = src.Name;
            var rule = p.Program.Rules[0];
            var emit = rule.Actions.Find(a => a.Kind == ActionKind.Emit);
            switch (op)
            {
                case "fused":
                {
                    var o = ItemJson.Clone(other);
                    float shift = (float)Math.Round(src.Duration * 0.5f + 0.6f, 1);
                    foreach (var a in o.Program.Rules[0].Actions)
                    {
                        if (a.Emitter != null) a.Emitter.Delay = Math.Min(5f, a.Emitter.Delay + shift);
                        if (a.Area != null && a.Kind != ActionKind.CreateBarrier) a.Area.Delay = Math.Min(5f, a.Area.Delay + shift);
                        rule.Actions.Add(a);
                    }
                    p.Name = src.Name + " into " + other.Name;
                    p.Blocks.AddRange(other.Blocks);
                    p.Airborne = src.Airborne || other.Airborne;
                    p.Tell = src.Tell;
                    break;
                }
                case "lingering":
                    emit.Payload.Add(Puddle(arch, tier, ref rng));
                    p.Name = src.Name + ", Lingering";
                    p.Tell = src.Tell + " Where the shots land, the floor stays dangerous.";
                    break;
                case "returning":
                    emit.Emitter.Returning = true;
                    p.Name = "Returning " + src.Name;
                    p.Tell = src.Tell + " The shots stop at the wall and come back.";
                    break;
                case "shattering":
                    emit.Emitter.SplitCount = 2;
                    emit.Emitter.Count = Math.Max(2, Math.Min(emit.Emitter.Count, EffectValidator.MaxProjectiles / 3));
                    p.Name = "Shattering " + src.Name;
                    p.Tell = src.Tell + " Each shot bursts into splinters where it strikes.";
                    break;
                case "doubled":
                {
                    var first = rule.Actions.Find(a => (a.Kind == ActionKind.Emit || a.Kind == ActionKind.CreateArea) && Readability.ContainsDamage(a.Payload));
                    var copy = ItemJson.Clone(first);
                    float shift = 0.7f + (copy.Emitter != null ? 0.3f : 0.6f);
                    if (copy.Emitter != null) copy.Emitter.Delay = Math.Min(5f, copy.Emitter.Delay + shift);
                    if (copy.Area != null) copy.Area.Delay = Math.Min(5f, copy.Area.Delay + shift);
                    rule.Actions.Add(copy);
                    p.Name = "Twice " + src.Name;
                    p.Tell = src.Tell + " A second wave follows the first.";
                    break;
                }
            }
            rule.Name = p.Name;
            Finish(p);
            return p;
        }
    }
}
