using System;
using Relic.World.Noise;
using Relic.World.Regions;

namespace Relic.World.Terrain
{
    /// <summary>
    /// Mesa terracing for badlands (RLC-069): flat treads and steep rocky risers, but only in the
    /// mesa band, from just above the water to MesaGone meters over the local ground and always
    /// under the snow line. Above it the terraces fade out, so a tall peak rising out of a badlands
    /// region is a smooth mountain, not a snowy staircase. Bed thickness varies with altitude (thin
    /// and thick beds in one mesa, 9-24 m) and the beds dip gently across the region.
    /// </summary>
    public sealed partial class TerrainGenerator
    {
        /// <summary>Mean bed (step) thickness, m.</summary>
        const double TerraceStep = 13.0;
        /// <summary>Altitude displacement of the beds (m) and its wavelength in altitude (m): thickness 13 / (1 ± ~0.45).</summary>
        const double StrataShift = 6.0, StrataAltitudeWavelength = 40.0;
        /// <summary>Beds dip by up to ± this many meters over StrataDipWavelength.</summary>
        const double StrataDip = 8.0, StrataDipWavelength = 1800.0;
        /// <summary>Terraces are full up to MesaFull m over the local ground and gone at MesaGone.</summary>
        const double MesaFull = 40.0, MesaGone = 70.0;
        /// <summary>...and gone this far under the snow line, fading over the 30 m below that.</summary>
        const double MesaSnowGap = 12.0;

        readonly SimplexNoise2 _strata;

        double Mesa(double x, double z, double h, double water, double ground, in RegionParams p)
        {
            double climate = 0.5 * _temperature.Fbm(x / TemperatureWavelength, z / TemperatureWavelength, 2) + p.TemperatureBias;
            double top = ground + MesaGone;
            double snowTop = SnowLineFor(climate, p.HeightScale) - MesaSnowGap;
            if (snowTop < top) top = snowTop;
            double full = top - (MesaGone - MesaFull);
            double t = p.Terrace * WorldMath.SmoothStep(water + 1.0, water + 6.0, h) * WorldMath.SmoothStep(top, full, h);
            if (t < 1e-4) return h;

            // Bed coordinate u(h): one unit per bed. The altitude noise makes beds thinner and
            // thicker; du/dh stays positive (StrataShift x max slope of the noise < wavelength).
            double ha = h + StrataDip * _strata.Fbm(x / StrataDipWavelength, z / StrataDipWavelength, 1);
            double a = ha / StrataAltitudeWavelength;
            const double eps = 1e-3;
            double n = _strata.Sample(a, 917.3);
            double dn = (_strata.Sample(a + eps, 917.3) - _strata.Sample(a - eps, 917.3)) / (2.0 * eps);
            double u = (ha + StrataShift * n) / TerraceStep;
            double dudh = (1.0 + StrataShift * dn / StrataAltitudeWavelength) / TerraceStep;
            if (dudh < 0.3 / TerraceStep) dudh = 0.3 / TerraceStep;

            // Risers about 3x the underlying slope and treads that keep 15% of it: reads as mesas,
            // and a riser spans several vertices even at 4 m LODs instead of aliasing into sawtooth
            // triangles. Riser slopes over ~40 degrees shade as rock.
            double fl = Math.Floor(u);
            double f = u - fl;
            double su = fl + 0.85 * WorldMath.SmoothStep(0.3, 0.7, f) + 0.15 * f;
            double stepped = h + (su - u) / dudh;
            return WorldMath.Lerp(h, stepped, t);
        }
    }
}
