using System;

namespace DroneVision.Core
{
    public sealed class LowPass
    {
        private bool _has;
        private double _y;

        public void Reset() { _has = false; _y = 0; }

        public double Apply(double value, double alpha)
        {
            _y = _has ? alpha * value + (1 - alpha) * _y : value;
            _has = true;
            return _y;
        }
    }

    /// <summary>
    /// One Euro filter: heavy smoothing while still, near-zero lag while moving.
    /// Exposes the filtered derivative (<see cref="Dx"/>, units/second) for lead compensation.
    /// </summary>
    public sealed class OneEuroFilter
    {
        public double MinCutoff;
        public double Beta;
        public double DCutoff;
        public double Dx { get; private set; }

        private readonly LowPass _x = new LowPass();
        private readonly LowPass _dx = new LowPass();
        private bool _has;
        private double _lastT;
        private double _lastRaw;

        public OneEuroFilter(double minCutoff, double beta, double dCutoff = 1.0)
        {
            MinCutoff = minCutoff;
            Beta = beta;
            DCutoff = dCutoff;
        }

        public static double Alpha(double cutoff, double dt)
        {
            double tau = 1.0 / (2.0 * Math.PI * cutoff);
            return 1.0 / (1.0 + tau / Math.Max(dt, 1e-4));
        }

        public void Reset()
        {
            _x.Reset();
            _dx.Reset();
            _has = false;
            Dx = 0;
        }

        /// <param name="t">seconds</param>
        public double Apply(double value, double t)
        {
            if (!_has)
            {
                _has = true;
                _lastT = t;
                _lastRaw = value;
                Dx = 0;
                return _x.Apply(value, 1.0);
            }
            double dt = Math.Max(t - _lastT, 1e-4);
            double rawDx = (value - _lastRaw) / dt;
            Dx = _dx.Apply(rawDx, Alpha(DCutoff, dt));
            double cutoff = MinCutoff + Beta * Math.Abs(Dx);
            double y = _x.Apply(value, Alpha(cutoff, dt));
            _lastT = t;
            _lastRaw = value;
            return y;
        }
    }

    internal static class MathUtil
    {
        public static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        public static double Hypot(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }
    }
}
