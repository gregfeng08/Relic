using NUnit.Framework;
using Relic.World.Chunks;
using Relic.World.Terrain;

namespace Relic.World.Tests
{
    public class PropScatterTests
    {
        [Test]
        public void PlacementIsDeterministicAndInsideTheChunk()
        {
            var g = new TerrainGenerator(1337);
            var a = new PropInstance[PropScatter.MaxPerChunk];
            var b = new PropInstance[PropScatter.MaxPerChunk];
            int total = 0;
            for (long cx = -6; cx <= 6; cx++)
            for (long cz = -6; cz <= 6; cz++)
            {
                var c = new ChunkCoord(cx, cz);
                int na = PropScatter.Place(g, c, a);
                int nb = PropScatter.Place(new TerrainGenerator(1337), c, b);
                Assert.AreEqual(na, nb);
                for (int i = 0; i < na; i++)
                {
                    Assert.AreEqual(a[i].X, b[i].X);
                    Assert.AreEqual(a[i].Kind, b[i].Kind);
                    Assert.That(a[i].X, Is.InRange(0f, ChunkMath.ChunkSize));
                    Assert.That(a[i].Z, Is.InRange(0f, ChunkMath.ChunkSize));
                    Assert.That(a[i].Scale, Is.GreaterThan(0.3f));
                    double wx = ChunkMath.OriginX(c) + a[i].X, wz = ChunkMath.OriginZ(c) + a[i].Z;
                    g.HeightAndWater(wx, wz, out float h, out float w);
                    Assert.That(h, Is.GreaterThan(w), "props never stand in water");
                }
                total += na;
            }
            Assert.Greater(total, 50, "some props near spawn");
        }

        [Test]
        public void ForestsAreDenserThanGrassland()
        {
            Assert.Greater(PropScatter.TreeDensity(Biome.Forest), PropScatter.TreeDensity(Biome.Grassland) * 5f);
            Assert.AreEqual(0f, PropScatter.TreeDensity(Biome.Seabed));
            Assert.AreEqual(0f, PropScatter.TreeDensity(Biome.Desert));
        }
    }
}
