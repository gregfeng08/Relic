using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Secondary motion for hair, scarf tails and coat panels: Verlet spring chains (VRM
    /// spring-bone style) that pull back toward their rest shape, feel gravity and wind, and are
    /// pushed out of body capsules. Stepped in fixed-size substeps so it behaves the same at any
    /// frame rate. Tails are stored relative to the avatar root, so a floating-origin recenter or
    /// a teleport moves them with the hero instead of whipping them across the world.
    /// </summary>
    public sealed class SpringChains
    {
        sealed class Joint
        {
            public Transform T;
            public Vector3 Axis;     // rest direction to the child, parent space (rest rotations are identity)
            public float Length;
            public Vector3 Tail;     // root-relative, world axes
            public Vector3 Prev;
        }

        sealed class Chain
        {
            public ChainDef Def;
            public readonly List<Joint> Joints = new List<Joint>();
        }

        public struct Capsule
        {
            public Transform Bone;
            public Vector3 A, B;   // bone-local endpoints
            public float Radius;
        }

        const float StepTime = 1f / 90f;
        const int MaxSteps = 8;
        const float TeleportDistance = 3f;

        readonly List<Chain> _chains = new List<Chain>();
        readonly List<Capsule> _colliders = new List<Capsule>();
        readonly Transform _root;
        Vector3 _lastRoot;
        float _lastStep = StepTime;
        bool _initialized;

        /// <summary>World-space wind acceleration (the animator feeds -velocity so things trail when running).</summary>
        public Vector3 Wind;

        public int ChainCount => _chains.Count;

        public SpringChains(HeroRig rig, IList<ChainDef> defs)
        {
            _root = rig.RootTransform;
            foreach (ChainDef d in defs)
            {
                var c = new Chain { Def = d };
                for (int i = 0; i < d.Bones.Count - 1; i++)
                {
                    Vector3 off = rig.Rest[d.Bones[i + 1]] - rig.Rest[d.Bones[i]];
                    c.Joints.Add(new Joint { T = rig.Bones[d.Bones[i]], Axis = off.normalized, Length = Mathf.Max(1e-4f, off.magnitude) });
                }
                _chains.Add(c);
            }
        }

        public void AddCollider(Transform bone, Vector3 a, Vector3 b, float radius) =>
            _colliders.Add(new Capsule { Bone = bone, A = a, B = b, Radius = radius });

        /// <summary>Snap every chain to its rest shape under the current pose.</summary>
        public void Reset()
        {
            Vector3 root = _root.position;
            foreach (Chain c in _chains)
                foreach (Joint j in c.Joints)
                {
                    j.T.localRotation = Quaternion.identity;
                    Vector3 tail = j.T.position + j.T.parent.rotation * j.Axis * j.Length;
                    j.Tail = j.Prev = tail - root;
                }
            _lastRoot = root;
            _initialized = true;
        }

        public void Step(float dt)
        {
            if (!_initialized) { Reset(); return; }
            Vector3 root = _root.position;
            Vector3 delta = root - _lastRoot;
            _lastRoot = root;
            if (delta.magnitude > TeleportDistance) delta = Vector3.zero; // origin shift or teleport: carry everything along
            if (dt <= 0f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / StepTime), 1, MaxSteps);
            float h = Mathf.Min(dt, StepTime * MaxSteps) / steps;
            Vector3 stepDelta = delta / steps;

            // The root moved this frame: in root-relative terms the world-fixed tails drift back.
            for (int s = 0; s < steps; s++)
            {
                foreach (Chain c in _chains)
                {
                    float inertia = c.Def.Inertia;
                    foreach (Joint j in c.Joints)
                    {
                        j.Tail -= stepDelta * inertia;
                        j.Prev -= stepDelta * inertia;
                    }
                }
                // Bodies are already posed for this frame: joint positions relative to the root are exact.
                Simulate(h, root);
            }
        }

        void Simulate(float h, Vector3 root)
        {
            float velScale = h / _lastStep;
            _lastStep = h;
            foreach (Chain c in _chains)
            {
                ChainDef d = c.Def;
                float pull = 1f - Mathf.Exp(-d.Stiffness * 30f * h);
                float keep = Mathf.Exp(-d.Drag * 6f * h);
                Vector3 accel = (Physics.gravity * d.Gravity + Wind) * (h * h);
                foreach (Joint j in c.Joints)
                {
                    Vector3 pos = j.T.position - root;
                    Quaternion restRot = j.T.parent.rotation;
                    Vector3 restDir = restRot * j.Axis;
                    Vector3 target = pos + restDir * j.Length;
                    Vector3 next = j.Tail + (j.Tail - j.Prev) * keep * velScale;
                    next += (target - next) * pull + accel;
                    next = pos + SafeDir(next - pos, restDir) * j.Length;
                    next = Collide(next, pos, root, d.Radius, j.Length, restDir);
                    j.Prev = j.Tail;
                    j.Tail = next;
                    j.T.rotation = Quaternion.FromToRotation(restDir, next - pos) * restRot;
                }
            }
        }

        Vector3 Collide(Vector3 tail, Vector3 pos, Vector3 root, float radius, float length, Vector3 restDir)
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                Capsule k = _colliders[i];
                if (k.Bone == null) continue;
                Vector3 a = k.Bone.TransformPoint(k.A) - root, b = k.Bone.TransformPoint(k.B) - root;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(tail - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                Vector3 closest = a + ab * t;
                Vector3 d = tail - closest;
                float min = k.Radius + radius;
                if (d.sqrMagnitude < min * min)
                {
                    tail = closest + SafeDir(d, restDir) * min;
                    tail = pos + SafeDir(tail - pos, restDir) * length;
                }
            }
            return tail;
        }

        static Vector3 SafeDir(Vector3 v, Vector3 fallback)
        {
            float m = v.magnitude;
            return m > 1e-6f ? v / m : fallback;
        }
    }
}
