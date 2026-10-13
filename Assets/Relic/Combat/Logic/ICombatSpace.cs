using System;
using System.Collections.Generic;
using Relic.Core.Effects;

namespace Relic.Combat
{
    /// <summary>
    /// Everything spatial the combat rules need. In the game, a Unity implementation
    /// spawns real projectiles, areas and walls and reports what they touch. In tests
    /// and balance sims, AbstractSpace models it statistically.
    /// The space calls back into the rules; it never applies damage itself.
    /// </summary>
    public interface ICombatSpace
    {
        /// <summary>Launch projectiles. Call onHit(victim) once per projectile that hits something.</summary>
        void Emit(CombatEntity owner, CombatEntity aimTarget, EmitterSpec emitter, Action<CombatEntity> onHit);

        /// <summary>Create an area. Call onTick(entitiesInside) at each application (once if TickInterval is 0).</summary>
        void CreateArea(CombatEntity owner, CombatEntity aimTarget, AreaSpec area, bool permanent, Action<IReadOnlyList<CombatEntity>> onTick);

        void CreateBarrier(CombatEntity owner, CombatEntity aimTarget, AreaSpec wall);

        /// <summary>Move target by meters along the owner→target line (positive = away, negative = toward owner).</summary>
        void Displace(CombatEntity owner, CombatEntity target, float meters);

        float Distance(CombatEntity a, CombatEntity b);

        /// <summary>Advance projectiles and areas.</summary>
        void Tick(float dt);
    }

    /// <summary>Weather, time of day and location tags for conditions.</summary>
    public sealed class CombatEnvironment
    {
        public bool IsNight;
        public bool IsRaining;
        public readonly HashSet<string> RegionTags = new HashSet<string>();
    }
}
