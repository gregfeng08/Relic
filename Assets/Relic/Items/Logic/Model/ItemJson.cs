using Newtonsoft.Json;
using Relic.Core.Effects;

namespace Relic.Items
{
    /// <summary>
    /// JSON for items and boss definitions: the same conventions as EffectJson
    /// (camelCase, enums as strings, unknown fields rejected), so a saved item
    /// embeds its EffectProgram in exactly the grammar's format.
    /// </summary>
    public static class ItemJson
    {
        public static string Serialize<T>(T value, bool indented = false) =>
            JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, EffectJson.Settings);

        public static T Deserialize<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json, EffectJson.Settings);

        public static bool TryDeserialize<T>(string json, out T value, out string error) where T : class
        {
            try
            {
                value = Deserialize<T>(json);
                error = value == null ? "empty document" : null;
                return value != null;
            }
            catch (JsonException e)
            {
                value = null;
                error = e.Message;
                return false;
            }
        }

        /// <summary>Deep copy through JSON (programs are mutated by ScaleToFit, so clone before experimenting).</summary>
        public static T Clone<T>(T value) => Deserialize<T>(Serialize(value));
    }
}
