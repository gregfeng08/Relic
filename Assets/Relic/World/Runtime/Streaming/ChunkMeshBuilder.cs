using System.Diagnostics;
using Relic.World.Chunks;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Reusable buffers for one chunk mesh at one LOD. Filled on a worker thread, uploaded on the
    /// main thread, then returned to a pool. Only plain arrays and Unity value types are touched
    /// off the main thread.
    /// </summary>
    internal sealed class ChunkMeshData
    {
        public readonly int Lod;
        public readonly int Side;          // vertices per side
        public ChunkCoord Coord;
        public int Epoch;
        public int Ticket;

        public readonly Vector3[] Positions;
        public readonly Vector3[] Normals;
        public readonly Color32[] Colors;
        public readonly Vector3[] Rock;
        public readonly int VertexCount;
        public float MinY, MaxY;

        public readonly float[] Heights;   // (Side + 2)^2, includes a one-vertex border for normals
        public readonly float[] Water;     // Side^2
        /// <summary>Terrain triangles: each cell split along its flatter diagonal, then the skirts.</summary>
        public readonly int[] Indices;

        public bool HasWater;
        public readonly Vector3[] WaterPositions;
        public readonly Color32[] WaterColors;
        public readonly int[] WaterIndices;
        public int WaterIndexCount;
        public float WaterMinY, WaterMaxY;

        public double BuildMs;
        public System.Exception Error;

        public ChunkMeshData(int lod)
        {
            Lod = lod;
            Side = ChunkMath.VerticesPerSide(lod);
            VertexCount = Side * Side + 4 * Side;
            Positions = new Vector3[VertexCount];
            Normals = new Vector3[VertexCount];
            Colors = new Color32[VertexCount];
            Rock = new Vector3[VertexCount];
            Heights = new float[(Side + 2) * (Side + 2)];
            Water = new float[Side * Side];
            WaterPositions = new Vector3[Side * Side];
            WaterColors = new Color32[Side * Side];
            WaterIndices = new int[(Side - 1) * (Side - 1) * 6];
            Indices = (int[])ChunkMeshBuilder.IndicesFor(lod).Clone();
        }
    }

    /// <summary>Builds terrain and water mesh data for a chunk from the pure height function.</summary>
    internal static class ChunkMeshBuilder
    {
        static readonly int[][] TerrainIndices = new int[ChunkMath.MaxLod + 1][];

        static ChunkMeshBuilder()
        {
            for (int lod = 0; lod <= ChunkMath.MaxLod; lod++) TerrainIndices[lod] = BuildIndices(ChunkMath.VerticesPerSide(lod));
        }

        public static int[] IndicesFor(int lod) => TerrainIndices[lod];

        /// <summary>Writes the chunk's terrain into a mesh (main thread). Creates the mesh when m is null.</summary>
        public static Mesh CreateTerrainMesh(ChunkMeshData d, Mesh m = null)
        {
            if (m == null) m = new Mesh { name = "TerrainChunk" };
            else m.Clear();
            m.SetVertices(d.Positions, 0, d.VertexCount);
            m.SetNormals(d.Normals, 0, d.VertexCount);
            m.SetColors(d.Colors, 0, d.VertexCount);
            m.SetUVs(0, d.Rock, 0, d.VertexCount);
            m.SetIndices(d.Indices, MeshTopology.Triangles, 0, false);
            float size = ChunkMath.ChunkSize;
            m.bounds = new Bounds(new Vector3(size * 0.5f, (d.MinY + d.MaxY) * 0.5f, size * 0.5f), new Vector3(size, d.MaxY - d.MinY + 0.1f, size));
            return m;
        }

        /// <summary>Fills d for d.Coord at d.Lod. Thread-safe (the generator is immutable).</summary>
        public static void Build(TerrainGenerator gen, ChunkMeshData d)
        {
            var sw = Stopwatch.StartNew();
            int n = d.Side;
            int g = n + 2;
            int lod = d.Lod;
            float step = ChunkMath.StepMeters(lod);
            long cx = d.Coord.X, cz = d.Coord.Z;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minW = float.MaxValue, maxW = float.MinValue;
            bool anyWater = false;

            // Interior: full samples (height, water, colors).
            for (int j = 0; j < n; j++)
            {
                double z = ChunkMath.VertexWorld(cz, j, lod);
                for (int i = 0; i < n; i++)
                {
                    double x = ChunkMath.VertexWorld(cx, i, lod);
                    TerrainSample s = gen.Sample(x, z);
                    int k = i + j * n;
                    d.Heights[(i + 1) + (j + 1) * g] = s.Height;
                    d.Water[k] = s.WaterLevel;
                    d.Positions[k] = new Vector3(i * step, s.Height, j * step);
                    d.Colors[k] = ToColor32(s.Ground, s.Snow); // alpha: snow cover
                    d.Rock[k] = new Vector3(s.Rock.R, s.Rock.G, s.Rock.B);
                    if (s.Height < minY) minY = s.Height;
                    if (s.Height > maxY) maxY = s.Height;
                    if (s.Height < s.WaterLevel) anyWater = true;
                }
            }

            // Border ring: heights only, so normals match the neighbor chunk exactly.
            for (int b = -1; b <= n; b++)
            {
                SetBorder(gen, d, cx, cz, lod, b, -1, g);
                SetBorder(gen, d, cx, cz, lod, b, n, g);
                if (b >= 0 && b < n)
                {
                    SetBorder(gen, d, cx, cz, lod, -1, b, g);
                    SetBorder(gen, d, cx, cz, lod, n, b, g);
                }
            }

            // Normals from central differences.
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    int c = (i + 1) + (j + 1) * g;
                    float hl = d.Heights[c - 1], hr = d.Heights[c + 1];
                    float hd = d.Heights[c - g], hu = d.Heights[c + g];
                    d.Normals[i + j * n] = new Vector3(hl - hr, 2f * step, hd - hu).normalized;
                }
            }

            Triangulate(d);

            // Skirts: a copy of each edge vertex pushed down, same normal and color.
            float skirt = ChunkMath.SkirtDepth(lod);
            int baseIndex = n * n;
            for (int e = 0; e < 4; e++)
            {
                for (int k = 0; k < n; k++)
                {
                    int src = EdgeVertex(e, k, n);
                    int dst = baseIndex + e * n + k;
                    Vector3 p = d.Positions[src];
                    p.y -= skirt;
                    d.Positions[dst] = p;
                    d.Normals[dst] = d.Normals[src];
                    d.Colors[dst] = d.Colors[src];
                    d.Rock[dst] = d.Rock[src];
                }
            }
            d.MinY = minY - skirt;
            d.MaxY = maxY;

            // Water: a grid at the local water level, only quads that touch water.
            d.HasWater = false;
            d.WaterIndexCount = 0;
            if (anyWater)
            {
                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int k = i + j * n;
                        float w = d.Water[k];
                        float h = d.Positions[k].y;
                        d.WaterPositions[k] = new Vector3(i * step, w, j * step);
                        byte depth = (byte)(Mathf.Clamp01((w - h) / 10f) * 255f);
                        d.WaterColors[k] = new Color32(depth, 0, 0, 255);
                        if (w < minW) minW = w;
                        if (w > maxW) maxW = w;
                    }
                }
                int wi = 0;
                for (int j = 0; j < n - 1; j++)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        int a = i + j * n, b2 = a + n, c = a + n + 1, e = a + 1;
                        if (d.Positions[a].y < d.Water[a] || d.Positions[b2].y < d.Water[b2] ||
                            d.Positions[c].y < d.Water[c] || d.Positions[e].y < d.Water[e])
                        {
                            d.WaterIndices[wi++] = a; d.WaterIndices[wi++] = b2; d.WaterIndices[wi++] = c;
                            d.WaterIndices[wi++] = a; d.WaterIndices[wi++] = c; d.WaterIndices[wi++] = e;
                        }
                    }
                }
                d.WaterIndexCount = wi;
                d.HasWater = wi > 0;
                d.WaterMinY = minW;
                d.WaterMaxY = maxW;
            }

            d.BuildMs = sw.Elapsed.TotalMilliseconds;
        }

        static void SetBorder(TerrainGenerator gen, ChunkMeshData d, long cx, long cz, int lod, int i, int j, int g)
        {
            double x = ChunkMath.VertexWorld(cx, i, lod);
            double z = ChunkMath.VertexWorld(cz, j, lod);
            d.Heights[(i + 1) + (j + 1) * g] = gen.HeightAt(x, z);
        }

        static Color32 ToColor32(Rgb c, float a) =>
            new Color32((byte)(Mathf.Clamp01(c.R) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.G) * 255f + 0.5f), (byte)(Mathf.Clamp01(c.B) * 255f + 0.5f), (byte)(Mathf.Clamp01(a) * 255f + 0.5f));

        /// <summary>Edge e (0 south, 1 north, 2 west, 3 east), k-th vertex along it.</summary>
        static int EdgeVertex(int e, int k, int n)
        {
            switch (e)
            {
                case 0: return k;                  // j = 0
                case 1: return k + (n - 1) * n;    // j = n-1
                case 2: return k * n;              // i = 0
                default: return (n - 1) + k * n;   // i = n-1
            }
        }

        /// <summary>
        /// Splits each grid cell along its flatter diagonal (the two corners with the closest
        /// heights). A fixed diagonal cuts across crests, valleys and terrace edges that run the
        /// other way and turns them into a sawtooth of hard triangle ridges; following the
        /// surface keeps them straight. Uses only the cell's own heights, so neighbours and
        /// colliders agree. Skirt indices (after the cells) are left as built.
        /// </summary>
        static void Triangulate(ChunkMeshData d)
        {
            int n = d.Side;
            int[] idx = d.Indices;
            Vector3[] p = d.Positions;
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = i + j * n, b = a + n, c = a + n + 1, e = a + 1;
                    float ac = Mathf.Abs(p[a].y - p[c].y), be = Mathf.Abs(p[b].y - p[e].y);
                    if (ac <= be)
                    {
                        idx[t++] = a; idx[t++] = b; idx[t++] = c;
                        idx[t++] = a; idx[t++] = c; idx[t++] = e;
                    }
                    else
                    {
                        idx[t++] = a; idx[t++] = b; idx[t++] = e;
                        idx[t++] = b; idx[t++] = c; idx[t++] = e;
                    }
                }
            }
        }

        static int[] BuildIndices(int n)
        {
            int quads = (n - 1) * (n - 1);
            int skirtQuads = 4 * (n - 1);
            var idx = new int[quads * 6 + skirtQuads * 12];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a = i + j * n, b = a + n, c = a + n + 1, d = a + 1;
                    // Clockwise seen from above (Unity front faces).
                    idx[t++] = a; idx[t++] = b; idx[t++] = c;
                    idx[t++] = a; idx[t++] = c; idx[t++] = d;
                }
            }
            int baseIndex = n * n;
            for (int e = 0; e < 4; e++)
            {
                for (int k = 0; k < n - 1; k++)
                {
                    int top0 = EdgeVertex(e, k, n), top1 = EdgeVertex(e, k + 1, n);
                    int bot0 = baseIndex + e * n + k, bot1 = bot0 + 1;
                    // Two-sided so the skirt hides cracks seen from either side.
                    idx[t++] = top0; idx[t++] = top1; idx[t++] = bot1;
                    idx[t++] = top0; idx[t++] = bot1; idx[t++] = bot0;
                    idx[t++] = top0; idx[t++] = bot1; idx[t++] = top1;
                    idx[t++] = top0; idx[t++] = bot0; idx[t++] = bot1;
                }
            }
            return idx;
        }
    }
}
