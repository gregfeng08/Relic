using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Relic.Core.Effects
{
    /// <summary>
    /// JSON form of the effect grammar: camelCase fields, enums as strings.
    /// This is the format the LLM writes and the format saved to disk.
    /// </summary>
    public static class EffectJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            Formatting = Formatting.None,
        };

        public static string Serialize(EffectProgram program, bool indented = false) =>
            JsonConvert.SerializeObject(program, indented ? Formatting.Indented : Formatting.None, Settings);

        /// <summary>Throws JsonException on unknown fields or bad enum names, so malformed LLM output is rejected, not guessed at.</summary>
        public static EffectProgram Deserialize(string json) =>
            JsonConvert.DeserializeObject<EffectProgram>(json, Settings);

        public static bool TryDeserialize(string json, out EffectProgram program, out string error)
        {
            try
            {
                program = Deserialize(json);
                error = program == null ? "empty document" : null;
                return program != null;
            }
            catch (JsonException e)
            {
                program = null;
                error = e.Message;
                return false;
            }
        }
    }
}
