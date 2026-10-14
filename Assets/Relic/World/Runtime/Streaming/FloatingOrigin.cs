using System;
using System.Collections.Generic;
using Relic.World.Chunks;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Keeps the scene near (0, 0, 0) so physics and shaders stay precise at any distance.
    /// World position (double) = scene position + (OriginX, 0, OriginZ). When the tracked target
    /// passes the threshold (4 km), the origin jumps to the target's chunk and everything that
    /// registered (player, camera) moves by the same delta; listeners (the chunk streamer)
    /// re-place themselves from their own double coordinates, so error never accumulates.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class FloatingOrigin : MonoBehaviour
    {
        public double Threshold = FloatingOriginMath.DefaultThreshold;
        public Transform Target;

        public double OriginX { get; private set; }
        public double OriginZ { get; private set; }
        public int ShiftCount { get; private set; }

        /// <summary>Raised after a shift with the scene-space delta that was subtracted.</summary>
        public event Action<Vector3> Shifted;

        readonly List<Transform> _moved = new List<Transform>();

        public void Register(Transform t) { if (t != null && !_moved.Contains(t)) _moved.Add(t); }

        public Vector3 ToScene(double worldX, double worldY, double worldZ) =>
            new Vector3((float)(worldX - OriginX), (float)worldY, (float)(worldZ - OriginZ));

        public void ToWorld(Vector3 scene, out double worldX, out double worldZ)
        {
            worldX = scene.x + OriginX;
            worldZ = scene.z + OriginZ;
        }

        void Update()
        {
            if (Target == null) return;
            Vector3 p = Target.position;
            if (!FloatingOriginMath.ShouldRecenter(p.x, p.z, Threshold)) return;
            ToWorld(p, out double wx, out double wz);
            FloatingOriginMath.NewOrigin(wx, wz, out double ox, out double oz);
            Recenter(ox, oz);
        }

        /// <summary>Moves the origin to (ox, oz) world meters (should be on the chunk grid).</summary>
        public void Recenter(double ox, double oz)
        {
            var delta = new Vector3((float)(ox - OriginX), 0f, (float)(oz - OriginZ));
            OriginX = ox;
            OriginZ = oz;
            ShiftCount++;
            foreach (Transform t in _moved)
                if (t != null) t.position -= delta;
            Physics.SyncTransforms();
            Shader.SetGlobalVector(ShaderIds.WorldOffset, new Vector4((float)Mod(OriginX, 4096.0), 0f, (float)Mod(OriginZ, 4096.0), 0f));
            Shifted?.Invoke(delta);
            Debug.Log("[Relic.World] Floating origin moved to (" + OriginX + ", " + OriginZ + ")");
        }

        void OnEnable()
        {
            Shader.SetGlobalVector(ShaderIds.WorldOffset, new Vector4((float)Mod(OriginX, 4096.0), 0f, (float)Mod(OriginZ, 4096.0), 0f));
        }

        static double Mod(double a, double m)
        {
            double r = a % m;
            return r < 0 ? r + m : r;
        }
    }

    internal static class ShaderIds
    {
        public static readonly int WorldOffset = Shader.PropertyToID("_RelicWorldOffset");
        public static readonly int AmbientSky = Shader.PropertyToID("_RelicAmbientSky");
        public static readonly int AmbientGround = Shader.PropertyToID("_RelicAmbientGround");
        public static readonly int ShadeColor = Shader.PropertyToID("_RelicShadeColor");
        public static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        public static readonly int ZenithColor = Shader.PropertyToID("_ZenithColor");
        public static readonly int HorizonColor = Shader.PropertyToID("_HorizonColor");
        public static readonly int GroundColor = Shader.PropertyToID("_GroundColor");
        public static readonly int SunColor = Shader.PropertyToID("_SunColor");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    }
}
