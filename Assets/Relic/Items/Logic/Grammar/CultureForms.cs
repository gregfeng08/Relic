using System;
using System.Collections.Generic;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Maps the Lore Bible's culture item forms ("carved longbow", "hand-bell") onto Items
    /// base types, and relic forms onto kitbash forms (Forms.RelicForms). A culture's form
    /// is the noun its items are named with; when a culture makes nothing for a base, the
    /// archetype's plain noun is used instead ("Sabre"). A test lists any Bible form missing here.
    /// </summary>
    public static class CultureForms
    {
        public sealed class Entry
        {
            public string Form;
            public BaseType Base;
            /// <summary>Kit form id for relics (Forms.RelicForms); null for other bases.</summary>
            public string Kit;
            public Entry(string form, BaseType b, string kit = null) { Form = form; Base = b; Kit = kit; }
        }

        static Entry E(string f, BaseType b, string kit = null) => new Entry(f, b, kit);

        public static readonly Entry[] All =
        {
            // aruvi
            E("carved longbow", BaseType.Bow), E("lacquered staff", BaseType.Staff), E("moth-silk cloak", BaseType.LightArmor),
            E("seed-pouch charm", BaseType.Trinket), E("seed-pouch charm", BaseType.Relic, "charm"), E("bark-bound tome", BaseType.Tome),
            E("antler knife", BaseType.Sword), E("spirit mask", BaseType.Relic, "mask"), E("wooden flute", BaseType.Relic, "flute"),
            // eskeln
            E("bearded axe", BaseType.Greatsword), E("antler-hilted knife", BaseType.Sword), E("sledge-spear", BaseType.Greatsword),
            E("whalebone bow", BaseType.Bow), E("fur cloak", BaseType.LightArmor), E("horn", BaseType.Relic, "horn"),
            E("saga-staff", BaseType.Staff), E("round shield", BaseType.Relic, "shield"),
            // lunnet
            E("punt-pole staff", BaseType.Staff), E("fishing spear", BaseType.Greatsword), E("reed shield", BaseType.Relic, "shield"),
            E("gourd lantern", BaseType.Relic, "lantern"), E("herb satchel", BaseType.Trinket), E("trident", BaseType.Greatsword),
            E("eel-skin boots", BaseType.LightArmor), E("rush-paper scroll", BaseType.Tome),
            // ochrel
            E("curved glaive", BaseType.Greatsword), E("sling", BaseType.Bow), E("glass orb", BaseType.Relic, "orb"),
            E("star-chart tome", BaseType.Tome), E("sun-mirror shield", BaseType.Relic, "shield"), E("clay flask", BaseType.Relic, "flask"),
            E("desert veil", BaseType.LightArmor), E("copper bracer", BaseType.Trinket),
            // solanne
            E("cutlass", BaseType.Sword), E("harpoon", BaseType.Greatsword), E("spyglass", BaseType.Relic, "spyglass"),
            E("compass", BaseType.Relic, "compass"), E("lighthouse lantern", BaseType.Relic, "lantern"), E("sailcloth coat", BaseType.LightArmor),
            E("sea-glass charm", BaseType.Trinket), E("sea-glass charm", BaseType.Relic, "charm"), E("oar-staff", BaseType.Staff),
            // teskari
            E("bell", BaseType.Relic, "bell"), E("kite-blade", BaseType.Sword), E("cedar staff", BaseType.Staff),
            E("porcelain mask", BaseType.Relic, "mask"), E("climbing pick", BaseType.Sword), E("war-fan", BaseType.Trinket),
            E("lacquered lamellar", BaseType.MediumArmor), E("scroll case", BaseType.Relic, "scroll-case"),
            // wendle
            E("lantern", BaseType.Relic, "lantern"), E("walking staff", BaseType.Staff), E("shepherd's crook", BaseType.Staff),
            E("sickle", BaseType.Sword), E("cleaver", BaseType.Sword), E("wool cloak", BaseType.LightArmor),
            E("quilted coat", BaseType.LightArmor), E("pie-tin buckler", BaseType.Relic, "shield"), E("hand-bell", BaseType.Relic, "bell"),
            E("longbow", BaseType.Bow),
        };

        /// <summary>The culture's forms that make this base, in data order.</summary>
        public static List<Entry> For(CultureDef culture, BaseType b)
        {
            var list = new List<Entry>();
            if (culture == null) return list;
            foreach (var form in culture.ItemForms)
                foreach (var e in All)
                    if (e.Base == b && string.Equals(e.Form, form, StringComparison.OrdinalIgnoreCase)) list.Add(e);
            return list;
        }

        /// <summary>True when the culture makes at least one form of this base.</summary>
        public static bool Makes(CultureDef culture, BaseType b) => For(culture, b).Count > 0;

        /// <summary>Bible forms with no entry here (for the data test and the report).</summary>
        public static List<string> Unmapped(LoreBible bible)
        {
            var missing = new List<string>();
            foreach (var c in bible.Cultures)
                foreach (var f in c.ItemForms)
                    if (Array.Find(All, e => string.Equals(e.Form, f, StringComparison.OrdinalIgnoreCase)) == null) missing.Add(c.Id + ": " + f);
            return missing;
        }

        /// <summary>The material a form names in its own words ("whalebone bow", "porcelain mask"), or null. Whole words only ("cloak" is not oak).</summary>
        public static MaterialDef NamedMaterial(string form)
        {
            if (string.IsNullOrEmpty(form)) return null;
            MaterialDef best = null;
            foreach (var m in Materials.All)
            {
                if (m.Kind == MaterialKind.Accent || (best != null && m.Name.Length <= best.Name.Length)) continue;
                int i = form.IndexOf(m.Name, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                int end = i + m.Name.Length;
                bool startOk = i == 0 || !char.IsLetter(form[i - 1]);
                bool endOk = end == form.Length || !char.IsLetter(form[end]);
                if (startOk && endOk) best = m;
            }
            return best;
        }
    }
}
