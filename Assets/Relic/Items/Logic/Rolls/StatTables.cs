using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Items
{
    /// <summary>How a stat's range grows with region tier.</summary>
    public enum TierGrowth
    {
        /// <summary>Power numbers (damage, DEF, pools): × (1 + 0.5 × tier), the 03-combat power band (~6× at tier 10).</summary>
        Power,
        /// <summary>Trained-stat bonuses: × (1 + 0.2 × tier).</summary>
        Moderate,
        /// <summary>Rates and percentages (attack speed, crit): × (1 + 0.04 × tier).</summary>
        Slight,
    }

    public sealed class StatLine
    {
        public ItemStatKind Stat;
        public float Min;
        public float Ceiling;
        public TierGrowth Growth;
        public int Decimals;

        public StatLine(ItemStatKind stat, float min, float ceiling, TierGrowth growth, int decimals = 1)
        {
            Stat = stat; Min = min; Ceiling = ceiling; Growth = growth; Decimals = decimals;
        }
    }

    /// <summary>
    /// Roll tables: each base type has primary stat lines (always present) and a
    /// pool of secondary lines (Uncommon +1, Rare +2, Epic/Legendary +3).
    /// Numbers are tier-0 Common ranges; rarity and tier multiply them.
    /// First-pass values, to be tuned with the combat runtime.
    /// </summary>
    public static class StatTables
    {
        public const int Version = 1;

        // Rarity mostly buys bonus effects and extra stat lines, not raw numbers (power bands, 03-combat).
        static readonly float[] RarityMultiplier = { 1.00f, 1.05f, 1.10f, 1.15f, 1.20f };
        static readonly int[] SecondaryCount = { 0, 1, 2, 3, 3 };

        public static float RarityFactor(Rarity r) => RarityMultiplier[(int)r];

        public static float TierFactor(TierGrowth g, int tier)
        {
            int t = Math.Max(0, Math.Min(tier, Difficulty.MaxTier));
            switch (g)
            {
                case TierGrowth.Power: return PowerBudget.TierFactor(t); // shared with effect budgets (1 + 0.2·tier)
                case TierGrowth.Moderate: return 1f + 0.1f * t; // trained-stat bonus lines: gear shouldn't outgrow training
                default: return 1f + 0.04f * t;
            }
        }

        static StatLine L(ItemStatKind k, float min, float ceil, TierGrowth g, int dec = 1) => new StatLine(k, min, ceil, g, dec);

        public static List<StatLine> Primary(BaseType t)
        {
            switch (t)
            {
                case BaseType.Sword: return new List<StatLine> { L(ItemStatKind.Damage, 8f, 10f, TierGrowth.Power), L(ItemStatKind.AttackSpeed, 1.4f, 1.6f, TierGrowth.Slight, 2) };
                case BaseType.Greatsword: return new List<StatLine> { L(ItemStatKind.Damage, 16f, 20f, TierGrowth.Power), L(ItemStatKind.AttackSpeed, 0.7f, 0.85f, TierGrowth.Slight, 2) };
                case BaseType.Bow: return new List<StatLine> { L(ItemStatKind.Damage, 7f, 9f, TierGrowth.Power), L(ItemStatKind.AttackSpeed, 1.6f, 1.9f, TierGrowth.Slight, 2) };
                case BaseType.Staff: return new List<StatLine> { L(ItemStatKind.Damage, 9f, 11f, TierGrowth.Power), L(ItemStatKind.AttackSpeed, 1.1f, 1.3f, TierGrowth.Slight, 2), L(ItemStatKind.MaxMana, 10f, 15f, TierGrowth.Power, 0) };
                case BaseType.Tome: return new List<StatLine> { L(ItemStatKind.Damage, 6f, 8f, TierGrowth.Power), L(ItemStatKind.AttackSpeed, 1.8f, 2.1f, TierGrowth.Slight, 2), L(ItemStatKind.ManaRegen, 0.5f, 1f, TierGrowth.Power, 2) };
                case BaseType.LightArmor: return new List<StatLine> { L(ItemStatKind.Defense, 2f, 3f, TierGrowth.Power), L(ItemStatKind.MoveSpeed, 3f, 5f, TierGrowth.Slight) };
                case BaseType.MediumArmor: return new List<StatLine> { L(ItemStatKind.Defense, 4f, 5f, TierGrowth.Power), L(ItemStatKind.MaxHealth, 10f, 15f, TierGrowth.Power, 0) };
                case BaseType.HeavyArmor: return new List<StatLine> { L(ItemStatKind.Defense, 6f, 8f, TierGrowth.Power), L(ItemStatKind.MaxHealth, 20f, 30f, TierGrowth.Power, 0) };
                case BaseType.Trinket: return new List<StatLine>(); // its primary is a trained stat, picked per item (see PickPrimaryTrainedStat)
                case BaseType.Relic: return new List<StatLine> { L(ItemStatKind.ResonanceGain, 5f, 10f, TierGrowth.Slight) };
            }
            return new List<StatLine>();
        }

        static StatLine Trained(ItemStatKind k) => L(k, 1f, 2f, TierGrowth.Moderate);

        public static List<StatLine> SecondaryPool(BaseType t)
        {
            var pool = new List<StatLine>();
            switch (ItemSlots.For(t))
            {
                case ItemSlot.Weapon:
                    pool.Add(L(ItemStatKind.CritChance, 3f, 5f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.SkillPower, 5f, 10f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.MaxStamina, 5f, 10f, TierGrowth.Power, 0));
                    if (t == BaseType.Staff || t == BaseType.Tome)
                    {
                        pool.Add(Trained(ItemStatKind.Intelligence));
                        pool.Add(Trained(ItemStatKind.Insight));
                        if (t == BaseType.Staff) pool.Add(L(ItemStatKind.ManaRegen, 0.5f, 1f, TierGrowth.Power, 2));
                        else pool.Add(L(ItemStatKind.MaxMana, 10f, 15f, TierGrowth.Power, 0));
                    }
                    else
                    {
                        pool.Add(Trained(ItemStatKind.Strength));
                        pool.Add(Trained(ItemStatKind.Agility));
                        pool.Add(L(ItemStatKind.MaxHealth, 8f, 12f, TierGrowth.Power, 0));
                    }
                    break;
                case ItemSlot.Armor:
                    if (t != BaseType.MediumArmor && t != BaseType.HeavyArmor) pool.Add(L(ItemStatKind.MaxHealth, 8f, 12f, TierGrowth.Power, 0));
                    pool.Add(L(ItemStatKind.MaxStamina, 5f, 10f, TierGrowth.Power, 0));
                    pool.Add(L(ItemStatKind.StatusResist, 4f, 8f, TierGrowth.Slight));
                    pool.Add(Trained(ItemStatKind.Vitality));
                    pool.Add(L(ItemStatKind.ManaRegen, 0.3f, 0.6f, TierGrowth.Power, 2));
                    break;
                case ItemSlot.Trinket:
                    foreach (var k in TrainedStats) pool.Add(Trained(k));
                    pool.Add(L(ItemStatKind.CritChance, 2f, 4f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.MaxMana, 8f, 12f, TierGrowth.Power, 0));
                    pool.Add(L(ItemStatKind.MaxHealth, 8f, 12f, TierGrowth.Power, 0));
                    pool.Add(L(ItemStatKind.StatusResist, 3f, 6f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.MoveSpeed, 2f, 4f, TierGrowth.Slight));
                    break;
                case ItemSlot.Relic:
                    foreach (var k in TrainedStats) pool.Add(L(k, 1f, 3f, TierGrowth.Moderate));
                    pool.Add(L(ItemStatKind.StatusResist, 4f, 8f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.CritChance, 3f, 5f, TierGrowth.Slight));
                    pool.Add(L(ItemStatKind.MaxHealth, 10f, 15f, TierGrowth.Power, 0));
                    pool.Add(L(ItemStatKind.MaxMana, 10f, 15f, TierGrowth.Power, 0));
                    break;
            }
            return pool;
        }

        public static readonly ItemStatKind[] TrainedStats =
        {
            ItemStatKind.Strength, ItemStatKind.Agility, ItemStatKind.Vitality,
            ItemStatKind.Intelligence, ItemStatKind.Insight, ItemStatKind.Charisma,
        };

        public static int SecondaryLines(Rarity r) => SecondaryCount[(int)r];

        /// <summary>
        /// The stat lines an item has. Which lines it gets is fixed by the item seed
        /// (forge rerolls change values, not lines).
        /// </summary>
        public static List<StatLine> LinesFor(BaseType t, Rarity r, ulong itemSeed)
        {
            var rng = Pcg32.ForPurpose(itemSeed, "item.statlines");
            var lines = Primary(t);
            if (t == BaseType.Trinket) lines.Add(L(TrainedStats[rng.Range(0, TrainedStats.Length)], 1f, 2f, TierGrowth.Moderate));

            var pool = SecondaryPool(t);
            pool.RemoveAll(p => lines.Exists(l => l.Stat == p.Stat));
            int want = Math.Min(SecondaryLines(r), pool.Count);
            for (int i = 0; i < want; i++)
            {
                int pick = rng.Range(0, pool.Count);
                lines.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return lines;
        }

        /// <summary>The Core trained stat a line maps to, if any.</summary>
        public static StatId? CoreStat(ItemStatKind k)
        {
            switch (k)
            {
                case ItemStatKind.Strength: return StatId.STR;
                case ItemStatKind.Agility: return StatId.AGI;
                case ItemStatKind.Vitality: return StatId.VIT;
                case ItemStatKind.Intelligence: return StatId.INT;
                case ItemStatKind.Insight: return StatId.INS;
                case ItemStatKind.Charisma: return StatId.CHR;
                case ItemStatKind.Defense: return StatId.DEF;
                default: return null;
            }
        }

        public static bool IsPercent(ItemStatKind k) =>
            k == ItemStatKind.CritChance || k == ItemStatKind.SkillPower || k == ItemStatKind.MoveSpeed ||
            k == ItemStatKind.StatusResist || k == ItemStatKind.ResonanceGain;
    }

    /// <summary>Rolls stat values: uniform in [min, ceiling], then overroll steps past the ceiling.</summary>
    public static class StatRoller
    {
        /// <summary>
        /// Rolls every line of an item. rerollCount 0 is the original drop; each forge
        /// reroll is a fresh sample from sub-seed Hash.Combine(itemSeed, rerollCount).
        /// </summary>
        public static List<ItemStat> Roll(BaseType t, Rarity r, int tier, ulong itemSeed, int rerollCount)
        {
            var lines = StatTables.LinesFor(t, r, itemSeed);
            var rng = Pcg32.ForPurpose(Hash.Combine(itemSeed, (ulong)rerollCount), "item.stats");
            var stats = new List<ItemStat>(lines.Count);
            foreach (var line in lines) stats.Add(RollLine(line, r, tier, ref rng));
            return stats;
        }

        public static ItemStat RollLine(StatLine line, Rarity r, int tier, ref Pcg32 rng)
        {
            // Attack speed is a feel stat: a weapon type swings at its own pace at every rarity and tier.
            float f = line.Stat == ItemStatKind.AttackSpeed ? 1f : StatTables.RarityFactor(r) * StatTables.TierFactor(line.Growth, tier);
            float min = Round(line.Min * f, line.Decimals);
            float ceiling = Round(line.Ceiling * f, line.Decimals);
            float uniform = rng.Range(min, ceiling);
            int steps = Overroll.Steps(ref rng);
            float value = Round(steps > 0 ? Overroll.Apply(ceiling, steps) : uniform, line.Decimals);
            // An overroll must read as past the ceiling even after rounding (+1 display unit at least).
            if (steps > 0) value = Math.Max(value, Round(ceiling + (float)Math.Pow(10, -line.Decimals), line.Decimals));
            return new ItemStat
            {
                Stat = line.Stat,
                Min = min,
                Ceiling = ceiling,
                OverrollSteps = steps,
                Value = value,
            };
        }

        static float Round(float v, int decimals) => (float)Math.Round(v, decimals, MidpointRounding.AwayFromZero);
    }
}
