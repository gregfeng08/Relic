using System;
using UnityEngine;

namespace Relic.Magic.Desk
{
    /// <summary>Loads the shipped glyph set (Assets/Relic/Magic/Resources/RelicGlyphs.json).</summary>
    public static class GlyphLibrary
    {
        public const string ResourceName = "RelicGlyphs";

        static GlyphRegistry _cached;

        public static GlyphRegistry Load()
        {
            if (_cached != null) return _cached;
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) throw new InvalidOperationException($"Missing Resources/{ResourceName}.json (Assets/Relic/Magic/Resources)");
            _cached = GlyphRegistry.FromJson(asset.text);
            return _cached;
        }
    }
}
