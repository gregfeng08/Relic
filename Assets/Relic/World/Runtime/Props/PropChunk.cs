using System.Collections.Generic;
using Relic.World.Chunks;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// The props of one 64 m chunk, ready to draw: instance matrices grouped by kind (scene space,
    /// moved on floating-origin shifts), the raw instances (for colliders) and a bounding box.
    /// Large props (trees, boulders, bushes) exist for every chunk in view; ground cover only
    /// near the player.
    /// </summary>
    internal sealed class PropChunk
    {
        public ChunkCoord Coord;
        public int Stamp;
        public bool LargeRequested, LargeReady;
        public bool SmallRequested, SmallReady;
        public int SmallTicket;
        /// <summary>Bumped whenever the record is reused for another chunk, so stale results are dropped.</summary>
        public int Generation;

        public PropInstance[] Large = new PropInstance[0];
        public int LargeCount;

        /// <summary>Per kind: matrices of that kind in this chunk (null when none). Large and small share the table.</summary>
        public readonly Matrix4x4[][] Matrices = new Matrix4x4[PropKinds.Count][];
        public readonly int[] Counts = new int[PropKinds.Count];
        public Bounds Bounds;

        public void Reset(ChunkCoord c)
        {
            Coord = c;
            Generation++;
            LargeRequested = LargeReady = SmallRequested = SmallReady = false;
            LargeCount = 0;
            SmallTicket++;
            for (int k = 0; k < Counts.Length; k++) Counts[k] = 0;
        }

        /// <summary>Adds instances (chunk-local positions) as scene-space matrices.</summary>
        public void AddInstances(PropInstance[] items, int count, FloatingOrigin origin)
        {
            double ox = ChunkMath.OriginX(Coord), oz = ChunkMath.OriginZ(Coord);
            Vector3 chunkScene = origin.ToScene(ox, 0.0, oz);
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                PropInstance p = items[i];
                int k = (int)p.Kind;
                Matrix4x4[] arr = Matrices[k];
                if (arr == null || Counts[k] >= arr.Length)
                {
                    var bigger = new Matrix4x4[arr == null ? 16 : arr.Length * 2];
                    if (arr != null) System.Array.Copy(arr, bigger, Counts[k]);
                    Matrices[k] = arr = bigger;
                }
                var pos = new Vector3(chunkScene.x + p.X, p.Y, chunkScene.z + p.Z);
                arr[Counts[k]++] = Matrix4x4.TRS(pos, Quaternion.Euler(0f, p.Yaw, 0f), Vector3.one * p.Scale);
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            if (count > 0) Encapsulate(chunkScene, minY, maxY + 14f);
        }

        /// <summary>Drops ground cover (far from the player again) but keeps the large props.</summary>
        public void ClearSmall()
        {
            for (int k = 0; k < Counts.Length; k++)
                if (PropKinds.ClassOf((PropKind)k) == PropClass.Ground || IsSmallMedium((PropKind)k)) Counts[k] = 0;
            SmallReady = SmallRequested = false;
            SmallTicket++;
        }

        /// <summary>Medium props that come from the ground-cover pass (bushes come from the large pass).</summary>
        public static bool IsSmallMedium(PropKind k) => k == PropKind.Rock || k == PropKind.DeadShrub || k == PropKind.Succulent;

        void Encapsulate(Vector3 chunkScene, float minY, float maxY)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(chunkScene.x, minY, chunkScene.z), new Vector3(chunkScene.x + ChunkMath.ChunkSize, maxY, chunkScene.z + ChunkMath.ChunkSize));
            if (Bounds.size == Vector3.zero) Bounds = b;
            else Bounds.Encapsulate(b);
        }

        public void Shift(Vector3 delta)
        {
            for (int k = 0; k < Counts.Length; k++)
            {
                Matrix4x4[] arr = Matrices[k];
                for (int i = 0; i < Counts[k]; i++)
                {
                    arr[i].m03 -= delta.x;
                    arr[i].m23 -= delta.z;
                }
            }
            Bounds.center -= new Vector3(delta.x, 0f, delta.z);
        }

        public void ClearAll()
        {
            for (int k = 0; k < Counts.Length; k++) Counts[k] = 0;
            Bounds = new Bounds();
            LargeCount = 0;
        }
    }

    /// <summary>A placement job for the prop workers.</summary>
    internal sealed class PropJob
    {
        public ChunkCoord Coord;
        public bool Small;
        public int Ticket;
        public PropInstance[] Items;
        public int Count;
        public System.Exception Error;
    }

    internal static class PropJobPool
    {
        static readonly Stack<PropJob> Pool = new Stack<PropJob>();

        public static PropJob Rent(bool small)
        {
            lock (Pool)
            {
                while (Pool.Count > 0)
                {
                    PropJob j = Pool.Pop();
                    if ((j.Items.Length == PropScatter.MaxSmallPerChunk) == small) return j;
                }
            }
            return new PropJob { Items = new PropInstance[small ? PropScatter.MaxSmallPerChunk : PropScatter.MaxPerChunk] };
        }

        public static void Return(PropJob j)
        {
            j.Error = null;
            lock (Pool) { if (Pool.Count < 64) Pool.Push(j); }
        }
    }
}
