using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Items.Text
{
    /// <summary>
    /// Prompt text and output schemas for the LLM step. The grammar summary is
    /// built from the Core enums, so it never drifts from what the parser accepts.
    /// </summary>
    public static class Prompts
    {
        public const int Version = 1;

        public static string SystemPrompt(DraftKind kind)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You write items for Relic, a bright medieval fantasy action RPG set in one procedurally generated world, the Wide.");
            sb.AppendLine("Answer with a single JSON object that matches the schema. No prose outside the JSON.");
            sb.AppendLine();
            sb.AppendLine("THE ITEM SENTENCE");
            sb.AppendLine("- Every item arrives already decided: base, archetype (its purpose), culture and maker, material, mood and motif. These are facts. Keep them; do not change or contradict them.");
            sb.AppendLine("- The name follows one of the culture's naming patterns and keeps the item's form noun. You may add flair inside the pattern, never new proper nouns.");
            sb.AppendLine("- Effects should match the motif (its statuses) and the archetype (its triggers); the mood says whether they lean bright (allies, auras, discovery) or somber (drawbacks, curses).");
            sb.AppendLine();
            sb.AppendLine("STYLE GUIDE (docs/lore/style-guide.md)");
            sb.AppendLine(LoreSource.StyleGuideText.Trim());
            sb.AppendLine();
            sb.AppendLine("HARD RULES (checked in code)");
            sb.AppendLine("- Never use these words: " + string.Join(", ", StyleGuide.Forbidden) + ".");
            sb.AppendLine("- Avoid any word listed as overused in the request.");
            sb.AppendLine("- Names: at most " + DraftValidator.MaxNameWords + " words and " + DraftValidator.MaxNameLength + " characters.");
            sb.AppendLine("- Every sentence: at most " + StyleGuide.MaxSentenceWords + " words. Capitalized names must come from the request.");
            if (kind == DraftKind.RareName)
            {
                sb.AppendLine("- flavor: exactly one short, concrete sentence (under " + DraftValidator.MaxFlavorLength + " characters).");
                return sb.ToString();
            }
            sb.AppendLine("- lore: " + StyleGuide.MinLoreSentences + "–" + StyleGuide.MaxLoreSentences + " sentences. Concrete: who made or carried it, what it did, who cared about it. Mention at least one place, faction, person, legend or era from the world context BY NAME and list their ids in worldRefs.");
            if (kind == DraftKind.Legendary)
                sb.AppendLine("- Legendary lore names a legend or an era (ids legend.* / era.*) and ends with a lead: a hint toward a place other than where it was made or found.");
            sb.AppendLine();
            sb.AppendLine(GrammarSummary());
            return sb.ToString();
        }

        /// <summary>Compact description of the effect grammar (05-effect-grammar.md), generated from the Core enums.</summary>
        public static string GrammarSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("EFFECT GRAMMAR (enchantments are rules: trigger → conditions → actions)");
            sb.AppendLine("triggers: " + Names<TriggerKind>() + ". Periodic needs trigger.interval (s ≥ 0.25); OnLowHealth needs trigger.threshold (0–1); OnStatusApplied needs trigger.status.");
            sb.AppendLine("conditions (optional, each may set negate): " + Names<ConditionKind>() + ". Health conditions use value 0–1; distance conditions use value in meters; InRegionTag uses tag.");
            sb.AppendLine("actions: " + Names<ActionKind>() + ".");
            sb.AppendLine("  Damage(amount, damageKind " + Names<DamageKind>() + "); ApplyStatus(status, duration ≤ 15 s, stacks 1–5); RemoveStatus(status); Heal(amount); RestoreResource(resource, amount);");
            sb.AppendLine("  Knockback/Pull(amount = meters ≤ 15); ModifyStat(stat, amount or multiplier like 0.1 = +10%, duration 0 = while equipped);");
            sb.AppendLine("  Emit(emitter, payload): emitter.shape " + Names<EmitShape>() + ", count, speed 2–40 m/s, range ≤ 40 m, spreadDegrees, seeking, returning, splitCount, size, airborne. Payload = actions applied to whatever a projectile hits.");
            sb.AppendLine("  CreateArea(area, payload): area.shape Circle/Cone/Line, radius ≤ 12 m, angle, length/width, duration ≤ 30 s, tickInterval (0 = once), delay, followsSelf. Payload actions inside should target EnemiesInArea or AlliesInArea.");
            sb.AppendLine("  CreateBarrier(area with shape Wall, length ≤ 20, width, duration).");
            sb.AppendLine("targets: " + Names<TargetKind>() + " (Target = whatever the trigger is about; Self = the wearer).");
            sb.AppendLine("statuses: " + Names<StatusId>(skipFirst: true) + ". stats: " + Names<StatId>() + ". resources: " + Names<ResourceId>() + ".");
            sb.AppendLine("Limits: ≤ 4 rules, ≤ 4 actions per list, payload nesting ≤ 3, ≤ 24 projectiles (count × (1 + splitCount)). Passive rules may only ModifyStat or CreateArea.");
            sb.AppendLine("Units: meters, seconds; damage 10 = one starter-weapon hit at tier 0.");
            sb.AppendLine("POWER BUDGET: every rule costs power points (pp); 1 pp ≈ one 10-damage hit every 4 s. Cost grows with damage, status weight × duration, projectile count, area size × ticks, and how often the trigger fires (" + TriggerFrequencies() + "). Chance, cooldowns and conditions make rules cheaper. Self-harm (damage or statuses on Self, lowering your own stats) has NEGATIVE cost: a drawback buys power elsewhere. Stay under the budget you are given; over-budget effects are shrunk.");
            return sb.ToString();
        }

        /// <summary>
        /// Trigger frequency multipliers, read from <see cref="PowerBudget.Frequency"/> so the
        /// prompt always quotes the numbers the budget actually charges.
        /// </summary>
        static string TriggerFrequencies()
        {
            var parts = new List<string>();
            foreach (TriggerKind kind in Enum.GetValues(typeof(TriggerKind)))
            {
                if (kind == TriggerKind.Periodic) { parts.Add("Periodic 4/interval"); continue; }
                float f = PowerBudget.Frequency(new TriggerSpec { Kind = kind }, 0f);
                if (Math.Abs(f - 1f) < 0.001f) continue;
                parts.Add(kind + " " + f.ToString("0.##", CultureInfo.InvariantCulture) + "×");
            }
            return string.Join(", ", parts) + ", other triggers 1×";
        }

        static string Names<T>(bool skipFirst = false) where T : Enum
        {
            var names = new List<string>(Enum.GetNames(typeof(T)));
            if (skipFirst && names.Count > 0) names.RemoveAt(0);
            return string.Join(", ", names);
        }

        public static string UserPrompt(RelicBrief b)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            switch (b.Kind)
            {
                case DraftKind.RareName:
                    sb.AppendLine($"Write the name and one-line flavor for a Rare {Noun(b)} (region tier {b.Tier}).");
                    break;
                case DraftKind.Epic:
                    sb.AppendLine($"Write an Epic {Noun(b)} (region tier {b.Tier}): name, lore, worldRefs, and exactly 1 enchantment rule.");
                    sb.AppendLine(string.Format(ci, "Enchantment budget: {0:0.0} pp.", b.BudgetPp));
                    break;
                default:
                    sb.AppendLine($"Write a Legendary relic, a {b.Form} (region tier {b.Tier}): name, lore, worldRefs, {b.EnchantmentCount} enchantment rule(s), an ultimate, and visual parameters.");
                    sb.AppendLine(string.Format(ci, "Budget for enchantments + ultimate together: {0:0.0} pp. The ultimate is an Active rule released with Q; it always costs one full resonance bar and has a {1:0} s cooldown (set by the game).", b.BudgetPp, EffectTemplates.UltimateCooldown));
                    sb.AppendLine("visual.kitParts must come from: " + string.Join(", ", Forms.FormById(b.Sentence?.Kit ?? b.Form)?.Parts ?? Forms.KitParts) + " (any of: " + string.Join(", ", Forms.KitParts) + ").");
                    sb.AppendLine("visual.decal must be one of: " + string.Join(", ", Forms.Decals) + ". wear 0–1, emissiveIntensity 0–4, emissiveColor \"#RRGGBB\".");
                    break;
            }
            if (b.Sentence != null) sb.AppendLine(SentenceSummary(b));
            else if (b.Theme != null) sb.AppendLine($"Theme: {b.Theme.Motif.Id}, {b.Theme.Mood.Id}. Preferred statuses: {b.Theme.Primary}, {b.Theme.Secondary}.");
            if (b.Cursed) sb.AppendLine("This one is cursed: include a drawback for the wearer (self damage, a status on Self, or lowering a stat of Self) and spend the power it buys.");
            if (b.HomeRegion != null) sb.AppendLine($"It was found in {b.HomeRegion.Name} (id {b.HomeRegion.Id}, tags: {string.Join(", ", b.HomeRegion.Tags)}).");
            if (b.Stats != null && b.Stats.Count > 0)
            {
                var parts = new List<string>();
                foreach (var s in b.Stats) parts.Add(s.Stat + " " + s.Value.ToString("0.##", ci));
                sb.AppendLine("Rolled stats (already decided): " + string.Join(", ", parts) + ".");
            }
            if (b.BannedWords != null && b.BannedWords.Count > 0) sb.AppendLine("Overused words (do not use in the name): " + string.Join(", ", b.BannedWords) + ".");
            if (b.Kind != DraftKind.RareName && b.World != null)
            {
                sb.AppendLine("WORLD CONTEXT (use these names; put their ids in worldRefs):");
                sb.AppendLine(WorldSummary(b.World));
            }
            else if (b.World != null && b.HomeRegion != null)
            {
                sb.AppendLine("You may mention: " + b.HomeRegion.Name + ", " + b.HomeRegion.Landmark + ".");
            }
            return sb.ToString();
        }

        /// <summary>The item sentence as facts for the writer (17-item-grammar).</summary>
        public static string SentenceSummary(RelicBrief b)
        {
            var s = b.Sentence;
            if (s == null) return "";
            var bible = b.World?.Bible ?? LoreSource.TryDefault();
            var culture = bible?.Culture(s.Culture);
            var arch = b.Archetype ?? Archetypes.ById(s.Archetype);
            var sb = new StringBuilder();
            sb.AppendLine("ITEM SENTENCE (facts: keep them)");
            sb.AppendLine($"- form: {s.Form} (the name must keep \"{Relic.Lore.LoreSampler.Title(s.HeadNoun)}\")");
            sb.AppendLine($"- archetype: {arch?.Label} ({arch?.Feel}); favored triggers: {string.Join(", ", arch?.Triggers ?? new TriggerKind[0])}");
            sb.AppendLine($"- material: {s.Material}{(s.Accent != null ? ", trimmed with " + s.Accent : "")} ({s.MaterialTag})");
            if (b.Theme != null)
            {
                sb.AppendLine($"- mood: {b.Theme.Mood.Id} ({b.Theme.Tone}); effects lean: {string.Join(", ", b.Theme.Mood.EffectLean)}");
                sb.AppendLine($"- motif: {b.Theme.Motif.Id}; statuses: {string.Join(", ", b.Theme.Motif.Statuses)}; shapes: {string.Join(", ", b.Theme.Motif.Shapes)}; words: {string.Join(", ", b.Theme.Motif.Words)}");
            }
            if (b.Kind != DraftKind.RareName)
            {
                sb.AppendLine($"- culture: {culture?.Name} ({culture?.Identity})");
                sb.AppendLine($"- maker: {s.Maker} of {s.MakerHome}{(s.MakersMark != null ? "; signs work with " + s.MakersMark : "")}");
            }
            else sb.AppendLine($"- made in: {s.MakerHome}");
            if (culture != null)
            {
                var patterns = new List<string>();
                foreach (var p in culture.ItemNaming.Patterns) if (b.Kind != DraftKind.RareName || !p.Contains("{maker}")) patterns.Add(p);
                sb.AppendLine($"- naming patterns: {string.Join(" | ", patterns)}");
                sb.AppendLine($"- {{epithet}}: {string.Join(", ", culture.ItemNaming.Epithets)}; {{adjective}}: {string.Join(", ", culture.ItemNaming.Adjectives)}");
            }
            return sb.ToString().TrimEnd();
        }

        static string Noun(RelicBrief b) => b.Form ?? b.BaseType.ToString().ToLowerInvariant();

        public static string WorldSummary(WorldContext w)
        {
            var sb = new StringBuilder();
            foreach (var r in w.Regions) sb.AppendLine($"- {r.Id}: {r.Name}, region ({string.Join(", ", r.Tags)}), landmark {r.Landmark}");
            foreach (var f in w.Factions) sb.AppendLine($"- {f.Id}: {f.Name}, faction: {f.Ethos}");
            foreach (var p in w.Figures) sb.AppendLine($"- {p.Id}: {p.Name}, {p.Title} of {w.NameOf(p.FactionId)}; {p.Fate}");
            foreach (var e in w.Events) sb.AppendLine($"- {e.Id}: {e.Name}, {e.YearsAgo} years ago");
            if (w.Bible != null)
            {
                foreach (var l in w.Bible.Legends) sb.AppendLine($"- {WorldContext.LegendId(l)}: {Relic.Lore.LoreSampler.LegendName(l)}, legend ({l.Tone}): {l.Summary}");
                foreach (var e in w.Bible.Eras) sb.AppendLine($"- {WorldContext.EraId(e)}: {e.Name}, era, {e.StartYearsAgo}-{e.EndYearsAgo} years ago: {e.Summary}");
            }
            return sb.ToString().TrimEnd();
        }

        public static string Retry(IList<string> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Your previous answer was rejected for these reasons:");
            foreach (var e in errors) sb.Append("- ").AppendLine(e);
            sb.Append("Return the corrected JSON object only.");
            return sb.ToString();
        }

        // ---------- output schemas ----------

        public static string Schema(RelicBrief b)
        {
            var props = new JObject { ["name"] = new JObject { ["type"] = "string" } };
            var required = new JArray("name");
            if (b.Kind == DraftKind.RareName)
            {
                props["flavor"] = new JObject { ["type"] = "string" };
                required.Add("flavor");
            }
            else
            {
                var ids = new JArray();
                if (b.World != null) foreach (var id in b.World.AllIds()) ids.Add(id);
                props["lore"] = new JObject { ["type"] = "string" };
                props["worldRefs"] = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 6, ["items"] = ids.Count > 0 ? new JObject { ["type"] = "string", ["enum"] = ids } : new JObject { ["type"] = "string" } };
                props["enchantments"] = JsonSchemaBuilder.Program(1, b.Kind == DraftKind.Epic ? 1 : 3);
                required.Add("lore"); required.Add("worldRefs"); required.Add("enchantments");
                if (b.Kind == DraftKind.Legendary)
                {
                    props["ultimate"] = JsonSchemaBuilder.Rule();
                    props["visual"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["kitParts"] = new JObject { ["type"] = "array", ["minItems"] = 1, ["maxItems"] = 6, ["items"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Forms.KitParts) } },
                            ["material"] = new JObject { ["type"] = "string" },
                            ["wear"] = new JObject { ["type"] = "number" },
                            ["emissiveColor"] = new JObject { ["type"] = "string", ["pattern"] = "^#[0-9A-Fa-f]{6}$" },
                            ["decal"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Forms.Decals) },
                            ["emissiveIntensity"] = new JObject { ["type"] = "number" },
                        },
                        ["required"] = new JArray("kitParts", "material", "wear", "emissiveColor", "decal", "emissiveIntensity"),
                        ["additionalProperties"] = false,
                    };
                    required.Add("ultimate"); required.Add("visual");
                }
            }
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = required,
                ["additionalProperties"] = false,
            };
            return schema.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
