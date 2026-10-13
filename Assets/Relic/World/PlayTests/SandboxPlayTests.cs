using System.Collections;
using NUnit.Framework;
using Relic.World.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Relic.World.PlayTests
{
    /// <summary>
    /// Play-mode smoke tests for the terrain sandbox: the world streams in, the player spawns on a
    /// collider and stands, an auto-sprint crosses many chunks without errors, and the floating
    /// origin recenters. (Frame times here are not meaningful: batchmode has no real GPU load.)
    /// Folder is PlayTests, not Tests, so the dotnet harness (pure C#) doesn't compile it.
    /// </summary>
    public class SandboxPlayTests
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
            Time.timeScale = 1f;
            TerrainSandbox.SeedOverride = null;
            if (_sandbox != null) Object.Destroy(_sandbox.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldLoadsAndPlayerStands()
        {
            Assert.IsTrue(_sandbox.Spawned, "world never became ready");
            Assert.Greater(_sandbox.Streamer.CollidersActive, 0, "no colliders near the player");
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(MoveState.Grounded, _sandbox.Player.State);
            _sandbox.Probe(_sandbox.Player.transform.position, out float ground, out _);
            Assert.That(Mathf.Abs(_sandbox.Player.transform.position.y - ground), Is.LessThan(0.6f), "player should rest on the terrain");

            float t = 0f;
            while (!_sandbox.Streamer.IsSettled && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(_sandbox.Streamer.IsSettled, "streamer never finished the initial view (" + _sandbox.Streamer.PendingWork + " pending)");
            Assert.Greater(_sandbox.Streamer.VisibleChunks, 1500);
            Debug.Log("[Relic.World.Test] settled: " + _sandbox.Streamer.VisibleChunks + " chunks, " + _sandbox.Streamer.TotalBuilt + " builds, avg " +
                      (_sandbox.Streamer.TotalBuildMs / _sandbox.Streamer.TotalBuilt).ToString("0.00") + " ms, max " + _sandbox.Streamer.MaxBuildMs.ToString("0.0") + " ms");
        }

        [UnityTest]
        public IEnumerator AutoSprintCrossesChunksAndOriginRecenters()
        {
            Assert.IsTrue(_sandbox.Spawned);
            _sandbox.BenchmarkDistance = 4500f;
            Time.timeScale = 8f; // ~76 m/s: a stress test for streaming, far above sprint speed
            _sandbox.StartBenchmark();
            float t = 0f;
            while (_sandbox.Perf.BenchmarkRunning && t < 120f) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1f;
            Assert.IsFalse(_sandbox.Perf.BenchmarkRunning, "benchmark did not finish");
            Assert.GreaterOrEqual(_sandbox.Origin.ShiftCount, 1, "4.5 km from the origin should have recentered the world");
            Vector3 p = _sandbox.Player.transform.position;
            Assert.That(new Vector2(p.x, p.z).magnitude, Is.LessThan(4100f), "scene position should stay near the origin");
            _sandbox.Origin.ToWorld(p, out double wx, out double wz);
            Assert.That(System.Math.Sqrt(wx * wx + wz * wz), Is.GreaterThan(4000.0));
            Debug.Log("[Relic.World.Test] " + _sandbox.Perf.LastSummary);

            // Let streaming catch up at the destination, then check we can stand there.
            _sandbox.Player.AutoRun = false;
            t = 0f;
            while (!_sandbox.Streamer.HasColliderAt(p) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(_sandbox.Streamer.HasColliderAt(_sandbox.Player.transform.position), "no collider at destination");
        }

        [UnityTest]
        public IEnumerator FarAwayTerrainStreams()
        {
            // Jump 100 km out: the origin recenters and chunks still build there.
            Assert.IsTrue(_sandbox.Spawned);
            _sandbox.Origin.Recenter(100032.0, -64.0);
            _sandbox.Player.Teleport(new Vector3(0f, _sandbox.Generator.HeightAt(100032.0, -64.0) + 1f, 0f));
            float t = 0f;
            while (!_sandbox.Streamer.HasColliderAt(_sandbox.Player.transform.position) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(_sandbox.Streamer.HasColliderAt(_sandbox.Player.transform.position));
            yield return new WaitForSeconds(1f);
            MoveState s = _sandbox.Player.State;
            Assert.IsTrue(s == MoveState.Grounded || s == MoveState.Swimming, "player should stand or swim, was " + s);
        }
    }
}
