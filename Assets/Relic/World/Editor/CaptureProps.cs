using System;
using System.Collections.Generic;
using Relic.World.Chunks;
using Relic.World.Regions;
using Relic.World.Runtime;
using Relic.World.Terrain;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Relic.World.Editor
{
    /// <summary>
    /// Props in captures: the same placement and meshes as the runtime, but drawn as plain
    /// renderers (batchmode stills can't rely on per-frame instanced draws). Plus a capture set
    /// that visits each region family to show its foliage.
    /// </summary>
    internal static partial class CaptureViews
    {
        static PropMeshes _meshes;
        static Material _propMat, _grassMat;

        public static void AddPropViews(TerrainGenerator gen, List<CaptureView> views)
        {
            var wanted = new[] { RegionFamily.Forest, RegionFamily.Meadow, RegionFamily.Badlands, RegionFamily.Frost, RegionFamily.Highlands, RegionFamily.Coast };
            foreach (RegionFamily f in wanted)
            {
                // A dry, gentle spot deep inside the nearest region of this family.
                if (!FindBest(gen, 4500.0, 24.0, (x, z) =>
                    {
                        RegionSample r = gen.Regions.Sample(x, z);
                        if (r.Family != f || r.W0 < 0.9f) return double.MinValue;
                        gen.HeightAndWater(x, z, out float h, out float w);
                        if (h < w + 2f || Slope(gen, x, z) > 0.35) return double.MinValue;
                        return -Math.Sqrt(x * x + z * z);
                    }, out double px, out double pz)) continue;
                string name = "p_" + f.ToString().ToLowerInvariant();
                views.Add(new CaptureView { Name = name + "_hero", X = px, Z = pz, Yaw = 30f, Pitch = 10f, Distance = 6f, Height = 1.5f, Hero = true, Props = true, ViewDistance = 1100f });
                views.Add(new CaptureView { Name = name + "_wide", X = px, Z = pz, Yaw = 210f, Pitch = 9f, Distance = 40f, Height = 14f, Props = true, ViewDistance = 1300f });
            }
        }

        internal static void AfterChunksBuilt(SandboxCapture.CaptureEnv env, CaptureView v, ChunkMeshData[] chunks, Transform root, double originX, double originZ)
        {
            if (!v.Props) return;
            if (_meshes == null)
            {
                _meshes = new PropMeshes();
                _propMat = new Material(Shader.Find("Relic/World/ToonProp")) { enableInstancing = true };
                _grassMat = new Material(Shader.Find("Relic/World/ToonGrass")) { enableInstancing = true };
                _propMat.SetFloat("_WindStrength", 0f);
                _grassMat.SetFloat("_WindStrength", 0f);
                _grassMat.SetFloat("_FadeStart", 1000f);
                _grassMat.SetFloat("_FadeEnd", 1001f);
            }
            var large = new PropInstance[PropScatter.MaxPerChunk];
            var small = new PropInstance[PropScatter.MaxSmallPerChunk];
            int placed = 0;
            foreach (ChunkMeshData d in chunks)
            {
                double cx = ChunkMath.OriginX(d.Coord) + 32.0 - v.X, cz = ChunkMath.OriginZ(d.Coord) + 32.0 - v.Z;
                double dist = Math.Sqrt(cx * cx + cz * cz);
                if (dist > 520.0) continue;
                var chunkPos = new Vector3((float)(ChunkMath.OriginX(d.Coord) - originX), 0f, (float)(ChunkMath.OriginZ(d.Coord) - originZ));
                int n = PropScatter.Place(env.Gen, d.Coord, large);
                placed += Spawn(large, n, chunkPos, root, dist < 160.0, dist);
                if (dist < 120.0)
                {
                    int m = PropScatter.PlaceSmall(env.Gen, d.Coord, small);
                    placed += Spawn(small, m, chunkPos, root, true, dist);
                }
            }
            Debug.Log("[Relic.World] Capture " + v.Name + ": " + placed + " props");
        }

        static int Spawn(PropInstance[] items, int n, Vector3 chunkPos, Transform root, bool near, double chunkDist)
        {
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                PropInstance p = items[i];
                PropClass cls = PropKinds.ClassOf(p.Kind);
                if (cls == PropClass.Ground && chunkDist > 90.0) continue;
                if (cls == PropClass.Medium && chunkDist > 170.0) continue;
                Mesh mesh = near || cls != PropClass.Large ? _meshes.Near[(int)p.Kind] : _meshes.Far[(int)p.Kind];
                var go = new GameObject("Prop");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(chunkPos + new Vector3(p.X, p.Y, p.Z), Quaternion.Euler(0f, p.Yaw, 0f));
                go.transform.localScale = Vector3.one * p.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = cls == PropClass.Ground ? _grassMat : _propMat;
                r.shadowCastingMode = cls == PropClass.Ground || !near ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                count++;
            }
            return count;
        }
    }
}
