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
            sb.AppendLine("You write items for Relic, an action RPG set in one procedurally generated world.");
            sb.AppendLine("Answer with a single JSON object that matches the schema. No prose outside the JSON.");
            sb.AppendLine();
            sb.AppendLine("STYLE GUIDE");
            sb.AppendLine("- Names and lore must sound like THIS world: use the places, factions, people and events in the world context, and its plain, concrete vocabulary (weirs, tithes, kilns, ferries, ledgers).");
            sb.AppendLine("- Not generic fantasy. Never use these words: " + string.Join(", ", StyleGuide.Forbidden) + ".");
            sb.AppendLine("- Avoid any word listed as overused in the request.");
            sb.AppendLine("- Names: at most " + DraftValidator.MaxNameWords + " words and " + DraftValidator.MaxNameLength + " characters.");
            if (kind == DraftKind.RareName)
            {
                sb.AppendLine("- flavor: exactly one short, concrete sentence (under " + DraftValidator.MaxFlavorLength + " characters).");
                return sb.ToString();
            }
            sb.AppendLine("- lore: 2–4 sentences. Concrete and historical: who made or carried it, what happened, where it went. Mention at least one place, faction, person or event from the world context BY NAME and list their ids in worldRefs.");
            sb.AppendLine("- End the lore with a lead: a hint that points the player toward another place or person in the world.");
            sb.AppendLine("- Make the enchantments match the lore: if the lore says it drowned a town, the effects should soak and pull.");
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
            sb.AppendLine("POWER BUDGET: every rule costs power points (pp); 1 pp ≈ one 10-damage hit every 4 s. Cost grows with damage, status weight × duration, projectile count, area size × ticks, and how often the trigger fires (OnHit ≈ 2.5×, OnKill 0.5×, Periodic 4/interval). Chance, cooldowns and conditions make rules cheaper. Self-harm (damage or statuses on Self, lowering your own stats) has NEGATIVE cost: a drawback buys power elsewhere. Stay under the budget you are given; over-budget effects are shrunk.");
            return sb.ToString();
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
                    sb.AppendLine("visual.kitParts must come from: " + string.Join(", ", Forms.FormById(b.Form)?.Parts ?? Forms.KitParts) + " (any of: " + string.Join(", ", Forms.KitParts) + ").");
                    sb.AppendLine("visual.decal must be one of: " + string.Join(", ", Forms.Decals) + ". wear 0–1, emissiveIntensity 0–4, emissiveColor \"#RRGGBB\".");
                    break;
            }
            if (b.Theme != null) sb.AppendLine($"Theme: {b.Theme.Motif.Id}, {b.Theme.Mood.Id}. Preferred statuses: {b.Theme.Motif.Primary}, {b.Theme.Motif.Secondary}.");
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

        static string Noun(RelicBrief b) => b.Form ?? b.BaseType.ToString().ToLowerInvariant();

        public static string WorldSummary(WorldContext w)
        {
            var sb = new StringBuilder();
            foreach (var r in w.Regions) sb.AppendLine($"- {r.Id}: {r.Name}, region ({string.Join(", ", r.Tags)}), landmark {r.Landmark}");
            foreach (var f in w.Factions) sb.AppendLine($"- {f.Id}: {f.Name}, faction: {f.Ethos}");
            foreach (var p in w.Figures) sb.AppendLine($"- {p.Id}: {p.Name}, {p.Title} of {w.NameOf(p.FactionId)}; {p.Fate}");
            foreach (var e in w.Events) sb.AppendLine($"- {e.Id}: {e.Name}, {e.YearsAgo} years ago");
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
                if (b.World != null)
                {
                    foreach (var r in b.World.Regions) ids.Add(r.Id);
                    foreach (var f in b.World.Factions) ids.Add(f.Id);
                    foreach (var p in b.World.Figures) ids.Add(p.Id);
                    foreach (var e in b.World.Events) ids.Add(e.Id);
                }
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
