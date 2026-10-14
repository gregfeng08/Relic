using System;
using System.IO;
using Relic.Magic.Desk;
using UnityEditor;
using UnityEngine;

namespace Relic.Magic.DeskEditor
{
    /// <summary>
    /// Saves PNGs of the glyph sheet, every pre-written page as the desk inks it,
    /// and the arena mid-cast, without entering Play mode. Handy for reviews.
    /// Batchmode: Unity.exe -batchmode -quit -projectPath . -executeMethod
    ///   Relic.Magic.DeskEditor.WritingDeskSnapshots.Capture -snapshotDir &lt;folder&gt;
    /// (default folder: Temp/MagicSnapshots).
    /// </summary>
    public static class WritingDeskSnapshots
    {
        [MenuItem("Relic/Magic/Save Desk Snapshots")]
        public static void Capture()
        {
            string dir = ArgValue("-snapshotDir") ?? Path.Combine("Temp", "MagicSnapshots");
            Directory.CreateDirectory(dir);
            var registry = GlyphLibrary.Load();

            // 1. Glyph sheet: cores, inner, ring rows, in palette order.
            const int cell = 130;
            var sheet = new InkCanvas(cell * 8);
            int row = 0;
            foreach (GlyphRole role in Enum.GetValues(typeof(GlyphRole)))
            {
                int col = 0;
                var color = role == GlyphRole.Core ? new Color32(196, 120, 16, 255) : role == GlyphRole.Inner ? new Color32(24, 112, 140, 255) : new Color32(160, 34, 44, 255);
                foreach (var g in registry.WithRole(role))
                {
                    var center = new Vec2(cell * (col + 0.5f), cell * 8 - cell * (row + 0.5f));
                    foreach (var s in g.Render(center, cell * 0.62f)) sheet.Polyline(s.Points, color, 2.4f);
                    col++;
                }
                row++;
            }
            sheet.Apply();
            Save(sheet.Texture, Path.Combine(dir, "glyph-sheet.png"));
            sheet.Dispose();

            // 2. Every pre-written page, inked and colored by the real desk.
            var go = new GameObject("Snapshot Desk");
            try
            {
                var bootstrap = go.AddComponent<WritingDeskBootstrap>();
                var desk = bootstrap.Build();
                for (int i = 0; i < PrewrittenSpells.All.Count; i++)
                {
                    var page = PrewrittenSpells.All[i];
                    desk.LoadPage(page);
                    Save(desk.CanvasTexture, Path.Combine(dir, $"page-{i + 1}-{Slug(page.Title)}.png"));
                    Debug.Log($"[Relic.Magic] {page.Title}: {desk.Spell.Flavor?.Name}, mana {desk.Spell.Mana}, cooldown {desk.Spell.Cooldown}, ok {desk.Spell.Success}");
                }

                // 3. The arena a moment into a few casts.
                var cam = go.GetComponentInChildren<Camera>(true);
                foreach (var c in go.GetComponentsInChildren<Camera>(true))
                    if (c.name == "Arena Camera") cam = c;
                var rt = new RenderTexture(976, 720, 24);
                foreach (var title in new[] { "Tide Seekers", "Ember Nova", "Stone Rampart", "Dawn Halo" })
                {
                    var page = Find(title);
                    if (page == null) continue;
                    desk.LoadPage(page);
                    bootstrap.Preview.Play(desk.Spell.Program);
                    for (int t = 0; t < 12; t++) bootstrap.Preview.Tick(0.05f);
                    cam.targetTexture = rt;
                    cam.rect = new Rect(0, 0, 1, 1);
                    cam.Render();
                    cam.targetTexture = null;
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    Save(tex, Path.Combine(dir, $"arena-{Slug(title)}.png"));
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("[Relic.Magic] Snapshots saved to " + Path.GetFullPath(dir));
        }

        static PrewrittenSpells.Page Find(string title)
        {
            foreach (var p in PrewrittenSpells.All)
                if (p.Title == title)
                    return p;
            return null;
        }

        static void Save(Texture2D tex, string path) => File.WriteAllBytes(path, tex.EncodeToPNG());

        static string Slug(string s) => s.ToLowerInvariant().Replace(' ', '-');

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];
            return null;
        }
    }
}
