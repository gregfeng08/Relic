using NUnit.Framework;
using Relic.Magic.Desk;
using Relic.Magic.DeskEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Relic.Magic.EditorTests
{
    /// <summary>
    /// Unity-side smoke tests for the Writing Desk runtime (the pure logic is
    /// covered by Relic.Magic.Tests). They can't judge feel; they check that the
    /// pieces build, load data and run without errors.
    /// </summary>
    public class WritingDeskSmokeTests
    {
        [Test]
        public void GlyphSetLoadsFromResources()
        {
            var reg = GlyphLibrary.Load();
            Assert.AreEqual(21, reg.All.Count);
        }

        [Test]
        public void CanvasInksPixels()
        {
            var canvas = new InkCanvas(64);
            try
            {
                var before = canvas.Texture.GetPixel(32, 32);
                canvas.Line(new Vec2(10, 32), new Vec2(54, 32), new Color32(0, 0, 0, 255), 2f);
                canvas.Apply();
                var after = canvas.Texture.GetPixel(32, 32);
                Assert.Less(after.r, before.r - 0.3f);
            }
            finally { canvas.Dispose(); }
        }

        [Test]
        public void PreviewPlaysTideSeekers()
        {
            var preview = SpellPreview.Build(Vector3.zero);
            try
            {
                var spell = SpellCompiler.Compile(SpellRecipe.Parse("water | split seek | line"), GlyphLibrary.Load());
                Assert.IsTrue(spell.Success);
                preview.Play(spell.Program);
                preview.Tick(0.01f);
                Assert.AreEqual(3, preview.ActiveProjectiles);
                for (int i = 0; i < 400; i++) preview.Tick(0.02f);
                Assert.AreEqual(0, preview.ActiveProjectiles, "bolts should have hit or run out of range");
            }
            finally { Object.DestroyImmediate(preview.gameObject); }
        }

        [Test]
        public void PreviewPlaysEveryPrewrittenPage()
        {
            var preview = SpellPreview.Build(Vector3.zero);
            try
            {
                foreach (var page in PrewrittenSpells.All)
                {
                    var spell = SpellCompiler.Compile(SpellRecipe.Parse(page.Recipe), GlyphLibrary.Load());
                    Assert.IsTrue(spell.Success, page.Title);
                    preview.Play(spell.Program);
                    for (int i = 0; i < 300; i++) preview.Tick(0.05f);
                    Assert.AreEqual(0, preview.ActiveProjectiles, page.Title + " left projectiles behind after 15 s");
                }
            }
            finally { Object.DestroyImmediate(preview.gameObject); }
        }

        [Test]
        public void DeskBuildsAndOpensAPage()
        {
            var go = new GameObject("Desk Test");
            try
            {
                var desk = go.AddComponent<WritingDeskBootstrap>().Build();
                desk.LoadPage(PrewrittenSpells.All[0]);
                Assert.IsTrue(desk.Spell.Success, string.Join("\n", desk.Spell.Diagnostics));
                Assert.AreEqual("Tide Seekers", desk.Spell.Flavor.Name);
                desk.ClearPage();
                Assert.IsFalse(desk.Spell.Success);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WritingDeskSceneHasTheBootstrap()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(WritingDeskSceneBuilder.ScenePath),
                "run Relic/Magic/Rebuild Writing Desk Scene");
            var scene = EditorSceneManager.OpenScene(WritingDeskSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                bool found = false;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<WritingDeskBootstrap>() != null) found = true;
                Assert.IsTrue(found);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
