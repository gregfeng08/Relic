using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using UnityEngine;

namespace Relic.Magic.Desk
{
    /// <summary>
    /// A tiny test arena that plays a compiled spell's shapes with primitives and
    /// line outlines: projectiles (single, fan, ring, line, orbit, seeking,
    /// returning, splitting), areas and walls, hitting three drifting dummies.
    /// A debug visualizer only; the real combat runtime is built at integration.
    ///
    /// Driven by <see cref="Tick"/> (called from Update), so tests can step it
    /// in edit mode.
    /// </summary>
    public sealed class SpellPreview : MonoBehaviour
    {
        public const float AimDistance = 10f;
        const float OrbitRadius = 2.2f;
        const float TurnRateDeg = 200f;

        Transform _root;
        Transform _caster;
        readonly List<Transform> _dummies = new List<Transform>();
        readonly List<Vector3> _dummyHome = new List<Vector3>();
        readonly List<float> _dummyFlash = new List<float>();
        readonly List<Color> _dummyFlashColor = new List<Color>();

        readonly List<Scheduled> _schedule = new List<Scheduled>();
        readonly List<Projectile> _projectiles = new List<Projectile>();
        readonly List<Timed> _timed = new List<Timed>();
        float _time;
        float _clock;
        Pcg32 _rng = new Pcg32(7);

        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        sealed class Scheduled
        {
            public float At;
            public Action Run;
        }

        sealed class Timed
        {
            public GameObject Go;
            public float Until;
            public Action<float> Update; // gets normalized age 0..1
            public float Start;
        }

        sealed class Projectile
        {
            public Transform T;
            public Vector3 Dir;
            public float Speed, Range, Traveled, Size;
            public bool Seeking, Returning, Coming;
            public int SplitCount;
            public bool Orbit;
            public float OrbitAngle, OrbitUntil;
            public Color Color;
            public readonly Dictionary<int, float> OrbitHitCooldown = new Dictionary<int, float>();
        }

        public int ActiveProjectiles => _projectiles.Count;
        public int ActiveEffects => _timed.Count;
        public Transform Root => _root;

        // ---------------- Arena ----------------

        /// <summary>Builds the arena (ground, caster, dummies) under a new root object.</summary>
        public static SpellPreview Build(Vector3 origin)
        {
            var root = new GameObject("Spell Preview Arena");
            root.transform.position = origin;
            var preview = root.AddComponent<SpellPreview>();
            preview._root = root.transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, 0f, 8f);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            StripCollider(ground);
            ground.GetComponent<Renderer>().sharedMaterial = MaterialFor(new Color(0.22f, 0.26f, 0.2f));

            // Range marks every 5 m so distances read at a glance.
            for (int m = 5; m <= 25; m += 5)
                preview.Outline("Range " + m, Circle(Vector3.zero, m, 64), new Color(0.32f, 0.36f, 0.3f), 0.04f, root.transform);

            var caster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            caster.name = "Caster";
            caster.transform.SetParent(root.transform, false);
            caster.transform.localPosition = new Vector3(0f, 1f, 0f);
            StripCollider(caster);
            caster.GetComponent<Renderer>().sharedMaterial = MaterialFor(new Color(0.85f, 0.8f, 0.7f));
            preview._caster = caster.transform;

            var homes = new[] { new Vector3(-4.5f, 1f, 9f), new Vector3(0f, 1f, 12f), new Vector3(4.5f, 1f, 8f) };
            for (int i = 0; i < homes.Length; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                d.name = "Dummy " + (i + 1);
                d.transform.SetParent(root.transform, false);
                d.transform.localPosition = homes[i];
                d.transform.localScale = new Vector3(1f, 1f, 1f);
                StripCollider(d);
                d.GetComponent<Renderer>().sharedMaterial = MaterialFor(DummyColor);
                preview._dummies.Add(d.transform);
                preview._dummyHome.Add(homes[i]);
                preview._dummyFlash.Add(0f);
                preview._dummyFlashColor.Add(Color.white);
            }
            return preview;
        }

        static readonly Color DummyColor = new Color(0.55f, 0.45f, 0.35f);

        void Update() => Tick(Time.deltaTime);

