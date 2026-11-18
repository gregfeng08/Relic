using System;
using Relic.Core.Effects;
using System.Collections.Generic;
using Relic.Core.Random;

namespace Relic.Items
{
    /// <summary>
    /// Stat lines from the item grammar (17): the base's primaries, then (Uncommon+) one
    /// archetype line, then further secondaries drawn mostly from the archetype's favored
    /// lines, never a line the base forbids (no STR on a staff, no AGI on heavy armor).
    /// Every item also carries its material's small property tag.
    /// </summary>
    public static class ArchetypeLines
    {
        public const string ArchetypeSource = "archetype";
        public const string MaterialSource = "material";

        /// <summary>Default range for a stat line on a slot (tier-0 Common; rarity and tier scale it).</summary>
        public static StatLine LineFor(ItemStatKind k, BaseType t)
        {
            var slot = ItemSlots.For(t);
            bool small = slot == ItemSlot.Trinket;
            switch (k)
            {
                case ItemStatKind.CritChance: return StatTables.L(k, small ? 2f : 3f, small ? 4f : 5f, TierGrowth.Slight);
                case ItemStatKind.SkillPower: return StatTables.L(k, 5f, 10f, TierGrowth.Slight);
                case ItemStatKind.MaxStamina: return StatTables.L(k, 5f, 10f, TierGrowth.Power, 0);
                case ItemStatKind.MaxHealth: return slot == ItemSlot.Relic ? StatTables.L(k, 10f, 15f, TierGrowth.Power, 0) : StatTables.L(k, 8f, 12f, TierGrowth.Power, 0);
                case ItemStatKind.MaxMana: return small ? StatTables.L(k, 8f, 12f, TierGrowth.Power, 0) : StatTables.L(k, 10f, 15f, TierGrowth.Power, 0);
                case ItemStatKind.ManaRegen: return slot == ItemSlot.Armor ? StatTables.L(k, 0.3f, 0.6f, TierGrowth.Power, 2) : StatTables.L(k, 0.5f, 1f, TierGrowth.Power, 2);
                case ItemStatKind.StatusResist: return small ? StatTables.L(k, 3f, 6f, TierGrowth.Slight) : StatTables.L(k, 4f, 8f, TierGrowth.Slight);
                case ItemStatKind.MoveSpeed: return StatTables.L(k, 2f, 4f, TierGrowth.Slight);
                case ItemStatKind.Defense: return StatTables.L(k, 1f, 2f, TierGrowth.Power);
                case ItemStatKind.ResonanceGain: return StatTables.L(k, 5f, 10f, TierGrowth.Slight);
                default:
                    return slot == ItemSlot.Relic ? StatTables.L(k, 1f, 3f, TierGrowth.Moderate) : StatTables.Trained(k);
            }
        }

        /// <summary>The small line a material adds (about a third of a normal line). Never AGI, STR or INT.</summary>
        public static StatLine TagLine(MaterialDef m)
        {
            StatLine l;
            switch (m.Tag)
            {
                case ItemStatKind.StatusResist: l = StatTables.L(m.Tag, 1f, 3f, TierGrowth.Slight); break;
                case ItemStatKind.MaxStamina: l = StatTables.L(m.Tag, 3f, 5f, TierGrowth.Power, 0); break;
                case ItemStatKind.MaxHealth: l = StatTables.L(m.Tag, 4f, 6f, TierGrowth.Power, 0); break;
                case ItemStatKind.MoveSpeed: l = StatTables.L(m.Tag, 1f, 2f, TierGrowth.Slight); break;
                case ItemStatKind.ManaRegen: l = StatTables.L(m.Tag, 0.1f, 0.3f, TierGrowth.Power, 2); break;
                case ItemStatKind.Defense: l = StatTables.L(m.Tag, 0.5f, 1f, TierGrowth.Power); break;
                default: l = StatTables.L(m.Tag, 0.5f, 1f, TierGrowth.Moderate); break;
            }
            l.Source = MaterialSource;
            return l;
        }

        static bool Usable(ItemStatKind k, BaseType t)
        {
            if (Archetypes.Forbids(t, k)) return false;
            // Weapon numbers and resonance belong to their own bases.
            if (k == ItemStatKind.Damage || k == ItemStatKind.AttackSpeed) return false;
            if (k == ItemStatKind.ResonanceGain) return t == BaseType.Relic;
            return true;
        }

        public static List<StatLine> LinesFor(BaseType t, Rarity r, ulong itemSeed, ArchetypeDef arch, MaterialDef material)
        {
            var rng = Pcg32.ForPurpose(itemSeed, "item.statlines");
            var lines = StatTables.Primary(t);
            if (t == BaseType.Trinket) lines.Add(StatTables.Trained(arch.Trained));

            int want = StatTables.SecondaryLines(r);
            if (want > 0)
            {
                // The archetype line: its first favored line the base allows and doesn't have yet.
                foreach (var k in arch.StatLines)
                {
                    if (!Usable(k, t) || lines.Exists(l => l.Stat == k)) continue;
                    var line = LineFor(k, t);
                    line.Source = ArchetypeSource;
                    lines.Add(line);
                    want--;
                    break;
                }
            }

            // Further secondaries: the base's pool plus the archetype's lines (three times as likely).
            var pool = new List<StatLine>();
            var weights = new List<int>();
            foreach (var p in StatTables.SecondaryPool(t))
                if (Usable(p.Stat, t) && !lines.Exists(l => l.Stat == p.Stat)) { pool.Add(p); weights.Add(arch.FavorsLine(p.Stat) ? 3 : 1); }
            foreach (var k in arch.StatLines)
                if (Usable(k, t) && !lines.Exists(l => l.Stat == k) && !pool.Exists(p => p.Stat == k)) { pool.Add(LineFor(k, t)); weights.Add(3); }
            for (int i = 0; i < want && pool.Count > 0; i++)
            {
                int total = 0;
                foreach (int w in weights) total += w;
                int x = rng.Range(0, total), pick = 0;
                for (; pick < pool.Count - 1; pick++) { x -= weights[pick]; if (x < 0) break; }
                lines.Add(pool[pick]);
                pool.RemoveAt(pick);
                weights.RemoveAt(pick);
            }

            if (material != null && Usable(material.Tag, t) && !lines.Exists(l => l.Stat == material.Tag)) lines.Add(TagLine(material));
            return lines;
        }
    }
}
