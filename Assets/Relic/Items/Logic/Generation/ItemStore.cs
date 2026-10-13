using System.Collections.Generic;
using System.IO;

namespace Relic.Items
{
    /// <summary>
    /// Persistence for generated items. LLM text is not reproducible, so it is
    /// generated once and saved (06-tech-architecture); the pipeline checks the
    /// store before generating and returns the saved item when it exists.
    /// </summary>
    public interface IItemStore
    {
        bool TryGet(string id, out Item item);
        void Put(Item item);
    }

    public sealed class InMemoryItemStore : IItemStore
    {
        readonly Dictionary<string, string> _json = new Dictionary<string, string>();

        public int Count => _json.Count;

        public bool TryGet(string id, out Item item)
        {
            item = null;
            if (!_json.TryGetValue(id, out var json)) return false;
            item = ItemJson.Deserialize<Item>(json); // a copy, like a load from disk
            return true;
        }

        public void Put(Item item) => _json[item.Id] = ItemJson.Serialize(item);
    }

    /// <summary>One JSON file per item: &lt;directory&gt;/&lt;id&gt;.json.</summary>
    public sealed class JsonFileItemStore : IItemStore
    {
        readonly string _dir;

        public JsonFileItemStore(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        string PathFor(string id) => Path.Combine(_dir, id + ".json");

        public bool TryGet(string id, out Item item)
        {
            item = null;
            var p = PathFor(id);
            if (!File.Exists(p)) return false;
            item = ItemJson.Deserialize<Item>(File.ReadAllText(p));
            return item != null;
        }

        public void Put(Item item)
        {
            var p = PathFor(item.Id);
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, ItemJson.Serialize(item, indented: true));
            if (File.Exists(p)) File.Delete(p);
            File.Move(tmp, p);
        }
    }
}
