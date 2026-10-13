using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Combat
{
    /// <summary>
    /// A geometry-free ICombatSpace for tests and balance simulations. Everyone stands
    /// Distance meters apart. Projectiles hit their aim target with a probability that
    /// depends on their shape (a ring mostly misses a single enemy; a seeking bolt mostly
    /// hits). Areas catch enemies with AreaInsideChance per tick. Deterministic per seed.
    /// </summary>
    public sealed class AbstractSpace : ICombatSpace
    {
        public float DistanceMeters = 8f;
        /// <summary>Chance an aimed projectile hits a single target (player skill / enemy dodging).</summary>
        public float HitChance = 0.7f;
        public float SeekingFactor = 1.35f;
        /// <summary>Chance an enemy is inside an area at a given tick.</summary>
        public float AreaInsideChance = 0.6f;

        /// <summary>Total displacement applied per entity (positive = pushed away).</summary>
        public readonly Dictionary<CombatEntity, float> Displacement = new Dictionary<CombatEntity, float>();
        public int BarriersCreated { get; private set; }

        List<CombatEntity> _entities = new List<CombatEntity>();
        readonly List<Scheduled> _queue = new List<Scheduled>();
        Pcg32 _rng;
        float _time;

        struct Scheduled
        {
            public float At;
            public Action Run;
        }

        public AbstractSpace(ulong seed)
        {
            _rng = Pcg32.ForPurpose(seed, "abstract-space");
        }

        /// <summary>Share the sim's entity list so areas can find who is inside.</summary>
        public void Bind(List<CombatEntity> entities) => _entities = entities;

        void Schedule(float delay, Action run) => _queue.Add(new Scheduled { At = _time + Math.Max(0f, delay), Run = run });

        public void Emit(CombatEntity owner, CombatEntity aimTarget, EmitterSpec e, Action<CombatEntity> onHit)
        {
            if (aimTarget == null) return;
            float travel = e.Delay + DistanceMeters / Math.Max(0.1f, e.Speed);
            float p = Math.Min(1f, HitChance * ShapeFactor(e) * (e.Seeking ? SeekingFactor : 1f));
            for (int i = 0; i < Math.Max(1, e.Count); i++)
            {
                float stagger = e.Shape == EmitShape.Line || e.Shape == EmitShape.Spiral ? i * 0.08f
                              : e.Shape == EmitShape.Orbit ? i * Math.Max(0.1f, e.Range) / Math.Max(1, e.Count) : 0f;
                Schedule(travel + stagger, () =>
                {
                    if (!_rng.Chance(p)) return;
                    onHit(aimTarget);
                    for (int s = 0; s < e.SplitCount; s++)
                        if (_rng.Chance(0.25f)) onHit(aimTarget);
                    if (e.Returning && _rng.Chance(0.5f))
                        Schedule(DistanceMeters / Math.Max(0.1f, e.Speed), () => onHit(aimTarget));
                });
            }
        }

        static float ShapeFactor(EmitterSpec e)
        {
            switch (e.Shape)
            {
                case EmitShape.Cone: return Clamp(40f / Math.Max(1f, e.SpreadDegrees), 0.3f, 1f);
                case EmitShape.Ring:
                case EmitShape.Spiral: return Math.Min(1f, 2f / Math.Max(1, e.Count));
                case EmitShape.Burst: return 0.6f;
                default: return 1f;
            }
        }

        public void CreateArea(CombatEntity owner, CombatEntity aimTarget, AreaSpec area, bool permanent, Action<IReadOnlyList<CombatEntity>> onTick)
        {
            float interval = area.TickInterval > 0f ? area.TickInterval : (permanent ? 1f : 0f);
            float lifetime = permanent ? float.MaxValue : area.Duration;

            Action tick = null;
            float elapsed = 0f;
            tick = () =>
            {
                if (!owner.Alive) return;
                onTick(Inside(owner, aimTarget, area));
                if (interval <= 0f) return;
                elapsed += interval;
                if (elapsed < lifetime) Schedule(interval, tick);
            };
            Schedule(area.Delay, tick);
        }

        List<CombatEntity> Inside(CombatEntity owner, CombatEntity aimTarget, AreaSpec area)
        {
            var inside = new List<CombatEntity>();
            foreach (var e in _entities)
            {
                if (!e.Alive) continue;
                bool ally = !owner.IsEnemyOf(e);
                if (ally)
                {
                    if (area.FollowsSelf) inside.Add(e);
                }
                else if (area.FollowsSelf || e == aimTarget)
                {
                    if (_rng.Chance(AreaInsideChance)) inside.Add(e);
                }
            }
            return inside;
        }

        public void CreateBarrier(CombatEntity owner, CombatEntity aimTarget, AreaSpec wall) => BarriersCreated++;

        public void Displace(CombatEntity owner, CombatEntity target, float meters)
        {
            Displacement.TryGetValue(target, out float d);
            Displacement[target] = d + meters;
        }

        public float Distance(CombatEntity a, CombatEntity b) => a == b ? 0f : DistanceMeters;

        public void Tick(float dt)
        {
            _time += dt;
            // Callbacks may schedule more events; keep draining whatever is due.
            for (int guard = 0; guard < 10000; guard++)
            {
                int idx = -1;
                for (int i = 0; i < _queue.Count; i++)
                    if (_queue[i].At <= _time && (idx < 0 || _queue[i].At < _queue[idx].At)) idx = i;
                if (idx < 0) break;
                var ev = _queue[idx];
                _queue.RemoveAt(idx);
                ev.Run();
            }
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
