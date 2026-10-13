using NUnit.Framework;
using Relic.World.Noise;
using Relic.World.Terrain;

namespace Relic.World.Tests
{
    public class NoiseTests
    {
        [Test]
        public void SimplexIsDeterministicAndSeedDependent()
        {
            var a = new SimplexNoise2(42);
            var b = new SimplexNoise2(42);
            var c = new SimplexNoise2(43);
            int differ = 0;
            for (int i = 0; i < 200; i++)
            {
                double x = i * 0.731 - 50.0, y = i * 1.37 + 3.0;
                Assert.AreEqual(a.Sample(x, y), b.Sample(x, y));
                if (a.Sample(x, y) != c.Sample(x, y)) differ++;
            }
            Assert.Greater(differ, 150);
        }

        [Test]
        public void SimplexStaysInRangeWithZeroMean()
        {
            var n = new SimplexNoise2(7);
            double min = double.MaxValue, max = double.MinValue, sum = 0.0;
            int count = 0;
            for (int i = 0; i < 300; i++)
            {
                for (int j = 0; j < 300; j++)
                {
                    double v = n.Sample(i * 0.173, j * 0.191);
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                    count++;
                }
            }
            Assert.That(min, Is.GreaterThan(-1.1));
            Assert.That(max, Is.LessThan(1.1));
            Assert.That(max - min, Is.GreaterThan(1.2), "noise should use most of its range");
            Assert.That(sum / count, Is.InRange(-0.08, 0.08));
        }

        [Test]
        public void SimplexIsContinuous()
        {
            var n = new SimplexNoise2(99);
            double prev = n.Sample(0, 0.3);
            for (int i = 1; i < 20000; i++)
            {
                double v = n.Sample(i * 0.001, 0.3);
                Assert.That(System.Math.Abs(v - prev), Is.LessThan(0.02));
                prev = v;
            }
        }

        [Test]
        public void SimplexIsFiniteFarFromOrigin()
        {
            var n = new SimplexNoise2(1);
            foreach (double c in new[] { 1e6, -1e6, 3.3e7, -9.1e8 })
            {
                double v = n.Sample(c, -c * 0.5);
                Assert.IsTrue(WorldMath.IsFinite(v));
                Assert.That(v, Is.InRange(-1.1, 1.1));
                Assert.That(n.Ridged(c, c, 3), Is.InRange(0.0, 1.0));
            }
        }

        [Test]
        public void FloorToLongHandlesNegativesAndLargeValues()
        {
            Assert.AreEqual(-1, WorldMath.FloorToLong(-0.5));
            Assert.AreEqual(-1, WorldMath.FloorToLong(-1.0));
            Assert.AreEqual(-2, WorldMath.FloorToLong(-1.0000001));
            Assert.AreEqual(0, WorldMath.FloorToLong(0.999));
            Assert.AreEqual(1000000, WorldMath.FloorToLong(1000000.25));
            Assert.AreEqual(-1000001, WorldMath.FloorToLong(-1000000.25));
        }

        [Test]
        public void MonotoneSplineIsMonotoneAndClamps()
        {
            var s = new MonotoneSpline(new[] { -1.0, 0.0, 0.5, 1.0 }, new[] { -10.0, 0.0, 2.0, 30.0 });
            Assert.AreEqual(-10.0, s.Evaluate(-5.0));
            Assert.AreEqual(30.0, s.Evaluate(5.0));
            Assert.AreEqual(2.0, s.Evaluate(0.5), 1e-12);
            double prev = s.Evaluate(-1.0);
            for (int i = 1; i <= 2000; i++)
            {
                double v = s.Evaluate(-1.0 + i * 0.001);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev - 1e-12));
                prev = v;
            }
        }
    }
}
