using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Items
{
    // Templates added for the item grammar (17): a Hunter's long shot, a Wanderer's
    // tailwind, and the bright moods' ally, aura and discovery effects (hearth, rally,
    // lamplight). Registered in EffectTemplates.Enchantments with their trigger and leans.
    public static partial class EffectTemplates
    {
        static readonly string[] LongshotNouns = { "Long Shot", "Far Mark", "Steady Aim", "Patience" };
        static readonly string[] TailwindNouns = { "Tailwind", "Quickstep", "Stride", "Second Wind" };
        static readonly string[] HearthNouns = { "Hearth", "Welcome", "Kettle", "Supper Bell" };
        static readonly string[] RallyNouns = { "Rally", "Good Cheer", "Chorus", "Company" };
        static readonly string[] LamplightNouns = { "Lamplight", "Keen Eye", "Wayfinding", "Lookout" };

        /// <summary>Hunter: hits from far away land harder and carry the motif's status.</summary>
        static EffectFitter.RuleBuilder Longshot(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, LongshotNouns, ref rng, "patient");
            float distance = 8f + 2f * rng.Range(0, 3);
            var st = ctx.Primary; var kind = ctx.DamageKind;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnHit, 1f);
                r.Conditions.Add(new ConditionSpec { Kind = ConditionKind.DistanceToTargetAbove, Value = distance });
                r.Actions.Add(Dmg(7f * s, kind));
                r.Actions.Add(Status(st, 1.5f));
                return r;
            };
        }

        /// <summary>Haste: a dash quickens you for a moment and gives some breath back.</summary>
        static EffectFitter.RuleBuilder Tailwind(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, TailwindNouns, ref rng, "brisk");
            var stat = Archetypes.Forbids(ctx.BaseType, ItemStatKind.Agility) ? StatId.VIT : StatId.AGI;
            return s =>
            {
                var r = Rule(name, TriggerKind.OnDash, 3f);
                r.Actions.Add(StatMul(stat, Math.Min(0.3f, 0.05f + 0.02f * s), 3f));
                r.Actions.Add(Restore(ResourceId.Stamina, 3f * s));
                return r;
            };
        }

        /// <summary>Ally and regen: every few seconds, you and anyone near you mend a little.</summary>
        static EffectFitter.RuleBuilder Hearth(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, HearthNouns, ref rng, "kindly");
            float interval = 5f + rng.Range(0, 3);
            float cap = 20f * ctx.PowerBand;
            return s =>
            {
                var r = Rule(name, TriggerKind.Periodic);
                r.Trigger.Interval = interval;
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 4f, Duration = 0.5f, FollowsSelf = true },
                    new ActionSpec { Kind = ActionKind.Heal, Amount = Math.Min(cap, 1f + 1.5f * s), Target = TargetKind.AlliesInArea }));
                return r;
            };
        }

        /// <summary>Ally aura: friends near you stand a little taller in the item's favored stat.</summary>
        static EffectFitter.RuleBuilder Rally(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, RallyNouns, ref rng, "merry");
            var fav = ctx.FavoredStat; float cap = 3f * (1f + 0.2f * ctx.Tier);
            return s =>
            {
                var r = Rule(name, TriggerKind.Passive);
                r.Actions.Add(Area(new AreaSpec { Shape = AreaShape.Circle, Radius = 5f, Duration = 30f, TickInterval = 2f, FollowsSelf = true },
                    new ActionSpec { Kind = ActionKind.ModifyStat, Stat = fav, Amount = Math.Min(cap, 0.5f + 0.3f * s), Duration = 3f, Target = TargetKind.AlliesInArea }));
                return r;
            };
        }

        /// <summary>Reveal: the wearer notices more (Insight while equipped).</summary>
        static EffectFitter.RuleBuilder Lamplight(TemplateContext ctx, ref Pcg32 rng)
        {
            string name = RuleName(ctx, LamplightNouns, ref rng, "bright");
            float cap = 3f * (1f + 0.2f * ctx.Tier);
            return s =>
            {
                var r = Rule(name, TriggerKind.Passive);
                r.Actions.Add(StatUp(StatId.INS, Math.Min(cap, 0.5f + 0.5f * s), 0f));
                return r;
            };
        }
    }

    /// <summary>
    /// Chooses enchantment templates from the item sentence: about 80% from the archetype's
    /// favored list, the rest from whatever the mood leans toward. Within either group,
    /// templates that answer the mood's lean are twice as likely.
    /// </summary>
    public static class EnchantmentChooser
    {
        public const float FavoredShare = 0.8f;

        /// <summary>Templates in the order they should be tried: the pick first, then sensible fallbacks.</summary>
        public static List<EffectTemplate> Ranked(TemplateContext ctx, ref Pcg32 rng, ICollection<string> exclude = null)
        {
            var leans = ctx.Theme?.Mood.EffectLean;
            var favored = new List<EffectTemplate>();
            var others = new List<EffectTemplate>();
            foreach (var t in EffectTemplates.Enchantments)
            {
                if (exclude != null && exclude.Contains(t.Id)) continue;
                if (ctx.Archetype != null && ctx.Archetype.FavorsTemplate(t.Id)) favored.Add(t);
                else others.Add(t);
            }
            bool useFavored = favored.Count > 0 && (others.Count == 0 || rng.Chance(FavoredShare));
            var first = useFavored ? favored : others;
            var second = useFavored ? others : favored;
            var ranked = new List<EffectTemplate>();
            Drain(first, leans, useFavored ? 2 : 4, ranked, ref rng);
            Drain(second, leans, 2, ranked, ref rng);
            return ranked;
        }

        /// <summary>Weighted draw without replacement: lean-matching templates get the bonus weight.</summary>
        static void Drain(List<EffectTemplate> pool, IList<string> leans, int bonus, List<EffectTemplate> into, ref Pcg32 rng)
        {
            var left = new List<EffectTemplate>(pool);
            while (left.Count > 0)
            {
                int total = 0;
                foreach (var t in left) total += t.AnswersAny(leans) ? bonus : 1;
                int x = rng.Range(0, total), i = 0;
                for (; i < left.Count - 1; i++) { x -= left[i].AnswersAny(leans) ? bonus : 1; if (x < 0) break; }
                into.Add(left[i]);
                left.RemoveAt(i);
            }
        }
    }
}
