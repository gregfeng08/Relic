using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Stats;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// The coherence validator (17-item-grammar), next to the budget validator: does every
    /// part of an item follow from its sentence? Stat lines from the archetype and no line the
    /// base forbids; statuses from the motif and archetype; drawbacks only where the mood is
    /// somber; the look from the material and motif; the name and lore from the culture; the
    /// layers its rarity promises. Messages say what is wrong and why, in plain words.
    /// </summary>
    public static class CoherenceValidator
    {
        public static List<string> Validate(Item item, WorldContext world, LoreBible bible = null)
        {
            var errors = new List<string>();
            var s = item.Grammar;
            if (s == null) { errors.Add(Who(item) + ": has no item sentence (Item.Grammar)"); return errors; }
            bible = bible ?? world?.Lore ?? LoreSource.Default;
            string who = Who(item);
            void Err(string msg) => errors.Add(who + ": " + msg);

            var arch = Archetypes.ById(s.Archetype);
            var culture = bible.Culture(s.Culture);
            var mat = Materials.Find(s.Material);
            if (arch == null) { Err($"unknown archetype \"{s.Archetype}\""); return errors; }
            if (culture == null) { Err($"unknown culture \"{s.Culture}\""); return errors; }
            if (mat == null) { Err($"unknown material \"{s.Material}\""); return errors; }
            if (s.Layers != ItemSentence.LayersFor(item.Rarity)) Err($"layers {s.Layers} don't match a {item.Rarity} item ({ItemSentence.LayersFor(item.Rarity)})");

            // Culture belongs where the item was found.
            var region = world?.Region(s.RegionId);
            if (region != null && !culture.Families.Exists(f => f.ToString() == region.Family))
                Err($"{culture.Adjective} work found in {region.Name} ({region.Family}), but the {culture.Adjective} don't live in {region.Family} regions");

            // Material: grounded in the base and the tier.
            var kinds = ItemGrammar.KindsFor(item.BaseType, s.Kit);
            if (Array.IndexOf(kinds, mat.Kind) < 0)
                Err($"made of {mat.Name} ({mat.Kind}), but a {Pretty(item.BaseType)}{(s.Kit != null ? " (" + s.Kit + ")" : "")} is made of {string.Join(" or ", kinds)}");
            bool namedByForm = CultureForms.NamedMaterial(s.Form)?.Name == mat.Name;
            if (mat.TierMin > item.Tier && !namedByForm) Err($"{mat.Name} first turns up at tier {mat.TierMin}, but this is a tier {item.Tier} item");

            CheckStats(item, s, arch, mat, Err);
            CheckEffects(item, s, arch, bible, Err);
            CheckLayers(item, s, culture, world, Err);

            // The look comes from the material and the motif.
            if (item.Visual == null) Err("has no visual");
            else
            {
                if (!string.Equals(item.Visual.Material, s.Material, StringComparison.OrdinalIgnoreCase))
                    Err($"looks like {item.Visual.Material}, but it is made of {s.Material}");
                var motif = bible.Motif(s.Motif);
                if (s.Has(GrammarLayer.Theme) && motif != null && !string.Equals(item.Visual.EmissiveColor, motif.Color, StringComparison.OrdinalIgnoreCase))
                    Err($"glows {item.Visual.EmissiveColor}, but its motif {motif.Id} glows {motif.Color}");
            }
            return errors;
        }

        static void CheckStats(Item item, ItemSentence s, ArchetypeDef arch, MaterialDef mat, Action<string> Err)
        {
            int archetypeLines = 0;
            foreach (var st in item.Stats)
            {
                if (Archetypes.Forbids(item.BaseType, st.Stat))
                    Err($"rolls {st.Stat}, which a {Pretty(item.BaseType)} never carries");
                if (st.Source == ArchetypeLines.ArchetypeSource)
                {
                    archetypeLines++;
                    if (!arch.FavorsLine(st.Stat)) Err($"its archetype line is {st.Stat}, which a {arch.Label} doesn't favor ({string.Join(", ", arch.StatLines)})");
                }
                if (st.Source == ArchetypeLines.MaterialSource && st.Stat != mat.Tag)
                    Err($"its material line is {st.Stat}, but {mat.Name}'s tag is {mat.Tag}");
            }
            if (item.Rarity == Rarity.Common && archetypeLines > 0) Err("a Common item shows an archetype line; archetypes start at Uncommon");
            if (item.Rarity >= Rarity.Uncommon && archetypeLines != 1) Err($"has {archetypeLines} archetype lines; Uncommon and up have exactly one ({arch.Label}: {arch.StatLines[0]}...)");
            if (item.BaseType == BaseType.Trinket && (item.Stats.Count == 0 || item.Stats[0].Stat != arch.Trained))
                Err($"a {arch.Label}'s trinket carries {arch.Trained} as its main line");
        }

        static void CheckEffects(Item item, ItemSentence s, ArchetypeDef arch, LoreBible bible, Action<string> Err)
        {
            var theme = s.Has(GrammarLayer.Theme) ? ItemGrammar.ThemeOf(s, bible) : null;
            var ctx = new TemplateContext { Theme = theme, Archetype = arch, BaseType = item.BaseType, Tier = item.Tier, Rarity = item.Rarity };
            var allowed = ctx.AllowedStatuses();
            bool drawback = false;
            foreach (var r in item.Effects.Rules)
            {
                if (r.Name == EffectTemplates.CurseRuleName) drawback = true;
                Walk(r.Actions, a =>
                {
                    bool onSelf = a.Target == TargetKind.Self || a.Target == TargetKind.AlliesInArea;
                    if (a.Kind == ActionKind.ApplyStatus && !onSelf && !allowed.Contains(a.Status))
                        Err($"\"{r.Name}\" inflicts {a.Status}, which fits neither its motif ({(theme != null ? string.Join("/", theme.Motif.Statuses) : "none yet")}) nor a {arch.Label} ({string.Join("/", arch.Statuses)})");
                    if (a.Kind == ActionKind.ModifyStat && onSelf && (a.Amount > 0f || a.Multiplier > 0f) && Archetypes.Forbids(item.BaseType, TemplateContext.KindOf(a.Stat)))
                        Err($"\"{r.Name}\" raises {a.Stat}, which a {Pretty(item.BaseType)} never grants");
                    if (onSelf && PowerBudget.Cost(a) < 0f && a.Payload == null) drawback = true;
                });
            }
            if (drawback && !s.IsSomber) Err($"carries a drawback, but its mood ({s.Mood}) is bright; curses and prices belong to somber moods");
            var mood = bible.Mood(s.Mood);
            if (item.Rarity >= Rarity.Epic && mood != null && mood.EffectLean.Contains("curse") && !drawback)
                Err($"its mood is {s.Mood}, which always carries a price, but no drawback was found");
        }

        static void CheckLayers(Item item, ItemSentence s, CultureDef culture, WorldContext world, Action<string> Err)
        {
            if (!s.Has(GrammarLayer.Theme) && item.Theme != null) Err($"a {item.Rarity} item shows a theme ({item.Theme}); themes start at Rare");
            if (s.Has(GrammarLayer.Theme) && item.Theme != s.Motif + " · " + s.Mood) Err($"shows theme \"{item.Theme}\" but its sentence says {s.Motif} · {s.Mood}");

            if (!s.Has(GrammarLayer.Name))
            {
                string plain = ItemGenerator.TableName(s);
                if (item.Name != plain) Err($"a {item.Rarity} item has a plain name (\"{plain}\"), not \"{item.Name}\"");
                if (item.Lore != null) Err($"a {item.Rarity} item has no lore");
                return;
            }

            if (item.Name.IndexOf(s.HeadNoun, StringComparison.OrdinalIgnoreCase) < 0) Err($"name \"{item.Name}\" drops its form noun \"{s.HeadNoun}\"");
            if (s.NamePattern == null) Err($"name \"{item.Name}\" follows none of the {culture.Adjective} naming patterns ({string.Join(" | ", culture.ItemNaming.Patterns)})");
            else if (!s.Has(GrammarLayer.Maker) && s.NamePattern.Contains("{maker}")) Err($"a {item.Rarity} item names its maker; makers are named from Epic up");

            if (!s.Has(GrammarLayer.Lore))
            {
                if (item.Lore?.Flavor == null) Err("a Rare item has a one-line flavor");
                if (item.Lore?.History != null) Err("a Rare item has no lore paragraph; lore starts at Epic");
                return;
            }

            if (item.Lore?.History == null) { Err($"an {item.Rarity} item has a lore line"); return; }
            if (string.IsNullOrEmpty(s.Maker)) Err("has no maker; Epic items are made by someone");
            if (item.Rarity == Rarity.Epic)
            {
                int written = 0;
                foreach (var r in item.Effects.Rules)
                    if (r.Trigger.Kind != TriggerKind.Active && r.Name != s.SlotPassive) written++;
                if (written != 1) Err($"has {written} enchantments; an Epic item has exactly one");
            }

            if (!s.Has(GrammarLayer.WorldRefs) || world == null) return;
            bool place = false, bible = false, lead = false;
            foreach (var id in item.Lore.WorldRefs)
            {
                if (world.IsPlace(id)) { place = true; if (id != s.RegionId && id != s.MakerHomeId) lead = true; }
                if (world.IsBibleRef(id)) bible = true;
            }
            if (!place) Err("Legendary lore names no place");
            if (!bible) Err("Legendary lore names no legend or era from the Lore Bible");
            if (!lead) Err("Legendary lore has no lead toward another place");
            var last = item.Effects.Rules.Count > 0 ? item.Effects.Rules[item.Effects.Rules.Count - 1] : null;
            if (last == null || last.Trigger.Kind != TriggerKind.Active || last.CostResource != ResourceId.Resonance) Err("a Legendary relic ends with its ultimate (Q)");
        }

        static void Walk(List<ActionSpec> actions, Action<ActionSpec> visit)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                if (a == null) continue;
                visit(a);
                Walk(a.Payload, visit);
            }
        }

        static string Who(Item item) =>
            $"{item.Id} \"{item.Name}\" ({item.Rarity} {Pretty(item.BaseType)} t{item.Tier}{(item.Grammar != null ? "; " + item.Grammar : "")})";

        static string Pretty(BaseType t)
        {
            switch (t)
            {
                case BaseType.LightArmor: return "light armor";
                case BaseType.MediumArmor: return "medium armor";
                case BaseType.HeavyArmor: return "heavy armor";
                default: return t.ToString().ToLowerInvariant();
            }
        }
    }
}
