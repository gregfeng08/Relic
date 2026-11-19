using System;
using System.Collections.Generic;
using Relic.Core.Random;

namespace Relic.Lore
{
    /// <summary>
    /// Grows a world's peoples from the culture templates (16, decision 2026-11-18): 5–8
    /// cultures per world seed, each a <see cref="CultureDef"/> in the same shape Items has
    /// always consumed. A culture takes one phonology family (a seeded subset of its
    /// syllables, so two cultures of one family sound related but not identical), the
    /// template's region families and climates, and a seeded selection of its crafts,
    /// materials (always at least one metal), foods, festivals, values, factions, motifs,
    /// item forms (at least two relic forms, with their tags) and naming. Every region family
    /// is claimed by at least one culture. Pure and deterministic: same rules and seed, same cultures.
    /// </summary>
    public static class CultureGenerator
    {
        public const int MinCultures = 5, MaxCultures = 8;
        public const string Purpose = "lore.cultures.v1";

        public static List<CultureDef> Generate(LoreBible rules, ulong worldSeed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (rules.CultureTemplates.Count < MinCultures) throw new LoreDataException("need at least " + MinCultures + " culture templates");
            var rng = Pcg32.ForPurpose(worldSeed, Purpose);
            int count = Math.Min(rng.Range(MinCultures, MaxCultures + 1), rules.CultureTemplates.Count);

            var pool = new List<CultureTemplate>(rules.CultureTemplates);
            var cultures = new List<CultureDef>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                var t = TakeWeighted(pool, ref rng);
                cultures.Add(Build(rules, t, usedNames, ref rng));
            }
            CoverFamilies(cultures, rules, ref rng);
            AddRelations(cultures, ref rng);
            return cultures;
        }

