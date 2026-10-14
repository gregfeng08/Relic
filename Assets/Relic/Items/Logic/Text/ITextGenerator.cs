using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Relic.Items.Text
{
    public sealed class TextMessage
    {
        /// <summary>"system", "user" or "assistant".</summary>
        public string Role;
        public string Content;

        public TextMessage() { }
        public TextMessage(string role, string content) { Role = role; Content = content; }
    }

    /// <summary>One generation call. The output must match JsonSchema when it is set.</summary>
    public sealed class TextRequest
    {
        /// <summary>What is being written ("relic", "epic", "rare-name"): for logs and fakes.</summary>
        public string Purpose;
        public List<TextMessage> Messages = new List<TextMessage>();
        /// <summary>JSON schema (as a JSON string) that constrains the output; null = free text.</summary>
        public string JsonSchema;
        public int MaxTokens = 1200;
        public float Temperature = 0.8f;
        /// <summary>Sampler seed (LLM output still isn't reproducible across hardware; results are persisted).</summary>
        public int Seed;
        /// <summary>
        /// The structured brief behind the prompt (a RelicBrief). Real LLMs ignore it;
        /// the deterministic TemplateGenerator writes its JSON from it.
        /// </summary>
        public object Context;
    }

    public sealed class TextResult
    {
        public string Text;
        /// <summary>Model or generator that wrote it.</summary>
        public string Source;
        public double Seconds;
        public int PromptTokens;
        public int CompletionTokens;
    }

    /// <summary>Thrown when a generator can't produce any text (HTTP failure, timeout, server error).</summary>
    public sealed class TextGenerationException : Exception
    {
        public TextGenerationException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Anything that writes text for the item pipeline: a local llama.cpp server,
    /// canned responses for tests, the deterministic template writer, later a cloud model.
    /// </summary>
    public interface ITextGenerator
    {
        string Name { get; }
        Task<TextResult> GenerateAsync(TextRequest request, CancellationToken cancellationToken = default);
    }
}
