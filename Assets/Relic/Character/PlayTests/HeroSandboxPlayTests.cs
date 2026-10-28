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
            for (int i = 0; i < 30; i++) yield return null;

            var driver = _sandbox.Player.GetComponent<HeroPlayerDriver>();
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
    }
}