        void OnDestroy() => Stop();

        // ---------------- Playing ----------------

        /// <summary>Clears whatever is playing and plays the program's first rule from the caster.</summary>
        public void Play(EffectProgram program)
        {
            Stop();
            _time = 0f;
            _rng = new Pcg32(7);
            if (program?.Rules == null || program.Rules.Count == 0) return;
            foreach (var a in program.Rules[0].Actions) PlayAction(a);
        }

        public void Stop()
        {
            _schedule.Clear();
            foreach (var p in _projectiles) Kill(p.T.gameObject);
            _projectiles.Clear();
            foreach (var t in _timed) Kill(t.Go);
            _timed.Clear();
        }

        void PlayAction(ActionSpec a)
        {
            var color = PayloadColor(a.Payload);
            switch (a.Kind)
            {
                case ActionKind.Emit:
                {
                    var e = a.Emitter;
                    if (e == null) return;
                    if (e.Delay > 0f) Tell(CasterFloor + Vector3.up * 1.2f, e.Delay, color);
                    ScheduleEmit(e, color);
                    break;
                }
                case ActionKind.CreateArea:
                {
                    var ar = a.Area;
                    if (ar == null) return;
                    At(ar.Delay, () => SpawnArea(ar, color, a.Payload));
                    if (ar.Delay > 0f) AreaTell(ar, color);
                    break;
                }
                case ActionKind.CreateBarrier:
                {
                    var ar = a.Area;
                    if (ar == null) return;
                    At(ar.Delay, () => SpawnWall(ar, color));
                    break;
                }
            }
        }

        Vector3 CasterFloor => _root.position;
        Vector3 Forward => _root.forward;
        Vector3 AimPoint => CasterFloor + Forward * AimDistance;

        void ScheduleEmit(EmitterSpec e, Color color)
        {
            int count = Mathf.Max(1, e.Count);
            switch (e.Shape)
            {
                case EmitShape.Single:
                    At(e.Delay, () => Spawn(e, Forward, color));
                    break;
                case EmitShape.Line:
                    for (int i = 0; i < count; i++) At(e.Delay + 0.12f * i, () => Spawn(e, Forward, color));
                    break;
                case EmitShape.Cone:
                    At(e.Delay, () =>
                    {
                        for (int i = 0; i < count; i++)
                        {
                            float t = count == 1 ? 0.5f : (float)i / (count - 1);
                            Spawn(e, Quaternion.Euler(0f, (t - 0.5f) * e.SpreadDegrees, 0f) * Forward, color);
                        }
                    });
                    break;
                case EmitShape.Ring:
                    At(e.Delay, () =>
                    {
                        for (int i = 0; i < count; i++) Spawn(e, Quaternion.Euler(0f, 360f * i / count, 0f) * Forward, color);
                    });
                    break;
                case EmitShape.Spiral:
                    for (int i = 0; i < count; i++)
                    {
                        float ang = 360f * i / count;
                        At(e.Delay + 0.06f * i, () => Spawn(e, Quaternion.Euler(0f, ang, 0f) * Forward, color));
                    }
                    break;
                case EmitShape.Burst:
                    At(e.Delay, () =>
                    {
                        for (int i = 0; i < count; i++)
                            Spawn(e, Quaternion.Euler(0f, _rng.Range(-0.5f, 0.5f) * Mathf.Max(30f, e.SpreadDegrees), 0f) * Forward, color);
                    });
                    break;
                case EmitShape.Orbit:
                    At(e.Delay, () =>
                    {
                        for (int i = 0; i < count; i++) SpawnOrbit(e, 360f * i / count, color);
                    });
                    break;
            }
        }

        void Spawn(EmitterSpec e, Vector3 dir, Color color, float sizeScale = 1f, int splits = -1, Vector3? from = null)
        {
            var go = Ball("Projectile", Mathf.Max(0.15f, e.Size * 2f * sizeScale), color);
            go.transform.position = from ?? CasterFloor + Vector3.up + dir * 0.6f;
            _projectiles.Add(new Projectile
            {
                T = go.transform,
                Dir = dir.normalized,
                Speed = e.Speed,
                Range = e.Range,
                Size = e.Size * sizeScale,
                Seeking = e.Seeking,
                Returning = e.Returning,
                SplitCount = splits >= 0 ? splits : e.SplitCount,
                Color = color,
            });
        }

