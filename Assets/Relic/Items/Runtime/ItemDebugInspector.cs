using Relic.Core.Effects;
using Relic.Items.Bosses;
using Relic.Items.Samples;
using UnityEngine;

namespace Relic.Items.Runtime
{
    /// <summary>
    /// Debug inspector: add to any GameObject, set a seed, then use the component's
    /// context menu (⋮) "Generate item" / "Generate boss" to see the generated
    /// JSON and a readable description in the Inspector. Template mode only (no LLM).
    /// </summary>
    public sealed class ItemDebugInspector : MonoBehaviour
    {
        [Header("Item")]
        public long worldSeed = 2026;
        public int index;
        [Range(0, 10)] public int tier = 3;
        public Rarity rarity = Rarity.Legendary;
        public bool pickBaseType;
        public BaseType baseType = BaseType.Sword;

        [Header("Output")]
        [TextArea(6, 30)] public string description;
        [TextArea(6, 40)] public string json;

        [ContextMenu("Generate item")]
        public void GenerateItem()
        {
            ulong seed = unchecked((ulong)worldSeed);
            var world = StubWorld.Create(seed);
            var gen = new ItemGenerator(world);
            var item = gen.Generate(new ItemSpec
            {
                WorldSeed = seed,
                Index = index,
                Tier = tier,
                Rarity = rarity,
                BaseType = pickBaseType ? baseType : (BaseType?)null,
            });
            description = SampleWriter.ItemMarkdown(item, index, world, true);
            json = ItemJson.Serialize(item, indented: true);
        }

        [ContextMenu("Generate boss")]
        public void GenerateBoss()
        {
            ulong seed = unchecked((ulong)worldSeed);
            var world = StubWorld.Create(seed);
            var boss = BossGenerator.Generate(seed, index, tier, world);
            description = SampleWriter.BossMarkdown(boss, index, world);
            json = ItemJson.Serialize(boss, indented: true);
        }
    }
}
