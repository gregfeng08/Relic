using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// The hero's one material: Relic/Character/Toon reads albedo, outline width, emission, warm
    /// shade and rim from the vertices, so the whole hero is a single draw (plus its outline pass).
    /// Lighting comes from the same RelicToonCommon code and globals as the World toon shader.
    /// </summary>
    public sealed class HeroMaterials
    {
        public Material Material { get; private set; }

        public HeroMaterials(Material worldToon)
        {
            Shader s = Shader.Find("Relic/Character/Toon");
            if (s != null) Material = new Material(s) { name = "Hero Toon" };
            else
            {
                // Missing shader (stripped build): the World toon still draws the shape, untinted.
                Debug.LogWarning("[Relic.Character] Relic/Character/Toon not found; falling back to the World toon material.");
                Material = worldToon != null ? new Material(worldToon) : new Material(Shader.Find("Relic/World/Toon"));
            }
        }

        public static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        public void Destroy()
        {
            if (Material == null) return;
            if (Application.isPlaying) Object.Destroy(Material); else Object.DestroyImmediate(Material);
            Material = null;
        }
    }
}
