using System.Collections.Generic;
using System.Text;

namespace Relic.Lore
{
    /// <summary>
    /// Builds docs/lore/samples.md: a short, readable tour of what the Bible generates
    /// (names, regions, factions, relics, events, a festival) for Greg's tone review.
    /// </summary>
    public static class LoreSamples
    {
        public const string TemplateBanner = "TEMPLATE MODE: Bible data plus the name generator, no LLM";

        public static string Markdown(LoreBible bible, ulong seed)
        {
            var s = new LoreSampler(bible, seed);
            var md = new StringBuilder();
            md.Append("# Lore samples (seed ").Append(seed).Append(")\n\n");
            md.Append("> ").Append(TemplateBanner).Append(". Regenerate with\n");
            md.Append("> `RELIC_LORE_SEED=").Append(seed).Append(" dotnet test tools/Relic.Tests --filter Name=WriteLoreSamples`.\n");
            md.Append("> Later the LLM narrates inside these structures; it never invents names or facts.\n\n");

            md.Append("## Names\n\n| Culture | People | Places |\n|---|---|---|\n");
            foreach (var c in bible.Cultures)
            {
                md.Append("| **").Append(c.Adjective).Append("** | ");
                md.Append(s.Person(c)).Append(", ").Append(s.Person(c)).Append(", ").Append(s.Person(c)).Append(" | ");
                md.Append(s.Place(c)).Append(", ").Append(s.Place(c)).Append(", ").Append(s.Place(c)).Append(" |\n");
            }

            md.Append("\n## Regions\n\n");
            for (int i = 0; i < 3; i++)
            {
                var c = s.AnyCulture();
                var era = s.Pick(bible.Eras);
                md.Append("- **").Append(TemplateText.Capitalize(s.Region(c))).Append("**: ").Append(c.Adjective)
                  .Append(" country. ").Append(TemplateText.Capitalize(s.Pick(c.Values))).Append(" matters here, and the land remembers ")
                  .Append(era.Name).Append(": ").Append(s.Pick(era.Echoes)).Append(".\n");
            }

            md.Append("\n## Factions\n\n");
            var used = new HashSet<string>();
            for (int i = 0; i < 5; i++)
            {
                var c = s.AnyCulture();
                var f = s.FactionArchetypeFor(c);
                if (!used.Add(f.Id)) { i--; continue; }
                md.Append("- **").Append(s.FactionName(f, c)).Append("** *(").Append(c.Adjective).Append(")*: ").Append(f.Label)
                  .Append(". Wants to ").Append(s.Pick(f.Goals)).Append(". Sign: ").Append(s.Pick(f.Symbols)).Append('.');
                if (f.Rivals.Count > 0) md.Append(" Friendly rivals: ").Append(bible.Faction(s.Pick(f.Rivals)).Label).Append('.');
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
            md.Append("\n*Seven bright moods and three somber ones, the Bible's 70/30 rule.*\n");

            md.Append("\n## Events\n\n");
            var kinds = new[] { "founding-hearth", "discovery-ruin", "rivalry-race", "lost-expedition-letters", "migration" };
            foreach (var id in kinds)
            {
                var ev = s.EventOf(bible.Event(id), s.AnyCulture());
                md.Append("- **").Append(ev.Key).Append("**: ").Append(ev.Value).Append('\n');
            }

            md.Append("\n## A festival\n\n");
            var fc = s.AnyCulture();
            var fest = s.Pick(fc.Festivals);
            string town = s.Place(fc);
            string founder = s.Person(fc);
            md.Append("**").Append(TemplateText.Capitalize(fest.Name)).Append(" at ").Append(town).Append("** (")
              .Append(fc.Adjective).Append(", ").Append(fest.Season).Append(")\n\n");
            md.Append(fest.Description).Append(" The custom here goes back to ").Append(founder).Append(", who first served ")
              .Append(s.Pick(fc.Foods)).Append(" to the whole of ").Append(town).Append(" and refused to stop. This year ")
              .Append(LoreSampler.MidSentence(s.FactionName(s.FactionArchetypeFor(fc), fc))).Append(" are in charge, and the prize is a ")
              .Append(s.Pick(fc.ItemForms)).Append(" marked with ").Append(s.Pick(fc.ItemNaming.MakersMarks)).Append(".\n");
            return md.ToString();
        }

    }
}
