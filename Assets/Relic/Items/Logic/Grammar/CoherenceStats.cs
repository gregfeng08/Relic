using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Effects;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Coherence across many items: the 80% archetype-trigger rule and the 70/30 mood split
    /// can only be judged in aggregate. Feed items in with <see cref="Add"/>.
    /// </summary>
    public sealed class CoherenceStats
    {
        public int Items, Failed;
        public int Themed, Bright, Somber;
        /// <summary>Enchantment rules (not weapon skills, ultimates, slot passives or curse rules) and those on a favored trigger.</summary>
        public int Enchantments, Favored;
        public int Cursed, CursedSomber;
        public readonly Dictionary<string, int> Moods = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Archetypes = new Dictionary<string, int>();
        public readonly List<string> Errors = new List<string>();

        public void Add(Item item, WorldContext world, LoreBible bible = null)
        {
            Items++;
            var errs = CoherenceValidator.Validate(item, world, bible);
            if (errs.Count > 0) { Failed++; Errors.AddRange(errs); }
            var s = item.Grammar;
            if (s == null) return;
            Bump(Archetypes, s.Archetype);
            if (s.Has(GrammarLayer.Theme))
            {
                Themed++;
                if (s.IsSomber) Somber++; else Bright++;
                Bump(Moods, s.Mood);
            }
            var arch = Relic.Items.Archetypes.ById(s.Archetype);
            bool cursed = false;
            foreach (var r in item.Effects.Rules)
            {
                if (r.Name == EffectTemplates.CurseRuleName) { cursed = true; continue; }
                foreach (var a in r.Actions)
                    if ((a.Target == TargetKind.Self) && PowerBudget.Cost(a) < 0f && a.Payload == null) cursed = true;
                if (r.Trigger.Kind == TriggerKind.Active || r.Name == s.SlotPassive) continue;
                Enchantments++;
                if (arch != null && arch.Favors(r.Trigger.Kind)) Favored++;
            }
            if (cursed) { Cursed++; if (s.IsSomber) CursedSomber++; }
        }

        public double FavoredShare => Enchantments == 0 ? 0 : (double)Favored / Enchantments;
        public double BrightShare => Themed == 0 ? 0 : (double)Bright / Themed;

        static void Bump(Dictionary<string, int> d, string k) => d[k] = (d.TryGetValue(k, out int n) ? n : 0) + 1;

        public override string ToString()
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, "{0} items, {1} incoherent; archetype triggers {2:P0} of {3} enchantments; mood {4:P0} bright / {5:P0} somber of {6} themed; {7} cursed ({8} somber)",
                Items, Failed, FavoredShare, Enchantments, BrightShare, Themed == 0 ? 0 : (double)Somber / Themed, Themed, Cursed, CursedSomber);
        }
    }
}
