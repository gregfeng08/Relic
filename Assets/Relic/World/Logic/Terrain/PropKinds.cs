namespace Relic.World.Terrain
{
    /// <summary>Props scattered on the terrain. Values are stable (saved data may refer to them).</summary>
    public enum PropKind : byte
    {
        Broadleaf = 0,
        Pine = 1,
        Boulder = 2,
        Birch = 3,
        DeadTree = 4,
        Palm = 5,
        SnowPine = 6,
        Bush = 7,
        Rock = 8,
        DeadShrub = 9,
        Succulent = 10,
        Grass = 11,
        DryGrass = 12,
        Flowers = 13,
    }

    /// <summary>How far a prop is drawn, which decides its LOD and collider treatment.</summary>
    public enum PropClass : byte
    {
        /// <summary>Trees and boulders: drawn to the view distance (far LOD), colliders near the player.</summary>
        Large = 0,
        /// <summary>Bushes, rocks, shrubs: drawn to ~150 m.</summary>
        Medium = 1,
        /// <summary>Grass and flowers: drawn to ~70 m, sway in the wind.</summary>
        Ground = 2,
    }

    /// <summary>One prop, positioned relative to its chunk's origin (meters).</summary>
    public struct PropInstance
    {
        public PropKind Kind;
        public float X, Y, Z;
        public float Scale;
        public float Yaw; // degrees
    }

    public static class PropKinds
    {
        public const int Count = 14;

        public static PropClass ClassOf(PropKind k)
        {
            switch (k)
            {
                case PropKind.Bush:
                case PropKind.Rock:
                case PropKind.DeadShrub:
                case PropKind.Succulent:
                    return PropClass.Medium;
                case PropKind.Grass:
                case PropKind.DryGrass:
                case PropKind.Flowers:
                    return PropClass.Ground;
                default:
                    return PropClass.Large;
            }
        }

        public static bool IsTree(PropKind k) =>
            k == PropKind.Broadleaf || k == PropKind.Pine || k == PropKind.Birch || k == PropKind.DeadTree || k == PropKind.Palm || k == PropKind.SnowPine;

        /// <summary>Props the player bumps into (near colliders): trees, boulders and big rocks.</summary>
        public static bool IsSolid(PropKind k) => IsTree(k) || k == PropKind.Boulder;
    }
}
