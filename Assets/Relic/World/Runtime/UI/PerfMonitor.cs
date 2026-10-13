using System;
using System.Text;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Frame-time statistics for the M1 acceptance check (average ≤ 16.6 ms, no hitch over 50 ms
    /// while sprinting across chunks). Logs a line every 10 s while the player moves, a session
    /// summary on exit, and a pass/fail summary for the F8 benchmark run. Lines start with
    /// "[Relic.Perf]" so they're easy to find in the Console or Editor.log.
    /// </summary>
    public sealed class PerfMonitor : MonoBehaviour
    {
        public ChunkStreamer Streamer;
        public PlayerController Player;
        public FloatingOrigin Origin;
        public float WindowSeconds = 10f;
        public float HitchMs = 50f;
        public float BudgetMs = 16.6f;

        public bool Recording { get; private set; }
        public float SmoothedMs { get; private set; } = 16f;
        public float LastMs { get; private set; }
        public bool BenchmarkRunning => _benchTarget > 0;
        public double BenchmarkProgress => _benchTarget > 0 && _bench != null ? _bench.Distance / _benchTarget : 0;
        public string LastSummary { get; private set; } = "";

        public Stats Session = new Stats();
        Stats _window = new Stats();
        float _windowStart;
        double _lastX, _lastZ;
        bool _hasLast;
        double _benchTarget;
        string _benchLabel;
        Action _benchDone;
        Stats _bench;


        /// <summary>Start counting frames (call once the world has loaded, so the load spike isn't counted).</summary>
        public void StartRecording()
        {
            Recording = true;
            _windowStart = Time.unscaledTime;
            _window = new Stats();
        }

        public void BeginBenchmark(string label, double distance, Action onDone)
        {
            _bench = new Stats();
            _benchTarget = distance;
            _benchLabel = label;
            _benchDone = onDone;
            Debug.Log("[Relic.Perf] Benchmark started: " + label);
        }

        public void CancelBenchmark()
        {
            if (_benchTarget <= 0) return;
            _benchTarget = 0;
            Debug.Log("[Relic.Perf] Benchmark cancelled.\n" + Format("benchmark (partial)", _bench));
        }

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            LastMs = ms;
            SmoothedMs = Mathf.Lerp(SmoothedMs, ms, 0.05f);

            double moved = 0;
            if (Player != null && Origin != null)
            {
                Origin.ToWorld(Player.transform.position, out double x, out double z);
                if (_hasLast)
                {
                    double dx = x - _lastX, dz = z - _lastZ;
                    moved = Math.Sqrt(dx * dx + dz * dz);
                    if (moved > 60) moved = 0; // teleport or reseed
                }
                _lastX = x; _lastZ = z; _hasLast = true;
            }

            if (!Recording) return;
            Session.Add(ms, moved, HitchMs, BudgetMs);
            _window.Add(ms, moved, HitchMs, BudgetMs);

            if (_benchTarget > 0)
            {
                _bench.Add(ms, moved, HitchMs, BudgetMs);
                if (_bench.Distance >= _benchTarget)
                {
                    _benchTarget = 0;
                    LastSummary = Format("BENCHMARK " + _benchLabel, _bench);
                    Debug.Log(LastSummary);
                    _benchDone?.Invoke();
                }
            }

            if (Time.unscaledTime - _windowStart >= WindowSeconds)
            {
                if (_window.Distance > 5) Debug.Log(FormatShort(_window));
                _window = new Stats();
                _windowStart = Time.unscaledTime;
            }
        }

        string FormatShort(Stats s)
        {
            return "[Relic.Perf] " + WindowSeconds.ToString("0") + "s: avg " + s.AverageMs.ToString("0.0") + " ms (" + (1000.0 / Math.Max(0.01, s.AverageMs)).ToString("0") +
                   " fps), p99 " + s.Percentile(0.99).ToString("0.0") + " ms, max " + s.MaxMs.ToString("0.0") + " ms, hitches>" + HitchMs + "ms " + s.Hitches +
                   ", moved " + s.Distance.ToString("0") + " m" + StreamerLine();
        }

        string StreamerLine()
        {
            if (Streamer == null) return "";
            double avg = Streamer.TotalBuilt > 0 ? Streamer.TotalBuildMs / Streamer.TotalBuilt : 0;
            return " | chunks built " + Streamer.TotalBuilt + " (avg " + avg.ToString("0.00") + " ms, max " + Streamer.MaxBuildMs.ToString("0.0") +
                   " ms on " + Streamer.WorkerCount + " workers), max upload frame " + Streamer.MaxUploadMs.ToString("0.00") + " ms";
        }

        public string Format(string label, Stats s)
        {
            bool pass = s.Frames > 0 && s.AverageMs <= BudgetMs && s.Hitches == 0;
            var sb = new StringBuilder();
            sb.Append("[Relic.Perf] SUMMARY ").Append(label).Append(pass ? "  PASS" : "  FAIL").Append('\n');
            sb.Append("  frames ").Append(s.Frames).Append(" over ").Append((s.TotalMs / 1000.0).ToString("0.0")).Append(" s\n");
            sb.Append("  avg ").Append(s.AverageMs.ToString("0.00")).Append(" ms (").Append((1000.0 / Math.Max(0.01, s.AverageMs)).ToString("0")).Append(" fps)")
              .Append(", p50 ").Append(s.Percentile(0.5).ToString("0.0"))
              .Append(", p95 ").Append(s.Percentile(0.95).ToString("0.0"))
              .Append(", p99 ").Append(s.Percentile(0.99).ToString("0.0"))
              .Append(", max ").Append(s.MaxMs.ToString("0.0")).Append(" ms\n");
            sb.Append("  frames over ").Append(BudgetMs).Append(" ms: ").Append(s.OverBudget)
              .Append(" (").Append((100.0 * s.OverBudget / Math.Max(1, s.Frames)).ToString("0.0")).Append("%), hitches over ").Append(HitchMs).Append(" ms: ").Append(s.Hitches).Append('\n');
            sb.Append("  distance ").Append(s.Distance.ToString("0")).Append(" m, avg speed ").Append((s.Distance / Math.Max(0.001, s.TotalMs / 1000.0)).ToString("0.0")).Append(" m/s\n");
            if (Streamer != null) sb.Append(" ").Append(StreamerLine()).Append(", colliders ").Append(Streamer.CollidersActive).Append('\n');
            if (Origin != null) sb.Append("  floating-origin shifts ").Append(Origin.ShiftCount).Append('\n');
            sb.Append("  ").Append(SystemInfo.processorType).Append(" / ").Append(SystemInfo.graphicsDeviceName).Append(" / ").Append(Screen.width).Append('x').Append(Screen.height);
            return sb.ToString();
        }

        void OnDestroy()
        {
            if (Session.Frames > 30) Debug.Log(Format("session", Session));
        }

        /// <summary>Frame-time accumulator with a 0.25 ms histogram up to 200 ms.</summary>
        public sealed class Stats
        {
            const float BinMs = 0.25f;
            readonly int[] _hist = new int[801];
            public int Frames;
            public double TotalMs;
            public float MaxMs;
            public int Hitches;
            public int OverBudget;
            public double Distance;

            public double AverageMs => Frames > 0 ? TotalMs / Frames : 0;

            public void Add(float ms, double moved, float hitch, float budget)
            {
                Frames++;
                TotalMs += ms;
                if (ms > MaxMs) MaxMs = ms;
                if (ms > hitch) Hitches++;
                if (ms > budget) OverBudget++;
                Distance += moved;
                int bin = Mathf.Clamp((int)(ms / BinMs), 0, _hist.Length - 1);
                _hist[bin]++;
            }

            public double Percentile(double p)
            {
                if (Frames == 0) return 0;
                int target = (int)Math.Ceiling(p * Frames);
                int acc = 0;
                for (int i = 0; i < _hist.Length; i++)
                {
                    acc += _hist[i];
                    if (acc >= target) return (i + 1) * BinMs;
                }
                return MaxMs;
            }
        }
    }
}
