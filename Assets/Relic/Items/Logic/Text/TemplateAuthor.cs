using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Items.Text
{
    /// <summary>
    /// Deterministic stand-in for the LLM: writes the same draft shape from
    /// tables and sentence templates. Used for TEMPLATE MODE samples (so the
    /// pipeline, budgets and rolls can be reviewed before any model is approved)
    /// and as the fallback relic when the LLM fails every attempt.
    /// </summary>
    public static class TemplateAuthor
    {
        public const string Source = "template-v1";

        public static RelicDraft Compose(RelicBrief b)
        {
            var rng = Pcg32.ForPurpose(b.Seed, "relic.template");
            var refs = PickRefs(b, ref rng);
            var draft = new RelicDraft { Name = Name(b, refs) };
            if (b.Kind == DraftKind.RareName)
            {
                draft.Flavor = Flavor(b, refs, ref rng);
                return draft;
            }
            draft.Lore = Lore(b, refs, ref rng, out var used);
            draft.WorldRefs = used;
            var ctx = new TemplateContext { Theme = b.Theme, Tier = b.Tier, Rarity = b.Rarity, BaseType = b.BaseType, HomeRegion = b.HomeRegion };

            if (b.Kind == DraftKind.Epic)
            {
                draft.Enchantments = new EffectProgram();
                float target = b.BudgetPp * (0.85f + 0.1f * rng.NextFloat());
                draft.Enchantments.Rules.Add(FitEnchantment(ctx, target, new HashSet<string>(), b.Cursed, ref rng, out _));
                return draft;
            }

            // Legendary: ultimate first (its share is measured without resonance relief), then enchantments share what's left.
            var ultTemplate = EffectTemplates.Ultimates[rng.Range(0, EffectTemplates.Ultimates.Length)];
            var ultBuilder = ultTemplate.Factory(ctx, ref rng);
            draft.Ultimate = EffectFitter.Fit(ultBuilder, b.BudgetPp * (0.25f + 0.1f * rng.NextFloat()), EffectFitter.UnrelievedCost);
            float remaining = b.BudgetPp - PowerBudget.Cost(draft.Ultimate);

            draft.Enchantments = new EffectProgram();
            int count = Math.Max(1, Math.Min(3, b.EnchantmentCount));
            bool cursed = b.Cursed;
            EffectRule curse = null;
            if (cursed && count < 3)
            {
                // A standing curse rule (lowers a stat while equipped) buys power for the others.
                curse = EffectTemplates.CurseRule(ctx, ref rng);
                remaining -= PowerBudget.Cost(curse); // negative cost: adds budget
                cursed = false;
            }
            float fill = 0.88f + 0.1f * rng.NextFloat();
            var weights = new float[count];
            float wsum = 0f;
            for (int i = 0; i < count; i++) { weights[i] = 1f + rng.NextFloat(); wsum += weights[i]; }
            var usedTemplates = new HashSet<string>();
            float pool = remaining * fill;
            for (int i = 0; i < count; i++)
            {
                float target = pool * weights[i] / wsum;
                wsum -= weights[i];
                var rule = FitEnchantment(ctx, target, usedTemplates, cursed && i == 0, ref rng, out float cost);
                pool -= cost;
                draft.Enchantments.Rules.Add(rule);
            }
            if (curse != null) draft.Enchantments.Rules.Add(curse);
            draft.Visual = Visual(b, ref rng);
            return draft;
        }

        /// <summary>Picks a template that fits the target and builds it; tries others if one can't fit.</summary>
        static EffectRule FitEnchantment(TemplateContext ctx, float target, HashSet<string> used, bool cursed, ref Pcg32 rng, out float cost)
        {
            var all = EffectTemplates.Enchantments;
            int start = rng.Range(0, all.Length);
            ActionSpec drawback = cursed ? EffectTemplates.Drawback(ctx, ref rng) : null;
            EffectRule best = null; float bestCost = float.MaxValue;
            for (int k = 0; k < all.Length; k++)
            {
                var t = all[(start + k) % all.Length];
                if (used.Contains(t.Id)) continue;
                var builder = t.Factory(ctx, ref rng);
                EffectFitter.RuleBuilder withCurse = builder;
                if (drawback != null)
                    withCurse = s =>
                    {
                        var r = builder(s);
                        if (r.Actions.Count < EffectValidator.MaxActionsPerList && r.Trigger.Kind != TriggerKind.Passive) r.Actions.Add(ItemJson.Clone(drawback));
                        return r;
                    };
                var rule = EffectFitter.Fit(withCurse, target);
                float c = PowerBudget.Cost(rule);
                if (c <= target + 1e-3f && c >= 0.5f * target)
                {
                    used.Add(t.Id);
                    cost = c;
                    return rule;
                }
                if (c <= target + 1e-3f && (best == null || c > bestCost || bestCost > target)) { best = rule; bestCost = c; }
            }
            if (best == null)
            {
                // Nothing fits (tiny budget): a bare status rider at minimum strength, sized down by ScaleToFit later.
                best = EffectFitter.Fit(EffectTemplates.Enchantments[0].Factory(ctx, ref rng), target);
                bestCost = PowerBudget.Cost(best);
            }
            cost = bestCost;
            return best;
        }

        // ---------- world references ----------

        sealed class Refs
        {
            public WorldRegion Region, LeadRegion;
            public WorldFaction Faction, LeadFaction;
            public WorldFigure Figure, LeadFigure;
            public WorldEvent Event;
        }

        static Refs PickRefs(RelicBrief b, ref Pcg32 rng)
        {
            var w = b.World ?? StubWorld.Create(b.Seed);
            var r = new Refs();
            r.Region = b.HomeRegion ?? w.Regions[rng.Range(0, w.Regions.Count)];
            r.Figure = w.Figures[rng.Range(0, w.Figures.Count)];
            r.Faction = w.Faction(r.Figure.FactionId) ?? w.Factions[0];
            var local = w.Events.FindAll(e => e.RegionId == r.Region.Id);
            r.Event = local.Count > 0 && rng.Chance(0.7f) ? local[rng.Range(0, local.Count)] : w.Events[rng.Range(0, w.Events.Count)];
            do r.LeadRegion = w.Regions[rng.Range(0, w.Regions.Count)]; while (r.LeadRegion == r.Region && w.Regions.Count > 1);
            do r.LeadFigure = w.Figures[rng.Range(0, w.Figures.Count)]; while (r.LeadFigure == r.Figure && w.Figures.Count > 1);
            do r.LeadFaction = w.Factions[rng.Range(0, w.Factions.Count)]; while (r.LeadFaction == r.Faction && w.Factions.Count > 1);
            return r;
        }

        // ---------- names ----------

        static string Cap(string w) => string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + w.Substring(1);

        static string CapWords(string s)
        {
            var outp = new System.Text.StringBuilder();
            int i = 0;
            foreach (char c in s)
            {
                bool start = i == 0 || s[i - 1] == ' ' || s[i - 1] == '-';
                outp.Append(start ? char.ToUpperInvariant(c) : c);
                i++;
            }
            return outp.ToString();
        }

        static string FirstName(WorldFigure f) => f.Name.Split(' ')[0];

        static string WordKey(string name)
        {
            var w = StyleGuide.Words(name);
            w.Sort(StringComparer.Ordinal);
            return string.Join(" ", w);
        }

        static string Name(RelicBrief b, Refs refs)
        {
            var rng = Pcg32.ForPurpose(b.Seed, "relic.template.name");
            var motif = b.Theme?.Motif;
            var mood = b.Theme?.Mood;
            string noun = CapWords(b.Kind == DraftKind.Legendary ? b.Form.Replace('-', ' ') : (b.Form ?? b.BaseType.ToString()));
            string word() => CapWords(motif != null ? motif.Words[rng.Range(0, motif.Words.Length)] : "plain");
            string moodWord() => mood != null ? mood.Words[rng.Range(0, mood.Words.Length)] : "Old";

            var candidates = new List<string>();
            for (int round = 0; round < 6; round++)
            {
                var c = new List<string>
                {
                    FirstName(refs.Figure) + "'s " + noun,
                    "The " + moodWord() + " " + noun,
                    "The " + noun + " of " + refs.Region.Name,
                    moodWord() + " " + word(),
                    "The " + word() + " " + noun,
                    refs.Region.Name + " " + noun,
                    word() + " and " + word(),
                    FirstName(refs.LeadFigure) + "'s " + word(),
                    "The " + moodWord() + " " + word(),
                    noun + " of the " + DraftValidator.StripArticle(refs.Faction.Name).Split(' ')[0] + " " + word(),
                };
                // seeded shuffle so different relics favor different patterns
                for (int i = c.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = c[i]; c[i] = c[j]; c[j] = t; }
                candidates.AddRange(c);
            }

            var banned = new HashSet<string>(b.BannedWords ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var taken = new HashSet<string>(b.TakenNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var takenWordSets = new HashSet<string>();
            foreach (var t in taken) takenWordSets.Add(WordKey(t));
            foreach (var cand in candidates)
            {
                if (cand.Length > DraftValidator.MaxNameLength) continue;
                if (cand.Split(' ').Length > DraftValidator.MaxNameWords) continue;
                if (StyleGuide.ForbiddenIn(cand).Count > 0) continue;
                if (taken.Contains(cand) || takenWordSets.Contains(WordKey(cand))) continue; // also "Sloe and Hawthorn" after "Hawthorn and Sloe"
                var words = StyleGuide.Words(cand);
                if (new HashSet<string>(words).Count != words.Count) continue; // "Boulder and Boulder"
                bool ok = true;
                foreach (var w in words) if (banned.Contains(w)) { ok = false; break; }
                if (ok) return cand;
            }
            // Everything is overused (only possible with a tiny vocabulary): fall back to the least-bad candidate.
            return candidates[0];
        }

        // ---------- flavor (Rare) ----------

        static string Flavor(RelicBrief b, Refs r, ref Pcg32 rng)
        {
            var motif = b.Theme?.Motif;
            string word = motif != null ? motif.Words[rng.Range(0, motif.Words.Length)] : "oil";
            string[] lines =
            {
                $"Carried out of {r.Region.Name} by one of {r.Faction.Name}.",
                $"Stamped with the mark of {r.Faction.Name}, who would like it back.",
                $"It smells of {word}, like everything that comes out of {r.Region.Name}.",
                $"Found near {r.Region.Landmark}, wrapped in oilcloth and tied with a ferry knot.",
                $"Its grip is worn to the shape of somebody's hand from {r.Region.Name}.",
                $"{r.Figure.Name} owned one like it, before {r.Event.Name}.",
                $"Pawned twice in {r.LeadRegion.Name} and redeemed both times.",
            };
            return lines[rng.Range(0, lines.Length)];
        }

        // ---------- lore (Epic, Legendary) ----------

        static string Lore(RelicBrief b, Refs r, ref Pcg32 rng, out List<string> refs)
        {
            var found = new List<string>();
            refs = found;
            string form = b.Kind == DraftKind.Legendary ? b.Form.Replace('-', ' ') : (b.Form ?? b.BaseType.ToString()).ToLowerInvariant();
            string fig = r.Figure.Name, title = r.Figure.Title, fac = r.Faction.Name, reg = r.Region.Name, land = r.Region.Landmark, ev = r.Event.Name;
            int years = r.Event.YearsAgo;
            string mood = b.Theme?.Mood.Id ?? "vigil";
            string motif = b.Theme?.Motif.Id ?? "stone";
            string material = b.Theme != null ? b.Theme.Motif.Materials[rng.Range(0, b.Theme.Motif.Materials.Length)] : "iron";

            string[] origins;
            switch (mood)
            {
                case "betrayal":
                    origins = new[]
                    {
                        $"{fig}, {title} of {fac}, carried this {form} on the night {land} was opened to the enemy during {ev}.",
                        $"This {form} was a gift from {fig} to {fac}, given a week before {fig} sold them out at {land}.",
                    }; break;
                case "grief":
                    origins = new[]
                    {
                        $"{fig} had this {form} made after {ev}, from what was left of {land}.",
                        $"{fac} gave this {form} to the bereaved of {reg} after {ev}, and {fig} refused to give it back.",
                    }; break;
                case "oath":
                    origins = new[]
                    {
                        $"Members of {fac} swore their oaths on this {form} at {land}, and {fig}, {title}, was the last to do so.",
                        $"{fig} bound {fac} to its charter with this {form} and kept the binding until {ev}.",
                    }; break;
                case "hunger":
                    origins = new[]
                    {
                        $"During {ev}, {fig} traded the last grain in {reg} for this {form}.",
                        $"{fig}, {title} of {fac}, carried this {form} through {ev} and came back thinner every winter.",
                    }; break;
                case "exile":
                    origins = new[]
                    {
                        $"{fig} took this {form} out of {reg} when {fac} cast them out after {ev}.",
                        $"This {form} left {reg} with {fig}, {title} of {fac}, who was never allowed back past {land}.",
                    }; break;
                case "pride":
                    origins = new[]
                    {
                        $"{fig}, {title} of {fac}, commissioned this {form} to outshine everything else at {land}.",
                        $"{fac} paraded this {form} through {reg} after {ev}, and {fig} insisted on carrying it.",
                    }; break;
                case "mercy":
                    origins = new[]
                    {
                        $"{fig} of {fac} used this {form} to tend the sick of {reg} during {ev}.",
                        $"When {ev} reached {reg}, {fig}, {title}, opened {land} to everyone and set this {form} at the door.",
                    }; break;
                case "debt":
                    origins = new[]
                    {
                        $"{fac} took this {form} from {reg} as a tithe {years} years ago, and {fig} entered it in the ledger in red.",
                        $"{fig}, {title} of {fac}, pawned this {form} to cover a debt that came due during {ev}.",
                    }; break;
                case "homecoming":
                    origins = new[]
                    {
                        $"{fig} carried this {form} home to {reg} after {ev}, the only one of {fac} to come back.",
                        $"This {form} stood in a window at {land} so that {fig} could find the way home from {ev}.",
                    }; break;
                default: // vigil
                    origins = new[]
                    {
                        $"For {years} years this {form} hung at {land} in {reg}, and {fig} of {fac} kept watch over it through {ev}.",
                        $"{fig}, {title} of {fac}, stood watch at {land} with this {form} until {ev}.",
                    }; break;
            }

            string detail;
            switch (motif)
            {
                case "tide": detail = $"Its {material} still sweats brine, and whatever it strikes comes away soaked."; break;
                case "ember": detail = $"The {material} never quite cools, and it smells of kiln smoke and tallow."; break;
                case "frost": detail = $"Rime gathers on the {material} even at midsummer."; break;
                case "thorn": detail = $"Briar has grown through the {material}, and the thorns are still sharp."; break;
                case "storm": detail = $"The {material} hums before a squall, and the air near it tastes of copper."; break;
                case "venom": detail = $"The {material} is stained green where something was kept in it."; break;
                case "stone": detail = $"It is heavier than it looks, as if the {material} remembers being a quarry wall."; break;
                case "bell": detail = "When it is moved it rings once, and a little too long."; break;
                case "ink": detail = $"Tiny handwriting covers the {material}, in a cipher no one in {fac} will read aloud."; break;
                case "blood": detail = $"The {material} is dark where hands held it, and it is always warm."; break;
                case "salt": detail = $"A crust of salt reforms on the {material} overnight, however often it is wiped."; break;
                default: detail = "Moths gather around it at dusk, even when it is dark."; break;
            }

            string fate = $"{fig} {r.Figure.Fate}.";
            string[] leads =
            {
                $"{r.LeadFigure.Name} is said to keep its twin somewhere in {r.LeadRegion.Name}.",
                $"Its maker's mark matches the stones of {r.LeadRegion.Landmark} in {r.LeadRegion.Name}.",
                $"{r.LeadFaction.Name} still pay for news of where it went.",
                $"A matching {form} is recorded in the books of {r.LeadFaction.Name}, last seen in {r.LeadRegion.Name}.",
            };
            int leadIdx = rng.Range(0, leads.Length);

            var sentences = new List<string> { origins[rng.Range(0, origins.Length)], detail };
            bool withFate = rng.Chance(0.5f) && !sentences[0].Contains(r.Figure.Fate);
            if (withFate) sentences.Add(fate);
            sentences.Add(leads[leadIdx]);
            string text = string.Join(" ", sentences.ConvertAll(Cap));

            // Referenced ids: whatever names actually appear in the text.
            void Ref(string id, string name)
            {
                if (name != null && text.IndexOf(DraftValidator.StripArticle(name), StringComparison.OrdinalIgnoreCase) >= 0 && !found.Contains(id)) found.Add(id);
            }
            Ref(r.Figure.Id, r.Figure.Name);
            Ref(r.Faction.Id, r.Faction.Name);
            Ref(r.Region.Id, r.Region.Name);
            Ref(r.Event.Id, r.Event.Name);
            Ref(r.LeadFigure.Id, r.LeadFigure.Name);
            Ref(r.LeadRegion.Id, r.LeadRegion.Name);
            Ref(r.LeadFaction.Id, r.LeadFaction.Name);
            if (found.Count > 6) found.RemoveRange(6, found.Count - 6);
            return text;
        }

        // ---------- visual ----------

        static ItemVisual Visual(RelicBrief b, ref Pcg32 rng)
        {
            var form = Forms.FormById(b.Form) ?? Forms.RelicForms[0];
            var parts = new List<string>(form.Parts);
            int keep = Math.Min(parts.Count, 3 + rng.Range(0, 2));
            while (parts.Count > keep) parts.RemoveAt(rng.Range(1, parts.Count)); // keep the defining first part
            var motif = b.Theme?.Motif;
            string mood = b.Theme?.Mood.Id;
            float wear = 0.15f + 0.6f * rng.NextFloat() + (mood == "grief" || mood == "exile" || mood == "hunger" ? 0.2f : 0f);
            return new ItemVisual
            {
                KitParts = parts,
                Material = motif != null ? motif.Materials[rng.Range(0, motif.Materials.Length)] : "iron",
                Wear = (float)Math.Round(Math.Min(1f, wear), 2),
                EmissiveColor = motif?.Color ?? "#FFFFFF",
                Decal = Forms.DecalFor(motif?.Id),
                EmissiveIntensity = (float)Math.Round(Math.Min(4f, 0.5f + 0.3f * b.Tier), 2),
            };
        }
    }

    /// <summary>
    /// ITextGenerator that writes TemplateAuthor drafts as JSON, so TEMPLATE MODE
    /// runs through exactly the same parse → validate → fit path as an LLM.
    /// Requires TextRequest.Context to be the RelicBrief.
    /// </summary>
    public sealed class TemplateGenerator : ITextGenerator
    {
        public string Name => TemplateAuthor.Source;

        public Task<TextResult> GenerateAsync(TextRequest request, CancellationToken cancellationToken = default)
        {
            var brief = request.Context as RelicBrief;
            if (brief == null) throw new TextGenerationException("TemplateGenerator needs a RelicBrief in TextRequest.Context");
            var draft = TemplateAuthor.Compose(brief);
            return Task.FromResult(new TextResult { Text = ItemJson.Serialize(draft), Source = Name });
        }
    }
}
