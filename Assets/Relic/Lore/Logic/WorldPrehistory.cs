using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Relic.Core.Random;

namespace Relic.Lore
{
    /// <summary>A named place from a world's prehistory, home to one culture.</summary>
    public sealed class PrehistoryPlace
    {
        public string Name;
        public string Culture;
        public LoreFamily Family;
    }

    /// <summary>One event of a world's prehistory, instantiated from an event template.</summary>
    public sealed class PrehistoryEvent
    {
        public string Id;
        public string Template;
        public EventKind Kind;
        public Tone Tone;
        /// <summary>Era id (= its era template id).</summary>
        public string Era;
        public string Culture;
        /// <summary>The place it happened, when the template names one.</summary>
        public string Place;
        public int YearsAgo;
        public string Name;
        public string Summary;
        /// <summary>Legend ids it created or involved (figures and artifacts).</summary>
        public List<string> Legends = new List<string>();
        public List<EventTrace> Leaves = new List<EventTrace>();
    }

    /// <summary>
    /// A world's short procedural prehistory (16, decision 2026-11-18), generated at world
    /// creation so the first relics have something to remember: 3–4 eras from the era
    /// templates (always a founding era first and a waking era last), one founder per culture,
    /// a handful of events from the event templates whose eras fit, and the figures and
    /// relic-worthy artifacts those events leave. After this the Chronicler (20) writes
    /// history as players act. Pure and deterministic from the world seed.
    /// </summary>
    public sealed class WorldPrehistory
    {
        public const string Purpose = "lore.prehistory.v1";

        public List<EraDef> Eras = new List<EraDef>();
        public List<LegendDef> Founders = new List<LegendDef>();
        public List<LegendDef> Figures = new List<LegendDef>();
        public List<LegendDef> Artifacts = new List<LegendDef>();
        public List<PrehistoryEvent> Events = new List<PrehistoryEvent>();
        public List<PrehistoryPlace> Places = new List<PrehistoryPlace>();

        /// <summary>Founders, then figures, then artifacts: the world's referenceable famous names.</summary>
        public List<LegendDef> Legends
        {
            get
            {
                var all = new List<LegendDef>(Founders);
                all.AddRange(Figures);
                all.AddRange(Artifacts);
                return all;
            }
        }

        public List<PrehistoryPlace> PlacesOf(string cultureId) => Places.FindAll(p => p.Culture == cultureId);

        // ------------------------------------------------------------------ generation

