using System;
using System.Collections.Generic;
using System.IO;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Where Items gets its Lore Bible. Tools, tests and the editor find
    /// Assets/Relic/Lore/Data on disk by walking up from the working directory (the
    /// same way the Lore tests do). A player build has no Assets folder, so Unity
    /// code loads the Bible from TextAssets with <see cref="LoreBible.FromJson"/> and
    /// hands it over with <see cref="Use"/> before generating anything.
    /// </summary>
    public static class LoreSource
    {
        static LoreBible _bible;
        static string _styleGuide;
        static readonly object Gate = new object();

        /// <summary>Relative path of the Bible data folder from the repo root.</summary>
        public static readonly string DataPath = Path.Combine("Assets", "Relic", "Lore", "Data");

        /// <summary>Relative path of the narration style guide (docs/lore/style-guide.md).</summary>
        public static readonly string StyleGuidePath = Path.Combine("docs", "lore", "style-guide.md");

        /// <summary>The Bible every generator uses unless one is passed explicitly.</summary>
        public static LoreBible Default
        {
            get
            {
                lock (Gate)
                {
                    if (_bible != null) return _bible;
                    string root = FindRepoRoot();
                    if (root == null)
                        throw new LoreDataException("No Lore Bible: call LoreSource.Use(bible) first (player builds), or run from inside the repo so " + DataPath + " can be found.");
                    _bible = LoreBible.LoadDirectory(Path.Combine(root, DataPath));
                    return _bible;
                }
            }
        }

        /// <summary>The default Bible, or null when none can be found (style checks then use the Items lists only).</summary>
        public static LoreBible TryDefault()
        {
            try { return Default; }
            catch (LoreDataException) { return null; }
        }

        /// <summary>Injects a Bible (e.g. built from TextAssets in Unity). Null clears it so the next use reloads from disk.</summary>
        public static void Use(LoreBible bible)
        {
            lock (Gate) _bible = bible;
        }

        /// <summary>
        /// The narration style guide for LLM system prompts: docs/lore/style-guide.md when
        /// the repo is reachable, otherwise a condensed copy of its rules.
        /// </summary>
        public static string StyleGuideText
        {
            get
            {
                lock (Gate)
                {
                    if (_styleGuide != null) return _styleGuide;
                    string root = FindRepoRoot();
                    string path = root != null ? Path.Combine(root, StyleGuidePath) : null;
                    _styleGuide = path != null && File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : CondensedStyleGuide;
                    return _styleGuide;
                }
            }
        }

        /// <summary>Overrides the style guide text (Unity: from a TextAsset).</summary>
        public static void UseStyleGuide(string text)
        {
            lock (Gate) _styleGuide = text;
        }

        /// <summary>The folder holding Assets/Relic/Lore/Data, searched upward from the working and base directories.</summary>
        public static string FindRepoRoot()
        {
            var starts = new List<string>();
            try { starts.Add(Directory.GetCurrentDirectory()); } catch (Exception) { }
            try { starts.Add(AppContext.BaseDirectory); } catch (Exception) { }
            foreach (var start in starts)
            {
                if (string.IsNullOrEmpty(start)) continue;
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, DataPath))) return d.FullName;
                    d = d.Parent;
                }
            }
            return null;
        }

        /// <summary>The rules of docs/lore/style-guide.md in a few lines (used when the file isn't reachable).</summary>
        public const string CondensedStyleGuide =
            "Voice: a well-traveled aunt telling you about a place she loves: warm, specific, a little funny, never in a hurry to scare you. " +
            "Relic is bright medieval fantasy: wonder, craft, community, adventure. Darkness is an accent.\n" +
            "1. Use only the names you're given. Never invent a proper noun; say \"a smith\" or \"a village upriver\" instead.\n" +
            "2. Never contradict the facts you're given.\n" +
            "3. Follow the mood you're given (about 7 in 10 are bright). Don't drift darker than it asks.\n" +
            "4. Be concrete: name the material, the craft, the weather, the season.\n" +
            "5. Somber is quiet, not grim: no gore, no bodies, no despair.\n" +
            "6. Gentle humor, often. Never sarcastic, never modern.\n" +
            "7. Grounded magic: relics have power because they were used and loved. Say what the thing did and who cared about it.\n" +
            "8. Point somewhere: relic lore names at least one real place from the context.\n" +
            "9. Stay in the world: no game terms, no modern slang.\n" +
            "Sentences at most 22 words; relic lore 1-3 sentences; reading level grade 6-8; active voice.";
    }
}
