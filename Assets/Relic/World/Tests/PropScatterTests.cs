using NUnit.Framework;
using Relic.World.Chunks;
using Relic.World.Regions;
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
        public void GroundCoverIsDeterministicAndDry()
        {
            var g = new TerrainGenerator(1337);
            var a = new PropInstance[PropScatter.MaxSmallPerChunk];
            var b = new PropInstance[PropScatter.MaxSmallPerChunk];
            int total = 0, grass = 0;
            for (long cx = -4; cx <= 4; cx++)
            for (long cz = -4; cz <= 4; cz++)
            {
                var c = new ChunkCoord(cx, cz);
                int na = PropScatter.PlaceSmall(g, c, a);
                Assert.AreEqual(na, PropScatter.PlaceSmall(new TerrainGenerator(1337), c, b));
                for (int i = 0; i < na; i++)
                {
                    Assert.AreEqual(a[i].Z, b[i].Z);
                    Assert.AreEqual(PropClass.Ground == PropKinds.ClassOf(a[i].Kind) || PropKinds.ClassOf(a[i].Kind) == PropClass.Medium, true);
                    g.HeightAndWater(ChunkMath.OriginX(c) + a[i].X, ChunkMath.OriginZ(c) + a[i].Z, out float h, out float w);
                    Assert.That(h, Is.GreaterThan(w));
                    if (a[i].Kind == PropKind.Grass || a[i].Kind == PropKind.Flowers) grass++;
                }
                total += na;
            }
            Assert.Greater(grass, 200, "spawn meadow is grassy (" + total + " ground props)");
        }

        [Test]
        public void ForestRegionsAreDenserThanBadlandsAndNothingGrowsOnSnow()
        {
            Assert.Greater(PropScatter.FamilyTreeFactor(RegionFamily.Forest), 2f * PropScatter.FamilyTreeFactor(RegionFamily.Meadow));
            Assert.Less(PropScatter.FamilyTreeFactor(RegionFamily.Badlands), PropScatter.FamilyTreeFactor(RegionFamily.Meadow));
            var g = new TerrainGenerator(1337);
            var a = new PropInstance[PropScatter.MaxPerChunk];
            int trees = 0;
            for (long cx = -40; cx <= 40; cx += 3)
            for (long cz = -40; cz <= 40; cz += 3)
            {
                var c = new ChunkCoord(cx, cz);
                int n = PropScatter.Place(g, c, a);
                for (int i = 0; i < n; i++)
                {
                    if (!PropKinds.IsTree(a[i].Kind)) continue;
                    trees++;
                    TerrainSample s = g.Sample(ChunkMath.OriginX(c) + a[i].X, ChunkMath.OriginZ(c) + a[i].Z);
                    Assert.That(s.Height, Is.LessThanOrEqualTo(s.SnowLine), "tree above the snow line");
                }
            }
            Assert.Greater(trees, 100);
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