        static CultureTemplate TakeWeighted(List<CultureTemplate> pool, ref Pcg32 rng)
        {
            int total = 0;
            foreach (var t in pool) total += Math.Max(1, t.Weight);
            int r = rng.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                r -= Math.Max(1, pool[i].Weight);
                if (r < 0) { var t = pool[i]; pool.RemoveAt(i); return t; }
            }
            var last = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return last;
        }

        static CultureDef Build(LoreBible rules, CultureTemplate t, HashSet<string> usedNames, ref Pcg32 rng)
        {
            var fam = rules.Phonology(t.Phonologies[rng.Range(0, t.Phonologies.Count)]);
            var c = new CultureDef { Template = t.Id, PhonologyFamily = fam.Id, Phonology = Subset(fam.Phonology, ref rng) };

            // Name: a short root in the culture's own sounds; the adjective adds the family's ending.
            var names = new NameGenerator(c, rules.Style);
            string root = null, adjective = null;
            for (int k = 0; k < 64; k++)
            {
                string r = names.Word(ref rng, rng.Chance(0.5f) ? "SE" : "S");
                string end = fam.AdjectiveEndings.Count > 0 ? fam.AdjectiveEndings[rng.Range(0, fam.AdjectiveEndings.Count)] : "";
                string adj = NameGenerator.Normalize(NameGenerator.Join(r, end));
                if (r.Length < 3 || !names.IsPlausible(adj) || usedNames.Contains(r) || usedNames.Contains(adj)) continue;
                root = r; adjective = adj;
                break;
            }
            if (root == null) { root = names.Fallback() + usedNames.Count; adjective = root; }
            usedNames.Add(root);
            usedNames.Add(adjective);
            c.Id = root.ToLowerInvariant();
            c.Name = "the " + root;
            c.Adjective = adjective;

            c.Identity = Pick(t.Identities, ref rng);
            c.Architecture = Pick(t.Architecture, ref rng);
            c.Pride = Pick(t.Prides, ref rng);
            // Families: the template's strongest claim (sometimes its second), plus one neighbor.
            int home = t.Families.Count > 1 && rng.Chance(0.2f) ? 1 : 0;
            c.Families.Add(t.Families[home]);
            if (t.Families.Count > 1)
            {
                var others = t.Families.FindAll(f => f != t.Families[home]);
                c.Families.Add(others[rng.Range(0, others.Count)]);
            }
            c.Climates.AddRange(t.Climates);

            c.Crafts = Some(t.Crafts, 4, ref rng);
            c.Materials = Some(t.Materials, 4, ref rng);
            c.Materials.AddRange(Some(t.Metals, rng.Range(1, 3), ref rng));
            c.Foods = Some(t.Foods, 3, ref rng);
            c.Festivals = Some(t.Festivals, 2, ref rng);
            c.Values = Some(t.Values, 3, ref rng);
            c.Factions = Some(t.Factions, 4, ref rng);
            c.Motifs = Some(t.Motifs, 4, ref rng);
            PickForms(rules, t, c, ref rng);

            c.ItemNaming = new ItemNaming
            {
                Patterns = new List<string>(t.ItemNaming.Patterns),
                Epithets = Some(t.ItemNaming.Epithets, 3, ref rng),
                Adjectives = Some(t.ItemNaming.Adjectives, 3, ref rng),
                MakersMarks = Some(t.ItemNaming.MakersMarks, 2, ref rng),
            };

            var lines = Some(t.MoodLines, 2, ref rng);
            var slots = new Dictionary<string, string> { ["people"] = root, ["adjective"] = adjective };
            var mood = new List<string>();
            foreach (var l in lines) mood.Add(TemplateText.Capitalize(TemplateText.Fill(l, slots)));
            c.Mood = string.Join(" ", mood);
            return c;
        }

        /// <summary>About three quarters of the template's forms, always with at least two relic forms.</summary>
        static void PickForms(LoreBible rules, CultureTemplate t, CultureDef c, ref Pcg32 rng)
        {
            var relic = t.ItemForms.FindAll(f => rules.FormTagsOf(f).Exists(x => x.Base == "Relic"));
            var other = t.ItemForms.FindAll(f => !relic.Contains(f));
            var chosen = new HashSet<string>(Some(relic, Math.Max(2, (relic.Count * 3 + 3) / 4), ref rng));
            foreach (var f in Some(other, Math.Max(1, (other.Count * 3 + 3) / 4), ref rng)) chosen.Add(f);
            foreach (var f in t.ItemForms) // template order, so the culture reads the same way the data does
            {
                if (!chosen.Contains(f)) continue;
                c.ItemForms.Add(f);
                foreach (var tag in rules.FormTagsOf(f)) c.FormTags.Add(new FormTag { Form = tag.Form, Base = tag.Base, Kit = tag.Kit });
            }
        }

        /// <summary>Every region family gets at least one culture, preferring cultures whose template likes it.</summary>
        static void CoverFamilies(List<CultureDef> cultures, LoreBible rules, ref Pcg32 rng)
        {
            foreach (LoreFamily f in Enum.GetValues(typeof(LoreFamily)))
            {
                if (cultures.Exists(c => c.Families.Contains(f))) continue;
                var fans = cultures.FindAll(c => rules.CultureTemplate(c.Template).Families.Contains(f));
                var pool = fans.Count > 0 ? fans : cultures;
                int fewest = int.MaxValue;
                foreach (var c in pool) fewest = Math.Min(fewest, c.Families.Count);
                var best = pool.FindAll(c => c.Families.Count == fewest);
                best[rng.Range(0, best.Count)].Families.Add(f);
            }
        }

        static readonly string[] TradeNotes =
        {
            "{our} {craft} for the {their} {food}, and both sides think they got the better deal.",
            "Their {food} comes to market every week; {our} {craft} goes back the other way.",
        };
        static readonly string[] RivalNotes =
        {
            "A long, friendly argument with the {their} about who makes the better {food}.",
            "Every few years a race against the {their}; the loser hosts the feast.",
        };
        static readonly string[] KinNotes =
        {
            "Old friends of the {their}; their festivals share an evening.",
            "Many families have cousins among the {their}, and visit at harvest.",
        };
        static readonly string[] WaryNotes =
        {
            "Polite with the {their}, but they count the change twice.",
        };

        /// <summary>Neighbors (cultures sharing a region family) trade, compete, visit and, now and then, keep a careful distance.</summary>
        static void AddRelations(List<CultureDef> cultures, ref Pcg32 rng)
        {
            for (int i = 0; i < cultures.Count; i++)
                for (int j = i + 1; j < cultures.Count; j++)
                {
                    var a = cultures[i];
                    var b = cultures[j];
                    bool neighbors = a.Families.Exists(f => b.Families.Contains(f));
                    if (!neighbors && !rng.Chance(0.2f)) continue;
                    int roll = rng.Range(0, 9);
                    var kind = roll < 3 ? RelationKind.Trade : roll < 6 ? RelationKind.FriendlyRivalry : roll < 8 ? RelationKind.Kinship : RelationKind.Wary;
                    a.Relations.Add(Relation(a, b, kind, ref rng));
                    b.Relations.Add(Relation(b, a, kind, ref rng));
                }
        }

        static CultureRelation Relation(CultureDef us, CultureDef them, RelationKind kind, ref Pcg32 rng)
        {
            var pool = kind == RelationKind.Trade ? TradeNotes : kind == RelationKind.FriendlyRivalry ? RivalNotes : kind == RelationKind.Kinship ? KinNotes : WaryNotes;
            var values = new Dictionary<string, string>
            {
                ["our"] = us.Adjective,
                ["their"] = them.Adjective,
                ["craft"] = Pick(us.Crafts, ref rng),
                ["food"] = Pick(them.Foods, ref rng),
            };
            return new CultureRelation
            {
                Culture = them.Id,
                Kind = kind,
                Note = TemplateText.Capitalize(TemplateText.Fill(pool[rng.Range(0, pool.Length)], values)),
            };
        }

        /// <summary>
        /// A seeded subset of a family's syllables (about 70%, in data order) so each culture of
        /// a family has its own accent. The first start and end stay valid for the fallback name.
        /// </summary>
        static Phonology Subset(Phonology p, ref Pcg32 rng)
        {
            var s = new Phonology
            {
                Starts = Keep(p.Starts, 6, ref rng),
                Middles = p.Middles.Count > 0 ? Keep(p.Middles, 4, ref rng) : new List<string>(),
                Ends = Keep(p.Ends, 6, ref rng),
                Patterns = new List<SyllablePattern>(p.Patterns),
                SurnameChance = p.SurnameChance,
                SurnamePattern = p.SurnamePattern,
                MinLength = p.MinLength,
                MaxLength = p.MaxLength,
                Places = new PlaceNaming
                {
                    Suffixes = p.Places.Suffixes.Count > 0 ? Keep(p.Places.Suffixes, 3, ref rng) : new List<string>(),
                    Words = p.Places.Words.Count > 0 ? Keep(p.Places.Words, 3, ref rng) : new List<string>(),
                    SuffixChance = p.Places.SuffixChance,
                },
            };
            return s;
        }

        /// <summary>About 70% of the list in data order, always keeping the first entry (the family's fallback syllable).</summary>
        static List<string> Keep(List<string> all, int min, ref Pcg32 rng)
        {
            int n = Math.Min(all.Count, Math.Max(min, (all.Count * 7 + 9) / 10));
            var rest = Some(all.GetRange(1, all.Count - 1), n - 1, ref rng, keepOrder: true);
            rest.Insert(0, all[0]);
            return rest;
        }

        /// <summary>n distinct items (all of them when n ≥ count), in pick order or data order.</summary>
        static List<T> Some<T>(List<T> all, int n, ref Pcg32 rng, bool keepOrder = false)
        {
            var idx = new List<int>();
            for (int i = 0; i < all.Count; i++) idx.Add(i);
            for (int i = idx.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp;
            }
            if (n < idx.Count) idx.RemoveRange(n, idx.Count - n);
            if (keepOrder) idx.Sort();
            var list = new List<T>();
            foreach (int i in idx) list.Add(all[i]);
            return list;
        }

        static T Pick<T>(List<T> list, ref Pcg32 rng) => list[rng.Range(0, list.Count)];
    }
}
