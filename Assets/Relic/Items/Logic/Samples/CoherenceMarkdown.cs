using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Relic.Lore;

namespace Relic.Items.Samples
{
    /// <summary>The "why it's built this way" parts of the samples: each item's sentence and the set's coherence numbers.</summary>
    public static class CoherenceMarkdown
    {
        /// <summary>A short block under each item: the sentence (archetype, culture, material, mood/motif) and the check.</summary>
        public static string ItemSection(Item it, WorldContext world)
        {
            var s = it.Grammar;
            if (s == null) return "";
            var bible = world?.Bible ?? LoreSource.Default;
            var arch = Archetypes.ById(s.Archetype);
            var culture = bible.Culture(s.Culture);
            var mat = Materials.Find(s.Material);
            var motif = bible.Motif(s.Motif);
            var sb = new StringBuilder();
            sb.AppendLine("**Coherence** — *the item sentence*");
            sb.AppendLine();
            string kit = s.Kit != null ? $" (kit: {s.Kit})" : "";
            sb.AppendLine($"- **Base:** {Pretty(it.BaseType)}, a *{s.Form}*{kit}{(s.CultureForm ? $" — a {culture?.Adjective} form" : "")}");
            sb.AppendLine($"- **Archetype:** {arch?.Label} ({arch?.Feel}) → archetype line {Lines(it, ArchetypeLines.ArchetypeSource)}; favored triggers {string.Join(", ", arch?.Triggers ?? new Relic.Core.Effects.TriggerKind[0])}");
            string maker = s.Has(GrammarLayer.Maker) ? $"; made by **{s.Maker}** of {s.MakerHome}{(s.MakersMark != null ? ", who signs with " + s.MakersMark : "")}" : $" (from {s.MakerHome}; maker shown from Epic)";
            sb.AppendLine($"- **Culture:** {culture?.Adjective}{maker}");
            sb.AppendLine($"- **Material:** {s.Material}{(s.Accent != null ? " with " + s.Accent : "")} ({mat?.Kind.ToString().ToLowerInvariant()}, tier {mat?.TierMin}+) — *{s.MaterialTag}* → {Lines(it, ArchetypeLines.MaterialSource)}; wear {it.Visual?.Wear:0.##}");
            if (s.Has(GrammarLayer.Theme))
                sb.AppendLine($"- **Theme:** {s.Mood} ({s.Tone.ToLowerInvariant()}) · {s.Motif} — statuses {string.Join("/", motif.Statuses)}, shapes {string.Join("/", motif.Shapes)}, glow {motif.Color}");
            else
                sb.AppendLine($"- **Theme:** not shown below Rare (resolved as {s.Mood} · {s.Motif})");
            sb.AppendLine($"- **Quality:** {s.Quality} · **Name:** {(s.NamePattern != null ? "`" + s.NamePattern + "`" : "plain (quality + material + noun)")}");
            var errs = CoherenceValidator.Validate(it, world, bible);
            sb.AppendLine($"- **Layers ({it.Rarity}):** {s.Layers.ToString().Replace(", ", " · ")} · coherence check: {(errs.Count == 0 ? "passed" : "FAILED — " + string.Join("; ", errs))}");
            sb.AppendLine();
            return sb.ToString();
        }

        static string Lines(Item it, string source)
        {
            var lines = it.Stats.Where(x => x.Source == source).Select(x => x.Stat.ToString()).ToList();
            return lines.Count == 0 ? "none" : string.Join(", ", lines);
        }

        /// <summary>The set's coherence and mood split, for the top of the samples.</summary>
        public static string Summary(IEnumerable<Item> items, WorldContext world)
        {
            var stats = new CoherenceStats();
            foreach (var it in items) stats.Add(it, world);
            var sb = new StringBuilder();
            sb.AppendLine("## Coherence at a glance");
            sb.AppendLine();
            sb.AppendLine($"- **{stats.Items - stats.Failed} of {stats.Items}** items pass the coherence validator (and all pass the budget validator).");
            sb.AppendLine($"- **Mood split:** {stats.Bright} bright / {stats.Somber} somber of {stats.Themed} themed items ({stats.BrightShare:P0} bright; target about 70%). Moods: {string.Join(", ", stats.Moods.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value))}.");
            sb.AppendLine($"- **Archetype triggers:** {stats.Favored} of {stats.Enchantments} enchantments ({stats.FavoredShare:P0}; target about 80%) use their archetype's favored triggers.");
            sb.AppendLine($"- **Drawbacks:** {stats.Cursed} items carry a price, all of them somber ({stats.CursedSomber}).");
            sb.AppendLine($"- **Archetypes:** {string.Join(", ", stats.Archetypes.OrderByDescending(k => k.Value).Select(k => (Archetypes.ById(k.Key)?.Label ?? k.Key) + " " + k.Value))}.");
            if (stats.Errors.Count > 0) sb.AppendLine("- **Incoherent:** " + string.Join("; ", stats.Errors.Take(5)));
            sb.AppendLine();
            return sb.ToString();
        }

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
