using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Items
{
    /// <summary>
    /// Physical vocabulary: relic forms and their kitbash parts, decals, and the
    /// table names/materials for ordinary drops. The kit-part and decal lists are
    /// the closed vocabulary an LLM may use for a relic's visual (anything else is
    /// rejected), so the future kitbash renderer only needs these parts.
    /// </summary>
    public static class Forms
    {
        public sealed class RelicForm
        {
            public string Id;
            public string[] Parts;
            /// <summary>What it can be made of (17: grounded materials). Empty = any solid material.</summary>
            public MaterialKind[] Kinds = new MaterialKind[0];
            public RelicForm(string id, params string[] parts) { Id = id; Parts = parts; }
            public RelicForm Of(params MaterialKind[] kinds) { Kinds = kinds; return this; }
        }

        const MaterialKind Metal = MaterialKind.Metal, Glass = MaterialKind.Glass, Wood = MaterialKind.Wood, Stone = MaterialKind.Stone,
            Bone = MaterialKind.Bone, Clay = MaterialKind.Clay, Gem = MaterialKind.Gem, Paper = MaterialKind.Paper, Leather = MaterialKind.Leather;

        public static readonly RelicForm[] RelicForms =
        {
            new RelicForm("lantern", "cage", "wick", "lid", "handle", "ring", "lens").Of(Metal, Glass, Paper, Wood),
            new RelicForm("bell", "bell-mouth", "clapper", "handle", "band", "rim").Of(Metal),
            new RelicForm("censer", "chamber", "chain", "lid", "flue", "base").Of(Metal, Clay),
            new RelicForm("astrolabe", "dial", "ring", "needle", "rim", "plate").Of(Metal),
            new RelicForm("reliquary", "lid", "clasp", "chamber", "base", "plate").Of(Metal, Wood, Stone),
            new RelicForm("mask", "plate", "band", "horn", "gem", "tassel").Of(Wood, Clay, Metal, Bone),
            new RelicForm("crown", "band", "spike", "gem", "ring").Of(Metal),
            new RelicForm("compass", "dial", "needle", "lid", "rim", "chain").Of(Metal, Glass),
            new RelicForm("figurine", "base", "head", "gem", "horn", "antler").Of(Wood, Stone, Clay, Bone),
            new RelicForm("chalice", "rim", "base", "chamber", "gem", "band").Of(Metal, Clay, Glass),
            new RelicForm("hourglass", "chamber", "frame", "base", "rim").Of(Glass, Wood, Metal),
            new RelicForm("key", "key-bit", "ring", "band", "gem").Of(Metal),
            new RelicForm("seal-ring", "ring", "seal-face", "gem", "band").Of(Metal, Gem),
            new RelicForm("horn", "horn", "band", "chain", "rim").Of(Bone, Metal, Wood),
            new RelicForm("mirror", "mirror-glass", "frame", "handle", "rim").Of(Metal, Glass),
            new RelicForm("spindle", "spindle", "wheel", "needle", "base").Of(Wood, Bone),
            new RelicForm("ledger", "cover", "clasp", "chain", "binding").Of(Paper, Leather),
            new RelicForm("lodestone", "shard", "cage", "chain", "band").Of(Stone, Gem),
            new RelicForm("music-box", "lid", "chamber", "wheel", "key-bit", "base").Of(Wood, Metal),
            // forms the Lore Bible's cultures make (CultureForms maps their words onto these)
            new RelicForm("flute", "tube", "band", "ring", "tassel").Of(Wood, Bone),
            new RelicForm("charm", "cord", "gem", "band", "ring").Of(Glass, Gem, Stone, Bone, Wood, Metal),
            new RelicForm("shield", "plate", "rim", "band", "strap", "boss").Of(Wood, Metal),
            new RelicForm("orb", "orb", "cage", "base", "band").Of(Glass, Gem, Stone),
            new RelicForm("flask", "chamber", "lid", "band", "cord").Of(Clay, Glass, Metal),
            new RelicForm("spyglass", "tube", "lens", "band", "cap").Of(Metal, Wood),
            new RelicForm("scroll-case", "tube", "cap", "band", "cord").Of(Wood, Metal, Leather),
        };

        /// <summary>Every kit part the renderer will know. Closed list.</summary>
        public static readonly string[] KitParts = BuildKitParts();

        public static readonly string[] Decals =
        {
            "none", "cracks", "runes", "tally-marks", "salt-stain", "scorch", "barnacles", "ink-blots",
            "moss", "rust-bloom", "gilt-filigree", "verdigris", "wax-drips", "frost-rime", "thorn-scratches", "wind-ribbons",
            "petals", "honeycomb", "star-chart", "song-notes", "sun-rays", "painted-flowers", "makers-mark",
        };

        static string[] BuildKitParts()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal)
            {
                // weapon and armor parts
                "blade", "crossguard", "grip", "pommel", "limb", "string", "haft", "head", "orb", "cover",
                "binding", "plate", "strap", "buckle", "pauldron", "chain", "ring", "gem", "band",
            };
            foreach (var f in RelicForms) foreach (var p in f.Parts) set.Add(p);
            return new List<string>(set).ToArray();
        }

        public static RelicForm FormById(string id) => Array.Find(RelicForms, f => f.Id == id);

        public static string PickRelicForm(ulong seed)
        {
            var rng = Pcg32.ForPurpose(seed, "relic.form");
            return RelicForms[rng.Range(0, RelicForms.Length)].Id;
        }

        public static string DecalFor(string motif) => MotifTraits.Has(motif) ? MotifTraits.For(motif).Decal : "none";

        // ---------- table names for ordinary drops ----------

        static readonly string[][] Metals = { new[] { "Iron" }, new[] { "Steel", "Marsh-Iron" }, new[] { "Kiln-Steel", "Tempered Steel" }, new[] { "Blued Steel", "Bell-Steel" }, new[] { "Star-Iron", "Weir-Steel" } };
        static readonly string[][] Woods = { new[] { "Ash" }, new[] { "Yew", "Elm" }, new[] { "Bog-Oak", "Hornbeam" }, new[] { "Ironwood", "Black Yew" }, new[] { "Heartwood", "Salt-Cured Yew" } };
        static readonly string[][] Hides = { new[] { "Wool" }, new[] { "Boiled Leather", "Quilted" }, new[] { "Oxhide", "Studded" }, new[] { "Sealskin", "Lamellar" }, new[] { "Elkhide", "Riveted" } };
        static readonly string[][] Bindings = { new[] { "Calfskin" }, new[] { "Vellum", "Ledger-Bound" }, new[] { "Gilt Vellum", "Clasped" }, new[] { "Ironbound", "Chained" }, new[] { "Saltbound", "Lead-Sealed" } };

        public static string BaseNoun(BaseType t, ref Pcg32 rng)
        {
            string[] options;
            switch (t)
            {
                case BaseType.Sword: options = new[] { "Sword", "Arming Sword", "Sabre" }; break;
                case BaseType.Greatsword: options = new[] { "Greatsword", "Cleaver", "Two-Hander" }; break;
                case BaseType.Bow: options = new[] { "Longbow", "Recurve", "Hunting Bow" }; break;
                case BaseType.Staff: options = new[] { "Staff", "Crook", "Rod" }; break;
                case BaseType.Tome: options = new[] { "Tome", "Codex", "Daybook" }; break;
                case BaseType.LightArmor: options = new[] { "Jerkin", "Gambeson", "Coat" }; break;
                case BaseType.MediumArmor: options = new[] { "Brigandine", "Scale Coat", "Hauberk" }; break;
                case BaseType.HeavyArmor: options = new[] { "Plate Harness", "Cuirass", "Full Plate" }; break;
                case BaseType.Trinket: options = new[] { "Charm", "Brooch", "Signet", "Token" }; break;
                default: options = new[] { "Relic" }; break;
            }
            return options[rng.Range(0, options.Length)];
        }

        public static string MaterialWord(BaseType t, int tier, ref Pcg32 rng)
        {
            int band = Math.Max(0, Math.Min(4, tier / 2)); // tiers 0-1, 2-3, 4-5, 6-7, 8-10
            string[][] table;
            switch (t)
            {
                case BaseType.Bow:
                case BaseType.Staff: table = Woods; break;
                case BaseType.LightArmor: table = Hides; break;
                case BaseType.Tome: table = Bindings; break;
                case BaseType.Trinket: table = Metals; break;
                default: table = Metals; break;
            }
            var row = table[Math.Min(table.Length - 1, band)];
            return row[rng.Range(0, row.Length)];
        }

        static readonly string[] CommonQuality = { "Worn", "Plain", "Dented", "Serviceable" };
        static readonly string[] UncommonQuality = { "Fine", "Sturdy", "Balanced", "Well-Kept" };

        /// <summary>"Worn Iron Sword": table names for Common and Uncommon drops.</summary>
        public static string TableName(BaseType t, Rarity r, int tier, ulong seed)
        {
            var rng = Pcg32.ForPurpose(seed, "item.tablename");
            var q = r == Rarity.Common ? CommonQuality : UncommonQuality;
            return q[rng.Range(0, q.Length)] + " " + MaterialWord(t, tier, ref rng) + " " + BaseNoun(t, ref rng);
        }

        /// <summary>Kit parts for an ordinary (non-relic) item.</summary>
        public static List<string> TableKit(BaseType t)
        {
            switch (t)
            {
                case BaseType.Sword: return new List<string> { "blade", "crossguard", "grip", "pommel" };
                case BaseType.Greatsword: return new List<string> { "blade", "crossguard", "grip", "pommel", "band" };
                case BaseType.Bow: return new List<string> { "limb", "string", "grip" };
                case BaseType.Staff: return new List<string> { "haft", "head", "orb" };
                case BaseType.Tome: return new List<string> { "cover", "binding", "clasp" };
                case BaseType.LightArmor: return new List<string> { "plate", "strap", "buckle" };
                case BaseType.MediumArmor: return new List<string> { "plate", "strap", "buckle", "chain" };
                case BaseType.HeavyArmor: return new List<string> { "plate", "pauldron", "strap", "buckle" };
                case BaseType.Trinket: return new List<string> { "ring", "gem", "band" };
                default: return new List<string> { "shard" };
            }
        }
    }
}
