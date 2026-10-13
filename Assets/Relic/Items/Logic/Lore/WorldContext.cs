using System;
using System.Collections.Generic;
using Relic.Core.Random;

namespace Relic.Items
{
    // World context handed to relic generation. Until the world graph exists
    // (stream W + lead), StubWorld builds a small seeded set of fake regions,
    // factions, figures and events. The shape is meant to be easy to fill from
    // the real graph later: ids are stable strings, everything serializes to JSON.

    public sealed class WorldRegion
    {
        public string Id;
        public string Name;
        /// <summary>Region tags, e.g. "coast", "ruins". Usable in InRegionTag conditions.</summary>
        public List<string> Tags = new List<string>();
        public int Tier;
        /// <summary>A named place inside the region, e.g. "the Drowned Weir".</summary>
        public string Landmark;
    }

    public sealed class WorldFaction
    {
        public string Id;
        public string Name;
        public string Ethos;
        public string SeatRegionId;
    }

    public sealed class WorldFigure
    {
        public string Id;
        public string Name;
        public string Title;
        public string FactionId;
        /// <summary>What became of them, as a predicate after the name: "drowned at the Ninth Weir in Brackmere".</summary>
        public string Fate;
    }

    public sealed class WorldEvent
    {
        public string Id;
        public string Name;
        public string RegionId;
        public int YearsAgo;
    }

    public sealed class WorldContext
    {
        public int Version = 1;
        public ulong Seed;
        public List<WorldRegion> Regions = new List<WorldRegion>();
        public List<WorldFaction> Factions = new List<WorldFaction>();
        public List<WorldFigure> Figures = new List<WorldFigure>();
        public List<WorldEvent> Events = new List<WorldEvent>();

        public WorldRegion Region(string id) => Regions.Find(r => r.Id == id);
        public WorldFaction Faction(string id) => Factions.Find(f => f.Id == id);
        public WorldFigure Figure(string id) => Figures.Find(f => f.Id == id);
        public WorldEvent Event(string id) => Events.Find(e => e.Id == id);

        public bool Contains(string id) => NameOf(id) != null;

        /// <summary>Display name of any entity by id, or null.</summary>
        public string NameOf(string id)
        {
            if (id == null) return null;
            var r = Region(id); if (r != null) return r.Name;
            var f = Faction(id); if (f != null) return f.Name;
            var p = Figure(id); if (p != null) return p.Name;
            var e = Event(id); if (e != null) return e.Name;
            return null;
        }

        /// <summary>"region", "faction", "figure", "event" or null.</summary>
        public string KindOf(string id)
        {
            if (Region(id) != null) return "region";
            if (Faction(id) != null) return "faction";
            if (Figure(id) != null) return "figure";
            if (Event(id) != null) return "event";
            return null;
        }
    }

    /// <summary>
    /// Seeded stand-in for the world graph. Vocabulary is deliberately narrow and
    /// concrete (weirs, tithes, kilns, ferries) so generated text sounds like one
    /// place, not generic fantasy.
    /// </summary>
    public static class StubWorld
    {
        public const int Version = 1;

        static readonly string[] Biomes = { "coast", "marsh", "highland", "forest", "steppe", "tundra", "river", "badlands" };
        static readonly string[] Features = { "ruins", "mines", "shrines", "ferries", "orchards", "quarries", "saltpans", "kilns" };

        static readonly string[] RegionRoots =
        {
            "Brack", "Sedge", "Kell", "Orm", "Vesh", "Tarn", "Gall", "Hode", "Wend", "Cairn", "Lath", "Sallow",
            "Rook", "Quill", "Hask", "Dun", "Fenn", "Corve", "Ister", "Whin", "Marl", "Teasel", "Oxley", "Bramm",
        };

        static string[] SuffixesFor(string biome)
        {
            switch (biome)
            {
                case "coast": return new[] { "hythe", "ness", "haven", "strand" };
                case "marsh": return new[] { "mere", "fen", "carr", "sump" };
                case "highland": return new[] { "scar", "fell", "crag", "law" };
                case "forest": return new[] { "wold", "holt", "hurst", "shaw" };
                case "steppe": return new[] { "reach", "march", "lea", "down" };
                case "tundra": return new[] { "rime", "holm", "barrow", "waste" };
                case "river": return new[] { "ford", "wick", "bourne", "lock" };
                default: return new[] { "gulch", "stead", "combe", "scarp" };
            }
        }

        static readonly string[] LandmarkAdj = { "Drowned", "Split", "Ninth", "Hollow", "Tallow", "Leaning", "Sunken", "Burnt", "Salt", "Widow's", "Toll", "Iron", "Low", "Seven-Arch" };
        static readonly string[] LandmarkNoun = { "Weir", "Stair", "Bell-tower", "Ferry", "Kiln", "Archive", "Causeway", "Orchard", "Toll-house", "Cistern", "Watch-fire", "Mill", "Chapterhouse", "Quay" };

        static readonly string[] FactionAdj = { "Salt", "Lantern", "Ninefold", "Tallow", "Bellwright", "Reed", "Ledger", "Thorn", "Ferry", "Kiln", "Weir", "Cinder" };
        static readonly string[] FactionNoun = { "Choir", "Wardens", "Compact", "Synod", "Guild", "Company", "Covenant", "Assize", "Brotherhood", "Sisterhood" };
        static readonly string[] Ethos =
        {
            "keepers of the coastal lamps, paid in oil and secrets",
            "tithe-collectors who count every debt in salt",
            "oathbound ferrymen who never cross without a fare",
            "archivists who hold that nothing written can be forgiven",
            "kiln-priests who fire their dead into roof tiles",
            "bandit-lords who became magistrates and kept the habit",
            "bell-founders who ring a bell for every death they owe",
            "reed-cutters who rule the marsh roads by knowing where they sink",
            "surveyors who sell maps that are wrong on purpose",
            "a mercenary company that is paid in land, not coin",
        };

