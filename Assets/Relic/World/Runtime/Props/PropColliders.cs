using System.Collections.Generic;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Colliders for trees (trunk capsules) and boulders (spheres) near the player only, from a
    /// small pool, refreshed when the player has moved a few meters. They live on the PropLayer:
    /// the character collides with them, the camera ignores them (no pull-in jitter when a
    /// trunk passes between camera and hero), and climbing ignores them.
    /// </summary>
    public sealed class PropColliders : MonoBehaviour
    {
        public const int PropLayer = 1; // built-in TransparentFX: free, and needs no ProjectSettings change

        public PropStreamer Streamer;
        public Transform Target;
        public float Radius = 36f;
        public float RefreshDistance = 4f;

        public int Active { get; private set; }

        readonly List<CapsuleCollider> _trunks = new List<CapsuleCollider>();
        readonly List<SphereCollider> _rocks = new List<SphereCollider>();
        Vector3 _lastPos = new Vector3(float.MaxValue, 0f, 0f);
        Transform _root;

        void Awake()
        {
            _root = new GameObject("Prop Colliders").transform;
            _root.SetParent(transform, false);
        }

        /// <summary>Forces a refresh (after teleports and origin shifts).</summary>
        public void Invalidate() => _lastPos = new Vector3(float.MaxValue, 0f, 0f);

        void Update()
        {
            if (Streamer == null || Target == null) return;
            Vector3 p = Target.position;
            if ((p - _lastPos).sqrMagnitude < RefreshDistance * RefreshDistance) return;
            _lastPos = p;

            int trunks = 0, rocks = 0;
            float r2 = Radius * Radius;
            var chunks = Streamer.Visible;
            for (int c = 0; c < chunks.Count && c < 64; c++) // sorted near to far: only the nearest few matter
            {
                PropChunk ch = chunks[c];
                if (!ch.LargeReady) continue;
                Vector3 origin = Streamer.ChunkScene(ch.Coord);
                for (int i = 0; i < ch.LargeCount; i++)
                {
                    PropInstance inst = ch.Large[i];
                    if (!PropKinds.IsSolid(inst.Kind)) continue;
                    var pos = new Vector3(origin.x + inst.X, inst.Y, origin.z + inst.Z);
                    float dx = pos.x - p.x, dz = pos.z - p.z;
                    if (dx * dx + dz * dz > r2) continue;
                    if (inst.Kind == PropKind.Boulder)
                    {
                        SphereCollider s = Get(_rocks, rocks++);
                        s.transform.position = pos;
                        s.radius = 0.95f * inst.Scale;
                        s.center = new Vector3(0f, 0.5f * inst.Scale, 0f);
                    }
                    else
                    {
                        CapsuleCollider t = Get(_trunks, trunks++);
                        t.transform.position = pos;
                        t.radius = Mathf.Max(0.18f, 0.24f * inst.Scale);
                        t.height = 4.2f * inst.Scale;
                        t.center = new Vector3(0f, 2.1f * inst.Scale, 0f);
                    }
                }
            }
            for (int i = trunks; i < _trunks.Count; i++) if (_trunks[i].enabled) _trunks[i].enabled = false;
            for (int i = rocks; i < _rocks.Count; i++) if (_rocks[i].enabled) _rocks[i].enabled = false;
            Active = trunks + rocks;
        }

        T Get<T>(List<T> pool, int index) where T : Collider
        {
            if (index >= pool.Count)
            {
                var go = new GameObject(typeof(T) == typeof(SphereCollider) ? "Boulder" : "Trunk") { layer = PropLayer };
                go.transform.SetParent(_root, false);
                pool.Add(go.AddComponent<T>());
            }
            T col = pool[index];
            if (!col.enabled) col.enabled = true;
            return col;
        }
    }
}
