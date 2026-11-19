using System;
using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Random;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Seeded stand-in for the world graph, instantiated from the world's generated lore
    /// (16, decision 2026-11-18): eight regions settled by its procedural cultures and named
    /// after their prehistory places, five factions from faction archetypes, ten figures, and
    /// the prehistory's events (eras, founders and artifacts are referenceable as legends and
    /// eras). Vocabulary is bright by default; somber events appear at the templates' weights.
    /// </summary>
    public static class StubWorld
    {
        public const int Version = 3;
        const int RegionCount = 8, FactionCount = 5, FigureCount = 10, EventCount = 6;

        static readonly string[] Features = { "orchards", "ferries", "kilns", "markets", "shrines", "quarries", "lamp-towers", "ruins", "bridges", "mills" };
        static readonly string[] LandmarkAdj = { "Old", "Leaning", "Seven-Arch", "Lamplit", "Mossy", "Painted", "Upper", "Little", "Long", "Singing", "Crooked", "Blue" };
        static readonly string[] LandmarkNoun = { "Lamp-Tower", "Bell-House", "Ferry Steps", "Kiln Yard", "Milestone", "Waycairn", "Market Bridge", "Orchard Gate", "Mill", "Archive", "Weir", "Quay", "Terrace", "Hearthstone" };
        static readonly string[] Titles =
        {
            "Lamplighter", "Bellfounder", "Cartographer", "Glassblower", "Beekeeper", "Ferry-captain", "Archivist",
            "Weaver", "Brewer", "Shepherd", "Kitemaker", "Potter", "Wayfarer", "Herbalist", "Shipwright",
        };

        /// <summary>A world from the default lore constitution (<see cref="LoreSource.Default"/>), instantiated for the seed.</summary>
        public static WorldContext Create(ulong worldSeed) => FromBible(worldSeed, LoreSource.Default);

        /// <param name="bible">The constitution (instantiated here for the seed) or an already generated world bible.</param>
        public static WorldContext FromBible(ulong worldSeed, LoreBible bible)
        {
            if (bible == null) throw new ArgumentNullException(nameof(bible));
            if (!bible.IsWorld) bible = bible.ForWorld(worldSeed);
            var history = bible.History;
            var rng = Pcg32.ForPurpose(worldSeed, "items.stubworld.v3");
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
                // Regions take the names of their culture's prehistory places first, so old events point at real regions.
                string name = null;
                foreach (var p in history.PlacesOf(culture.Id))
                    if (usedNames.Add(p.Name)) { name = p.Name; break; }
                int guard = 0;
                if (name == null)
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

            AddEvents(w, history, ref rng);
            foreach (var f in w.Figures) f.Fate = Fate(w, ref rng);
            return w;
        }

        /// <summary>The prehistory's events, each placed in the region it names (or one of its culture's regions).</summary>
        static void AddEvents(WorldContext w, WorldPrehistory history, ref Pcg32 rng)
        {
            var usedIds = new HashSet<string>();
            foreach (var e in history.Events)
            {
                var region = w.Regions.Find(r => r.Name == e.Place);
                if (region == null)
                {
                    var home = w.Regions.FindAll(r => r.Culture == e.Culture);
                    region = home.Count > 0 ? home[rng.Range(0, home.Count)] : w.Regions[rng.Range(0, w.Regions.Count)];
                }
                string id = "evt." + Slug(e.Name);
                if (!usedIds.Add(id)) continue;
                w.Events.Add(new WorldEvent
                {
                    Id = id,
                    Name = e.Name,
                    RegionId = region.Id,
                    YearsAgo = e.YearsAgo,
                    Template = e.Template,
                    Tone = e.Tone.ToString(),
                    Era = e.Era,
                });
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
                case 1: return "still walks the old roads, last seen near " + region.Name;
                case 2: return "retired to " + region.Name + " and judges the pie contest every autumn";
                case 3: return "went looking for the edge of the map past " + region.Name;
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
