using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Relic.World.Chunks;
using Relic.World.Regions;
using Relic.World.Runtime;
using Relic.World.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Relic.World.Editor
{
    /// <summary>One screenshot: where the camera is and what it looks at.</summary>
    internal sealed class CaptureView
    {
        public string Name;
        public double X, Z;                 // focus point (world meters)
        public float Yaw, Pitch = 10f, Distance = 5.5f, Height = 1.55f;
        public bool Hero;
        /// <summary>-1: the LOD the streamer would choose. 0..3: every chunk at this LOD.</summary>
        public int ForceLod = -1;
        /// <summary>Only build chunks this close (m). Close-ups don't need the whole 1.5 km.</summary>
        public float ViewDistance = 1536f;
        /// <summary>When set, the camera sits at (CamX, ground + CamHeight, CamZ) and looks at the focus point + Height.</summary>
        public bool LookAt;
        public double CamX, CamZ;
        public float CamHeight = 20f;
        /// <summary>Sun elevation override (degrees); NaN keeps the default.</summary>
        public float SunElevation = float.NaN;
        public float SunAzimuth = float.NaN;
        /// <summary>Camera below the water surface (diving check).</summary>
        public bool Underwater;
        /// <summary>Place trees, rocks and grass (as plain renderers) around the focus.</summary>
        public bool Props;
    }

    /// <summary>
    /// Renders still screenshots of the generated world without entering play mode, for reviews
    /// and for checking the look from batchmode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Relic.World.Editor.SandboxCapture.Run -captureDir C:/tmp/shots [-captureSeed 1337] [-captureSet default|ridges|props|all]
    /// Builds chunks synchronously around each viewpoint (same mesh builder as the streamer),
    /// places the placeholder hero, and saves PNGs.
    /// </summary>
    public static class SandboxCapture
    {
        [MenuItem("Relic/World/Capture Screenshots")]
        public static void RunMenu() => Capture(Path.Combine(Application.dataPath, "../Temp/RelicCaptures"), 1337, "default");

        public static void Run()
        {
            string dir = Arg("-captureDir") ?? Path.Combine(Application.dataPath, "../Temp/RelicCaptures");
            ulong seed = ulong.TryParse(Arg("-captureSeed"), out ulong s) ? s : 1337UL;
            string set = Arg("-captureSet") ?? "default";
            try
            {
                Capture(dir, seed, set);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        public static void Capture(string dir, ulong seed, string set)
        {
            Directory.CreateDirectory(dir);
            SandboxSceneBuilder.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var env = new CaptureEnv
            {
                Terrain = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/ToonTerrain.mat"),
                Water = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/Water.mat"),
                Toon = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/ToonCharacter.mat"),
                Gen = new TerrainGenerator(seed),
            };
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/SkyGradient.mat");

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            env.Sky = sunGo.AddComponent<SkyController>();
            env.Sky.Sun = sun;
            env.Sky.SkyMaterial = skyMat;
            env.Sky.FogStart = 380f;
            env.Sky.FogEnd = 1490f;
            env.Sky.Apply();
            Shader.SetGlobalVector("_RelicWorldOffset", Vector4.zero);

            var views = new List<CaptureView>();
            bool all = set == "all";
            if (all || set == "default") AddDefaultViews(env.Gen, views);
            if (all || set == "ridges") CaptureViews.AddRidgeViews(env.Gen, views);
            if (all || set == "snow") CaptureViews.AddSnowViews(env.Gen, views);
            if (all || set == "dive") CaptureViews.AddDiveViews(env.Gen, views);
            if (all || set == "props") CaptureViews.AddPropViews(env.Gen, views);

            int index = 0;
            foreach (CaptureView v in views) RenderView(env, v, dir, ++index);
            Debug.Log("[Relic.World] Captured " + views.Count + " screenshots (" + set + ") to " + dir);
        }

        internal sealed class CaptureEnv
        {
            public TerrainGenerator Gen;
            public Material Terrain, Water, Toon;
            public SkyController Sky;
        }

        static void AddDefaultViews(TerrainGenerator gen, List<CaptureView> views)
        {
            gen.FindSpawn(out double sx, out double sz);
            views.Add(new CaptureView { Name = "01_spawn_hero", X = sx, Z = sz, Yaw = 40f, Pitch = 12f, Distance = 5.5f, Height = 1.55f, Hero = true, Props = true });
            views.Add(new CaptureView { Name = "02_spawn_vista", X = sx, Z = sz, Yaw = 200f, Pitch = 8f, Distance = 30f, Height = 25f, Props = true });

            // Find the nearest region of a few families and look at it from its edge.
            var wanted = new[] { RegionFamily.Highlands, RegionFamily.Badlands, RegionFamily.Coast, RegionFamily.Frost, RegionFamily.Forest, RegionFamily.Marsh };
            var found = new Dictionary<RegionFamily, (double x, double z)>();
            for (int ring = 1; ring < 40 && found.Count < wanted.Length; ring++)
            {
                double r = ring * 150.0;
                for (int k = 0; k < 48; k++)
                {
                    double a = k * Math.PI * 2 / 48;
                    double x = r * Math.Cos(a), z = r * Math.Sin(a);
                    RegionSample s = gen.Regions.Sample(x, z);
                    if (s.W0 < 0.98f || found.ContainsKey(s.Family)) continue;
                    if (Array.IndexOf(wanted, s.Family) < 0) continue;
                    found[s.Family] = (x, z);
                }
            }
            foreach (var kv in found)
            {
                // Stand 250 m back toward the origin, looking at the spot.
                double x = kv.Value.x, z = kv.Value.z;
                double len = Math.Sqrt(x * x + z * z);
                double bx = x - x / len * 250.0, bz = z - z / len * 250.0;
                float yaw = (float)(Math.Atan2(x - bx, z - bz) * 180.0 / Math.PI);
                views.Add(new CaptureView { Name = "03_" + kv.Key.ToString().ToLowerInvariant(), X = bx, Z = bz, Yaw = yaw, Pitch = 6f, Distance = 1f, Height = 22f, Props = true });
            }
        }

        static void RenderView(CaptureEnv env, CaptureView v, string dir, int index)
        {
            TerrainGenerator gen = env.Gen;
            env.Sky.SunElevation = float.IsNaN(v.SunElevation) ? 40f : v.SunElevation;
            env.Sky.SunAzimuth = float.IsNaN(v.SunAzimuth) ? 215f : v.SunAzimuth;
            env.Sky.Apply();

            var root = new GameObject("CaptureRoot");
            double originX = ChunkMath.ChunkIndex(v.X) * (double)ChunkMath.ChunkSize;
            double originZ = ChunkMath.ChunkIndex(v.Z) * (double)ChunkMath.ChunkSize;
            var meshes = new List<Mesh>();

            // Build every chunk within view distance at the LOD the streamer would choose (or the forced one).
            LodSettings lod = LodSettings.Default;
            lod.ViewDistance = Mathf.Min(lod.ViewDistance, v.ViewDistance);
            ChunkOffset[] plan = LodPlan.Build(lod);
            ChunkCoord anchor = ChunkMath.ChunkAt(v.X, v.Z);
            var datas = new ChunkMeshData[plan.Length];
            Parallel.For(0, plan.Length, i =>
            {
                int l = v.ForceLod >= 0 ? v.ForceLod : plan[i].Lod;
                var d = new ChunkMeshData(l) { Coord = new ChunkCoord(anchor.X + plan[i].Dx, anchor.Z + plan[i].Dz) };
                ChunkMeshBuilder.Build(gen, d);
                datas[i] = d;
            });
            foreach (ChunkMeshData d in datas)
            {
                var go = new GameObject("Chunk");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3((float)(ChunkMath.OriginX(d.Coord) - originX), 0f, (float)(ChunkMath.OriginZ(d.Coord) - originZ));
                Mesh m = ChunkMeshBuilder.CreateTerrainMesh(d);
                meshes.Add(m);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = env.Terrain;
                if (d.HasWater)
                {
                    var wgo = new GameObject("Water");
                    wgo.transform.SetParent(go.transform, false);
                    var wm = new Mesh();
                    int n = d.Side * d.Side;
                    wm.SetVertices(d.WaterPositions, 0, n);
                    wm.SetColors(d.WaterColors, 0, n);
                    wm.SetIndices(d.WaterIndices, 0, d.WaterIndexCount, MeshTopology.Triangles, 0, true, 0);
                    meshes.Add(wm);
                    wgo.AddComponent<MeshFilter>().sharedMesh = wm;
                    var wr = wgo.AddComponent<MeshRenderer>();
                    wr.sharedMaterial = env.Water;
                    wr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            CaptureViews.AfterChunksBuilt(env, v, datas, root.transform, originX, originZ);

            float ground = gen.HeightAt(v.X, v.Z);
            float water = gen.WaterLevelAt(v.X, v.Z);
            var focus = new Vector3((float)(v.X - originX), Mathf.Max(ground, water), (float)(v.Z - originZ));
            if (v.LookAt) focus.y = ground;

            if (v.Hero)
            {
                var hero = new GameObject("Hero");
                hero.transform.SetParent(root.transform, false);
                hero.transform.position = focus;
                hero.transform.rotation = Quaternion.Euler(0f, v.Yaw + 160f, 0f);
                hero.AddComponent<PlayerAvatar>().Build(env.Toon);
            }

            var camGo = new GameObject("CaptureCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.Skybox;

            Vector3 camPos;
            Quaternion rot;
            if (v.LookAt)
            {
                float camGround = gen.HeightAt(v.CamX, v.CamZ);
                camPos = new Vector3((float)(v.CamX - originX), Mathf.Max(camGround, v.Underwater ? camGround : gen.WaterLevelAt(v.CamX, v.CamZ)) + v.CamHeight, (float)(v.CamZ - originZ));
                rot = Quaternion.LookRotation(focus + Vector3.up * v.Height - camPos, Vector3.up);
            }
            else
            {
                rot = Quaternion.Euler(v.Pitch, v.Yaw, 0f);
                Vector3 pivot = focus + Vector3.up * v.Height;
                camPos = pivot - rot * Vector3.forward * v.Distance;
                float camGround = gen.HeightAt(camPos.x + originX, camPos.z + originZ);
                if (camPos.y < camGround + 1f) camPos.y = camGround + 1f;
            }
            cam.transform.SetPositionAndRotation(camPos, rot);
            CaptureViews.BeforeRender(env, v, cam);

            const int width = 1600, height = 900;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // second render: shadow maps and depth texture are warm
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string file = Path.Combine(dir, v.Name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            CaptureViews.AfterRender(env, v);

            TerrainSample s = gen.Sample(v.X, v.Z);
            Debug.Log("[Relic.World] Capture " + index + " " + v.Name + " at (" + v.X.ToString("0") + ", " + v.Z.ToString("0") + ") region " + s.Family + " biome " + s.Biome + " -> " + file);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(root);
            foreach (Mesh m in meshes) Object.DestroyImmediate(m);
        }
    }
}