        /// <param name="world">A bible whose <see cref="LoreBible.Cultures"/> are already generated.</param>
        public static WorldPrehistory Generate(LoreBible world, ulong worldSeed)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Cultures.Count == 0) throw new LoreDataException("prehistory needs the world's cultures first");
            var g = new Gen(world, worldSeed);
            g.MakePlaces();
            g.MakeEras();
            g.MakeFounders();
            g.FillEraSummaries();
            g.MakeEvents();
            return g.H;
        }

        sealed class Gen
        {
            public readonly WorldPrehistory H = new WorldPrehistory();
            readonly LoreBible _b;
            readonly PrehistoryRules _rules;
            readonly Dictionary<string, NameGenerator> _names = new Dictionary<string, NameGenerator>();
            readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly HashSet<string> _usedIds = new HashSet<string>(StringComparer.Ordinal);
            Pcg32 _rng;

            public Gen(LoreBible b, ulong seed)
            {
                _b = b;
                _rules = b.PrehistoryRules;
                _rng = Pcg32.ForPurpose(seed, Purpose);
                foreach (var c in b.Cultures) _names[c.Id] = new NameGenerator(c, b.Style);
            }

            T Pick<T>(IList<T> list) => list[_rng.Range(0, list.Count)];

            public void MakePlaces()
            {
                foreach (var c in _b.Cultures)
                {
                    int n = 2 + _rng.Range(0, 2);
                    for (int i = 0; i < n; i++)
                    {
                        string name = Unique(() => _names[c.Id].Place(ref _rng));
                        var fam = i < c.Families.Count ? c.Families[i] : c.Families[0];
                        H.Places.Add(new PrehistoryPlace { Name = name, Culture = c.Id, Family = fam });
                    }
                }
            }

            public void MakeEras()
            {
                var first = _rules.Eras.FindAll(e => e.Position == EraPosition.First);
                var middle = _rules.Eras.FindAll(e => e.Position == EraPosition.Middle);
                var last = _rules.Eras.FindAll(e => e.Position == EraPosition.Last);
                var chosen = new List<EraTemplate> { Pick(first) };
                int middles = Math.Min(middle.Count, 1 + _rng.Range(0, 2)); // 3 or 4 eras
                for (int i = 0; i < middles; i++)
                {
                    int total = 0;
                    foreach (var m in middle) total += Math.Max(1, m.Weight);
                    int r = _rng.Range(0, total);
                    foreach (var m in middle)
                    {
                        r -= Math.Max(1, m.Weight);
                        if (r < 0) { chosen.Add(m); middle.Remove(m); break; }
                    }
                }
                chosen.Add(Pick(last));

                // Year ranges, newest first: the waking era is a lifetime long, older ones a few centuries.
                int end = 0;
                var spans = new int[chosen.Count];
                for (int i = chosen.Count - 1; i >= 0; i--)
                {
                    int len = i == chosen.Count - 1 ? 5 * (12 + _rng.Range(0, 13)) : 5 * (30 + _rng.Range(0, 51));
                    spans[i] = len;
                }
                var eras = new EraDef[chosen.Count];
                for (int i = chosen.Count - 1; i >= 0; i--)
                {
                    var t = chosen[i];
                    string word = t.Words.Count > 0 ? Pick(t.Words) : "";
                    eras[i] = new EraDef
                    {
                        Id = t.Id,
                        Template = t.Id,
                        Tone = t.Tone,
                        Name = TemplateText.Fill(Pick(t.NamePatterns), new Dictionary<string, string> { ["word"] = word }),
                        Order = i,
                        EndYearsAgo = end,
                        StartYearsAgo = end + spans[i],
                        Echoes = new List<string>(t.Echoes),
                    };
                    end += spans[i];
                }
                H.Eras.AddRange(eras);
            }

            public void MakeFounders()
            {
                var dawn = H.Eras[0];
                foreach (var c in _b.Cultures)
                {
                    string name = UniquePerson(c);
                    var t = _b.CultureTemplate(c.Template);
                    var home = H.PlacesOf(c.Id)[0];
                    var values = new Dictionary<string, string>
                    {
                        ["founder"] = name,
                        ["culture"] = c.Adjective,
                        ["place"] = home.Name,
                        ["craft"] = Pick(c.Crafts),
                        ["form"] = Pick(c.ItemForms),
                        ["value"] = Pick(c.Values),
                    };
                    H.Founders.Add(new LegendDef
                    {
                        Id = Id("founder", name),
                        Kind = LegendKind.Figure,
                        Name = name,
                        Epithet = t != null && t.FounderEpithets.Count > 0 ? Pick(t.FounderEpithets) : "the Founder",
                        Culture = c.Id,
                        Era = dawn.Id,
                        Tone = Tone.Bright,
                        Summary = Sentence(Pick(_rules.FounderSummaries), values),
                        Motifs = new List<string> { Pick(c.Motifs) },
                    });
                    // Half the founders leave an heirloom the world has listened to ever since.
                    if (_rng.Chance(0.5f)) H.Artifacts.Add(Artifact(c, dawn, Tone.Bright, name, home.Name));
                }
            }

            public void FillEraSummaries()
            {
                foreach (var era in H.Eras)
                {
                    var t = _b.EraTemplate(era.Template);
                    var founder = Pick(H.Founders);
                    var c = _b.Culture(founder.Culture);
                    var values = new Dictionary<string, string>
                    {
                        ["culture"] = c.Adjective,
                        ["founder"] = founder.Name,
                        ["place"] = Pick(H.PlacesOf(c.Id)).Name,
                        ["word"] = t.Words.Count > 0 ? Pick(t.Words).ToLowerInvariant() : "lantern",
                    };
                    era.Summary = Sentence(Pick(t.Summaries), values);
                }
            }

            public void MakeEvents()
            {
                var usedTemplates = new HashSet<string>();
                for (int e = 0; e < H.Eras.Count; e++)
                {
                    var era = H.Eras[e];
                    var t = _b.EraTemplate(era.Template);
                    int count = t.Position == EraPosition.Middle ? 1 + _rng.Range(0, 2) : 2;
                    for (int k = 0; k < count; k++)
                    {
                        // The waking era always opens with a find, so every world has relic-worthy events.
                        bool needArtifact = t.Position == EraPosition.Last && k == 0;
                        var tpl = PickTemplate(era, usedTemplates, needArtifact);
                        if (tpl == null) continue;
                        usedTemplates.Add(tpl.Id);
                        H.Events.Add(Event(tpl, era));
                    }
                }
            }

            EventTemplate PickTemplate(EraDef era, HashSet<string> used, bool needArtifact)
            {
                var pool = _b.Events.FindAll(x => x.Eras.Contains(era.Template) && !used.Contains(x.Id)
                    && (!needArtifact || IsRelicWorthy(x)));
                if (pool.Count == 0 && needArtifact)
                    pool = _b.Events.FindAll(x => x.Eras.Contains(era.Template) && IsRelicWorthy(x));
                if (pool.Count == 0) pool = _b.Events.FindAll(x => x.Eras.Contains(era.Template) && !used.Contains(x.Id));
                if (pool.Count == 0) pool = _b.Events.FindAll(x => x.Eras.Contains(era.Template));
                if (pool.Count == 0) return null;
                int total = 0;
                foreach (var x in pool) total += Math.Max(1, x.Weight);
                int r = _rng.Range(0, total);
                foreach (var x in pool) { r -= Math.Max(1, x.Weight); if (r < 0) return x; }
                return pool[0];
            }

            PrehistoryEvent Event(EventTemplate t, EraDef era)
            {
                var c = Pick(_b.Cultures);
                var places = H.PlacesOf(c.Id);
                var ev = new PrehistoryEvent { Template = t.Id, Kind = t.Kind, Tone = t.Tone, Era = era.Id, Culture = c.Id, Leaves = new List<EventTrace>(t.Leaves) };
                var values = new Dictionary<string, string>();
                var created = new List<LegendDef>();
                string maker = null;
                foreach (var s in t.Slots)
                {
                    switch (s.Kind)
                    {
                        case SlotKind.Place:
                            ev.Place = Pick(places).Name;
                            values[s.Name] = ev.Place;
                            break;
                        case SlotKind.Region:
                        {
                            var other = Pick(H.Places);
                            values[s.Name] = "the lands around " + other.Name;
                            break;
                        }
                        case SlotKind.Faction: values[s.Name] = LoreSampler.MidSentence(FactionName(c)); break;
                        case SlotKind.Figure:
                        {
                            var founder = H.Founders.Find(f => f.Culture == c.Id);
                            if (s.Name == "founder" && era.Order == 0 && founder != null && !values.ContainsValue(founder.Name))
                            {
                                values[s.Name] = founder.Name;
                                ev.Legends.Add(founder.Id);
                                maker = maker ?? founder.Name;
                                break;
                            }
                            string name = UniquePerson(c);
                            var fig = new LegendDef
                            {
                                Id = Id("figure", name),
                                Kind = LegendKind.Figure,
                                Name = name,
                                Epithet = Pick(t.Tone == Tone.Somber ? _rules.SomberEpithets : _rules.BrightEpithets),
                                Culture = c.Id,
                                Era = era.Id,
                                Tone = t.Tone,
                                Motifs = new List<string> { Pick(c.Motifs) },
                            };
                            created.Add(fig);
                            H.Figures.Add(fig);
                            ev.Legends.Add(fig.Id);
                            maker = maker ?? name;
                            values[s.Name] = name;
                            break;
                        }
                        case SlotKind.Artifact:
                        {
                            var a = Artifact(c, era, t.Tone, maker ?? UniquePerson(c), ev.Place ?? places[0].Name);
                            H.Artifacts.Add(a);
                            ev.Legends.Add(a.Id);
                            values[s.Name] = a.Name;
                            break;
                        }
                        case SlotKind.Culture: values[s.Name] = c.Adjective; break;
                        case SlotKind.Era: values[s.Name] = era.Name; break;
                        case SlotKind.Legend: values[s.Name] = LoreSampler.LegendName(Pick(H.Founders)); break;
                        case SlotKind.Number: values[s.Name] = (5 * _rng.Range(1, 19)).ToString(CultureInfo.InvariantCulture); break;
                        default: values[s.Name] = s.Words.Count > 0 ? Pick(s.Words) : Pick(c.ItemForms); break;
                    }
                }
                var nameValues = new Dictionary<string, string>(values);
                foreach (var s in t.Slots) if (s.Kind == SlotKind.Word) nameValues[s.Name] = LoreSampler.Title(values[s.Name]);
                string evName = LoreSampler.MidSentence(TemplateText.Capitalize(TemplateText.Fill(Pick(t.NamePatterns), nameValues)));
                ev.Name = evName;
                ev.Summary = Sentence(t.Summary, values);
                ev.Id = Id("event", evName);
                int lo = era.EndYearsAgo + 5, hi = Math.Max(lo, era.StartYearsAgo - 5);
                ev.YearsAgo = 5 * ((lo + _rng.Range(0, hi - lo + 1)) / 5);
                foreach (var f in created) f.Summary = ev.Summary;
                return ev;
            }

            LegendDef Artifact(CultureDef c, EraDef era, Tone tone, string maker, string place)
            {
                // Prefer relic forms: these are the things that became relics.
                var relicForms = c.FormTags.FindAll(f => f.Base == "Relic");
                string form = relicForms.Count > 0 ? Pick(relicForms).Form : Pick(c.ItemForms);
                var motif = _b.Motif(Pick(c.Motifs));
                string name = null;
                for (int i = 0; i < 16 && (name == null || _usedNames.Contains(name)); i++)
                {
                    var naming = c.ItemNaming;
                    var values = new Dictionary<string, string>
                    {
                        ["maker"] = LoreSampler.FirstWord(maker),
                        ["form"] = LoreSampler.Title(form),
                        ["place"] = place,
                        ["epithet"] = Pick(naming.Epithets),
                        ["adjective"] = Pick(naming.Adjectives),
                        ["word"] = LoreSampler.Title(motif != null ? Pick(motif.Words) : form),
                    };
                    name = LoreSampler.WithArticle(TemplateText.Fill(Pick(naming.Patterns), values));
                }
                _usedNames.Add(name);
                var summary = new Dictionary<string, string>
                {
                    ["culture"] = c.Adjective,
                    ["form"] = form,
                    ["place"] = place,
                    ["era"] = era.Name,
                    ["maker"] = maker,
                };
                return new LegendDef
                {
                    Id = Id("artifact", name),
                    Kind = LegendKind.Artifact,
                    Name = name,
                    Culture = c.Id,
                    Era = era.Id,
                    Tone = tone,
                    Summary = Sentence(Pick(_rules.ArtifactSummaries), summary),
                    Motifs = motif != null ? new List<string> { motif.Id } : new List<string>(),
                };
            }

            string FactionName(CultureDef c)
            {
                var arch = _b.Faction(Pick(c.Factions));
                var motif = _b.Motif(Pick(c.Motifs));
                var values = new Dictionary<string, string>
                {
                    ["place"] = Pick(H.PlacesOf(c.Id)).Name,
                    ["founder"] = LoreSampler.FirstWord(_names[c.Id].Person(ref _rng)),
                    ["culture"] = c.Adjective,
                    ["word"] = LoreSampler.Title(Pick(motif.Words)),
                };
                return TemplateText.Fill(Pick(arch.NamePatterns), values);
            }

            string UniquePerson(CultureDef c) => Unique(() => _names[c.Id].Person(ref _rng), first: true);

            /// <summary>A generated name not used yet in this world (people by first word, so "Bram" never means two people).</summary>
            string Unique(Func<string> make, bool first = false)
            {
                string name = null;
                for (int i = 0; i < 32; i++)
                {
                    name = make();
                    if (_usedNames.Add(first ? LoreSampler.FirstWord(name) : name)) return name;
                }
                return name;
            }

            string Id(string prefix, string name)
            {
                string id = prefix + "-" + Slug(name);
                string unique = id;
                for (int i = 2; !_usedIds.Add(unique); i++) unique = id + "-" + i;
                return unique;
            }
        }

        /// <summary>Templates that create an artifact and leave it in the world: what the first relics remember.</summary>
        public static bool IsRelicWorthy(EventTemplate t) =>
            t.Leaves.Contains(EventTrace.Artifact) && t.Slots.Exists(s => s.Kind == SlotKind.Artifact);

        static string Sentence(string template, IDictionary<string, string> values) =>
            TemplateText.FixArticles(TemplateText.Capitalize(TemplateText.Fill(template, values)));

        /// <summary>"The Bell of Fenwick" → "the-bell-of-fenwick" (lower-case kebab, letters and digits only).</summary>
        public static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in (s ?? "").ToLowerInvariant())
            {
                if (ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9') sb.Append(ch);
                else if ((ch == ' ' || ch == '-') && sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }
    }
}
