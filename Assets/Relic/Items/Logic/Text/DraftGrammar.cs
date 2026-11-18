using System;
using System.Collections.Generic;

namespace Relic.Items.Text
{
    /// <summary>
    /// Draft checks that come from the item grammar (17) and the style guide: the writer
    /// keeps the sentence's names and facts (form noun, material), invents no proper nouns,
    /// keeps sentences short, and a Legendary relic points at a place and a Bible name.
    /// Every message is phrased to be fed back to the model verbatim.
    /// </summary>
    public static class DraftGrammar
    {
        public static void Check(RelicDraft d, RelicBrief brief, List<string> errors)
        {
            var s = brief.Sentence;
            if (d == null || s == null) return;

            if (!string.IsNullOrWhiteSpace(d.Name) && d.Name.IndexOf(s.HeadNoun, StringComparison.OrdinalIgnoreCase) < 0)
                errors.Add($"name must keep the item's form noun \"{LoreTitle(s.HeadNoun)}\" (it is a {s.Form})");

            var allowed = LoreRefs.AllowedProperNouns(brief.World, s);
            CheckProse("flavor", d.Flavor, allowed, errors);
            CheckProse("lore", d.Lore, allowed, errors);

            if (brief.Kind == DraftKind.Legendary && d.WorldRefs != null && brief.World != null)
            {
                var w = brief.World;
                bool place = false, bible = false, lead = false;
                foreach (var id in d.WorldRefs)
                {
                    if (w.IsPlace(id))
                    {
                        place = true;
                        if (id != s.RegionId && id != s.MakerHomeId) lead = true;
                    }
                    if (w.IsBibleRef(id)) bible = true;
                }
                if (!place) errors.Add("a Legendary relic's lore must name a place from the world context and list its id in worldRefs");
                if (!bible) errors.Add("a Legendary relic's lore must name a legend or an era from the Lore Bible (ids starting legend. or era.) and list it in worldRefs");
                if (!lead) errors.Add("end the lore with a lead: name a place other than where it was made or found, and list its id in worldRefs");
            }

            if (d.Visual != null && !string.IsNullOrEmpty(s.Material) && !string.Equals(d.Visual.Material, s.Material, StringComparison.OrdinalIgnoreCase))
                errors.Add($"visual.material must be the item's material, \"{s.Material}\"");
        }

        static void CheckProse(string field, string text, HashSet<string> allowed, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var n in StyleGuide.LongSentences(text))
                errors.Add($"{field} has a sentence of {n} words; keep every sentence to {StyleGuide.MaxSentenceWords} words or fewer");
            foreach (var w in LoreRefs.Invented(text, allowed))
                errors.Add($"{field} uses the proper noun \"{w}\", which is not in the world context; use only the names you are given");
        }

        static string LoreTitle(string s) => Relic.Lore.LoreSampler.Title(s);
    }
}
