using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Relic.Items.Text
{
    /// <summary>
    /// Canned responses for tests and CI: no model needed. Either a fixed queue of
    /// responses (the last one repeats once the queue runs out), or a function of
    /// the request and call number. Records every request so tests can check
    /// prompts and the errors fed back on retries.
    /// </summary>
    public sealed class FakeGenerator : ITextGenerator
    {
        readonly Queue<string> _queue;
        readonly Func<TextRequest, int, string> _respond;
        string _last;

        public readonly List<TextRequest> Requests = new List<TextRequest>();
        public string Name { get; set; } = "fake";
        /// <summary>When set, this call number (0-based) throws a TextGenerationException, like a dropped server.</summary>
        public HashSet<int> FailOnCalls = new HashSet<int>();

        public FakeGenerator(params string[] responses)
        {
            _queue = new Queue<string>(responses ?? new string[0]);
        }

        public FakeGenerator(Func<TextRequest, int, string> respond)
        {
            _respond = respond;
        }

        public int Calls => Requests.Count;

        public Task<TextResult> GenerateAsync(TextRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int call = Requests.Count;
            Requests.Add(request);
            if (FailOnCalls.Contains(call)) throw new TextGenerationException("fake failure on call " + call);

            string text;
            if (_respond != null) text = _respond(request, call);
            else if (_queue.Count > 0) text = _last = _queue.Dequeue();
            else if (_last != null) text = _last;
            else throw new TextGenerationException("FakeGenerator has no responses");

            return Task.FromResult(new TextResult { Text = text, Source = Name, Seconds = 0 });
        }
    }
}