        void SpawnOrbit(EmitterSpec e, float startDeg, Color color)
        {
            var go = Ball("Orbiting", Mathf.Max(0.2f, e.Size * 2f), color);
            var p = new Projectile
            {
                T = go.transform,
                Orbit = true,
                OrbitAngle = startDeg * Mathf.Deg2Rad,
                OrbitUntil = _time + Mathf.Max(0.5f, e.Range),
                Speed = e.Speed,
                Size = e.Size,
                Color = color,
            };
            PlaceOrbit(p);
            _projectiles.Add(p);
        }

        void PlaceOrbit(Projectile p) =>
            p.T.position = CasterFloor + Vector3.up + new Vector3(Mathf.Sin(p.OrbitAngle), 0f, Mathf.Cos(p.OrbitAngle)) * OrbitRadius;

        void SpawnArea(AreaSpec ar, Color color, List<ActionSpec> payload)
        {
            var center = ar.FollowsSelf ? CasterFloor : AimPoint;
            List<Vector3> shape;
            switch (ar.Shape)
            {
                case AreaShape.Cone: shape = Cone(CasterFloor, Forward, ar.Radius, ar.Angle); break;
                case AreaShape.Line:
                case AreaShape.Wall: shape = Rect(center, Forward, ar.Length, ar.Width); break;
                default: shape = Circle(center, ar.Radius, 48); break;
            }
            var go = Outline("Area", shape, color, 0.12f, _root);
            float start = _time;
            float duration = Mathf.Max(0.3f, ar.Duration);
            float tick = ar.TickInterval;
            float nextTick = _time;
            var lr = go.GetComponent<LineRenderer>();
            _timed.Add(new Timed
            {
                Go = go,
                Start = start,
                Until = _time + duration,
                Update = age =>
                {
                    float w = 0.12f + 0.08f * Mathf.Sin(age * Mathf.PI * 6f);
                    lr.widthMultiplier = w;
                    if (_time >= nextTick)
                    {
                        HitDummiesIn(ar, center, color);
                        nextTick = tick > 0f ? nextTick + tick : float.MaxValue;
                    }
                },
            });
        }

        void AreaTell(AreaSpec ar, Color color)
        {
            var center = ar.FollowsSelf ? CasterFloor : AimPoint;
            var shape = ar.Shape == AreaShape.Circle ? Circle(center, ar.Radius, 48) : ar.Shape == AreaShape.Cone
                ? Cone(CasterFloor, Forward, ar.Radius, ar.Angle) : Rect(center, Forward, ar.Length, ar.Width);
            var go = Outline("Area tell", shape, new Color(color.r, color.g, color.b) * 0.6f, 0.04f, _root);
            _timed.Add(new Timed { Go = go, Start = _time, Until = _time + ar.Delay });
        }

        void SpawnWall(AreaSpec ar, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            StripCollider(go);
            go.transform.SetParent(_root, true);
            go.transform.position = AimPoint + Vector3.up * 0.9f;
            go.transform.rotation = Quaternion.LookRotation(Forward);
            go.transform.localScale = new Vector3(Mathf.Max(0.5f, ar.Length), 1.8f, Mathf.Max(0.2f, ar.Width));
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(Color.Lerp(color, new Color(0.5f, 0.45f, 0.4f), 0.5f));
            _timed.Add(new Timed
            {
                Go = go,
                Start = _time,
                Until = _time + Mathf.Max(0.5f, ar.Duration),
                Update = age => go.transform.localScale = new Vector3(Mathf.Max(0.5f, ar.Length), 1.8f * Mathf.Clamp01(age * 8f), Mathf.Max(0.2f, ar.Width)),
            });
        }