        static readonly string[] GivenA = { "Is", "Ro", "Ma", "Ve", "To", "An", "Sa", "He", "Ori", "Ty", "Bel", "Cas", "Dov", "Em", "Fen", "Ha", "Ul", "Wy", "Ner", "Al" };
        static readonly string[] GivenB = { "el", "wen", "rik", "a", "enne", "os", "ard", "ith", "ova", "un", "ys", "ett", "omir", "ise" };
        static readonly string[] Surnames = { "Vey", "Marrow", "Quillon", "Hask", "Tallis", "Orme", "Brannock", "Sedley", "Kellan", "Corvane", "Whitlock", "Amsel", "Dunmore", "Pell", "Fairweather", "Ostrey" };
        static readonly string[] Titles =
        {
            "Lamp-warden", "Tithe-reeve", "Ferry-captain", "Archivist", "Kiln-mother", "Marshal", "Bell-founder",
            "Cartographer", "Chorister", "Weir-keeper", "Magistrate", "Quartermaster", "Heir", "Saltmaster",
        };

        static readonly string[] EventNouns =
        {
            "Drowning", "Burning", "Long Winter", "Tithe Riots", "Sundering", "Siege", "Silence",
            "Flood", "Fever", "Lantern Schism", "Bell Strike", "Salt Famine", "Ferry Mutiny", "Night of Ledgers",
        };

        public static WorldContext Create(ulong worldSeed)
        {
            var rng = Pcg32.ForPurpose(worldSeed, "items.stubworld");
            var w = new WorldContext { Seed = worldSeed };

            var roots = Shuffled(RegionRoots, ref rng);
            var usedLandmarks = new HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                string biome = Biomes[rng.Range(0, Biomes.Length)];
                string feature = Features[rng.Range(0, Features.Length)];
                var suffixes = SuffixesFor(biome);
                string name = roots[i] + suffixes[rng.Range(0, suffixes.Length)];
                string landmark;
                do landmark = "the " + Pick(LandmarkAdj, ref rng) + " " + Pick(LandmarkNoun, ref rng);
                while (!usedLandmarks.Add(landmark));
                w.Regions.Add(new WorldRegion
                {
                    Id = "reg." + name.ToLowerInvariant(),
                    Name = name,
                    Tags = new List<string> { biome, feature },
                    Tier = Math.Min(10, i * 10 / 7), // spread 0..10
                    Landmark = landmark,
                });
            }

            var usedFactions = new HashSet<string>();
            var ethos = Shuffled(Ethos, ref rng);
            for (int i = 0; i < 5; i++)
            {
                string name;
                do name = "the " + Pick(FactionAdj, ref rng) + " " + Pick(FactionNoun, ref rng);
                while (!usedFactions.Add(name.Split(' ')[1]));
                w.Factions.Add(new WorldFaction
                {
                    Id = "fac." + name.Substring(4).Replace(' ', '-').ToLowerInvariant(),
                    Name = name,
                    Ethos = ethos[i],
                    SeatRegionId = w.Regions[rng.Range(0, w.Regions.Count)].Id,
                });
            }

            var usedEvents = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                var region = w.Regions[rng.Range(0, w.Regions.Count)];
                string noun;
                do noun = Pick(EventNouns, ref rng);
                while (!usedEvents.Add(noun));
                string name = "the " + noun + " of " + region.Name;
                w.Events.Add(new WorldEvent
                {
                    Id = "evt." + noun.Replace(' ', '-').ToLowerInvariant() + "." + region.Name.ToLowerInvariant(),
                    Name = name,
                    RegionId = region.Id,
                    YearsAgo = 10 + rng.Range(0, 39) * 10,
                });
            }

            var usedPeople = new HashSet<string>();
            var surnames = Shuffled(Surnames, ref rng);
            for (int i = 0; i < 10; i++)
            {
                string given;
                do given = Pick(GivenA, ref rng) + Pick(GivenB, ref rng);
                while (!usedPeople.Add(given));
                string name = given + " " + surnames[i];
                var faction = w.Factions[rng.Range(0, w.Factions.Count)];
                w.Figures.Add(new WorldFigure
                {
                    Id = "fig." + given.ToLowerInvariant() + "-" + surnames[i].ToLowerInvariant(),
                    Name = name,
                    Title = Pick(Titles, ref rng),
                    FactionId = faction.Id,
                    Fate = Fate(w, ref rng),
                });
            }
            return w;
        }

        static string Fate(WorldContext w, ref Pcg32 rng)
        {
            var region = w.Regions[rng.Range(0, w.Regions.Count)];
            var ev = w.Events[rng.Range(0, w.Events.Count)];
            switch (rng.Range(0, 6))
            {
                case 0: return "drowned at " + region.Landmark + " in " + region.Name;
                case 1: return "was exiled to " + region.Name;
                case 2: return "was hanged in " + region.Name + " after " + ev.Name;
                case 3: return "vanished during " + ev.Name;
                case 4: return "is still alive, people say, somewhere in " + region.Name;
                default: return "lies buried under " + region.Landmark + " in " + region.Name;
            }
        }

        static string Pick(string[] a, ref Pcg32 rng) => a[rng.Range(0, a.Length)];

        static string[] Shuffled(string[] src, ref Pcg32 rng)
        {
            var a = (string[])src.Clone();
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var t = a[i]; a[i] = a[j]; a[j] = t;
            }
            return a;
        }
    }
}
