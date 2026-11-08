using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.World.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Relic.World.PlayTests
{
    /// <summary>
    /// Drives the player with scripted input at a fixed 60 fps (run, turn, dash into sprint, stop,
    /// reverse, jump, strafe, plus a floating-origin recenter mid-run) and checks the motion for
    /// the things that read as "not smooth": frame-to-frame zig-zags (jitter) in the camera and
    /// the body, snaps, ground micro-bouncing, and speed that overshoots or stutters.
    /// </summary>
    public class MovementSmoothnessTests
    {
        TerrainSandbox _sandbox;

        struct Sample
        {
            public Vector3 Player, Camera;   // world positions (scene + floating origin)
            public float CamYaw, CamPitch, Facing, Speed;
            public MoveState State;
            public bool Jump;
        }

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

        /// <summary>Scripted input for frame f of the run (60 fps).</summary>
        static PlayerInputState Script(int f)
        {
            var s = new PlayerInputState();
            if (f >= 30 && f < 320) s.Move = new Vector2(0f, 1f);                  // run forward
            if (f >= 150 && f < 200) s.Look = new Vector2(3f, 0f);                  // turn the camera while running
            if (f == 200) s.SprintPressed = true;                                    // dash...
            if (f >= 200 && f < 320) s.SprintHeld = true;                           // ...into sprint
            if (f >= 380 && f < 470) s.Move = new Vector2(0f, -1f);                 // reverse
            if (f >= 470 && f < 560) s.Move = new Vector2(0f, 1f);
            if (f == 480) { s.JumpPressed = true; }
            if (f >= 480 && f < 490) s.JumpHeld = true;
            if (f >= 560 && f < 620) s.Move = new Vector2(-1f, 0.3f);               // swing left
            if (f >= 640 && f < 680) s.Look = new Vector2(0f, -2f);                 // pitch the camera
            return s;
        }

        [UnityTest]
        public IEnumerator ScriptedRunHasNoSpikesOrJitter()
        {
            Assert.IsTrue(_sandbox.Spawned, "world never became ready");
            Time.captureDeltaTime = 1f / 60f;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(MoveState.Grounded, _sandbox.Player.State, "player should stand before the run");

            int frame = 0;
            PlayerInput.Override = () => Script(frame);
            var samples = new List<Sample>();
            FloatingOrigin origin = _sandbox.Origin;
            for (frame = 0; frame < 720; frame++)
            {
                if (frame == 420) origin.Recenter(origin.OriginX + 64.0, origin.OriginZ - 64.0); // origin shift mid-run
                yield return null;
                Transform p = _sandbox.Player.transform, c = _sandbox.OrbitCam.transform;
                samples.Add(new Sample
                {
                    Player = p.position + new Vector3((float)origin.OriginX, 0f, (float)origin.OriginZ),
                    Camera = c.position + new Vector3((float)origin.OriginX, 0f, (float)origin.OriginZ),
                    CamYaw = _sandbox.OrbitCam.Yaw,
                    CamPitch = _sandbox.OrbitCam.Pitch,
                    Facing = _sandbox.Player.FacingYaw,
                    Speed = _sandbox.Player.HorizontalSpeed,
                    State = _sandbox.Player.State,
                    Jump = Script(frame).JumpPressed,
                });
            }
            PlayerInput.Override = null;

            int camJitter = ZigZags(samples, s => s.Camera, 0.004f, out float camMaxD2);
            int bodyJitter = ZigZags(samples, s => s.Player, 0.004f, out float bodyMaxD2);
            int yawJitter = AngleZigZags(samples, s => s.CamYaw, 0.05f);
            int facingJitter = AngleZigZags(samples, s => s.Facing, 0.3f);
            float maxCamStep = 0f;
            for (int i = 1; i < samples.Count; i++) maxCamStep = Mathf.Max(maxCamStep, (samples[i].Camera - samples[i - 1].Camera).magnitude);

            int unplannedAirborne = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                bool jumpedRecently = false;
                for (int k = Mathf.Max(0, i - 3); k <= i; k++) jumpedRecently |= samples[k].Jump;
                if (samples[i - 1].State == MoveState.Grounded && samples[i].State == MoveState.Airborne && !jumpedRecently) unplannedAirborne++;
            }

            // Run-up from standstill (frames 30..90): speed eases in, never overshoots run speed.
            float runSpeed = _sandbox.PlayerConfig.RunSpeed;
            float maxRun = 0f; int speedDips = 0;
            for (int i = 31; i < 140; i++)
            {
                maxRun = Mathf.Max(maxRun, samples[i].Speed);
                if (samples[i].Speed < samples[i - 1].Speed - 0.15f) speedDips++;
            }

            string summary = "[Relic.World.Test] smoothness: camera zigzags " + camJitter + " (max d2 " + camMaxD2.ToString("0.000") + " m, max step " + maxCamStep.ToString("0.00") +
                             " m), body zigzags " + bodyJitter + " (max d2 " + bodyMaxD2.ToString("0.000") + "), cam yaw zigzags " + yawJitter + ", facing zigzags " + facingJitter +
                             ", unplanned airborne " + unplannedAirborne + ", run-up max " + maxRun.ToString("0.00") + " m/s, speed dips " + speedDips;
            Debug.Log(summary);

            Assert.AreEqual(0, camJitter, summary);
            Assert.AreEqual(0, yawJitter, summary);
            Assert.AreEqual(0, bodyJitter, summary);
            Assert.AreEqual(0, facingJitter, summary);
            Assert.Less(maxCamStep, 0.6f, "camera never jumps more than a sprinting dash allows per frame. " + summary);
            Assert.LessOrEqual(unplannedAirborne, 1, "no ground micro-bouncing. " + summary);
            Assert.LessOrEqual(maxRun, runSpeed * 1.02f, "run-up overshoots. " + summary);
            Assert.AreEqual(0, speedDips, "run-up stutters. " + summary);
        }

        [Test]
        public void JitterDetectorCatchesASnapButNotAnImpulse()
        {
            var snap = new List<Sample>();
            var impulse = new List<Sample>();
            for (int i = 0; i < 60; i++)
            {
                float x = i * 0.1f; // 6 m/s
                snap.Add(new Sample { Camera = new Vector3(x + (i == 30 ? 0.06f : 0f), 0f, 0f) });
                float v = i < 20 ? 0.1f : Mathf.Lerp(0.4f, 0.15f, Mathf.Clamp01((i - 20) / 14f)); // dash: burst then ease out
                impulse.Add(new Sample { Camera = new Vector3(i == 0 ? 0f : impulse[i - 1].Camera.x + v, 0f, 0f) });
            }
            Assert.Greater(ZigZags(snap, s => s.Camera, 0.004f, out _), 0, "a 6 cm one-frame snap is jitter");
            Assert.AreEqual(0, ZigZags(impulse, s => s.Camera, 0.004f, out _), "a dash burst is not jitter");
        }

        /// <summary>
        /// Counts jitter: the second difference (acceleration x dt²) alternating direction on three
        /// frames in a row (+ - +), each above a threshold. A snap and its correction look like that;
        /// a legitimate impulse (dash start, jump, landing) flips once and then stays smooth.
        /// </summary>
        static int ZigZags(List<Sample> s, System.Func<Sample, Vector3> pos, float threshold, out float maxD2)
        {
            int count = 0;
            maxD2 = 0f;
            var d2 = new List<Vector3>();
            for (int i = 1; i < s.Count - 1; i++) d2.Add(pos(s[i + 1]) - 2f * pos(s[i]) + pos(s[i - 1]));
            foreach (Vector3 v in d2) maxD2 = Mathf.Max(maxD2, v.magnitude);
            for (int i = 0; i + 2 < d2.Count; i++)
            {
                Vector3 a = d2[i], b = d2[i + 1], c = d2[i + 2];
                if (a.magnitude > threshold && b.magnitude > threshold && c.magnitude > threshold &&
                    Vector3.Dot(a, b) < -0.5f * a.magnitude * b.magnitude && Vector3.Dot(b, c) < -0.5f * b.magnitude * c.magnitude) count++;
            }
            return count;
        }

        static int AngleZigZags(List<Sample> s, System.Func<Sample, float> angle, float threshold)
        {
            int count = 0;
            var d2 = new List<float>();
            for (int i = 1; i < s.Count - 1; i++)
                d2.Add(Mathf.DeltaAngle(angle(s[i]), angle(s[i + 1])) - Mathf.DeltaAngle(angle(s[i - 1]), angle(s[i])));
            for (int i = 0; i + 2 < d2.Count; i++)
            {
                float a = d2[i], b = d2[i + 1], c = d2[i + 2];
                if (Mathf.Abs(a) > threshold && Mathf.Abs(b) > threshold && Mathf.Abs(c) > threshold &&
                    Mathf.Sign(a) != Mathf.Sign(b) && Mathf.Sign(b) != Mathf.Sign(c)) count++;
            }
            return count;
        }
    }
}
