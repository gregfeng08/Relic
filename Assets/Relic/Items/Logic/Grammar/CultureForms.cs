using System;
using System.Collections.Generic;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Maps a culture's item forms ("carved longbow", "hand-bell") onto Items base types, and
    /// relic forms onto kitbash forms (Forms.RelicForms), using the form tags the lore carries
    /// (Lore/Data/forms.json, copied onto each generated culture as <see cref="CultureDef.FormTags"/>).
    /// A culture's form is the noun its items are named with; when a culture makes nothing for
    /// a base, the archetype's plain noun is used instead ("Sabre"). A test lists any tag Items can't read.
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

        /// <summary>The tag as an entry, or null when its base isn't a BaseType or its kit isn't a relic form.</summary>
        public static Entry Read(FormTag tag)
        {
            if (tag == null || string.IsNullOrEmpty(tag.Form) || !Enum.TryParse(tag.Base, out BaseType b) || !Enum.IsDefined(typeof(BaseType), b)) return null;
            if (b == BaseType.Relic && Forms.FormById(tag.Kit) == null) return null;
            return new Entry(tag.Form, b, b == BaseType.Relic ? tag.Kit : null);
        }

        /// <summary>The culture's forms that make this base, in data order.</summary>
        public static List<Entry> For(CultureDef culture, BaseType b)
        {
            var list = new List<Entry>();
            if (culture == null) return list;
            foreach (var form in culture.ItemForms)
                foreach (var tag in culture.FormTags)
                {
                    if (!string.Equals(tag.Form, form, StringComparison.OrdinalIgnoreCase)) continue;
                    var e = Read(tag);
                    if (e != null && e.Base == b) list.Add(e);
                }
            return list;
        }

        /// <summary>True when the culture makes at least one form of this base.</summary>
        public static bool Makes(CultureDef culture, BaseType b) => For(culture, b).Count > 0;

        /// <summary>Form tags (in the constitution and in any generated cultures) that Items can't read, for the data test.</summary>
        public static List<string> Unmapped(LoreBible bible)
        {
            var missing = new List<string>();
            foreach (var t in bible.Forms)
                if (Read(t) == null) missing.Add("form tag: " + t.Form + " (" + t.Base + "/" + t.Kit + ")");
            foreach (var c in bible.Cultures)
                foreach (var f in c.ItemForms)
                    if (c.FormTags.Find(x => string.Equals(x.Form, f, StringComparison.OrdinalIgnoreCase) && Read(x) != null) == null) missing.Add(c.Id + ": " + f);
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
