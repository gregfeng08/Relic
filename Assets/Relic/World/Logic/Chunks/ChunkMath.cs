using System;
using System.Collections.Generic;

namespace Relic.World.Chunks
{
    /// <summary>Integer chunk coordinates. Chunk (x, z) covers [x*64, x*64+64) on each axis.</summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly long X;
        public readonly long Z;

        public ChunkCoord(long x, long z) { X = x; Z = z; }

        public bool Equals(ChunkCoord o) => X == o.X && Z == o.Z;
        public override bool Equals(object obj) => obj is ChunkCoord o && Equals(o);
        public override int GetHashCode() => unchecked((int)(X * 73856093L ^ Z * 83492791L));
        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
        public override string ToString() => "[" + X + ", " + Z + "]";
    }

    /// <summary>Chunk grid, LOD resolution and world-space math (pure, deterministic).</summary>
    public static class ChunkMath
    {
        public const int ChunkSize = 64;
        public const int MaxLod = 3;

        /// <summary>Vertices per side at a LOD: 65, 33, 17, 9.</summary>
        public static int VerticesPerSide(int lod) => (ChunkSize >> lod) + 1;

        /// <summary>Quads per side at a LOD: 64, 32, 16, 8.</summary>
        public static int QuadsPerSide(int lod) => ChunkSize >> lod;

        /// <summary>Vertex spacing in meters at a LOD: 1, 2, 4, 8.</summary>
        public static int StepMeters(int lod) => 1 << lod;

        public static long ChunkIndex(double worldCoord) => WorldMath.FloorToLong(worldCoord / ChunkSize);

        public static ChunkCoord ChunkAt(double x, double z) => new ChunkCoord(ChunkIndex(x), ChunkIndex(z));

        public static double OriginX(ChunkCoord c) => (double)c.X * ChunkSize;
        public static double OriginZ(ChunkCoord c) => (double)c.Z * ChunkSize;

        /// <summary>
        /// World coordinate of vertex i along one axis. Integer arithmetic, so a shared edge
        /// evaluates the exact same coordinates in both neighbors (no seams).
        /// </summary>
        public static double VertexWorld(long chunkIndex, int vertex, int lod) => (double)(chunkIndex * ChunkSize + (long)vertex * StepMeters(lod));

        /// <summary>Squared distance in meters from a point to the nearest point of a chunk's square.</summary>
        public static double DistanceSqToChunk(ChunkCoord c, double x, double z)
        {
            double minX = OriginX(c), minZ = OriginZ(c);
            double dx = x < minX ? minX - x : x > minX + ChunkSize ? x - (minX + ChunkSize) : 0.0;
            double dz = z < minZ ? minZ - z : z > minZ + ChunkSize ? z - (minZ + ChunkSize) : 0.0;
            return dx * dx + dz * dz;
        }

        /// <summary>Skirt depth in meters for a LOD: deep enough to hide cracks against the next coarser LOD.</summary>
        public static float SkirtDepth(int lod) => 2f + 4f * StepMeters(lod);
    }

    /// <summary>Distances (meters) at which each LOD ends. A chunk beyond the last is not loaded.</summary>
    public struct LodSettings
    {
        public float Lod0Distance;
        public float Lod1Distance;
        public float Lod2Distance;
        public float ViewDistance;
        /// <summary>Chunks closer than this (to the player) get a collider.</summary>
        public float ColliderDistance;

        public static LodSettings Default => new LodSettings
        {
            Lod0Distance = 160f,
            Lod1Distance = 384f,
            Lod2Distance = 768f,
            ViewDistance = 1536f,
            ColliderDistance = 72f,
        };

        public int LodForDistance(double meters)
        {
            if (meters < Lod0Distance) return 0;
            if (meters < Lod1Distance) return 1;
            if (meters < Lod2Distance) return 2;
            return 3;
        }
    }

    /// <summary>One wanted chunk relative to the anchor chunk, with its LOD.</summary>
    public struct ChunkOffset
    {
        public int Dx, Dz, Lod;
        public float Distance;
    }

    /// <summary>
    /// Precomputed set of chunk offsets around an anchor chunk, sorted near to far, with the
    /// LOD each should use. Distance is from the anchor chunk's center to the chunk's nearest point.
    /// </summary>
    public static class LodPlan
    {
        public static ChunkOffset[] Build(LodSettings s)
        {
            int r = (int)Math.Ceiling(s.ViewDistance / ChunkMath.ChunkSize) + 1;
            var list = new List<ChunkOffset>();
            double half = ChunkMath.ChunkSize * 0.5;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    var c = new ChunkCoord(dx, dz);
                    double d = Math.Sqrt(ChunkMath.DistanceSqToChunk(c, half, half));
                    if (d > s.ViewDistance) continue;
                    list.Add(new ChunkOffset { Dx = dx, Dz = dz, Lod = s.LodForDistance(d), Distance = (float)d });
                }
            }
            list.Sort((a, b) =>
            {
                int k = a.Distance.CompareTo(b.Distance);
                if (k != 0) return k;
                k = a.Dx.CompareTo(b.Dx);
                return k != 0 ? k : a.Dz.CompareTo(b.Dz);
            });
            return list.ToArray();
        }
    }

    /// <summary>
    /// Floating-origin math. The scene origin sits at a world position (OriginX, OriginZ) that
    /// is always a multiple of the chunk size; scene position = world position - origin.
    /// Only X and Z shift: terrain heights stay within a few hundred meters, so Y never needs it.
    /// </summary>
    public static class FloatingOriginMath
    {
        public const double DefaultThreshold = 4000.0;

        public static bool ShouldRecenter(double sceneX, double sceneZ, double threshold = DefaultThreshold)
            => sceneX * sceneX + sceneZ * sceneZ > threshold * threshold;

        /// <summary>The new origin for a player at the given world position: snapped to the chunk grid.</summary>
        public static void NewOrigin(double worldX, double worldZ, out double originX, out double originZ)
        {
            originX = ChunkMath.ChunkIndex(worldX) * (double)ChunkMath.ChunkSize;
            originZ = ChunkMath.ChunkIndex(worldZ) * (double)ChunkMath.ChunkSize;
        }
    }
}
