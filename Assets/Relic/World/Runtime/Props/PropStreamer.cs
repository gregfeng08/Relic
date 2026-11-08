using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Relic.World.Chunks;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Streams PropScatter output around the player: trees, bushes and boulders for every chunk in
    /// view, ground cover (grass, flowers, rocks, shrubs) for chunks near the player. Placement runs
    /// on two background threads (pure Logic, deterministic from the seed); the main thread only
    /// turns results into instance matrices. PropRenderer draws them, PropColliders adds
    /// colliders near the player.
    /// </summary>
    public sealed class PropStreamer : MonoBehaviour
    {
        [Tooltip("Ground cover (grass, flowers, small rocks) is placed for chunks this close (m).")]
        public float SmallRadius = 110f;
        public int MaxResultsPerFrame = 12;

        public int ChunkCount => _chunks.Count;
        public int PendingJobs => _pending;
        public float ViewDistance => _lod.ViewDistance;

        internal readonly List<PropChunk> Visible = new List<PropChunk>();

        TerrainGenerator _gen;
        FloatingOrigin _origin;
        Transform _target;
        LodSettings _lod;
        ChunkOffset[] _plan;
        readonly Dictionary<ChunkCoord, PropChunk> _chunks = new Dictionary<ChunkCoord, PropChunk>();
        readonly Stack<PropChunk> _pool = new Stack<PropChunk>();
        readonly List<PropChunk> _scratch = new List<PropChunk>();
        readonly ConcurrentQueue<PropJob> _results = new ConcurrentQueue<PropJob>();
        BlockingCollection<PropJob> _jobs;
        CancellationTokenSource _cts;
        Thread[] _workers;
        bool _hasAnchor;
        ChunkCoord _anchor;
        int _stamp;
        int _pending;
        bool _loggedError;

        public void Initialize(TerrainGenerator gen, FloatingOrigin origin, Transform target, LodSettings lod)
        {
            _gen = gen;
            _origin = origin;
            _target = target;
            _lod = lod;
            _plan = LodPlan.Build(lod);
            _origin.Shifted += OnOriginShifted;
            _jobs = new BlockingCollection<PropJob>(new ConcurrentQueue<PropJob>());
            _cts = new CancellationTokenSource();
            _workers = new Thread[2];
            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i] = new Thread(WorkerLoop) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "Relic.PropWorker." + i };
                _workers[i].Start();
            }
        }

        void WorkerLoop()
        {
            var gen = _gen;
            try
            {
                foreach (PropJob j in _jobs.GetConsumingEnumerable(_cts.Token))
                {
                    try { j.Count = j.Small ? PropScatter.PlaceSmall(gen, j.Coord, j.Items) : PropScatter.Place(gen, j.Coord, j.Items); }
                    catch (Exception e) { j.Error = e; j.Count = 0; }
                    _results.Enqueue(j);
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
            UpdateSmall(wx, wz);
            ProcessResults();
        }

        void UpdateAnchor(double wx, double wz)
        {
            ChunkCoord pc = ChunkMath.ChunkAt(wx, wz);
            if (_hasAnchor && (pc == _anchor || Math.Sqrt(ChunkMath.DistanceSqToChunk(_anchor, wx, wz)) < 16.0)) return;
            _anchor = pc;
            _hasAnchor = true;
            _stamp++;
            Visible.Clear();
            for (int i = 0; i < _plan.Length; i++)
            {
                var coord = new ChunkCoord(_anchor.X + _plan[i].Dx, _anchor.Z + _plan[i].Dz);
                if (!_chunks.TryGetValue(coord, out PropChunk c))
                {
                    c = _pool.Count > 0 ? _pool.Pop() : new PropChunk();
                    c.Reset(coord);
                    _chunks.Add(coord, c);
                }
                c.Stamp = _stamp;
                Visible.Add(c);
                if (!c.LargeRequested) Request(c, false);
            }
            _scratch.Clear();
            foreach (var kv in _chunks) if (kv.Value.Stamp != _stamp) _scratch.Add(kv.Value);
            foreach (PropChunk c in _scratch)
            {
                _chunks.Remove(c.Coord);
                c.ClearAll();
                c.SmallTicket++;
                c.Generation++;
                _pool.Push(c);
            }
        }

        void UpdateSmall(double wx, double wz)
        {
            double near2 = (double)SmallRadius * SmallRadius, far2 = (SmallRadius + 64.0) * (SmallRadius + 64.0);
            for (int i = 0; i < Visible.Count; i++)
            {
                PropChunk c = Visible[i];
                if (_plan[i].Distance > SmallRadius + 160f) break; // sorted near to far
                double d2 = ChunkMath.DistanceSqToChunk(c.Coord, wx, wz);
                if (d2 <= near2 && !c.SmallRequested) Request(c, true);
                else if (d2 > far2 && (c.SmallReady || c.SmallRequested)) c.ClearSmall();
            }
        }

        void Request(PropChunk c, bool small)
        {
            PropJob j = PropJobPool.Rent(small);
            j.Coord = c.Coord;
            j.Small = small;
            j.Ticket = small ? c.SmallTicket : c.Generation;
            if (small) c.SmallRequested = true;
            else c.LargeRequested = true;
            _pending++;
            _jobs.Add(j);
        }

        void ProcessResults()
        {
            int n = 0;
            while (n < MaxResultsPerFrame && _results.TryDequeue(out PropJob j))
            {
                _pending--;
                n++;
                if (j.Error != null && !_loggedError) { Debug.LogException(j.Error); _loggedError = true; }
                if (_chunks.TryGetValue(j.Coord, out PropChunk c) && j.Ticket == (j.Small ? c.SmallTicket : c.Generation))
                {
                    if (j.Small && c.SmallRequested && !c.SmallReady)
                    {
                        c.AddInstances(j.Items, j.Count, _origin);
                        c.SmallReady = true;
                    }
                    else if (!j.Small && !c.LargeReady)
                    {
                        c.AddInstances(j.Items, j.Count, _origin);
                        if (c.Large.Length < j.Count) c.Large = new PropInstance[PropScatter.MaxPerChunk];
                        Array.Copy(j.Items, c.Large, j.Count);
                        c.LargeCount = j.Count;
                        c.LargeReady = true;
                    }
                }
                PropJobPool.Return(j);
            }
        }

        void OnOriginShifted(Vector3 delta)
        {
            foreach (var kv in _chunks) kv.Value.Shift(delta);
        }

        void OnDestroy()
        {
            if (_origin != null) _origin.Shifted -= OnOriginShifted;
            try { _cts?.Cancel(); _jobs?.CompleteAdding(); }
            catch (ObjectDisposedException) { }
            if (_workers != null) foreach (Thread t in _workers) t.Join(200);
        }

        /// <summary>World-space chunk origin in scene coordinates (for colliders).</summary>
        internal Vector3 ChunkScene(ChunkCoord c) => _origin.ToScene(ChunkMath.OriginX(c), 0.0, ChunkMath.OriginZ(c));
    }
}
