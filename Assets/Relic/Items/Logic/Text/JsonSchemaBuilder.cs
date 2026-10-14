using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Relic.Core.Effects;

namespace Relic.Items.Text
{
    /// <summary>
    /// Builds JSON schemas for constrained decoding by reflecting over the Core
    /// grammar classes, so the schema always matches what EffectJson accepts
    /// (camelCase field names, enums as strings, no extra fields). Payload
    /// recursion is unrolled to the grammar's max depth: llama.cpp's
    /// schema-to-grammar converter is happiest without recursive $refs.
    /// </summary>
    public static class JsonSchemaBuilder
    {
        /// <summary>Schema for an object of type t (public instance fields only).</summary>
        public static JObject ForType(Type t, int payloadDepth = 1, string[] required = null)
        {
            var props = new JObject();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(List<ActionSpec>) && f.Name == nameof(ActionSpec.Payload))
                {
                    if (payloadDepth >= EffectValidator.MaxPayloadDepth) continue; // deepest level: no payload field at all
                    props[Camel(f.Name)] = new JObject
                    {
                        ["type"] = "array",
                        ["maxItems"] = EffectValidator.MaxActionsPerList,
                        ["items"] = ForType(typeof(ActionSpec), payloadDepth + 1, new[] { "kind" }),
                    };
                    continue;
                }
                props[Camel(f.Name)] = ForValue(f.FieldType, payloadDepth);
            }
            var o = new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["additionalProperties"] = false,
            };
            if (required != null && required.Length > 0) o["required"] = new JArray(required);
            return o;
        }

        static JObject ForValue(Type t, int depth)
        {
            if (t == typeof(float) || t == typeof(double)) return new JObject { ["type"] = "number" };
            if (t == typeof(int) || t == typeof(long)) return new JObject { ["type"] = "integer" };
            if (t == typeof(bool)) return new JObject { ["type"] = "boolean" };
            if (t == typeof(string)) return new JObject { ["type"] = "string" };
            if (t.IsEnum) return new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(t)) };
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var item = t.GetGenericArguments()[0];
                var arr = new JObject { ["type"] = "array", ["items"] = ForValue(item, depth) };
                if (item == typeof(ActionSpec)) arr["maxItems"] = EffectValidator.MaxActionsPerList;
                if (item == typeof(ConditionSpec)) arr["maxItems"] = 3;
                return arr;
            }
            if (t == typeof(ActionSpec)) return ForType(t, depth, new[] { "kind" });
            if (t == typeof(TriggerSpec)) return ForType(t, depth, new[] { "kind" });
            if (t == typeof(ConditionSpec)) return ForType(t, depth, new[] { "kind" });
            if (t.IsClass) return ForType(t, depth);
            return new JObject();
        }

        public static JObject Rule() => ForType(typeof(EffectRule), 1, new[] { "name", "trigger", "actions" });

        public static JObject Program(int minRules, int maxRules)
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["version"] = new JObject { ["type"] = "integer", ["enum"] = new JArray(1) },
                    ["rules"] = new JObject { ["type"] = "array", ["minItems"] = minRules, ["maxItems"] = maxRules, ["items"] = Rule() },
                },
                ["required"] = new JArray("rules"),
                ["additionalProperties"] = false,
            };
        }

        static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
