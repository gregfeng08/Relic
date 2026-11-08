using Relic.World.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Draws the streamed props with GPU instancing (Graphics.RenderMeshInstanced), one batch of
    /// up to 1023 instances per (kind, LOD). Per chunk: frustum test, then distance culling by
    /// class: ground cover to ~70 m, medium props to ~150 m (both shrink away before they're
    /// culled, no pop), trees and boulders to the view distance, switching to a cheap far mesh
    /// (no shadows) past ~150 m. Wind is a global the shaders read.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class PropRenderer : MonoBehaviour
    {
        public PropStreamer Streamer;
        public Camera View;
        public float GroundDistance = 70f;
        public float MediumDistance = 150f;
        public float NearTreeDistance = 150f;
        public Vector2 WindDirection = new Vector2(0.8f, 0.6f);
        public float WindGustSpeed = 1f;

        public int DrawnInstances { get; private set; }
        public int DrawCalls { get; private set; }
        /// <summary>Main-thread milliseconds spent gathering and submitting props (last frame, and running max/average).</summary>
        public float LastDrawMs { get; private set; }
        public float MaxDrawMs { get; private set; }
        public double TotalDrawMs { get; private set; }
        public int Frames { get; private set; }

        PropMeshes _meshes;
        Material _propMat, _grassMat;
        MaterialPropertyBlock _groundBlock, _mediumBlock, _largeBlock;
        readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        readonly Plane[] _planes = new Plane[6];
        static readonly int WindId = Shader.PropertyToID("_RelicWind");
        static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
        static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");

        public void Initialize(PropStreamer streamer, Camera view)
        {
            Streamer = streamer;
            View = view;
            _meshes = new PropMeshes();
            _propMat = new Material(Shader.Find("Relic/World/ToonProp")) { name = "Props (runtime)", enableInstancing = true };
            _grassMat = new Material(Shader.Find("Relic/World/ToonGrass")) { name = "Grass (runtime)", enableInstancing = true };
            _groundBlock = Block(GroundDistance * 0.72f, GroundDistance, 1.6f);
            _mediumBlock = Block(MediumDistance * 0.8f, MediumDistance, 0.6f);
            _largeBlock = Block(0f, 0f, 1f);
        }

        static MaterialPropertyBlock Block(float fadeStart, float fadeEnd, float wind)
        {
            var b = new MaterialPropertyBlock();
            b.SetFloat(FadeStartId, fadeStart);
            b.SetFloat(FadeEndId, fadeEnd);
            b.SetFloat(WindStrengthId, wind);
            return b;
        }

        void LateUpdate()
        {
            if (Streamer == null || _meshes == null) return;
            Camera cam = View != null ? View : Camera.main;
            if (cam == null) return;
            Shader.SetGlobalVector(WindId, new Vector4(WindDirection.x, 0f, WindDirection.y, WindGustSpeed));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Draw(cam);
            LastDrawMs = (float)sw.Elapsed.TotalMilliseconds;
            if (Frames > 30 && LastDrawMs > MaxDrawMs) MaxDrawMs = LastDrawMs;
            TotalDrawMs += LastDrawMs;
            Frames++;
        }

        /// <summary>Queues every visible prop for this frame's render of cam.</summary>
        public void Draw(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            Vector3 eye = cam.transform.position;
            float view = Streamer.ViewDistance;
            DrawnInstances = 0;
            DrawCalls = 0;
            for (int k = 0; k < PropKinds.Count; k++)
            {
                var kind = (PropKind)k;
                PropClass cls = PropKinds.ClassOf(kind);
                if (cls == PropClass.Large)
                {
                    DrawKind(k, cls, eye, 0f, NearTreeDistance, _meshes.Near[k], _propMat, _largeBlock, ShadowCastingMode.On);
                    DrawKind(k, cls, eye, NearTreeDistance, view, _meshes.Far[k], _propMat, _largeBlock, ShadowCastingMode.Off);
                }
                else if (cls == PropClass.Medium)
                    DrawKind(k, cls, eye, 0f, MediumDistance, _meshes.Near[k], _propMat, _mediumBlock, ShadowCastingMode.On);
                else
                    DrawKind(k, cls, eye, 0f, GroundDistance, _meshes.Near[k], _grassMat, _groundBlock, ShadowCastingMode.Off);
            }
        }

        void DrawKind(int k, PropClass cls, Vector3 eye, float minDist, float maxDist, Mesh mesh, Material mat, MaterialPropertyBlock block, ShadowCastingMode shadows)
        {
            if (mesh == null) return;
            var rp = new RenderParams(mat)
            {
                matProps = block,
                shadowCastingMode = shadows,
                receiveShadows = true,
                layer = 0,
                worldBounds = new Bounds(eye, Vector3.one * (maxDist * 2f + 200f)),
            };
            float min2 = minDist * minDist;
            int n = 0;
            var chunks = Streamer.Visible;
            for (int c = 0; c < chunks.Count; c++)
            {
                PropChunk ch = chunks[c];
                int count = ch.Counts[k];
                if (count == 0) continue;
                // Chunk-level LOD: distance from the eye to the chunk's center (horizontal).
                Vector3 center = ch.Bounds.center;
                float dx = center.x - eye.x, dz = center.z - eye.z;
                float d2 = dx * dx + dz * dz;
                float reach = cls == PropClass.Large ? 0f : 46f; // small props: any part of the chunk in range
                if (d2 < min2 || Mathf.Sqrt(d2) - reach > maxDist) continue;
                if (!GeometryUtility.TestPlanesAABB(_planes, ch.Bounds)) continue;
                Matrix4x4[] src = ch.Matrices[k];
                for (int i = 0; i < count; i++)
                {
                    _batch[n++] = src[i];
                    if (n == _batch.Length) { Flush(rp, mesh, n); n = 0; }
                }
            }
            if (n > 0) Flush(rp, mesh, n);
        }

        void Flush(in RenderParams rp, Mesh mesh, int n)
        {
            Graphics.RenderMeshInstanced(rp, mesh, 0, _batch, n);
            DrawnInstances += n;
            DrawCalls++;
        }

        void OnDestroy()
        {
            if (Frames > 0)
                Debug.Log("[Relic.Perf] Props: avg " + (TotalDrawMs / Frames).ToString("0.00") + " ms/frame gather+submit, max " + MaxDrawMs.ToString("0.00") + " ms, last frame " + DrawnInstances + " instances in " + DrawCalls + " draws");
            _meshes?.Destroy();
            if (_propMat != null) Destroy(_propMat);
            if (_grassMat != null) Destroy(_grassMat);
        }
    }
}
