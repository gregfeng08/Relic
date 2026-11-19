using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Relic.Lore;

namespace Relic.Items
{
    // World context handed to item and boss generation. Until the world graph exists
    // (stream W + lead), StubWorld instantiates a small seeded world from the world's
    // generated lore (LoreBible.ForWorld): regions settled by its procedural cultures and
    // named after their prehistory places, factions from faction archetypes, figures, and
    // events from the generated prehistory. The shape is meant to be easy to
    // fill from the real graph later: ids are stable strings, everything serializes.
    //
    // Besides world entities, the prehistory's famous names can be referenced:
    // "legend.<id>" (founders, figures, artifacts) and "era.<id>" (generated eras).

    public sealed class WorldRegion
    {
        public string Id;
        public string Name;
        /// <summary>Region tags, e.g. "coast", "orchards". Usable in InRegionTag conditions.</summary>
        public List<string> Tags = new List<string>();
        public int Tier;
        /// <summary>A named place inside the region, e.g. "the Leaning Lamp-Tower".</summary>
        public string Landmark;
        /// <summary>Region family (Lore.LoreFamily name): Meadow, Forest, Coast...</summary>
        public string Family;
        /// <summary>Lore Bible culture id of the people who live here.</summary>
        public string Culture;
    }

    public sealed class WorldFaction
    {
        public string Id;
        /// <summary>Mid-sentence form ("the Bramwick Map House", "House Teso").</summary>
        public string Name;
        public string Ethos;
        public string SeatRegionId;
        /// <summary>Lore Bible faction archetype id.</summary>
        public string Archetype;
        public string Culture;
    }

    public sealed class WorldFigure
    {
        public string Id;
        public string Name;
        public string Title;
        public string FactionId;
        public string Culture;
        /// <summary>What became of them, as a predicate after the name: "keeps a workshop at the Old Mill in Bramwick".</summary>
        public string Fate;
    }

    public sealed class WorldEvent
    {
        public string Id;
        public string Name;
        public string RegionId;
        public int YearsAgo;
        /// <summary>Lore Bible event template id, its tone and era.</summary>
        public string Template;
        public string Tone;
        public string Era;
    }

    public sealed class WorldContext
    {
        public int Version = 2;
        public ulong Seed;
        public List<WorldRegion> Regions = new List<WorldRegion>();
        public List<WorldFaction> Factions = new List<WorldFaction>();
        public List<WorldFigure> Figures = new List<WorldFigure>();
        public List<WorldEvent> Events = new List<WorldEvent>();

        /// <summary>The world's lore: the constitution instantiated for <see cref="Seed"/> (generated cultures, eras, founders, artifacts). Not serialized; <see cref="Lore"/> regenerates it after loading.</summary>
        [JsonIgnore] public LoreBible Bible;

        /// <summary><see cref="Bible"/>, regenerated from the seed (deterministically) when a loaded world has none.</summary>
        [JsonIgnore] public LoreBible Lore => Bible ?? (Bible = LoreSource.ForWorld(Seed));

        public const string LegendPrefix = "legend.";
        public const string EraPrefix = "era.";

        public WorldRegion Region(string id) => Regions.Find(r => r.Id == id);
        public WorldFaction Faction(string id) => Factions.Find(f => f.Id == id);
        public WorldFigure Figure(string id) => Figures.Find(f => f.Id == id);
        public WorldEvent Event(string id) => Events.Find(e => e.Id == id);

        public LegendDef Legend(string id) =>
            Bible != null && id != null && id.StartsWith(LegendPrefix, StringComparison.Ordinal) ? Bible.Legend(id.Substring(LegendPrefix.Length)) : null;

        public EraDef Era(string id) =>
            Bible != null && id != null && id.StartsWith(EraPrefix, StringComparison.Ordinal) ? Bible.Era(id.Substring(EraPrefix.Length)) : null;

        public static string LegendId(LegendDef l) => LegendPrefix + l.Id;
        public static string EraId(EraDef e) => EraPrefix + e.Id;

        public bool Contains(string id) => NameOf(id) != null;

        /// <summary>Display name of any entity by id (world or Bible), or null.</summary>
        public string NameOf(string id)
        {
            if (id == null) return null;
            var r = Region(id); if (r != null) return r.Name;
            var f = Faction(id); if (f != null) return f.Name;
            var p = Figure(id); if (p != null) return p.Name;
            var e = Event(id); if (e != null) return e.Name;
            var l = Legend(id); if (l != null) return l.Name;
            var era = Era(id); if (era != null) return era.Name;
            return null;
        }

        /// <summary>"region", "faction", "figure", "event", "legend", "era" or null.</summary>
        public string KindOf(string id)
        {
            if (Region(id) != null) return "region";
            if (Faction(id) != null) return "faction";
            if (Figure(id) != null) return "figure";
            if (Event(id) != null) return "event";
            if (Legend(id) != null) return "legend";
            if (Era(id) != null) return "era";
            return null;
        }

        /// <summary>True for places a lead can point at (regions).</summary>
        public bool IsPlace(string id) => Region(id) != null;

        /// <summary>True for the Bible's famous names (legends and eras).</summary>
        public bool IsBibleRef(string id) => Legend(id) != null || Era(id) != null;

        /// <summary>Every id a writer may put in worldRefs: world entities first, then Bible legends and eras.</summary>
        public List<string> AllIds()
        {
            var ids = new List<string>();
            foreach (var r in Regions) ids.Add(r.Id);
            foreach (var f in Factions) ids.Add(f.Id);
            foreach (var p in Figures) ids.Add(p.Id);
            foreach (var e in Events) ids.Add(e.Id);
            if (Bible != null)
            {
                foreach (var l in Bible.Legends) ids.Add(LegendId(l));
                foreach (var e in Bible.Eras) ids.Add(EraId(e));
            }
            return ids;
        }

        /// <summary>The region whose tier is closest (ties: the first).</summary>
        public WorldRegion NearestRegion(int tier)
        {
            WorldRegion best = null;
            foreach (var r in Regions)
                if (best == null || Math.Abs(r.Tier - tier) < Math.Abs(best.Tier - tier)) best = r;
            return best;
        }
    }
}
