using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Relic.Core.Effects;
using Relic.Items.Text;

namespace Relic.Items
{
    public sealed class BenchmarkReport
    {
        public int Relics;
        public int Attempts;
        public int FirstTryValid;
        public int JsonFailures;
        public int TransportFailures;
        public int Fallbacks;
        public double TotalSeconds;

        /// <summary>Answers that parsed as schema-shaped JSON, out of answers received.</summary>
        public double ValidJsonRate => Answered == 0 ? 0 : (Answered - JsonFailures) / (double)Answered;
        public int Answered => Attempts - TransportFailures;
        public double FirstTryRate => Relics == 0 ? 0 : FirstTryValid / (double)Relics;
        public double MeanRetries => Relics == 0 ? 0 : (Attempts - Relics) / (double)Relics;
        public double SecondsPerRelic => Relics == 0 ? 0 : TotalSeconds / Relics;

        public string ToMarkdownRow(string model)
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Format(ci, "| {0} | {1} | {2:P0} | {3:P0} | {4:0.00} | {5} | {6:0.0} | (nvidia-smi) |",
                model, Relics, ValidJsonRate, FirstTryRate, MeanRetries, Fallbacks, SecondsPerRelic);
        }
    }

    /// <summary>
    /// Model benchmark for the LLM step: generates N Legendary relics through the
    /// real pipeline and reports valid-JSON rate, first-try acceptance, retries,
    /// fallbacks and seconds per relic. VRAM isn't visible from C#: read it from
    /// nvidia-smi while the run is going.
    /// </summary>
    public static class LlmBenchmark
    {
        public const string MarkdownHeader =
            "| model | relics | valid JSON | accepted first try | retries / relic | fallbacks | s / relic | VRAM |\n" +
            "|---|---|---|---|---|---|---|---|";

        public static BenchmarkReport Run(ITextGenerator generator, WorldContext world, ulong worldSeed, int relics) =>
            RunAsync(generator, world, worldSeed, relics).GetAwaiter().GetResult();

        public static async Task<BenchmarkReport> RunAsync(ITextGenerator generator, WorldContext world, ulong worldSeed, int relics, CancellationToken ct = default)
        {
            var gen = new ItemGenerator(world, generator);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < relics; i++)
                await gen.GenerateAsync(new ItemSpec { WorldSeed = worldSeed, Index = 100000 + i, Tier = i % 11, Rarity = Rarity.Legendary }, ct).ConfigureAwait(false);
            sw.Stop();

            var r = new BenchmarkReport { Relics = relics, TotalSeconds = sw.Elapsed.TotalSeconds };
            foreach (var rec in gen.Records)
            {
                r.Attempts += rec.Attempts;
                r.JsonFailures += rec.JsonFailures;
                r.TransportFailures += rec.TransportFailures;
                if (rec.FirstTryValid) r.FirstTryValid++;
                if (rec.UsedFallback) r.Fallbacks++;
            }
            return r;
        }
    }
}
