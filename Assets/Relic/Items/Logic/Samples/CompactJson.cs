using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relic.Core.Effects;

namespace Relic.Items.Samples
{
    /// <summary>
    /// Grammar JSON with default-valued fields left out (they come back on parse),
    /// so a rule reads in one line. "kind" fields are always kept. Round-trips:
    /// EffectJson parses the compact form to the same program.
    /// </summary>
    public static class CompactJson
    {
        static readonly JObject DefRule = Defaults(new EffectRule());
        static readonly JObject DefTrigger = Defaults(new TriggerSpec());
        static readonly JObject DefCondition = Defaults(new ConditionSpec());
        static readonly JObject DefAction = Defaults(new ActionSpec());
        static readonly JObject DefEmitter = Defaults(new EmitterSpec());
        static readonly JObject DefArea = Defaults(new AreaSpec());

        static JObject Defaults(object o) => JObject.Parse(JsonConvert.SerializeObject(o, EffectJson.Settings));

        public static string Rule(EffectRule rule)
        {
            var program = JObject.Parse(EffectJson.Serialize(new EffectProgram { Rules = { rule } }));
            var o = (JObject)program["rules"][0];
            StripRule(o);
            return o.ToString(Formatting.None);
        }

        public static string Program(EffectProgram p)
        {
            var o = JObject.Parse(EffectJson.Serialize(p));
            foreach (var r in o["rules"].Children<JObject>()) StripRule(r);
            return o.ToString(Formatting.None);
        }

        static void Strip(JObject o, JObject def)
        {
            foreach (var prop in o.Properties().ToList())
            {
                if (prop.Name == "kind") continue;
                var d = def[prop.Name];
                if (d != null && JToken.DeepEquals(d, prop.Value)) prop.Remove();
            }
        }

        static void StripRule(JObject r)
        {
            if (r["trigger"] is JObject t) Strip(t, DefTrigger);
            if (r["conditions"] is JArray cs) foreach (var c in cs.Children<JObject>()) Strip(c, DefCondition);
            if (r["actions"] is JArray acts) foreach (var a in acts.Children<JObject>()) StripAction(a);
            Strip(r, DefRule);
        }

        static void StripAction(JObject a)
        {
            if (a["emitter"] is JObject e) Strip(e, DefEmitter);
            if (a["area"] is JObject ar) Strip(ar, DefArea);
            if (a["payload"] is JArray p) foreach (var x in p.Children<JObject>()) StripAction(x);
            Strip(a, DefAction);
        }
    }
}
