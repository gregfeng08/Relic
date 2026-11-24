using System.Collections;
using NUnit.Framework;
using Relic.World.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Relic.Character.PlayTests
{
    /// <summary>
    /// The sandbox spawns the procedural hero through the World avatar factory (registered by the
    /// Game layer), the debug kit/armor actions rebuild it, and it survives a floating-origin jump.
    /// </summary>
    public class HeroSandboxPlayTests
    {
        TerrainSandbox _sandbox;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TerrainSandbox.SeedOverride = 1337;
            var go = new GameObject("Sandbox Under Test");
            _sandbox = go.AddComponent<TerrainSandbox>();
            float t = 0f;
            while (!_sandbox.Spawned && t < 60f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TerrainSandbox.SeedOverride = null;
            if (_sandbox != null) Object.Destroy(_sandbox.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SandboxSpawnsTheProceduralHero()
        {
            Assert.IsTrue(_sandbox.Spawned, "world never finished loading");
            var hero = _sandbox.Player.GetComponent<HeroAvatar>();
            Assert.IsNotNull(hero, "the Game layer should register the Character avatar factory");
            Assert.IsNull(_sandbox.Player.GetComponent<PlayerAvatar>(), "placeholder should not be built too");
            Assert.IsNotNull(hero.Renderer);
            Assert.AreEqual("Unwritten", hero.Appearance.Name, "the sandbox starts as the unwritten wayfarer");
            Assert.AreEqual(0, hero.Look.ItemNames.Count, "who carries nothing yet");
            Assert.IsNotNull(hero.Mecanim, "driven by the hero Animator");
            Assert.IsTrue(hero.HumanAvatar.isValid && hero.HumanAvatar.isHuman);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsTrue(hero.Mecanim.GetCurrentAnimatorStateInfo(0).IsName(HeroMecanim.SIdle)
                || hero.Mecanim.GetCurrentAnimatorStateInfo(0).IsName(HeroMecanim.SFall)
                || hero.Mecanim.GetCurrentAnimatorStateInfo(0).IsName(HeroMecanim.SLand), "standing (or settling) at spawn");

            var driver = _sandbox.Player.GetComponent<HeroPlayerDriver>();
            driver.CyclePreset();
            yield return null;
            Assert.AreEqual("Wanderer", hero.Appearance.Name, "F11 cycles to the written looks");
            Assert.IsNotNull(hero.Look.Weapon, "a written look starts with the starter kit");
            Assert.IsNotNull(hero.Mecanim);

            driver.RollRandomKit();
            yield return null;
            Assert.IsNotNull(hero.Look.Weapon);
            Assert.AreEqual(5, hero.Look.ItemNames.Count);
            ArmorWeight before = hero.Look.Armor.Weight;
            driver.CycleArmor();
            yield return null;
            Assert.AreEqual((ArmorWeight)(((int)before + 1) % 3), hero.Look.Armor.Weight);
            Assert.LessOrEqual(hero.TriangleCount, 15000);
        }

        [UnityTest]
        public IEnumerator HeroStaysTogetherThroughAnOriginShift()
        {
            var hero = _sandbox.Player.GetComponent<HeroAvatar>();
            Assert.IsNotNull(hero);
            for (int i = 0; i < 20; i++) yield return null;
            _sandbox.Origin.Recenter(_sandbox.Origin.OriginX + 4096.0, _sandbox.Origin.OriginZ);
            yield return null;
            yield return null;
            Vector3 root = _sandbox.Player.transform.position;
            foreach (Transform t in hero.Rig.Bones)
                Assert.Less((t.position - root).magnitude, 2.5f, t.name + " was flung by the origin shift");
        }

        /// <summary>
        /// The CharacterController capsule is sized to the unwritten wayfarer (PlayerConfig body
        /// numbers): standing at spawn, the feet are on the capsule's bottom and the round head is
        /// inside it, top and sides.
        /// </summary>
        [UnityTest]
        public IEnumerator CapsuleFitsTheHero()
        {
            var hero = _sandbox.Player.GetComponent<HeroAvatar>();
            var cc = _sandbox.Player.GetComponent<CharacterController>();
            PlayerConfig config = _sandbox.Player.Config;
            Assert.AreEqual(config.BodyHeight, cc.height, 1e-4f);
            Assert.AreEqual(config.BodyRadius, cc.radius, 1e-4f);
            Assert.AreEqual(config.BodyHeight * 0.5f, cc.center.y, 1e-4f);
            float t = 0f;
            while (t < 2f || _sandbox.Player.State != MoveState.Grounded)
            {
                t += Time.deltaTime;
                if (t > 20f) Assert.Fail("never landed at spawn");
                yield return null;
            }
            for (int i = 0; i < 30; i++) yield return null;

            Vector3 feet = _sandbox.Player.transform.position;
            Transform head = hero.Rig.Bones[HeroRig.Head];
            float headRadius = HeroBlank.HeadRadius * hero.Shape.HeadScale * hero.Shape.Scale;
            foreach (int f in new[] { HeroRig.FootL, HeroRig.FootR })
                Assert.That(hero.Rig.Bones[f].position.y - feet.y, Is.InRange(-0.03f, 0.15f), "feet on the ground");
            float crown = hero.Shape.Height + hero.Rig.RootTransform.localPosition.y;
            Assert.LessOrEqual(crown, cc.height + 0.02f, "the head fits under the capsule top");
            Assert.Greater(crown, cc.height - 0.15f, "the capsule isn't much taller than the hero");
            Vector3 flat = head.position - feet;
            flat.y = 0f;
            Assert.LessOrEqual(flat.magnitude + headRadius, cc.radius + 0.06f, "the head fits inside the capsule's width");
            Assert.Less(config.HeadHeight, cc.height, "the underwater check uses a point inside the body");
            Assert.Greater(config.HeadHeight, hero.Rig.Bones[HeroRig.Neck].position.y - feet.y, "and it is at the head, not the chest");
        }

        /// <summary>
        /// On demand: renders the sandbox from the default follow camera with the hero at spawn
        /// (art-ref/renders/v3/v3_unwritten_sandbox.png next to the main checkout).
        /// Run with -testFilter Relic.Character.PlayTests.HeroSandboxPlayTests.CaptureFromTheDefaultCamera
        /// </summary>
        [UnityTest, Explicit("writes a review capture")]
        public IEnumerator CaptureFromTheDefaultCamera()
        {
            float t = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Camera cam = _sandbox.OrbitCam.GetComponent<Camera>();
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string project = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string repo = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(project)) == ".wt" ? System.IO.Path.GetFullPath(System.IO.Path.Combine(project, "../..")) : project;
            string dir = System.IO.Path.Combine(repo, "art-ref", "renders", "v3");
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "v3_unwritten_sandbox.png");
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log("[Relic.Character] Wrote " + file);
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
