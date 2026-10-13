using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Effects;

namespace Relic.Magic.Tests
{
    /// <summary>
    /// Compares two EffectPrograms field by field: kinds, enums, flags and
    /// counts must match exactly; numbers within a relative tolerance. Rule names
    /// are flavor and are ignored.
    /// </summary>
    public static class ProgramAssert
    {
        public static void Close(EffectProgram expected, EffectProgram actual, float relTol = 0.05f)
        {
            var problems = new List<string>();
            Compare(expected, actual, relTol, problems);
            if (problems.Count > 0)
                Assert.Fail("programs differ:\n  " + string.Join("\n  ", problems) +
                            "\nexpected: " + EffectJson.Serialize(expected) + "\nactual:   " + EffectJson.Serialize(actual));
        }

        static void Compare(EffectProgram e, EffectProgram a, float tol, List<string> p)
        {
            if (a == null) { p.Add("actual program is null"); return; }
            Eq("rules.count", e.Rules.Count, a.Rules.Count, p);
            for (int i = 0; i < Math.Min(e.Rules.Count, a.Rules.Count); i++)
            {
                var er = e.Rules[i];
                var ar = a.Rules[i];
                string path = $"rules[{i}]";
                Eq(path + ".trigger", er.Trigger.Kind, ar.Trigger.Kind, p);
                Num(path + ".cooldown", er.Cooldown, ar.Cooldown, tol, p);
                Num(path + ".chance", er.Chance, ar.Chance, tol, p);
                Eq(path + ".costResource", er.CostResource, ar.CostResource, p);
                Num(path + ".costAmount", er.CostAmount, ar.CostAmount, tol, p);
                Eq(path + ".conditions.count", er.Conditions.Count, ar.Conditions.Count, p);
                Actions(path + ".actions", er.Actions, ar.Actions, tol, p);
            }
        }

        static void Actions(string path, List<ActionSpec> e, List<ActionSpec> a, float tol, List<string> p)
        {
            int ec = e?.Count ?? 0, ac = a?.Count ?? 0;
            Eq(path + ".count", ec, ac, p);
            for (int i = 0; i < Math.Min(ec, ac); i++)
            {
                var x = e[i];
                var y = a[i];
                string q = $"{path}[{i}]";
                Eq(q + ".kind", x.Kind, y.Kind, p);
                Eq(q + ".target", x.Target, y.Target, p);
                Num(q + ".amount", x.Amount, y.Amount, tol, p);
                if (x.Kind == ActionKind.Damage) Eq(q + ".damageKind", x.DamageKind, y.DamageKind, p);
                Eq(q + ".status", x.Status, y.Status, p);
                Num(q + ".duration", x.Duration, y.Duration, tol, p);
                Eq(q + ".stacks", x.Stacks, y.Stacks, p);
                if ((x.Emitter == null) != (y.Emitter == null)) p.Add(q + ".emitter presence differs");
                else if (x.Emitter != null)
                {
                    var m = x.Emitter;
                    var n = y.Emitter;
                    Eq(q + ".emitter.shape", m.Shape, n.Shape, p);
                    Eq(q + ".emitter.count", m.Count, n.Count, p);
                    Num(q + ".emitter.speed", m.Speed, n.Speed, tol, p);
                    Num(q + ".emitter.range", m.Range, n.Range, tol, p);
                    Num(q + ".emitter.spreadDegrees", m.SpreadDegrees, n.SpreadDegrees, tol, p);
                    Num(q + ".emitter.delay", m.Delay, n.Delay, tol, p);
                    Eq(q + ".emitter.seeking", m.Seeking, n.Seeking, p);
                    Eq(q + ".emitter.returning", m.Returning, n.Returning, p);
                    Eq(q + ".emitter.splitCount", m.SplitCount, n.SplitCount, p);
                    Num(q + ".emitter.size", m.Size, n.Size, tol, p);
                    Eq(q + ".emitter.airborne", m.Airborne, n.Airborne, p);
                }
                if ((x.Area == null) != (y.Area == null)) p.Add(q + ".area presence differs");
                else if (x.Area != null)
                {
                    Eq(q + ".area.shape", x.Area.Shape, y.Area.Shape, p);
                    Num(q + ".area.radius", x.Area.Radius, y.Area.Radius, tol, p);
                    Num(q + ".area.length", x.Area.Length, y.Area.Length, tol, p);
                    Num(q + ".area.duration", x.Area.Duration, y.Area.Duration, tol, p);
                    Num(q + ".area.delay", x.Area.Delay, y.Area.Delay, tol, p);
                }
                Actions(q + ".payload", x.Payload, y.Payload, tol, p);
            }
        }

        static void Eq<T>(string path, T e, T a, List<string> p)
        {
            if (!EqualityComparer<T>.Default.Equals(e, a)) p.Add($"{path}: expected {e}, got {a}");
        }

        static void Num(string path, float e, float a, float tol, List<string> p)
        {
            float scale = Math.Max(Math.Abs(e), 1e-3f);
            if (Math.Abs(e - a) > tol * scale + 1e-4f) p.Add($"{path}: expected {e}, got {a}");
        }
    }
}
