using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Items
{
    /// <summary>Everything a template needs to theme and size an effect.</summary>
    public sealed class TemplateContext
    {
        public Theme Theme;            // null for unthemed (Common/Uncommon) items
        public int Tier;
        public Rarity Rarity;
        public BaseType BaseType;
        public WorldRegion HomeRegion; // region the item is tied to (for InRegionTag); may be null

        public StatusId Primary => Theme?.Motif.Primary ?? DefaultStatus(BaseType);
        public StatusId Secondary => Theme?.Motif.Secondary ?? StatusId.Staggered;
        public DamageKind DamageKind => Theme?.Motif.DamageKind ?? (BaseType == BaseType.Staff || BaseType == BaseType.Tome ? DamageKind.Magical : DamageKind.Physical);
        public float PowerBand => 1f + 0.5f * Math.Max(0, Math.Min(10, Tier));

        static StatusId DefaultStatus(BaseType t)
        {
            switch (t)
            {
                case BaseType.Staff: return StatusId.Burning;
                case BaseType.Tome: return StatusId.Dazed;
                case BaseType.Bow: return StatusId.Bleeding;
                case BaseType.Greatsword: return StatusId.Staggered;
                default: return StatusId.Bleeding;
            }
        }
    }

    public delegate EffectFitter.RuleBuilder TemplateFactory(TemplateContext ctx, ref Pcg32 rng);

    public sealed class EffectTemplate
    {
        public string Id;
        public TemplateFactory Factory;
        /// <summary>True when the template has a damage term that can soak up any budget.</summary>
        public bool Absorbs;
        public EffectTemplate(string id, bool absorbs, TemplateFactory f) { Id = id; Absorbs = absorbs; Factory = f; }
    }

    /// <summary>
    /// Hand-written effect shapes, themed by motif and sized by EffectFitter.
    /// Used for table items (weapon skills, armor/trinket passives), as the
    /// deterministic TEMPLATE MODE stand-in for LLM enchantments, and as the
    /// fallback when the LLM fails.
    /// </summary>
    public static class EffectTemplates
    {
        // ---------- small builders ----------

        static ActionSpec Dmg(float amount, DamageKind kind, TargetKind target = TargetKind.Target) =>
            new ActionSpec { Kind = ActionKind.Damage, Amount = Math.Max(0.5f, amount), DamageKind = kind, Target = target };

        static ActionSpec Status(StatusId s, float duration, int stacks = 1, TargetKind target = TargetKind.Target) =>
            new ActionSpec { Kind = ActionKind.ApplyStatus, Status = s, Duration = Clamp(duration, 0.5f, 15f), Stacks = Math.Max(1, Math.Min(5, stacks)), Target = target };

        static ActionSpec Heal(float amount) => new ActionSpec { Kind = ActionKind.Heal, Amount = Math.Max(0.5f, amount), Target = TargetKind.Self };

        static ActionSpec Restore(ResourceId r, float amount) => new ActionSpec { Kind = ActionKind.RestoreResource, Resource = r, Amount = Math.Max(1f, amount), Target = TargetKind.Self };

        static ActionSpec Push(float m, TargetKind target = TargetKind.Target) => new ActionSpec { Kind = ActionKind.Knockback, Amount = Clamp(m, 0.5f, 15f), Target = target };

        static ActionSpec Pull(float m, TargetKind target = TargetKind.Target) => new ActionSpec { Kind = ActionKind.Pull, Amount = Clamp(m, 0.5f, 15f), Target = target };

        static ActionSpec StatUp(StatId stat, float amount, float duration) =>
            new ActionSpec { Kind = ActionKind.ModifyStat, Stat = stat, Amount = amount, Duration = duration, Target = TargetKind.Self };

        static ActionSpec StatMul(StatId stat, float mult, float duration) =>
            new ActionSpec { Kind = ActionKind.ModifyStat, Stat = stat, Multiplier = mult, Duration = duration, Target = TargetKind.Self };

        static ActionSpec Emit(EmitterSpec e, params ActionSpec[] payload) =>
            new ActionSpec { Kind = ActionKind.Emit, Emitter = e, Payload = new List<ActionSpec>(payload) };

        static ActionSpec Area(AreaSpec a, params ActionSpec[] payload) =>
            new ActionSpec { Kind = ActionKind.CreateArea, Area = a, Payload = new List<ActionSpec>(payload) };

        static ActionSpec Wall(float length, float duration) =>
            new ActionSpec { Kind = ActionKind.CreateBarrier, Area = new AreaSpec { Shape = AreaShape.Wall, Length = length, Width = 1f, Duration = duration } };

        static EffectRule Rule(string name, TriggerKind trigger, float cooldown = 0f, float chance = 1f) =>
            new EffectRule { Name = name, Trigger = new TriggerSpec { Kind = trigger }, Cooldown = cooldown, Chance = chance };

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        static string Cap(string w) => string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + w.Substring(1);

        static string Pick(string[] a, ref Pcg32 rng) => a[rng.Range(0, a.Length)];

        static string RuleName(TemplateContext ctx, string[] nouns, ref Pcg32 rng, string fallbackWord)
        {
            string word = ctx.Theme != null ? Pick(ctx.Theme.Motif.Words, ref rng) : fallbackWord;
            return Cap(word.Split('-')[0]) + " " + Pick(nouns, ref rng);
        }

        static StatId Favored(TemplateContext ctx) => ctx.Theme?.Motif.FavoredStat ?? StatId.STR;

        // ---------- weapon skills (E) ----------

        public static EffectFitter.RuleBuilder WeaponSkill(TemplateContext ctx)
        {
            var kind = ctx.DamageKind;
            bool themed = ctx.Theme != null;
            var status = ctx.Primary;
            switch (ctx.BaseType)
            {
                case BaseType.Sword:
                    return s =>
                    {
                        var r = Rule("Crescent Cut", TriggerKind.Active, 6f);
                        r.CostResource = ResourceId.Stamina; r.CostAmount = 15f;
                        var a = Area(new AreaSpec { Shape = AreaShape.Cone, Radius = 3.5f, Angle = 100f, Duration = 0.3f, FollowsSelf = true }, Dmg(8f * s, kind, TargetKind.EnemiesInArea), Push(2f, TargetKind.EnemiesInArea));
                        if (themed) a.Payload.Add(Status(status, 2f, 1, TargetKind.EnemiesInArea));
                        r.Actions.Add(a);
                        return r;
                    };
                case BaseType.Greatsword:
                    return s =>
                    {
                        var r = Rule("Earthsplitter", TriggerKind.Active, 8f);
                        r.CostResource = ResourceId.Stamina; r.CostAmount = 25f;
                        var a = Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 3f, Duration = 0.4f, FollowsSelf = true }, Dmg(14f * s, kind, TargetKind.EnemiesInArea), Status(StatusId.Staggered, 1f, 1, TargetKind.EnemiesInArea));
                        if (themed && status != StatusId.Staggered) a.Payload.Add(Status(status, 2f, 1, TargetKind.EnemiesInArea));
                        r.Actions.Add(a);
                        return r;
                    };
                case BaseType.Bow:
                    return s =>
                    {
                        var r = Rule("Fan of Arrows", TriggerKind.Active, 5f);
                        r.CostResource = ResourceId.Stamina; r.CostAmount = 10f;
                        var e = Emit(new EmitterSpec { Shape = EmitShape.Cone, Count = 5, SpreadDegrees = 35f, Speed = 30f, Range = 25f, Size = 0.15f }, Dmg(4f * s, kind));
                        if (themed) e.Payload.Add(Status(status, 1.5f));
                        r.Actions.Add(e);
                        return r;
                    };
                case BaseType.Staff:
                    return s =>
                    {
                        var r = Rule("Splitting Orb", TriggerKind.Active, 6f);
                        r.CostResource = ResourceId.Mana; r.CostAmount = 20f;
                        var e = Emit(new EmitterSpec { Shape = EmitShape.Single, Count = 1, Speed = 12f, Range = 18f, SplitCount = 4, Size = 0.4f }, Dmg(9f * s, kind));
                        if (themed) e.Payload.Add(Status(status, 2f));
                        r.Actions.Add(e);
                        return r;
                    };
                case BaseType.Tome:
                    return s =>
                    {
                        var r = Rule("Orbiting Pages", TriggerKind.Active, 8f);
                        r.CostResource = ResourceId.Mana; r.CostAmount = 25f;
                        var e = Emit(new EmitterSpec { Shape = EmitShape.Orbit, Count = 3, Speed = 8f, Range = 6f, Size = 0.3f }, Dmg(4f * s, kind));
                        if (themed) e.Payload.Add(Status(status, 1.5f));
                        r.Actions.Add(e);
                        return r;
                    };
            }
            return null;
        }

        // ---------- armor and trinket passives ----------

        public static EffectFitter.RuleBuilder SlotPassive(TemplateContext ctx, ref Pcg32 rng)
        {
            var fav = Favored(ctx);
            float band = ctx.PowerBand;
            switch (ctx.BaseType)
            {
                case BaseType.LightArmor:
                    return s =>
                    {
                        var r = Rule("Second Wind", TriggerKind.OnDash, 3f);
                        r.Actions.Add(Restore(ResourceId.Stamina, 5f * s));
                        return r;
                    };
                case BaseType.MediumArmor:
                    return s =>
                    {
                        var r = Rule("Steady Breath", TriggerKind.OnTakeDamage, 5f);
                        r.Actions.Add(Heal(5f * s));
                        return r;
                    };
                case BaseType.HeavyArmor:
                    return s =>
                    {
                        var r = Rule("Bulwark", TriggerKind.Passive);
                        r.Actions.Add(StatUp(StatId.DEF, Math.Min(4f * band, 0.5f + s), 0f));
                        return r;
                    };
                default:
                {
                    var stat = ctx.Theme != null ? fav : (StatId)rng.Range(0, 6);
                    return s =>
                    {
                        var r = Rule("Keepsake", TriggerKind.Passive);
                        r.Actions.Add(StatUp(stat, Math.Min(3f * (1f + 0.2f * ctx.Tier), 0.5f + 0.5f * s), 0f));
                        return r;
                    };
                }
            }
        }

        // ---------- enchantments ----------

        static readonly string[] RiderNouns = { "Lash", "Bite", "Mark", "Kiss", "Touch", "Brand" };
        static readonly string[] ExploitNouns = { "Snare", "Reckoning", "Verdict", "Undoing", "Toll" };
        static readonly string[] BurstNouns = { "Wake", "Bloom", "Scatter", "Spill", "Requiem" };
        static readonly string[] RetortNouns = { "Rebuke", "Answer", "Recoil", "Reprisal" };
        static readonly string[] TrailNouns = { "Trail", "Furrow", "Wake", "Track" };
        static readonly string[] NerveNouns = { "Riposte", "Nerve", "Reply", "Flinch" };
        static readonly string[] RefugeNouns = { "Refuge", "Last Light", "Shelter", "Bastion" };
        static readonly string[] SentinelNouns = { "Herald", "Sentinel", "Spark", "Outrider" };
        static readonly string[] AuraNouns = { "Halo", "Mantle", "Air", "Pall" };
        static readonly string[] TemperNouns = { "Temper", "Grace", "Bearing", "Mettle" };
        static readonly string[] EchoNouns = { "Echo", "Cascade", "Relay", "Chorus" };
        static readonly string[] WeatherNouns = { "Omen", "Hour", "Season", "Tithe" };
        static readonly string[] MercyNouns = { "Mercy", "Coup", "Final Word", "Last Rite" };
        static readonly string[] HuntNouns = { "Quarry", "Hunt", "Challenge", "Grudge" };

        public static readonly EffectTemplate[] Enchantments =
        {
            new EffectTemplate("rider", true, Rider),
            new EffectTemplate("exploit", true, Exploit),
            new EffectTemplate("death-burst", true, DeathBurst),
            new EffectTemplate("retort", true, Retort),
            new EffectTemplate("trail", true, Trail),
            new EffectTemplate("riposte", true, Riposte),
            new EffectTemplate("refuge", true, Refuge),
            new EffectTemplate("sentinel", true, Sentinel),
            new EffectTemplate("aura", true, Aura),
            new EffectTemplate("temper", false, Temper),
            new EffectTemplate("echo", true, Echo),
            new EffectTemplate("weather", true, Weather),
            new EffectTemplate("executioner", true, Executioner),
            new EffectTemplate("bosshunter", true, BossHunter),
        };

        public static EffectTemplate EnchantmentById(string id) => Array.Find(Enchantments, t => t.Id == id);

        static EffectFitter.RuleBuilder Rider(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, RiderNouns, ref rng, "keen");
            float chance = 0.2f + 0.05f * rng.Range(0, 5);
            var st = ctx.Primary; var kind = ctx.DamageKind;
            bool stacking = st == StatusId.Bleeding || st == StatusId.Burning || st == StatusId.Poisoned;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 0f, chance);
                r.Actions.Add(Dmg(4f * s, kind));
                r.Actions.Add(Status(st, 1.5f + 0.25f * s, stacking ? 1 + (int)Math.Min(2, s / 3f) : 1));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Exploit(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, ExploitNouns, ref rng, "opportune");
            var st = ctx.Primary; var kind = ctx.DamageKind; var motif = ctx.Theme?.Motif.Id;
            var second = ctx.Secondary;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 2f);
                r.Conditions.Add(new ConditionSpec { Kind = ConditionKind.TargetHasStatus, Status = st });
                r.Actions.Add(Dmg(8f * s, kind));
                if (motif == "tide") r.Actions.Add(Pull(3f));
                else if (motif == "storm" || motif == "bell" || motif == "stone") r.Actions.Add(Push(3f));
                else if (second == StatusId.Rooted) r.Actions.Add(Status(StatusId.Rooted, Math.Min(3f, 1f + 0.2f * s)));
                else r.Actions.Add(Status(second == st ? StatusId.Staggered : second, 1.5f + 0.25f * s));
                return r;
            };
        }

        static EffectFitter.RuleBuilder DeathBurst(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, BurstNouns, ref rng, "parting");
            int count = 6 + 2 * rng.Range(0, 3);
            var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnKill);
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Ring, Count = count, Speed = 14f, Range = 10f, Size = 0.3f }, Dmg(6f * s, kind), Status(st, 2f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Retort(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, RetortNouns, ref rng, "stubborn");
            var st = ctx.Secondary; var kind = ctx.DamageKind;
            bool push = rng.Chance(0.5f);
            return s =>
            {
                var r = Rule(name, TriggerKind.OnTakeDamage, 3f);
                var a = Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 3f, Duration = 0.5f, FollowsSelf = true }, Dmg(5f * s, kind, TargetKind.EnemiesInArea));
                a.Payload.Add(push ? Push(2.5f, TargetKind.EnemiesInArea) : Status(st, 1.5f, 1, TargetKind.EnemiesInArea));
                r.Actions.Add(a);
                return r;
            };
        }

        static EffectFitter.RuleBuilder Trail(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, TrailNouns, ref rng, "dust");
            var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnDash, 1f);
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 2f, Duration = 2f, TickInterval = 0.5f, FollowsSelf = true },
                    Dmg(1.5f * s, kind, TargetKind.EnemiesInArea), Status(st, 1.5f, 1, TargetKind.EnemiesInArea)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Riposte(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, NerveNouns, ref rng, "steady");
            var fav = Favored(ctx); var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnCloseCall, 3f);
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Single, Count = 1, Speed = 20f, Range = 20f, Seeking = true, Size = 0.25f }, Dmg(6f * s, kind)));
                r.Actions.Add(StatMul(fav, Math.Min(0.3f, 0.05f + 0.02f * s), 4f));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Refuge(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, RefugeNouns, ref rng, "stubborn");
            float band = ctx.PowerBand; var kind = ctx.DamageKind; var st = ctx.Primary;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnLowHealth, 20f);
                r.Trigger.Threshold = 0.3f;
                r.Actions.Add(Heal(Math.Min(30f * band, 10f + 5f * s)));
                r.Actions.Add(Wall(5f, 4f));
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Ring, Count = 8, Speed = 12f, Range = 8f, Size = 0.3f }, Dmg(6f * s, kind), Status(st, 2f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Sentinel(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, SentinelNouns, ref rng, "watchful");
            float interval = 4f + rng.Range(0, 4);
            var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.Periodic);
                r.Trigger.Interval = interval;
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Single, Count = 1, Speed = 12f, Range = 18f, Seeking = true, Size = 0.3f }, Dmg(6f * s, kind), Status(st, 1.5f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Aura(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, AuraNouns, ref rng, "quiet");
            var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.Passive);
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 3.5f, Duration = 30f, TickInterval = 2f, FollowsSelf = true },
                    Dmg(0.5f * s, kind, TargetKind.EnemiesInArea), Status(st, Math.Min(3f, 0.5f + 0.1f * s), 1, TargetKind.EnemiesInArea)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Temper(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, TemperNouns, ref rng, "even");
            var fav = Favored(ctx); float cap = 3f * (1f + 0.2f * ctx.Tier);
            return s =>
            {
                var r = Rule(name, TriggerKind.Passive);
                r.Actions.Add(StatUp(fav, Math.Min(cap, 0.5f + 0.5f * s), 0f));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Echo(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, EchoNouns, ref rng, "answering");
            var st = ctx.Primary; var kind = ctx.DamageKind;
            var second = ctx.Secondary == st ? StatusId.Staggered : ctx.Secondary;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnStatusApplied, 1f, 0.5f);
                r.Trigger.Status = st;
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Cone, Count = 3, SpreadDegrees = 30f, Speed = 16f, Range = 12f, Size = 0.25f }, Dmg(4f * s, kind), Status(second, 1.5f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Weather(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, WeatherNouns, ref rng, "fair");
            var st = ctx.Primary; var kind = ctx.DamageKind;
            ConditionSpec cond;
            var motif = ctx.Theme?.Motif;
            if (motif?.WorldCondition != null && rng.Chance(0.6f)) cond = new ConditionSpec { Kind = motif.WorldCondition.Value };
            else if (ctx.HomeRegion != null && ctx.HomeRegion.Tags.Count > 0)
                cond = new ConditionSpec { Kind = ConditionKind.InRegionTag, Tag = ctx.HomeRegion.Tags[rng.Range(0, ctx.HomeRegion.Tags.Count)] };
            else cond = new ConditionSpec { Kind = rng.Chance(0.5f) ? ConditionKind.IsNight : ConditionKind.IsRaining };
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 1f);
                r.Conditions.Add(new ConditionSpec { Kind = cond.Kind, Tag = cond.Tag });
                r.Actions.Add(Dmg(8f * s, kind));
                r.Actions.Add(Status(st, 2f));
                return r;
            };
        }

        static EffectFitter.RuleBuilder Executioner(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, MercyNouns, ref rng, "final");
            var kind = ctx.DamageKind;
            float threshold = 0.25f + 0.05f * rng.Range(0, 3);
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 1.5f);
                r.Conditions.Add(new ConditionSpec { Kind = ConditionKind.TargetHealthBelow, Value = threshold });
                r.Actions.Add(Dmg(12f * s, kind));
                return r;
            };
        }

        static EffectFitter.RuleBuilder BossHunter(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, HuntNouns, ref rng, "old");
            var kind = ctx.DamageKind; var st = ctx.Secondary;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 1f);
                r.Conditions.Add(new ConditionSpec { Kind = ConditionKind.TargetIsBoss });
                r.Actions.Add(Dmg(7f * s, kind));
                r.Actions.Add(Status(st, 1.5f));
                return r;
            };
        }

        // ---------- ultimates (Q) ----------

        public const float UltimateCooldown = 30f;
        public const float UltimateResonanceCost = 100f;

        static readonly string[] UltimateNouns = { "Deluge", "Reckoning", "Last Toll", "Unmaking", "Flood", "Verdict", "Homecoming", "Vigil", "Tithe" };

        public static readonly EffectTemplate[] Ultimates =
        {
            new EffectTemplate("deluge", true, UDeluge),
            new EffectTemplate("volley", true, UVolley),
            new EffectTemplate("spiral", true, USpiral),
            new EffectTemplate("lance", true, ULance),
            new EffectTemplate("sanctum", true, USanctum),
            new EffectTemplate("seekers", true, USeekers),
            new EffectTemplate("avatar", true, UAvatar),
        };

        static EffectRule Ult(string name)
        {
            var r = Rule(name, TriggerKind.Active, UltimateCooldown);
            r.CostResource = ResourceId.Resonance;
            r.CostAmount = UltimateResonanceCost;
            return r;
        }

        static string UltName(TemplateContext ctx, ref Pcg32 rng)
        {
            string word = ctx.Theme != null ? Pick(ctx.Theme.Motif.Words, ref rng) : "grand";
            return "The " + Cap(word.Split('-')[0]) + " " + Pick(UltimateNouns, ref rng);
        }

        static EffectFitter.RuleBuilder UDeluge(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 6f, Delay = 0.3f, Duration = 4f, TickInterval = 0.5f },
                    Dmg(3f * s, kind, TargetKind.EnemiesInArea), Status(st, 2f, 1, TargetKind.EnemiesInArea)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder UVolley(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Ring, Count = 16, Speed = 16f, Range = 18f, Size = 0.35f }, Dmg(6f * s, kind), Status(st, 2f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder USpiral(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var st = ctx.Secondary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Spiral, Count = 20, Speed = 12f, Range = 20f, Size = 0.3f }, Dmg(5f * s, kind), Status(st, 1.5f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder ULance(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var kind = ctx.DamageKind; var st = ctx.Primary;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Line, Length = 18f, Width = 2.5f, Delay = 0.25f, Duration = 1f, TickInterval = 0.25f },
                    Dmg(8f * s, kind, TargetKind.EnemiesInArea), Status(st, 2f, 1, TargetKind.EnemiesInArea)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder USanctum(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var st = ctx.Secondary; var kind = ctx.DamageKind; float band = ctx.PowerBand;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Wall(8f, 6f));
                r.Actions.Add(Heal(Math.Min(40f * band, 10f + 4f * s)));
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 4f, Duration = 6f, TickInterval = 1f, FollowsSelf = true },
                    Dmg(2f * s, kind, TargetKind.EnemiesInArea), Status(st, 1.5f, 1, TargetKind.EnemiesInArea)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder USeekers(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Burst, Count = 8, SpreadDegrees = 120f, Speed = 14f, Range = 25f, Seeking = true, Size = 0.3f }, Dmg(6f * s, kind), Status(st, 2f)));
                return r;
            };
        }

        static EffectFitter.RuleBuilder UAvatar(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = UltName(ctx, ref rng); var fav = Favored(ctx); var kind = ctx.DamageKind;
            return s =>
            {
                var r = Ult(name);
                r.Actions.Add(StatMul(fav, Math.Min(0.5f, 0.1f + 0.02f * s), 10f));
                r.Actions.Add(Emit(new EmitterSpec { Shape = EmitShape.Orbit, Count = 4, Speed = 8f, Range = 8f, Size = 0.35f }, Dmg(4f * s, kind)));
                return r;
            };
        }

        // ---------- curses (drawbacks: negative cost, so cursed relics get more power elsewhere) ----------

        /// <summary>A drawback action to append to a rule. Never scaled down by ScaleToFit.</summary>
        public static ActionSpec Drawback(TemplateContext ctx, ref Pcg32 rng)
        {
            switch (rng.Range(0, 3))
            {
                case 0: return new ActionSpec { Kind = ActionKind.Damage, Target = TargetKind.Self, Amount = (float)Math.Round(4f * ctx.PowerBand), DamageKind = DamageKind.Physical };
                case 1: return new ActionSpec { Kind = ActionKind.ApplyStatus, Target = TargetKind.Self, Status = StatusId.Exhausted, Duration = 2f, Stacks = 1 };
                default: return new ActionSpec { Kind = ActionKind.ApplyStatus, Target = TargetKind.Self, Status = ctx.Theme?.Motif.Primary == StatusId.Burning ? StatusId.Burning : StatusId.Bleeding, Duration = 2f, Stacks = 1 };
            }
        }

        /// <summary>A standing curse rule: lowers one of your stats while equipped.</summary>
        public static EffectRule CurseRule(TemplateContext ctx, ref Pcg32 rng)
        {
            var stat = (StatId)rng.Range(0, 6);
            if (ctx.Theme != null && stat == ctx.Theme.Motif.FavoredStat) stat = stat == StatId.CHR ? StatId.STR : stat + 1;
            float amount = -(float)Math.Round(2f * (1f + 0.2f * ctx.Tier));
            return new EffectRule
            {
                Name = "Price of Keeping",
                Trigger = new TriggerSpec { Kind = TriggerKind.Passive },
                Actions = { StatUp(stat, amount, 0f) },
            };
        }
    }
}
