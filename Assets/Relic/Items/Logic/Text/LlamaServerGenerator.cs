using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Relic.Items.Text
{
    public enum LlamaEndpoint
    {
        /// <summary>OpenAI-compatible /v1/chat/completions with response_format json_schema (the server applies the model's chat template).</summary>
        ChatCompletions,
        /// <summary>Native /completion with json_schema; the prompt is rendered here with ChatML.</summary>
        Completion,
    }

    public sealed class LlamaServerOptions
    {
        /// <summary>llama-server address. Default port of llama-server is 8080.</summary>
        public string BaseUrl = "http://127.0.0.1:8080";
        public LlamaEndpoint Endpoint = LlamaEndpoint.ChatCompletions;
        /// <summary>Whole-request timeout. A 7–8B model on a laptop GPU writes a relic in ~5–20 s; CPU is far slower.</summary>
        public TimeSpan Timeout = TimeSpan.FromSeconds(120);
        /// <summary>Optional model name (llama-server serves one model and ignores it, but some proxies need it).</summary>
        public string Model;
        /// <summary>Optional bearer token if the server was started with --api-key.</summary>
        public string ApiKey;
        /// <summary>Reuse the KV cache for the shared system-prompt prefix (/completion only).</summary>
        public bool CachePrompt = true;
    }

    /// <summary>
    /// Talks to a local llama.cpp `llama-server` sidecar over HTTP (06-tech-architecture).
    /// Output is constrained by a JSON schema (the server turns it into a GBNF grammar),
    /// so even a small model returns parseable JSON; the pipeline still validates everything.
    /// </summary>
    public sealed class LlamaServerGenerator : ITextGenerator, IDisposable
    {
        readonly HttpClient _http;
        readonly LlamaServerOptions _options;

        public string Name => "llama-server" + (string.IsNullOrEmpty(_options.Model) ? "" : ":" + _options.Model);

        /// <param name="handler">Inject a handler for tests; null uses the default network stack.</param>
        public LlamaServerGenerator(LlamaServerOptions options = null, HttpMessageHandler handler = null)
        {
            _options = options ?? new LlamaServerOptions();
            _http = handler != null ? new HttpClient(handler, disposeHandler: false) : new HttpClient();
            _http.Timeout = _options.Timeout;
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        public void Dispose() => _http.Dispose();

        /// <summary>GET /health: true once the model is loaded.</summary>
        public async Task<bool> IsReadyAsync(CancellationToken ct = default)
        {
            try
            {
                using (var resp = await _http.GetAsync("health", ct).ConfigureAwait(false))
                    return resp.IsSuccessStatusCode;
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                return false;
            }
        }

        public async Task<TextResult> GenerateAsync(TextRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            bool chat = _options.Endpoint == LlamaEndpoint.ChatCompletions;
            string path = chat ? "v1/chat/completions" : "completion";
            JObject body = chat ? BuildChatBody(request) : BuildCompletionBody(request);

            var sw = Stopwatch.StartNew();
            string responseText;
            try
            {
                using (var msg = new HttpRequestMessage(HttpMethod.Post, path))
                {
                    msg.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    if (!string.IsNullOrEmpty(_options.ApiKey)) msg.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _options.ApiKey);
                    using (var resp = await _http.SendAsync(msg, cancellationToken).ConfigureAwait(false))
                    {
                        responseText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                            throw new TextGenerationException($"llama-server returned {(int)resp.StatusCode}: {Truncate(responseText, 300)}");
                    }
                }
            }
            catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TextGenerationException($"llama-server timed out after {_options.Timeout.TotalSeconds:0} s", e);
            }
            catch (HttpRequestException e)
            {
                throw new TextGenerationException("llama-server unreachable at " + _options.BaseUrl + ": " + e.Message, e);
            }
            sw.Stop();

            return chat ? ParseChat(responseText, sw.Elapsed.TotalSeconds) : ParseCompletion(responseText, sw.Elapsed.TotalSeconds);
        }

        // ---------- request bodies ----------

        public JObject BuildChatBody(TextRequest request)
        {
            var messages = new JArray();
            foreach (var m in request.Messages) messages.Add(new JObject { ["role"] = m.Role, ["content"] = m.Content });
            var body = new JObject
            {
                ["messages"] = messages,
                ["temperature"] = request.Temperature,
                ["max_tokens"] = request.MaxTokens,
                ["seed"] = request.Seed,
                ["stream"] = false,
            };
            if (!string.IsNullOrEmpty(_options.Model)) body["model"] = _options.Model;
            if (!string.IsNullOrEmpty(request.JsonSchema))
            {
                body["response_format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JObject
                    {
                        ["name"] = string.IsNullOrEmpty(request.Purpose) ? "output" : request.Purpose,
                        ["strict"] = true,
                        ["schema"] = JObject.Parse(request.JsonSchema),
                    },
                };
            }
            return body;
        }

        public JObject BuildCompletionBody(TextRequest request)
        {
            var body = new JObject
            {
                ["prompt"] = RenderChatMl(request),
                ["n_predict"] = request.MaxTokens,
                ["temperature"] = request.Temperature,
                ["seed"] = request.Seed,
                ["cache_prompt"] = _options.CachePrompt,
                ["stream"] = false,
            };
            if (!string.IsNullOrEmpty(request.JsonSchema)) body["json_schema"] = JObject.Parse(request.JsonSchema);
            return body;
        }

        /// <summary>ChatML rendering for /completion (Qwen-style models use it natively; most instruct models tolerate it).</summary>
        public static string RenderChatMl(TextRequest request)
        {
            var sb = new StringBuilder();
            foreach (var m in request.Messages)
                sb.Append("<|im_start|>").Append(m.Role).Append('\n').Append(m.Content).Append("<|im_end|>\n");
            sb.Append("<|im_start|>assistant\n");
            return sb.ToString();
        }

        // ---------- responses ----------

        TextResult ParseChat(string json, double seconds)
        {
            JObject o = ParseObject(json);
            var content = o.SelectToken("choices[0].message.content")?.Value<string>();
            if (content == null) throw new TextGenerationException("llama-server chat response has no choices[0].message.content: " + Truncate(json, 300));
            return new TextResult
            {
                Text = content,
                Source = Name,
                Seconds = seconds,
                PromptTokens = o.SelectToken("usage.prompt_tokens")?.Value<int>() ?? 0,
                CompletionTokens = o.SelectToken("usage.completion_tokens")?.Value<int>() ?? 0,
            };
        }

        TextResult ParseCompletion(string json, double seconds)
        {
            JObject o = ParseObject(json);
            var content = o["content"]?.Value<string>();
            if (content == null) throw new TextGenerationException("llama-server completion response has no content: " + Truncate(json, 300));
            return new TextResult
            {
                Text = content,
                Source = Name,
                Seconds = seconds,
                PromptTokens = o["tokens_evaluated"]?.Value<int>() ?? 0,
                CompletionTokens = o["tokens_predicted"]?.Value<int>() ?? 0,
            };
        }

        static JObject ParseObject(string json)
        {
            try { return JObject.Parse(json); }
            catch (JsonException e) { throw new TextGenerationException("llama-server returned non-JSON: " + Truncate(json, 200), e); }
        }

        static string Truncate(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}