        void Tell(Vector3 at, float seconds, Color color)
        {
            var go = Ball("Wind-up", 0.2f, color);
            go.transform.position = at;
            _timed.Add(new Timed
            {
                Go = go,
                Start = _time,
                Until = _time + seconds,
                Update = age => go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.9f, age),
            });
        }

        void At(float delay, Action run) => _schedule.Add(new Scheduled { At = _time + Mathf.Max(0f, delay), Run = run });

        // ---------------- Simulation ----------------

        public void Tick(float dt)
        {
            if (_root == null) return;
            _time += dt;
            _clock += dt;

            // Dummies drift side to side so seeking is visible.
            for (int i = 0; i < _dummies.Count; i++)
            {
                _dummies[i].position = _root.TransformPoint(_dummyHome[i] + new Vector3(Mathf.Sin(_clock * 0.7f + i * 2.1f) * 1.8f, 0f, 0f));
                if (_dummyFlash[i] > 0f)
                {
                    _dummyFlash[i] -= dt;
                    _dummies[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(_dummyFlash[i] > 0f ? _dummyFlashColor[i] : DummyColor);
                }
            }

            for (int i = 0; i < _schedule.Count; i++)
            {
                if (_schedule[i].At > _time) continue;
                var s = _schedule[i];
                _schedule.RemoveAt(i--);
                s.Run();
            }

            for (int i = _projectiles.Count - 1; i >= 0; i--)
                if (!StepProjectile(_projectiles[i], dt))
                {
                    Kill(_projectiles[i].T.gameObject);
                    _projectiles.RemoveAt(i);
                }

            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                var t = _timed[i];
                float span = Mathf.Max(1e-3f, t.Until - t.Start);
                t.Update?.Invoke(Mathf.Clamp01((_time - t.Start) / span));
                if (_time >= t.Until)
                {
                    Kill(t.Go);
                    _timed.RemoveAt(i);
                }
            }
        }

        /// <summary>Moves one projectile; false when it's done.</summary>
        bool StepProjectile(Projectile p, float dt)
        {
            if (p.Orbit)
            {
                p.OrbitAngle += p.Speed / OrbitRadius * dt;
                PlaceOrbit(p);
                for (int d = 0; d < _dummies.Count; d++)
                {
                    if (!Touches(p, _dummies[d])) continue;
                    p.OrbitHitCooldown.TryGetValue(d, out float until);
                    if (until > _time) continue;
                    p.OrbitHitCooldown[d] = _time + 0.5f;
                    Flash(d, p.Color);
                }
                return _time < p.OrbitUntil;
            }

            if (p.Coming)
            {
                var home = CasterFloor + Vector3.up;
                var to = home - p.T.position;
                if (to.magnitude < 0.6f) return false;
                p.T.position += to.normalized * p.Speed * dt;
                return true;
            }

            if (p.Seeking)
            {
                var target = Nearest(p.T.position);
                if (target != null)
                {
                    var want = target.position - p.T.position;
                    want.y = 0f;
                    if (want.sqrMagnitude > 0.01f)
                        p.Dir = Vector3.RotateTowards(p.Dir, want.normalized, TurnRateDeg * Mathf.Deg2Rad * dt, 0f);
                }
            }

            float step = p.Speed * dt;
            p.T.position += p.Dir * step;
            p.Traveled += step;

            for (int d = 0; d < _dummies.Count; d++)
            {
                if (!Touches(p, _dummies[d])) continue;
                Flash(d, p.Color);
                Split(p);
                if (p.Returning) { p.Coming = true; return true; }
                return false;
            }

            if (p.Traveled >= p.Range)
            {
                Split(p);
                if (p.Returning) { p.Coming = true; return true; }
                return false;
            }
            return true;
        }

        void Split(Projectile p)
        {
            if (p.SplitCount <= 0) return;
            var e = new EmitterSpec { Speed = p.Speed, Range = Mathf.Max(3f, p.Range * 0.4f), Size = p.Size };
            for (int i = 0; i < p.SplitCount; i++)
            {
                float t = p.SplitCount == 1 ? 0.5f : (float)i / (p.SplitCount - 1);
                var dir = Quaternion.Euler(0f, (t - 0.5f) * 80f, 0f) * p.Dir;
                Spawn(e, dir, p.Color, 0.7f, 0, p.T.position + dir * 0.8f);
            }
            p.SplitCount = 0;
        }

        bool Touches(Projectile p, Transform dummy)
        {
            var a = p.T.position;
            var b = dummy.position;
            a.y = b.y = 0f;
            return (a - b).magnitude < 0.5f + p.Size;
        }

        Transform Nearest(Vector3 from)
        {
            Transform best = null;
            float bestD = float.MaxValue;
            foreach (var d in _dummies)
            {
                float dist = (d.position - from).sqrMagnitude;
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        void HitDummiesIn(AreaSpec ar, Vector3 center, Color color)
        {
            for (int d = 0; d < _dummies.Count; d++)
            {
                var local = _dummies[d].position - center;
                local.y = 0f;
                bool inside;
                switch (ar.Shape)
                {
                    case AreaShape.Circle: inside = local.magnitude <= ar.Radius + 0.5f; break;
                    case AreaShape.Cone:
                        var fromCaster = _dummies[d].position - CasterFloor;
                        fromCaster.y = 0f;
                        inside = fromCaster.magnitude <= ar.Radius && Vector3.Angle(Forward, fromCaster) <= ar.Angle * 0.5f;
                        break;
                    default:
                        var right = Vector3.Cross(Vector3.up, Forward);
                        inside = Mathf.Abs(Vector3.Dot(local, right)) <= ar.Length * 0.5f + 0.5f && Mathf.Abs(Vector3.Dot(local, Forward)) <= ar.Width * 0.5f + 0.5f;
                        break;
                }
                if (inside) Flash(d, color);
            }
        }

        void Flash(int dummy, Color color)
        {
            _dummyFlash[dummy] = 0.45f;
            _dummyFlashColor[dummy] = color;
            _dummies[dummy].GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
        }

        // ---------------- Visual helpers ----------------

        /// <summary>The color of what a spell carries: its status, else its damage.</summary>
        public static Color PayloadColor(List<ActionSpec> payload)
        {
            if (payload != null)
            {
                foreach (var p in payload)
                    if (p.Kind == ActionKind.ApplyStatus)
                        switch (p.Status)
                        {
                            case StatusId.Soaked: return new Color(0.25f, 0.55f, 0.95f);
                            case StatusId.Burning: return new Color(1f, 0.5f, 0.12f);
                            case StatusId.Staggered: return new Color(0.62f, 0.5f, 0.38f);
                            case StatusId.Dazed: return new Color(1f, 0.92f, 0.4f);
                            case StatusId.Rooted: return new Color(0.3f, 0.75f, 0.3f);
                        }
                foreach (var p in payload)
                    if (p.Kind == ActionKind.Knockback) return new Color(0.78f, 0.93f, 0.97f);
            }
            return new Color(0.85f, 0.85f, 0.95f);
        }

        GameObject Ball(string name, float diameter, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            StripCollider(go);
            go.transform.SetParent(_root, true);
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        GameObject Outline(string name, List<Vector3> points, Color color, float width, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            lr.widthMultiplier = width;
            lr.sharedMaterial = MaterialFor(color);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static List<Vector3> Circle(Vector3 c, float r, int n)
        {
            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                pts.Add(c + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r));
            }
            return pts;
        }

        static List<Vector3> Cone(Vector3 apex, Vector3 forward, float r, float angle)
        {
            var pts = new List<Vector3> { apex + Vector3.up * 0.05f };
            int n = 16;
            for (int i = 0; i <= n; i++)
            {
                float a = (-0.5f + (float)i / n) * angle;
                pts.Add(apex + Quaternion.Euler(0f, a, 0f) * forward * r + Vector3.up * 0.05f);
            }
            return pts;
        }

        static List<Vector3> Rect(Vector3 c, Vector3 forward, float length, float width)
        {
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var l = right * (length * 0.5f);
            var w = forward * (width * 0.5f);
            var up = Vector3.up * 0.05f;
            return new List<Vector3> { c - l - w + up, c + l - w + up, c + l + w + up, c - l + w + up };
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>One unlit material per color (URP Unlit, with fallbacks).</summary>
        public static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            m = new Material(shader) { name = "Preview " + ColorUtility.ToHtmlStringRGB(color), hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.color = color;
            Materials[color] = m;
            return m;
        }
    }
}
