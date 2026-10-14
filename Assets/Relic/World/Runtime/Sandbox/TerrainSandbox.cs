using System.Collections.Generic;
using Relic.World.Chunks;
using Relic.World.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Relic.World.Runtime
{
    /// <summary>
    /// The terrain sandbox, built entirely from code: generator, floating origin, chunk streamer,
    /// sun and sky, player, camera, HUD and perf monitor. The scene only needs this one component.
    /// Keys: F3 overlay, F4 fly, F5 next seed, F6 day cycle, F8 3 km auto-sprint benchmark.
    /// </summary>
    public sealed class TerrainSandbox : MonoBehaviour
    {
        [Header("World")]
        public long Seed = 1337;
        [Tooltip("Set before Start (e.g. by tests) to override Seed.")]
        public static long? SeedOverride;

        [Header("Assets (created by Relic > World > Build Terrain Sandbox)")]
        public Material TerrainMaterial;
        public Material WaterMaterial;
        public Material SkyMaterial;
        public Material ToonMaterial;
        public PlayerConfig PlayerConfig;

        [Header("Streaming")]
        public float ViewDistance = 1536f;
        public float Lod0Distance = 160f;
        public float Lod1Distance = 384f;
        public float Lod2Distance = 768f;
        public float ColliderDistance = 72f;
        public float UploadBudgetMs = 2f;

        [Header("Debug")]
        public bool ShowOverlay = true;
        public float BenchmarkDistance = 3000f;
        [Tooltip("Start the auto-sprint benchmark as soon as the world has loaded.")]
        public bool BenchmarkOnStart;

        public TerrainGenerator Generator { get; private set; }
        public FloatingOrigin Origin { get; private set; }
        public ChunkStreamer Streamer { get; private set; }
        public PlayerController Player { get; private set; }
        public OrbitCamera OrbitCam { get; private set; }
        public PerfMonitor Perf { get; private set; }
        public SkyController Sky { get; private set; }
        public bool Spawned { get; private set; }
        public ulong CurrentSeed { get; private set; }

        readonly List<Object> _created = new List<Object>();
        SandboxHud _hud;
        float _spawnClock;

        void Start()
        {
            BuildWorld((ulong)(SeedOverride ?? Seed));
        }

        public void BuildWorld(ulong seed)
        {
            TearDown();
            CurrentSeed = seed;
            Spawned = false;
            _spawnClock = 0f;
            if (PlayerConfig == null) PlayerConfig = Track(ScriptableObject.CreateInstance<PlayerConfig>());
            Material terrainMat = TerrainMaterial != null ? TerrainMaterial : Track(new Material(Shader.Find("Relic/World/ToonTerrain")));
            Material waterMat = WaterMaterial != null ? WaterMaterial : Track(new Material(Shader.Find("Relic/World/Water")));
            Material skyMat = Track(SkyMaterial != null ? new Material(SkyMaterial) : new Material(Shader.Find("Relic/World/SkyGradient")));
            Material toonMat = ToonMaterial != null ? ToonMaterial : Track(new Material(Shader.Find("Relic/World/Toon")));

            Generator = new TerrainGenerator(seed);
            Generator.FindSpawn(out double spawnX, out double spawnZ);

            // Floating origin (starts at 0,0: spawn is always near the origin).
            var originGo = Track(new GameObject("Floating Origin"));
            Origin = originGo.AddComponent<FloatingOrigin>();

            // Sun and sky.
            var sunGo = Track(new GameObject("Sun"));
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            Sky = sunGo.AddComponent<SkyController>();
            Sky.Sun = sun;
            Sky.SkyMaterial = skyMat;
            Sky.FogEnd = ViewDistance * 0.97f;
            Sky.FogStart = ViewDistance * 0.25f;
            Sky.Apply();

            // Player.
            var playerGo = Track(new GameObject("Player"));
            playerGo.layer = 2;
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            Player = playerGo.AddComponent<PlayerController>();
            Player.Config = PlayerConfig;
            Player.Probe = Probe;
            Player.Frozen = true;
            var avatar = playerGo.AddComponent<PlayerAvatar>();
            avatar.Player = Player;
            avatar.Build(toonMat);

            // Camera.
            var camGo = Track(new GameObject("Main Camera"));
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = ViewDistance + 400f;
            cam.fieldOfView = PlayerConfig.CameraFov;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            OrbitCam = camGo.AddComponent<OrbitCamera>();
            OrbitCam.Config = PlayerConfig;
            OrbitCam.Target = playerGo.transform;
            OrbitCam.Probe = Probe;
            Player.OrbitCam = OrbitCam;

            Origin.Target = playerGo.transform;
            Origin.Register(playerGo.transform);
            Origin.Register(camGo.transform);
            Origin.Shifted += OrbitCam.OnOriginShifted;

            // Streaming.
            var streamGo = Track(new GameObject("World Streamer"));
            Streamer = streamGo.AddComponent<ChunkStreamer>();
            Streamer.UploadBudgetMs = UploadBudgetMs;
            Streamer.Lod = new LodSettings
            {
                Lod0Distance = Lod0Distance,
                Lod1Distance = Lod1Distance,
                Lod2Distance = Lod2Distance,
                ViewDistance = ViewDistance,
                ColliderDistance = ColliderDistance,
            };
            Streamer.Initialize(Generator, Origin, playerGo.transform, terrainMat, waterMat);

            // HUD and perf.
            Perf = streamGo.AddComponent<PerfMonitor>();
            Perf.Streamer = Streamer;
            Perf.Player = Player;
            Perf.Origin = Origin;
            _hud = streamGo.AddComponent<SandboxHud>();
            _hud.Player = Player;
            _hud.Streamer = Streamer;
            _hud.Origin = Origin;
            _hud.Perf = Perf;
            _hud.Generator = Generator;
            _hud.Seed = seed;
            _hud.ShowOverlay = ShowOverlay;
            _hud.Loading = true;

            // Park the player on the height field at spawn while the first chunks load.
            Vector3 spawn = Origin.ToScene(spawnX, Generator.HeightAt(spawnX, spawnZ) + 0.05, spawnZ);
            Player.Teleport(spawn);
            OrbitCam.Snap(45f);
            Debug.Log("[Relic.World] Sandbox seed " + seed + ", spawn (" + spawnX.ToString("0.0") + ", " + spawnZ.ToString("0.0") + "), " +
                      Streamer.WorkerCount + " chunk workers");
        }

        public void Probe(Vector3 scenePos, out float ground, out float water)
        {
            Origin.ToWorld(scenePos, out double wx, out double wz);
            Generator.HeightAndWater(wx, wz, out ground, out water);
        }

        void Update()
        {
            if (Player == null) return;
            PlayerInput.UpdateCursorLock();

            if (!Spawned)
            {
                _spawnClock += Time.unscaledDeltaTime;
                if (Streamer.HasColliderAt(Player.transform.position) || _spawnClock > 20f)
                {
                    Vector3 p = Player.transform.position;
                    Probe(p, out float ground, out _);
                    Player.Teleport(new Vector3(p.x, ground + 0.1f, p.z));
                    Player.Frozen = false;
                    Spawned = true;
                    _hud.Loading = false;
                    Perf.StartRecording();
                    Debug.Log("[Relic.World] World ready after " + _spawnClock.ToString("0.00") + " s (" + Streamer.TotalBuilt + " chunks built)");
                    if (BenchmarkOnStart) StartBenchmark();
                }
                return;
            }

            if (PlayerInput.KeyDown(Key.F3)) _hud.ShowOverlay = !_hud.ShowOverlay;
            if (PlayerInput.KeyDown(Key.F4)) Player.ToggleFly();
            if (PlayerInput.KeyDown(Key.F5)) { BuildWorld(CurrentSeed + 1); return; }
            if (PlayerInput.KeyDown(Key.F6) && Sky != null) Sky.DayCycle = !Sky.DayCycle;
            if (PlayerInput.KeyDown(Key.F8))
            {
                if (Perf.BenchmarkRunning) { Perf.CancelBenchmark(); Player.AutoRun = false; }
                else StartBenchmark();
            }
        }

        /// <summary>Auto-sprints in a straight line (camera direction) for BenchmarkDistance and logs a perf summary.</summary>
        public void StartBenchmark()
        {
            Vector3 dir = Quaternion.Euler(0f, OrbitCam.Yaw, 0f) * Vector3.forward;
            Player.AutoRunDirection = dir;
            Player.AutoRunSpeed = PlayerConfig.SprintSpeed;
            Player.AutoRun = true;
            Perf.BeginBenchmark("sprint " + BenchmarkDistance.ToString("0") + " m straight", BenchmarkDistance, () => Player.AutoRun = false);
        }

        T Track<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Destroy(_created[i]);
            _created.Clear();
            Player = null;
        }

        void OnDestroy() => TearDown();
    }
}
