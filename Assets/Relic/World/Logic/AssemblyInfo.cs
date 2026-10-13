using System.Runtime.CompilerServices;

// EditMode tests reach a few internal helpers (brute-force region search).
[assembly: InternalsVisibleTo("Relic.World.Tests")]
