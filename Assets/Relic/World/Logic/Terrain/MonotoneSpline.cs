using System;

namespace Relic.World.Terrain
{
    /// <summary>
    /// Monotone cubic (Fritsch-Carlson) interpolation through fixed knots. Clamps outside the
    /// knot range. Immutable after construction; Evaluate is thread-safe and allocation-free.
    /// </summary>
    public sealed class MonotoneSpline
    {
        readonly double[] _x;
        readonly double[] _y;
        readonly double[] _m;

        public MonotoneSpline(double[] xs, double[] ys)
        {
            if (xs == null || ys == null || xs.Length != ys.Length || xs.Length < 2)
                throw new ArgumentException("Need at least two knots with matching x and y.");
            int n = xs.Length;
            _x = (double[])xs.Clone();
            _y = (double[])ys.Clone();
            for (int i = 1; i < n; i++)
                if (!(_x[i] > _x[i - 1])) throw new ArgumentException("Knot x values must be strictly increasing.");

            var d = new double[n - 1];
            for (int i = 0; i < n - 1; i++) d[i] = (_y[i + 1] - _y[i]) / (_x[i + 1] - _x[i]);

            _m = new double[n];
            _m[0] = d[0];
            _m[n - 1] = d[n - 2];
            for (int i = 1; i < n - 1; i++) _m[i] = d[i - 1] * d[i] <= 0.0 ? 0.0 : (d[i - 1] + d[i]) * 0.5;

            for (int i = 0; i < n - 1; i++)
            {
                if (d[i] == 0.0) { _m[i] = 0.0; _m[i + 1] = 0.0; continue; }
                double a = _m[i] / d[i], b = _m[i + 1] / d[i];
                double s = a * a + b * b;
                if (s > 9.0)
                {
                    double t = 3.0 / Math.Sqrt(s);
                    _m[i] = t * a * d[i];
                    _m[i + 1] = t * b * d[i];
                }
            }
        }

        public double Evaluate(double x)
        {
            int n = _x.Length;
            if (x <= _x[0]) return _y[0];
            if (x >= _x[n - 1]) return _y[n - 1];
            int i = 0;
            while (i < n - 2 && x >= _x[i + 1]) i++;
            double h = _x[i + 1] - _x[i];
            double t = (x - _x[i]) / h;
            double t2 = t * t, t3 = t2 * t;
            double h00 = 2 * t3 - 3 * t2 + 1, h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
            return h00 * _y[i] + h10 * h * _m[i] + h01 * _y[i + 1] + h11 * h * _m[i + 1];
        }
    }
}
