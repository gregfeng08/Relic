using System;
using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Random;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Seeded stand-in for the world graph, instantiated from the Lore Bible (16, layer 3):
    /// eight regions settled by Bible cultures (names from each culture's NameGenerator),
    /// five factions from faction archetypes, ten figures and six events from the event
    /// templates. Vocabulary is bright by default; somber events appear at the Bible's weights.
    /// </summary>
    public static class StubWorld
    {
        public const int Version = 2;
        const int RegionCount = 8, FactionCount = 5, FigureCount = 10, EventCount = 6;

        static readonly string[] Features = { "orchards", "ferries", "kilns", "markets", "shrines", "quarries", "lamp-towers", "ruins", "bridges", "mills" };
        static readonly string[] LandmarkAdj = { "Old", "Leaning", "Seven-Arch", "Lamplit", "Mossy", "Painted", "Upper", "Little", "Long", "Singing", "Crooked", "Blue" };
        static readonly string[] LandmarkNoun = { "Lamp-Tower", "Bell-House", "Ferry Steps", "Kiln Yard", "Milestone", "Waycairn", "Market Bridge", "Orchard Gate", "Mill", "Archive", "Weir", "Quay", "Terrace", "Hearthstone" };
        static readonly string[] Titles =
        {
            "Lamplighter", "Bellfounder", "Cartographer", "Glassblower", "Beekeeper", "Ferry-captain", "Archivist",
            "Weaver", "Brewer", "Shepherd", "Kitemaker", "Potter", "Wayfarer", "Herbalist", "Shipwright",
        };

        /// <summary>A world from the default Lore Bible (<see cref="LoreSource.Default"/>).</summary>
        public static WorldContext Create(ulong worldSeed) => FromBible(worldSeed, LoreSource.Default);

        public static WorldContext FromBible(ulong worldSeed, LoreBible bible)
        {
            if (bible == null) throw new ArgumentNullException(nameof(bible));
            var rng = Pcg32.ForPurpose(worldSeed, "items.stubworld.v2");
            var w = new WorldContext { Seed = worldSeed, Bible = bible };
            var names = new Dictionary<string, NameGenerator>();
            NameGenerator Names(CultureDef c) => names.TryGetValue(c.Id, out var n) ? n : names[c.Id] = new NameGenerator(c, bible.Style);

            // Regions: every family once (shuffled), then one repeat; culture = the family's first claim, sometimes a neighbor.
            var families = new List<LoreFamily>((LoreFamily[])Enum.GetValues(typeof(LoreFamily)));
            Shuffle(families, ref rng);
            families.Add(families[rng.Range(0, families.Count)]);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedLandmarks = new HashSet<string>();
            for (int i = 0; i < RegionCount; i++)
            {
                var fam = families[i];
                var cultures = bible.CulturesOf(fam);
                var culture = cultures.Count > 1 && rng.Chance(0.3f) ? cultures[rng.Range(1, cultures.Count)] : cultures[0];
                string name;
                int guard = 0;
                do name = Names(culture).Place(ref rng); while (!usedNames.Add(name) && ++guard < 32);
                string landmark;
                do landmark = "the " + Pick(LandmarkAdj, ref rng) + " " + Pick(LandmarkNoun, ref rng);
                while (!usedLandmarks.Add(landmark));
                w.Regions.Add(new WorldRegion
                {
                    Id = "reg." + Slug(name),
                    Name = name,
                    Tags = new List<string> { fam.ToString().ToLowerInvariant(), Pick(Features, ref rng) },
                    Tier = Math.Min(10, i * 10 / (RegionCount - 1)),
                    Landmark = landmark,
                    Family = fam.ToString(),
                    Culture = culture.Id,
                });
            }

            // Factions: archetypes the region's culture favors, each name filled from its seat.
            var usedArchetypes = new HashSet<string>();
            for (int i = 0; i < FactionCount; i++)
            {
                var seat = w.Regions[rng.Range(0, w.Regions.Count)];
                var culture = bible.Culture(seat.Culture);
                FactionArchetype arch = null;
                for (int k = 0; k < 16 && (arch == null || usedArchetypes.Contains(arch.Id)); k++)
                    arch = bible.Faction(culture.Factions[rng.Range(0, culture.Factions.Count)]) ?? bible.Factions[rng.Range(0, bible.Factions.Count)];
                if (usedArchetypes.Contains(arch.Id)) arch = bible.Factions.Find(f => !usedArchetypes.Contains(f.Id));
                usedArchetypes.Add(arch.Id);
                var motif = bible.Motif(culture.Motifs[rng.Range(0, culture.Motifs.Count)]);
                var values = new Dictionary<string, string>
                {
                    ["place"] = seat.Name,
                    ["founder"] = LoreSampler.FirstWord(Names(culture).Person(ref rng)),
                    ["culture"] = culture.Adjective,
                    ["word"] = LoreSampler.Title(motif.Words[rng.Range(0, motif.Words.Count)]),
                };
                string name = LoreSampler.MidSentence(TemplateText.Fill(arch.NamePatterns[rng.Range(0, arch.NamePatterns.Count)], values));
                w.Factions.Add(new WorldFaction
                {
                    Id = "fac." + Slug(name.StartsWith("the ", StringComparison.Ordinal) ? name.Substring(4) : name),
                    Name = name,
                    Ethos = arch.Label + " who want to " + arch.Goals[rng.Range(0, arch.Goals.Count)],
                    SeatRegionId = seat.Id,
                    Archetype = arch.Id,
                    Culture = culture.Id,
                });
            }

            // Figures: named in their home culture's phonology.
            var usedPeople = new HashSet<string>();
            for (int i = 0; i < FigureCount; i++)
            {
                var home = w.Regions[rng.Range(0, w.Regions.Count)];
                var culture = bible.Culture(home.Culture);
                string name;
                int guard = 0;
                do name = Names(culture).Person(ref rng); while (!usedPeople.Add(LoreSampler.FirstWord(name)) && ++guard < 32);
                w.Figures.Add(new WorldFigure
                {
                    Id = "fig." + Slug(name),
                    Name = name,
                    Title = Pick(Titles, ref rng),
                    FactionId = w.Factions[rng.Range(0, w.Factions.Count)].Id,
                    Culture = culture.Id,
                });
            }

            AddEvents(w, bible, ref rng);
            foreach (var f in w.Figures) f.Fate = Fate(w, ref rng);
            return w;
        }

        static void AddEvents(WorldContext w, LoreBible bible, ref Pcg32 rng)
        {
            var usedIds = new HashSet<string>();
            int total = 0;
            foreach (var t in bible.Events) total += Math.Max(0, t.Weight);
            for (int i = 0; i < EventCount * 4 && w.Events.Count < EventCount; i++)
            {
                int r = rng.Range(0, Math.Max(1, total));
                EventTemplate t = bible.Events[0];
                foreach (var e in bible.Events) { r -= Math.Max(0, e.Weight); if (r < 0) { t = e; break; } }
                if (usedIds.Contains(t.Id)) continue;
                usedIds.Add(t.Id);

                var region = w.Regions[rng.Range(0, w.Regions.Count)];
                var culture = bible.Culture(region.Culture);
                var values = new Dictionary<string, string>();
                var usedFigures = new HashSet<WorldFigure>();
                foreach (var s in t.Slots) values[s.Name] = SlotValue(s, w, bible, region, culture, usedFigures, ref rng);
                string name = TemplateText.Capitalize(TemplateText.Fill(t.NamePatterns[rng.Range(0, t.NamePatterns.Count)], values));
                var era = bible.Era(t.Eras[rng.Range(0, t.Eras.Count)]);
                int lo = Math.Max(5, era.EndYearsAgo + 5), hi = Math.Max(lo, era.StartYearsAgo - 5);
                w.Events.Add(new WorldEvent
                {
                    Id = "evt." + Slug(name),
                    Name = LoreSampler.MidSentence(name),
                    RegionId = region.Id,
                    YearsAgo = 5 * ((lo + rng.Range(0, hi - lo + 1)) / 5),
                    Template = t.Id,
                    Tone = t.Tone.ToString(),
                    Era = era.Id,
                });
            }
        }

        static string SlotValue(TemplateSlot s, WorldContext w, LoreBible bible, WorldRegion region, CultureDef culture, HashSet<WorldFigure> used, ref Pcg32 rng)
        {
            switch (s.Kind)
            {
                case SlotKind.Place:
                case SlotKind.Region: return region.Name;
                case SlotKind.Faction: return w.Factions[rng.Range(0, w.Factions.Count)].Name;
                case SlotKind.Figure:
                {
                    WorldFigure f;
                    int guard = 0;
                    do f = w.Figures[rng.Range(0, w.Figures.Count)]; while (!used.Add(f) && ++guard < 16);
                    return f.Name;
                }
                case SlotKind.Artifact:
                {
                    var artifacts = bible.Legends.FindAll(l => l.Kind == LegendKind.Artifact && l.Tone == Tone.Bright);
                    return artifacts.Count > 0 ? artifacts[rng.Range(0, artifacts.Count)].Name : "a lost lantern";
                }
                case SlotKind.Culture: return culture.Adjective;
                case SlotKind.Era: return bible.Eras[rng.Range(0, bible.Eras.Count)].Name;
                case SlotKind.Legend: return LoreSampler.LegendName(bible.Legends[rng.Range(0, bible.Legends.Count)]);
                case SlotKind.Number: return (5 * rng.Range(1, 19)).ToString(CultureInfo.InvariantCulture);
                default:
                {
                    if (s.Words.Count > 0) return LoreSampler.Title(s.Words[rng.Range(0, s.Words.Count)]);
                    var motif = bible.Motif(culture.Motifs[rng.Range(0, culture.Motifs.Count)]);
                    return LoreSampler.Title(motif.Words[rng.Range(0, motif.Words.Count)]);
                }
            }
        }

        /// <summary>Mostly bright: people keep workshops, walk roads, retire and argue. About a quarter are quietly somber.</summary>
        static string Fate(WorldContext w, ref Pcg32 rng)
        {
            var region = w.Regions[rng.Range(0, w.Regions.Count)];
            if (rng.Chance(0.25f))
            {
                switch (rng.Range(0, 3))
                {
                    case 0: return "did not come home from the road past " + region.Name;
                    case 1: return "left " + region.Name + " after a quarrel and never went back";
                    default:
                    {
                        var named = w.Events.FindAll(e => e.Name.StartsWith("the ", StringComparison.Ordinal));
                        return named.Count > 0 ? "was last seen around the time of " + named[rng.Range(0, named.Count)].Name : "did not come home from the road past " + region.Name;
                    }
                }
            }
            switch (rng.Range(0, 5))
            {
                case 0: return "keeps a workshop at " + region.Landmark + " in " + region.Name;
                case 1: return "still walks the Long Roads, last seen near " + region.Name;
                case 2: return "retired to " + region.Name + " and judges the pie contest every autumn";
                case 3: return "went looking for the edge of the Wide past " + region.Name;
                default: return "teaches apprentices at " + region.Landmark + " in " + region.Name;
            }
        }

        static string Pick(string[] a, ref Pcg32 rng) => a[rng.Range(0, a.Length)];

        static void Shuffle<T>(List<T> list, ref Pcg32 rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        /// <summary>"The Bramwick Map House" → "bramwick-map-house".</summary>
        public static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if ((c == ' ' || c == '-') && sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }
    }
}
