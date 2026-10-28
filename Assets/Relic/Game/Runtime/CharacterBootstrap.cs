using Relic.Character;
using Relic.World.Runtime;
using UnityEngine;

namespace Relic.Game
{
    /// <summary>
    /// Wires the procedural hero (Relic.Character) into the World sandbox. World can't reference
    /// Character, so it exposes TerrainSandbox.AvatarFactory and the Game layer fills it in.
    /// </summary>
    public static class CharacterBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            TerrainSandbox.AvatarFactory = HeroPlayerDriver.Create;
        }
    }
}
