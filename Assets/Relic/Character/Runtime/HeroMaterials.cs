using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Material instances for the hero: tinted copies of the World toon material (cel ramp,
    /// rim and outline), plus unlit "flat" materials for painted face details and glow.
    /// Cached by color so a rebuild with the same kit reuses everything; destroyed with the hero.
    /// </summary>
    public sealed class HeroMaterials
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int RimStrength = Shader.PropertyToID("_RimStrength");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");

        readonly Material _toon;
        readonly Shader _flat;
        readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public HeroMaterials(Material toon)
        {
            _toon = toon != null ? toon : new Material(Shader.Find("Relic/World/Toon"));
            _flat = Shader.Find("Relic/Character/Flat");
        }

        /// <summary>A toon instance tinted to c. outline = outline width in px (World default 2.2).</summary>
        public Material Toon(Color c, float outline = 1.6f, float rim = 0.45f)
        {
            string key = "t" + Key(c) + outline.ToString("0.00") + rim.ToString("0.00");
            if (_cache.TryGetValue(key, out Material m)) return m;
            m = new Material(_toon) { name = "Hero Toon " + Key(c) };
            m.SetColor(BaseColor, c);
            m.SetFloat(OutlineWidth, outline);
            m.SetFloat(RimStrength, rim);
            // Outline a darker version of the part's own color (softer than black, anime-like).
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Color oc = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.1f + 0.15f), v * 0.32f);
            m.SetColor(OutlineColor, oc);
            _cache[key] = m;
            return m;
        }

        public Material Toon(Rgb c, float outline = 1.6f, float rim = 0.45f) => Toon(ToColor(c), outline, rim);

        /// <summary>Unlit color; intensity &gt; 1 glows. Falls back to an outline-less toon if the shader is missing.</summary>
        public Material Flat(Color c, float intensity = 1f)
        {
            string key = "f" + Key(c) + intensity.ToString("0.00");
            if (_cache.TryGetValue(key, out Material m)) return m;
            if (_flat != null)
            {
                m = new Material(_flat) { name = "Hero Flat " + Key(c) };
                m.SetColor(BaseColor, c);
                m.SetFloat(Intensity, intensity);
            }
            else
            {
                m = new Material(_toon) { name = "Hero Flat(toon) " + Key(c) };
                m.SetColor(BaseColor, c * intensity);
                m.SetFloat(OutlineWidth, 0f);
            }
            _cache[key] = m;
            return m;
        }

        public Material Glow(Rgb c, float intensity) => Flat(ToColor(c), 1.2f + intensity * 0.9f);

        public static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        static string Key(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public int Count => _cache.Count;

        public void Destroy()
        {
            foreach (Material m in _cache.Values)
            {
                if (m == null) continue;
                if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m);
            }
            _cache.Clear();
        }
    }
}
