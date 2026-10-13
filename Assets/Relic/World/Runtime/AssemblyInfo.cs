using System.Runtime.CompilerServices;

// The editor tools (scene builder, screenshot capture) build chunks directly.
[assembly: InternalsVisibleTo("Relic.World.Editor")]
[assembly: InternalsVisibleTo("Relic.World.PlayTests")]
