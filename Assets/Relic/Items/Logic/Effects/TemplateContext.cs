using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Items
{
    /// <summary>
    /// Everything a template needs to theme and size an effect: the item's grammar sentence
    /// read as effect inputs. Statuses come from the motif (Rare+) and the archetype; the
    /// favored stat never breaks a base's coherence rule (no AGI bonus on heavy armor).
    /// </summary>
    public sealed class TemplateContext
    {
        /// <summary>Null for unthemed (Common/Uncommon) items.</summary>
        public Theme Theme;
        public ArchetypeDef Archetype;
        public int Tier;
        public Rarity Rarity;
        public BaseType BaseType;
        /// <summary>Region the item is tied to (for InRegionTag); may be null.</summary>
        public WorldRegion HomeRegion;

        public StatusId Primary
        {
            get
            {
                if (Theme != null) return Theme.Primary;
                if (Archetype != null && Archetype.Statuses.Length > 0) return Archetype.Statuses[0];
                return DefaultStatus(BaseType);
            }
        }

        /// <summary>The motif's second status, else one of the archetype's, else the primary again.</summary>
        public StatusId Secondary
        {
            get
            {
                if (Theme != null && Theme.Secondary != StatusId.None) return Theme.Secondary;
                var p = Primary;
                if (Archetype != null)
                    foreach (var s in Archetype.Statuses)
                        if (s != p) return s;
                return p;
            }
        }

        public DamageKind DamageKind => Theme?.DamageKind ?? (BaseType == BaseType.Staff || BaseType == BaseType.Tome ? DamageKind.Magical : DamageKind.Physical);
        public float PowerBand => 1f + 0.5f * Math.Max(0, Math.Min(10, Tier));

        /// <summary>The stat this item's effects raise: the motif's, else the archetype's trained stat, never one the base forbids.</summary>
        public StatId FavoredStat
        {
            get
            {
                StatId fav = Theme?.FavoredStat ?? CoreOf(Archetype?.Trained) ?? StatId.STR;
                if (!Archetypes.Forbids(BaseType, KindOf(fav))) return fav;
                var trained = CoreOf(Archetype?.Trained);
                return trained != null && !Archetypes.Forbids(BaseType, KindOf(trained.Value)) ? trained.Value : StatId.VIT;
            }
        }

        /// <summary>Every status this item's effects may inflict on enemies (motif ∪ archetype ∪ the base's own).</summary>
        public HashSet<StatusId> AllowedStatuses()
        {
            var set = new HashSet<StatusId>();
            if (Theme != null) foreach (var s in Theme.Motif.Statuses) set.Add(s);
            if (Archetype != null) foreach (var s in Archetype.Statuses) set.Add(s);
            set.Add(Primary);
            set.Add(Secondary);
            if (BaseType == BaseType.Greatsword) set.Add(StatusId.Staggered); // Earthsplitter always staggers
            return set;
        }

        static StatusId DefaultStatus(BaseType t)
        {
            switch (t)
            {
                case BaseType.Staff: return StatusId.Burning;
                case BaseType.Tome: return StatusId.Dazed;
                case BaseType.Greatsword: return StatusId.Staggered;
                default: return StatusId.Bleeding;
            }
        }

        public static StatId? CoreOf(ItemStatKind? k) => k == null ? (StatId?)null : StatTables.CoreStat(k.Value);

        public static ItemStatKind KindOf(StatId s)
        {
            switch (s)
            {
                case StatId.STR: return ItemStatKind.Strength;
                case StatId.AGI: return ItemStatKind.Agility;
                case StatId.VIT: return ItemStatKind.Vitality;
                case StatId.INT: return ItemStatKind.Intelligence;
                case StatId.INS: return ItemStatKind.Insight;
                case StatId.CHR: return ItemStatKind.Charisma;
                default: return ItemStatKind.Defense;
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
        /// <summary>The trigger its rule uses (for archetype matching).</summary>
        public TriggerKind Trigger;
        /// <summary>Mood effect leans it answers (moods.json effectLean: "ally", "haste", "precision"...).</summary>
        public string[] Leans = new string[0];

        public EffectTemplate(string id, bool absorbs, TemplateFactory f) { Id = id; Absorbs = absorbs; Factory = f; }

        public EffectTemplate(string id, bool absorbs, TriggerKind trigger, string leans, TemplateFactory f) : this(id, absorbs, f)
        {
            Trigger = trigger;
            Leans = leans.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public bool AnswersAny(IList<string> leans)
        {
            if (leans == null) return false;
            foreach (var l in Leans) if (leans.Contains(l)) return true;
            return false;
        }
    }
}
