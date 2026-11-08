using System.Collections;
using NUnit.Framework;
using Relic.World.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Relic.World.PlayTests
{
    /// <summary>Diving: Ctrl/C takes you under, breath runs out, you're forced up and stamina drains.</summary>
    public class DiveTests
    {
        TerrainSandbox _sandbox;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TerrainSandbox.SeedOverride = 1337;
            var go = new GameObject("Sandbox Under Test");
            _sandbox = go.AddComponent<TerrainSandbox>();
            float t = 0f;
            while (!_sandbox.Spawned && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerInput.Override = null;
            Time.captureDeltaTime = 0f;
            TerrainSandbox.SeedOverride = null;
            if (_sandbox != null) Object.Destroy(_sandbox.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DiveRunsOutOfBreathAndSurfaces()
        {
            Assert.IsTrue(_sandbox.Spawned);
            // Find open, deep water near spawn and drop the player in.
            var gen = _sandbox.Generator;
            double bx = 0, bz = 0, best = double.MaxValue;
            for (double z = -2500; z <= 2500; z += 20)
            for (double x = -2500; x <= 2500; x += 20)
            {
                gen.HeightAndWater(x, z, out float h, out float w);
                if (w - h < 12f) continue;
                double d = x * x + z * z;
                if (d < best) { best = d; bx = x; bz = z; }
            }
            Assert.Less(best, double.MaxValue, "no deep water within 2.5 km");
            FloatingOrigin origin = _sandbox.Origin;
            origin.Recenter(System.Math.Floor(bx / 64.0) * 64.0, System.Math.Floor(bz / 64.0) * 64.0);
            gen.HeightAndWater(bx, bz, out float ground, out float water);
            PlayerController p = _sandbox.Player;
            p.Teleport(origin.ToScene(bx, water - 0.5, bz));
            float t = 0f;
            while (!_sandbox.Streamer.HasColliderAt(p.transform.position) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }

            Time.captureDeltaTime = 1f / 60f;
            int frame = 0;
            PlayerInput.Override = () => new PlayerInputState { DescendHeld = frame < 120 };
            for (; frame < 120; frame++) yield return null;
            Assert.AreEqual(MoveState.Diving, p.State, "holding Ctrl/C dives");
            Assert.IsTrue(p.HeadUnderwater, "head is under after 2 s of diving");
            Assert.Less(p.transform.position.y, water - 2.5f, "dove below the surface");

            // Hold depth with Ctrl until breath runs out.
            PlayerInput.Override = () => new PlayerInputState { DescendHeld = true };
            float stamina0 = p.Stamina;
            int limit = Mathf.CeilToInt((_sandbox.PlayerConfig.BreathMax + 5f) * 60f);
            for (frame = 0; frame < limit && !p.OutOfBreath; frame++) yield return null;
            Assert.IsTrue(p.OutOfBreath, "breath ran out");

            // Out of breath: forced up even while still holding Ctrl, stamina drains, then surface.
            for (frame = 0; frame < 60 * 15 && p.State == MoveState.Diving; frame++) yield return null;
            Assert.AreEqual(MoveState.Swimming, p.State, "forced to the surface");
            Assert.Less(p.Stamina, stamina0, "drowning drains stamina");
            Assert.Greater(p.SuffocationSeconds, 0f);
            PlayerInput.Override = null;
            Debug.Log("[Relic.World.Test] dive: surfaced after " + p.SuffocationSeconds.ToString("0.0") + " s out of breath, stamina " + p.Stamina.ToString("0"));
        }
    }
}
