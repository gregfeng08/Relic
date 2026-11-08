using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Relic.Lore
{
    /// <summary>Thrown when Lore Bible data is malformed or inconsistent.</summary>
    public sealed class LoreDataException : Exception
    {
        public LoreDataException(string message) : base(message) { }
        public LoreDataException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// JSON settings for the Lore Bible, matching Relic.Core.Effects.EffectJson: camelCase
    /// fields, enums as strings, unknown fields rejected. Lists are replaced, not appended to.
    /// </summary>
    public static class LoreJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter { AllowIntegerValues = false } },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.Indented,
        };

        /// <summary>Parses one document. <paramref name="source"/> names the file in error messages.</summary>
        public static T Parse<T>(string json, string source)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new LoreDataException(source + ": empty document");
            try
            {
                var value = JsonConvert.DeserializeObject<T>(json, Settings);
                if (value == null) throw new LoreDataException(source + ": empty document");
                return value;
            }
            catch (JsonException e)
            {
                throw new LoreDataException(source + ": " + e.Message, e);
            }
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
    }
}
