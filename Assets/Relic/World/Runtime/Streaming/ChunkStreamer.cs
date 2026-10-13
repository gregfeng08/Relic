using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Relic.World.Chunks;
using Relic.World.Terrain;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Streams 64 m terrain chunks around a target: decides the wanted chunks and LODs (near to
    /// far), builds meshes on background threads from the pure height function, uploads a few per
    /// frame within a time budget, bakes colliders for the nearest chunks with the job system, and
    /// re-places chunks when the floating origin moves. An old LOD stays visible until its
    /// replacement is ready, so there are no holes while streaming.
    /// </summary>
    public sealed class ChunkStreamer : MonoBehaviour
    {
        [Tooltip("Main-thread milliseconds per frame allowed for mesh uploads.")]
        public float UploadBudgetMs = 2.0f;
        [Tooltip("Hard cap on uploads per frame.")]
        public int MaxUploadsPerFrame = 16;
        [Tooltip("The anchor chunk changes only once the target is this far (m) outside it, so LODs don't flap on a border.")]
        public float AnchorHysteresis = 16f;

        public LodSettings Lod = LodSettings.Default;

        // --- Stats (read by the debug overlay and perf log) ---
        public int LoadedChunks => _chunks.Count;
        public int VisibleChunks { get; private set; }
        public int InFlight => _inFlight;
        public int PendingWork { get; private set; }
        public int CollidersActive { get; private set; }
        public long TotalBuilt { get; private set; }
        public double TotalBuildMs { get; private set; }
        public double MaxBuildMs { get; private set; }
        public float LastUploadMs { get; private set; }
        public float MaxUploadMs { get; private set; }
        public int WorkerCount { get; private set; }

        TerrainGenerator _gen;
        FloatingOrigin _origin;
        Transform _target;
        Material _terrainMat;
        Material _waterMat;

        ChunkOffset[] _plan;
        readonly Dictionary<ChunkCoord, TerrainChunk> _chunks = new Dictionary<ChunkCoord, TerrainChunk>();
        readonly List<TerrainChunk> _ordered = new List<TerrainChunk>();
        readonly List<TerrainChunk> _scratch = new List<TerrainChunk>();
        readonly List<TerrainChunk> _withCollider = new List<TerrainChunk>();
        readonly Stack<TerrainChunk> _chunkPool = new Stack<TerrainChunk>();
        readonly Stack<ChunkMeshData>[] _dataPools = new Stack<ChunkMeshData>[ChunkMath.MaxLod + 1];

        readonly ConcurrentQueue<ChunkMeshData> _results = new ConcurrentQueue<ChunkMeshData>();
        BlockingCollection<ChunkMeshData> _jobs;
        CancellationTokenSource _cts;
        Thread[] _workers;
        int _inFlight;
        int _maxInFlight;
        int _epoch;
        int _ticket;
        int _stamp;
        bool _hasAnchor;
        ChunkCoord _anchor;
        Transform _root;
        bool _loggedError;

        const MeshColliderCookingOptions Cooking =
            MeshColliderCookingOptions.CookForFasterSimulation |
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.UseFastMidphase;

        public void Initialize(TerrainGenerator generator, FloatingOrigin origin, Transform target, Material terrain, Material water)
        {
            _gen = generator;
            _origin = origin;
            _target = target;
            _terrainMat = terrain;
            _waterMat = water;
            _plan = LodPlan.Build(Lod);
            for (int i = 0; i < _dataPools.Length; i++) _dataPools[i] = new Stack<ChunkMeshData>();
            _root = new GameObject("Terrain Chunks").transform;
            _root.SetParent(transform, false);
            _origin.Shifted += OnOriginShifted;
            StartWorkers();
        }

        public TerrainGenerator Generator => _gen;

        /// <summary>True when the chunk under a scene position is at LOD0 with its collider ready.</summary>
        public bool HasColliderAt(Vector3 scenePos)
        {
            _origin.ToWorld(scenePos, out double wx, out double wz);
            return _chunks.TryGetValue(ChunkMath.ChunkAt(wx, wz), out var c) && c.ColliderReady;
        }

        /// <summary>True once every wanted chunk shows its wanted LOD.</summary>
        public bool IsSettled => _hasAnchor && PendingWork == 0 && _inFlight == 0;

        void StartWorkers()
        {
            WorkerCount = Mathf.Clamp(SystemInfo.processorCount - 2, 2, 8);
            _maxInFlight = WorkerCount * 3;
            _jobs = new BlockingCollection<ChunkMeshData>(new ConcurrentQueue<ChunkMeshData>());
            _cts = new CancellationTokenSource();
            _workers = new Thread[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                var t = new Thread(WorkerLoop) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "Relic.ChunkWorker." + i };
                _workers[i] = t;
                t.Start();
            }
        }

        void WorkerLoop()
        {
            var gen = _gen;
            var jobs = _jobs;
            var token = _cts.Token;
            try
            {
                foreach (ChunkMeshData d in jobs.GetConsumingEnumerable(token))
                {
                    try { ChunkMeshBuilder.Build(gen, d); }
                    catch (Exception e) { d.Error = e; }
                    _results.Enqueue(d);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        void Update()
        {
            if (_gen == null || _target == null) return;
            _origin.ToWorld(_target.position, out double wx, out double wz);
            UpdateAnchor(wx, wz);
            ProcessResults();
            Dispatch();
            UpdateColliders(wx, wz);
        }

        void UpdateAnchor(double wx, double wz)
        {
            ChunkCoord pc = ChunkMath.ChunkAt(wx, wz);
            if (_hasAnchor)
            {
                if (pc == _anchor) return;
                double d = Math.Sqrt(ChunkMath.DistanceSqToChunk(_anchor, wx, wz));
                if (d < AnchorHysteresis) return;
            }
            _anchor = pc;
            _hasAnchor = true;
            Replan();
        }

        void Replan()
        {
            _stamp++;
            _ordered.Clear();
            for (int i = 0; i < _plan.Length; i++)
            {
                ChunkOffset o = _plan[i];
                var coord = new ChunkCoord(_anchor.X + o.Dx, _anchor.Z + o.Dz);
                if (!_chunks.TryGetValue(coord, out TerrainChunk c))
                {
                    c = _chunkPool.Count > 0 ? _chunkPool.Pop() : new TerrainChunk();
                    c.Reset(coord);
                    _chunks.Add(coord, c);
                }
                c.DesiredLod = o.Lod;
                c.Stamp = _stamp;
                _ordered.Add(c);
            }

            _scratch.Clear();
            foreach (var kv in _chunks)
                if (kv.Value.Stamp != _stamp) _scratch.Add(kv.Value);
            foreach (TerrainChunk c in _scratch)
            {
                _chunks.Remove(c.Coord);
                ReleaseChunk(c);
            }
            _scratch.Clear();
        }

        void Dispatch()
        {
            int pending = 0;
            int visible = 0;
            for (int i = 0; i < _ordered.Count; i++)
            {
                TerrainChunk c = _ordered[i];
                if (c.Lod >= 0) visible++;
                if (c.Failed || c.Lod == c.DesiredLod) continue;
                if (c.RequestedLod == c.DesiredLod) continue;
                pending++;
                if (_inFlight >= _maxInFlight || c.RequestedLod >= 0) continue;

                ChunkMeshData d = RentData(c.DesiredLod);
                d.Coord = c.Coord;
                d.Epoch = _epoch;
                d.Error = null;
                c.RequestedLod = c.DesiredLod;
                c.Ticket = ++_ticket;
                d.Ticket = c.Ticket;
                _inFlight++;
                _jobs.Add(d);
            }
            PendingWork = pending;
            VisibleChunks = visible;
        }

        void ProcessResults()
        {
            var sw = Stopwatch.StartNew();
            int uploads = 0;
            while (uploads < MaxUploadsPerFrame && sw.Elapsed.TotalMilliseconds < UploadBudgetMs && _results.TryDequeue(out ChunkMeshData d))
            {
                _inFlight--;
                if (d.Error != null)
                {
                    if (!_loggedError) { Debug.LogException(d.Error); _loggedError = true; }
                    if (_chunks.TryGetValue(d.Coord, out TerrainChunk bad) && bad.Ticket == d.Ticket) { bad.Failed = true; bad.RequestedLod = -1; }
                    ReturnData(d);
                    continue;
                }
                TotalBuilt++;
                TotalBuildMs += d.BuildMs;
                if (d.BuildMs > MaxBuildMs) MaxBuildMs = d.BuildMs;

                if (d.Epoch == _epoch && _chunks.TryGetValue(d.Coord, out TerrainChunk c) && c.Ticket == d.Ticket)
                {
                    c.RequestedLod = -1;
                    if (d.Lod == c.DesiredLod)
                    {
                        Upload(c, d);
                        uploads++;
                    }
                }
                ReturnData(d);
            }
            LastUploadMs = (float)sw.Elapsed.TotalMilliseconds;
            if (LastUploadMs > MaxUploadMs) MaxUploadMs = LastUploadMs;
        }

        void Upload(TerrainChunk c, ChunkMeshData d)
        {
            EnsureObjects(c);
            DropCollider(c);

            Mesh m = c.Mesh;
            m.Clear();
            m.SetVertices(d.Positions, 0, d.VertexCount);
            m.SetNormals(d.Normals, 0, d.VertexCount);
            m.SetColors(d.Colors, 0, d.VertexCount);
            m.SetUVs(0, d.Rock, 0, d.VertexCount);
            m.SetIndices(ChunkMeshBuilder.IndicesFor(d.Lod), MeshTopology.Triangles, 0, false);
            float size = ChunkMath.ChunkSize;
            m.bounds = new Bounds(new Vector3(size * 0.5f, (d.MinY + d.MaxY) * 0.5f, size * 0.5f), new Vector3(size, d.MaxY - d.MinY + 0.1f, size));
            c.Renderer.shadowCastingMode = d.Lod <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (d.HasWater)
            {
                EnsureWater(c);
                Mesh w = c.WaterMesh;
                int n = d.Side * d.Side;
                w.Clear();
                w.SetVertices(d.WaterPositions, 0, n);
                w.SetColors(d.WaterColors, 0, n);
                w.SetIndices(d.WaterIndices, 0, d.WaterIndexCount, MeshTopology.Triangles, 0, false, 0);
                w.bounds = new Bounds(new Vector3(size * 0.5f, (d.WaterMinY + d.WaterMaxY) * 0.5f, size * 0.5f), new Vector3(size, d.WaterMaxY - d.WaterMinY + 0.1f, size));
                c.Water.SetActive(true);
            }
            else if (c.Water != null)
            {
                c.Water.SetActive(false);
            }

            c.Lod = d.Lod;
            if (!c.Go.activeSelf) c.Go.SetActive(true);
        }

        void EnsureObjects(TerrainChunk c)
        {
            if (c.Go == null)
            {
                c.Go = new GameObject("Chunk");
                c.Go.transform.SetParent(_root, false);
                c.Filter = c.Go.AddComponent<MeshFilter>();
                c.Renderer = c.Go.AddComponent<MeshRenderer>();
                c.Renderer.sharedMaterial = _terrainMat;
                c.Renderer.receiveShadows = true;
                c.Renderer.lightProbeUsage = LightProbeUsage.Off;
                c.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                c.Mesh = new Mesh { name = "TerrainChunk" };
                c.Mesh.MarkDynamic();
                c.Filter.sharedMesh = c.Mesh;
            }
            c.Go.name = "Chunk " + c.Coord;
            c.Go.transform.localPosition = _origin.ToScene(ChunkMath.OriginX(c.Coord), 0.0, ChunkMath.OriginZ(c.Coord));
        }

        void EnsureWater(TerrainChunk c)
        {
            if (c.Water != null) return;
            c.Water = new GameObject("Water");
            c.Water.layer = 4; // built-in "Water" layer
            c.Water.transform.SetParent(c.Go.transform, false);
            var f = c.Water.AddComponent<MeshFilter>();
            var r = c.Water.AddComponent<MeshRenderer>();
            r.sharedMaterial = _waterMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            c.WaterMesh = new Mesh { name = "WaterChunk" };
            c.WaterMesh.MarkDynamic();
            f.sharedMesh = c.WaterMesh;
        }

        void UpdateColliders(double wx, double wz)
        {
            float cd = Lod.ColliderDistance;
            double cd2 = (double)cd * cd;
            float scanLimit = cd + ChunkMath.ChunkSize * 1.5f + AnchorHysteresis;

            // Want colliders on LOD0 chunks near the target.
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_plan[i].Distance > scanLimit) break;
                TerrainChunk c = _ordered[i];
                bool want = c.Lod == 0 && ChunkMath.DistanceSqToChunk(c.Coord, wx, wz) <= cd2;
                if (want && c.ColliderState == ColliderState.None)
                {
                    c.BakeHandle = new BakeJob { MeshId = c.Mesh.GetEntityId() }.Schedule();
                    c.ColliderState = ColliderState.Baking;
                    _withCollider.Add(c);
                }
            }

            int active = 0;
            for (int i = _withCollider.Count - 1; i >= 0; i--)
            {
                TerrainChunk c = _withCollider[i];
                bool want = c.Stamp == _stamp && c.Lod == 0 && ChunkMath.DistanceSqToChunk(c.Coord, wx, wz) <= (cd + 24.0) * (cd + 24.0);
                if (c.ColliderState == ColliderState.Baking && c.BakeHandle.IsCompleted)
                {
                    c.BakeHandle.Complete();
                    if (c.Collider == null)
                    {
                        c.Collider = c.Go.AddComponent<MeshCollider>();
                        c.Collider.cookingOptions = Cooking;
                    }
                    c.Collider.sharedMesh = c.Mesh;
                    c.Collider.enabled = true;
                    c.ColliderState = ColliderState.Ready;
                }
                if (!want && c.ColliderState != ColliderState.Baking)
                {
                    DropCollider(c);
                    continue;
                }
                if (c.ColliderState == ColliderState.Ready) active++;
            }
            CollidersActive = active;
        }

        void DropCollider(TerrainChunk c)
        {
            if (c.ColliderState == ColliderState.Baking) c.BakeHandle.Complete();
            if (c.Collider != null)
            {
                c.Collider.sharedMesh = null;
                c.Collider.enabled = false;
            }
            if (c.ColliderState != ColliderState.None) _withCollider.Remove(c);
            c.ColliderState = ColliderState.None;
        }

        void ReleaseChunk(TerrainChunk c)
        {
            DropCollider(c);
            if (c.Go != null) c.Go.SetActive(false);
            c.Lod = -1;
            c.RequestedLod = -1;
            c.Ticket = 0;
            _chunkPool.Push(c);
        }

        void OnOriginShifted(Vector3 delta)
        {
            foreach (var kv in _chunks)
            {
                TerrainChunk c = kv.Value;
                if (c.Go != null)
                    c.Go.transform.localPosition = _origin.ToScene(ChunkMath.OriginX(c.Coord), 0.0, ChunkMath.OriginZ(c.Coord));
            }
            foreach (TerrainChunk c in _chunkPool)
                if (c.Go != null) c.Go.transform.localPosition = new Vector3(0, -10000f, 0);
        }

        ChunkMeshData RentData(int lod)
        {
            Stack<ChunkMeshData> pool = _dataPools[lod];
            return pool.Count > 0 ? pool.Pop() : new ChunkMeshData(lod);
        }

        void ReturnData(ChunkMeshData d) => _dataPools[d.Lod].Push(d);

        void OnDestroy()
        {
            _epoch++;
            if (_origin != null) _origin.Shifted -= OnOriginShifted;
            try
            {
                _cts?.Cancel();
                _jobs?.CompleteAdding();
            }
            catch (ObjectDisposedException) { }
            if (_workers != null)
                foreach (Thread t in _workers) t.Join(200);

            void Cleanup(TerrainChunk c)
            {
                if (c.ColliderState == ColliderState.Baking) c.BakeHandle.Complete();
                if (c.Mesh != null) Destroy(c.Mesh);
                if (c.WaterMesh != null) Destroy(c.WaterMesh);
            }
            foreach (var kv in _chunks) Cleanup(kv.Value);
            foreach (TerrainChunk c in _chunkPool) Cleanup(c);
        }

        struct BakeJob : IJob
        {
            public EntityId MeshId;
            public void Execute() => Physics.BakeMesh(MeshId, false, Cooking);
        }

        enum ColliderState { None, Baking, Ready }

        sealed class TerrainChunk
        {
            public ChunkCoord Coord;
            public int Lod = -1;
            public int DesiredLod;
            public int RequestedLod = -1;
            public int Ticket;
            public int Stamp;
            public bool Failed;
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public GameObject Water;
            public Mesh WaterMesh;
            public MeshCollider Collider;
            public ColliderState ColliderState;
            public JobHandle BakeHandle;

            public bool ColliderReady => ColliderState == ColliderState.Ready;

            public void Reset(ChunkCoord coord)
            {
                Coord = coord;
                Lod = -1;
                RequestedLod = -1;
                Ticket = 0;
                Failed = false;
                ColliderState = ColliderState.None;
            }
        }
    }
}
