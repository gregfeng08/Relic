using System.Collections.Generic;
using System.Text;

namespace Relic.Lore
{
    /// <summary>
    /// Builds docs/lore/samples.md: one generated world's cultures and prehistory, plus the
    /// names, factions, relics and a festival its templates produce, for Greg's tone review.
    /// </summary>
    public static class LoreSamples
    {
        public const string TemplateBanner = "TEMPLATE MODE: the lore constitution plus a world seed, no LLM";

        /// <param name="bible">The constitution (a world is generated from the seed) or a world bible.</param>
        public static string Markdown(LoreBible bible, ulong seed)
        {
            var w = bible.IsWorld ? bible : bible.ForWorld(seed);
            var h = w.History;
            var s = new LoreSampler(w, seed);
            var md = new StringBuilder();
            md.Append("# Lore samples (world seed ").Append(seed).Append(")\n\n");
            md.Append("> ").Append(TemplateBanner).Append(". Regenerate with\n");
            md.Append("> `RELIC_LORE_SEED=").Append(seed).Append(" dotnet test tools/Relic.Tests --filter Name=WriteLoreSamples`.\n");
            md.Append("> Every name, culture, era and event below is generated from the seed; another seed gives another world.\n");
            md.Append("> Later the LLM narrates inside these structures; it never invents names or facts.\n\n");

            md.Append("## Cultures\n\n");
            md.Append(w.Cultures.Count).Append(" peoples this world. Each comes from a culture template (in brackets) and speaks one phonology family.\n\n");
            foreach (var c in w.Cultures)
            {
                var t = w.CultureTemplate(c.Template);
                md.Append("### ").Append(TemplateText.Capitalize(c.Name)).Append(" (").Append(c.Adjective).Append(")\n");
                md.Append("*[").Append(t != null ? t.Label : c.Template).Append(" · ").Append(c.PhonologyFamily).Append(" names · ")
                  .Append(string.Join(", ", c.Families)).Append("]*\n\n");
                md.Append(c.Identity).Append(' ').Append(c.Mood).Append("\n\n");
                md.Append("- **People:** ").Append(s.Person(c)).Append(", ").Append(s.Person(c)).Append(", ").Append(s.Person(c))
                  .Append(". **Places:** ").Append(string.Join(", ", h.PlacesOf(c.Id).ConvertAll(p => p.Name))).Append(".\n");
                md.Append("- **Crafts:** ").Append(string.Join(", ", c.Crafts)).Append(". **Materials:** ").Append(string.Join(", ", c.Materials)).Append(".\n");
                md.Append("- **Makes:** ").Append(string.Join(", ", c.ItemForms)).Append(". **Marks:** ").Append(string.Join("; ", c.ItemNaming.MakersMarks)).Append(".\n");
                md.Append("- **Values:** ").Append(string.Join(", ", c.Values)).Append(". **Festivals:** ")
                  .Append(string.Join(", ", c.Festivals.ConvertAll(f => f.Name + " (" + f.Season + ")"))).Append(".\n");
                foreach (var r in c.Relations)
                    md.Append("- *").Append(r.Kind).Append(" with ").Append(w.Culture(r.Culture).Adjective).Append(":* ").Append(r.Note).Append('\n');
                md.Append('\n');
            }

            md.Append("## Prehistory\n\n");
            foreach (var era in w.Eras)
            {
                md.Append("### ").Append(TemplateText.Capitalize(era.Name)).Append(" (").Append(era.StartYearsAgo).Append("–").Append(era.EndYearsAgo)
                  .Append(" years ago, ").Append(era.Tone.ToString().ToLowerInvariant()).Append(")\n\n");
                md.Append(era.Summary).Append(" *Echoes:* ").Append(string.Join(", ", era.Echoes)).Append(".\n\n");
                foreach (var l in h.Founders)
                    if (l.Era == era.Id) md.Append("- **").Append(LoreSampler.LegendName(l)).Append("** (founder): ").Append(l.Summary).Append('\n');
                foreach (var ev in h.Events)
                    if (ev.Era == era.Id)
                        md.Append("- **").Append(TemplateText.Capitalize(ev.Name)).Append("** (").Append(ev.YearsAgo).Append(" years ago, ")
                          .Append(ev.Tone.ToString().ToLowerInvariant()).Append("): ").Append(ev.Summary).Append('\n');
                foreach (var a in h.Artifacts)
                    if (a.Era == era.Id) md.Append("- *Relic:* **").Append(TemplateText.Capitalize(a.Name)).Append("**. ").Append(a.Summary).Append('\n');
                md.Append('\n');
            }

            md.Append("## Factions\n\n");
            var used = new HashSet<string>();
            for (int i = 0, guard = 0; i < 5 && guard < 100; i++, guard++)
            {
                var c = s.AnyCulture();
                var f = s.FactionArchetypeFor(c);
                if (!used.Add(f.Id)) { i--; continue; }
                md.Append("- **").Append(s.FactionName(f, c)).Append("** *(").Append(c.Adjective).Append(")*: ").Append(f.Label)
                  .Append(". Wants to ").Append(s.Pick(f.Goals)).Append(". Sign: ").Append(s.Pick(f.Symbols)).Append('.');
                if (f.Rivals.Count > 0) md.Append(" Friendly rivals: ").Append(w.Faction(s.Pick(f.Rivals)).Label).Append('.');
                md.Append('\n');
            }

            md.Append("\n## Relics\n\n");
            // A showcase of ten uses the exact 7/3 split in a shuffled order; live generation is 70/30 on average.
            const int relicCount = 10;
            var tones = new List<Tone>();
            for (int i = 0; i < relicCount; i++) tones.Add(i < 7 ? Tone.Bright : Tone.Somber);
            s.Shuffle(tones);
            var usedLines = new HashSet<string>();
            foreach (var tone in tones)
            {
                var r = s.RelicOf(s.AnyCulture(), s.PickMoodIn(tone), usedLines);
                md.Append("- **").Append(r.Name).Append("** *(").Append(r.Culture.Adjective).Append(' ').Append(r.Form)
                  .Append(" · ").Append(r.Motif.Id).Append(" · ").Append(r.Mood.Id).Append(")*  \n  ").Append(r.Line)
                  .Append(" Maker's mark: ").Append(r.Mark).Append(".\n");
            }
            md.Append("\n*Seven bright moods and three somber ones, the 70/30 rule.*\n");

            md.Append("\n## A festival\n\n");
            var fc = s.AnyCulture();
            var fest = s.Pick(fc.Festivals);
            string town = s.Pick(h.PlacesOf(fc.Id)).Name;
            string founder = s.Person(fc);
            md.Append("**").Append(TemplateText.Capitalize(fest.Name)).Append(" at ").Append(town).Append("** (")
              .Append(fc.Adjective).Append(", ").Append(fest.Season).Append(")\n\n");
            var fm = new StringBuilder();
            fm.Append(fest.Description).Append(" The custom here goes back to ").Append(founder).Append(", who first served ")
              .Append(s.Pick(fc.Foods)).Append(" to the whole of ").Append(town).Append(" and refused to stop. This year ")
              .Append(LoreSampler.MidSentence(s.FactionName(s.FactionArchetypeFor(fc), fc))).Append(" are in charge, and the prize is a ")
              .Append(s.Pick(fc.ItemForms)).Append(" marked with ").Append(s.Pick(fc.ItemNaming.MakersMarks)).Append(".\n");
            md.Append(TemplateText.FixArticles(fm.ToString()));
            return md.ToString();
        }
    }
}
