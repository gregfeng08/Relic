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
    /// <summary>
    /// Renders still screenshots of the generated world without entering play mode, for reviews
    /// and for checking the look from batchmode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Relic.World.Editor.SandboxCapture.Run -captureDir C:/tmp/shots [-captureSeed 1337]
    /// Builds chunks synchronously around each viewpoint (same mesh builder as the streamer),
    /// places the placeholder hero, and saves PNGs.
    /// </summary>
    public static class SandboxCapture
    {
        [MenuItem("Relic/World/Capture Screenshots")]
        public static void RunMenu() => Capture(Path.Combine(Application.dataPath, "../Temp/RelicCaptures"), 1337);

        public static void Run()
        {
            string dir = Arg("-captureDir") ?? Path.Combine(Application.dataPath, "../Temp/RelicCaptures");
            ulong seed = ulong.TryParse(Arg("-captureSeed"), out ulong s) ? s : 1337UL;
            try
            {
                Capture(dir, seed);
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

        struct View
        {
            public string Name;
            public double X, Z;
            public float Yaw, Pitch, Distance, Height;
            public bool Hero;
        }

        public static void Capture(string dir, ulong seed)
        {
            Directory.CreateDirectory(dir);
            SandboxSceneBuilder.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/ToonTerrain.mat");
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/Water.mat");
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/SkyGradient.mat");
            var toonMat = AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.MaterialsDir + "/ToonCharacter.mat");

            var gen = new TerrainGenerator(seed);
            gen.FindSpawn(out double sx, out double sz);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            var sky = sunGo.AddComponent<SkyController>();
            sky.Sun = sun;
            sky.SkyMaterial = skyMat;
            sky.FogStart = 380f;
            sky.FogEnd = 1490f;
            sky.Apply();
            Shader.SetGlobalVector("_RelicWorldOffset", Vector4.zero);

            var views = new List<View>
            {
                new View { Name = "01_spawn_hero", X = sx, Z = sz, Yaw = 40f, Pitch = 12f, Distance = 5.5f, Height = 1.55f, Hero = true },
                new View { Name = "02_spawn_vista", X = sx, Z = sz, Yaw = 200f, Pitch = 8f, Distance = 30f, Height = 25f },
            };
            AddFamilyViews(gen, views);

            int index = 0;
            foreach (View v in views)
            {
                RenderView(gen, v, dir, terrainMat, waterMat, toonMat, ++index);
            }
            Debug.Log("[Relic.World] Captured " + views.Count + " screenshots to " + dir);
        }

        static void AddFamilyViews(TerrainGenerator gen, List<View> views)
        {
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
                views.Add(new View { Name = "03_" + kv.Key.ToString().ToLowerInvariant(), X = bx, Z = bz, Yaw = yaw, Pitch = 6f, Distance = 1f, Height = 22f });
            }
        }

        static void RenderView(TerrainGenerator gen, View v, string dir, Material terrainMat, Material waterMat, Material toonMat, int index)
        {
            var root = new GameObject("CaptureRoot");
            double originX = ChunkMath.ChunkIndex(v.X) * (double)ChunkMath.ChunkSize;
            double originZ = ChunkMath.ChunkIndex(v.Z) * (double)ChunkMath.ChunkSize;
            var meshes = new List<Mesh>();

            // Build every chunk within view distance at the LOD the streamer would choose.
            LodSettings lod = LodSettings.Default;
            ChunkOffset[] plan = LodPlan.Build(lod);
            ChunkCoord anchor = ChunkMath.ChunkAt(v.X, v.Z);
            var datas = new ChunkMeshData[plan.Length];
            Parallel.For(0, plan.Length, i =>
            {
                var d = new ChunkMeshData(plan[i].Lod) { Coord = new ChunkCoord(anchor.X + plan[i].Dx, anchor.Z + plan[i].Dz) };
                ChunkMeshBuilder.Build(gen, d);
                datas[i] = d;
            });
            foreach (ChunkMeshData d in datas)
            {
                var go = new GameObject("Chunk");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3((float)(ChunkMath.OriginX(d.Coord) - originX), 0f, (float)(ChunkMath.OriginZ(d.Coord) - originZ));
                var m = new Mesh();
                m.SetVertices(d.Positions, 0, d.VertexCount);
                m.SetNormals(d.Normals, 0, d.VertexCount);
                m.SetColors(d.Colors, 0, d.VertexCount);
                m.SetUVs(0, d.Rock, 0, d.VertexCount);
                m.SetIndices(ChunkMeshBuilder.IndicesFor(d.Lod), MeshTopology.Triangles, 0, true);
                meshes.Add(m);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = terrainMat;
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
                    wr.sharedMaterial = waterMat;
                    wr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            float ground = gen.HeightAt(v.X, v.Z);
            float water = gen.WaterLevelAt(v.X, v.Z);
            var focus = new Vector3((float)(v.X - originX), Mathf.Max(ground, water), (float)(v.Z - originZ));

            PlayerAvatar avatar = null;
            if (v.Hero)
            {
                var hero = new GameObject("Hero");
                hero.transform.SetParent(root.transform, false);
                hero.transform.position = focus;
                hero.transform.rotation = Quaternion.Euler(0f, v.Yaw + 160f, 0f);
                avatar = hero.AddComponent<PlayerAvatar>();
                avatar.Build(toonMat);
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
            Quaternion rot = Quaternion.Euler(v.Pitch, v.Yaw, 0f);
            Vector3 pivot = focus + Vector3.up * v.Height;
            Vector3 camPos = pivot - rot * Vector3.forward * v.Distance;
            float camGround = gen.HeightAt(camPos.x + originX, camPos.z + originZ);
            if (camPos.y < camGround + 1f) camPos.y = camGround + 1f;
            cam.transform.SetPositionAndRotation(camPos, rot);

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

            TerrainSample s = gen.Sample(v.X, v.Z);
            Debug.Log("[Relic.World] Capture " + index + " " + v.Name + " at (" + v.X.ToString("0") + ", " + v.Z.ToString("0") + ") region " + s.Family + " biome " + s.Biome + " -> " + file);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(root);
            foreach (Mesh m in meshes) Object.DestroyImmediate(m);
        }
    }
}
