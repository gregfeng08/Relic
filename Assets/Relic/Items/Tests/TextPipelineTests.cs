using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Stats;
using Relic.Items.Text;

namespace Relic.Items.Tests
{
    public class TextPipelineTests
    {
        const ulong World = 77;

        static ItemSpec RelicSpec(int index = 0, int tier = 3) => new ItemSpec { WorldSeed = World, Index = index, Tier = tier, Rarity = Rarity.Legendary };

        /// <summary>A valid draft for whatever brief the pipeline sends (what a well-behaved model would write).</summary>
        static string Good(TextRequest req) => ItemJson.Serialize(TemplateAuthor.Compose((RelicBrief)req.Context));

        static JObject GoodObject(TextRequest req) => JObject.Parse(Good(req));

        [Test]
        public void ValidFirstAnswerIsAcceptedAsLlmOutput()
        {
            var fake = new FakeGenerator((req, call) => Good(req)) { Name = "fake-model" };
            var gen = new ItemGenerator(StubWorld.Create(World), fake);
            var item = gen.Generate(RelicSpec());
            Assert.AreEqual(1, fake.Calls);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
            Assert.AreEqual("fake-model", item.Generator.TextSource);
            Assert.AreEqual(1, item.Generator.Attempts);
            Assert.IsTrue(gen.Records[0].FirstTryValid);
        }

        [Test]
        public void PromptCarriesSchemaGrammarWorldAndBudget()
        {
            var fake = new FakeGenerator((req, call) => Good(req));
            new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            var req = fake.Requests[0];
            Assert.AreEqual("system", req.Messages[0].Role);
            StringAssert.Contains("EFFECT GRAMMAR", req.Messages[0].Content);
            StringAssert.Contains("OnCloseCall", req.Messages[0].Content);
            StringAssert.Contains("ethereal", req.Messages[0].Content); // banned list is spelled out
            StringAssert.Contains("WORLD CONTEXT", req.Messages[1].Content);
            StringAssert.Contains("pp", req.Messages[1].Content);
            var schema = JObject.Parse(req.JsonSchema);
            CollectionAssert.IsSubsetOf(new[] { "name", "lore", "worldRefs", "enchantments", "ultimate", "visual" }, schema["required"].Values<string>().ToList());
        }

        [Test]
        public void MalformedJsonIsRetriedWithTheErrorFedBack()
        {
            var fake = new FakeGenerator((req, call) => call == 0 ? "{ \"name\": \"Broken" : Good(req));
            var gen = new ItemGenerator(StubWorld.Create(World), fake);
            var item = gen.Generate(RelicSpec());
            Assert.AreEqual(2, fake.Calls);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
            var retry = fake.Requests[1].Messages;
            Assert.AreEqual("assistant", retry[retry.Count - 2].Role);
            StringAssert.Contains("not valid JSON", retry[retry.Count - 1].Content);
            Assert.AreEqual(1, gen.Records[0].JsonFailures);
        }

        [Test]
        public void UnknownFieldsAreRejectedNotGuessed()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = GoodObject(req);
                if (call == 0) o["powerLevel"] = 9000;
                return o.ToString();
            });
            var item = new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            Assert.AreEqual(2, fake.Calls);
            StringAssert.Contains("powerLevel", fake.Requests[1].Messages.Last().Content);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
        }

        [Test]
        public void StyleViolationsAndUnknownWorldRefsAreFedBack()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = GoodObject(req);
                if (call == 0) { o["name"] = "The Ethereal Doom"; o["worldRefs"] = new JArray("reg.atlantis"); }
                return o.ToString();
            });
            var item = new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            string feedback = fake.Requests[1].Messages.Last().Content;
            StringAssert.Contains("ethereal", feedback);
            StringAssert.Contains("doom", feedback);
            StringAssert.Contains("reg.atlantis", feedback);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
        }

        [Test]
        public void StructuralGrammarErrorsAreFedBack()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = GoodObject(req);
                if (call == 0) o["enchantments"]["rules"][0]["chance"] = 0; // chance must be in (0, 1]
                return o.ToString();
            });
            new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            StringAssert.Contains("chance must be in (0, 1]", fake.Requests[1].Messages.Last().Content);
        }

        [Test]
        public void FourBadAnswersFallBackToATemplateRelic()
        {
            var fake = new FakeGenerator("not json at all", "{\"name\": 3}", "[]", "{\"name\":\"x\"}");
            var gen = new ItemGenerator(StubWorld.Create(World), fake);
            var item = gen.Generate(RelicSpec());
            Assert.AreEqual(4, fake.Calls, "1 attempt + 3 retries");
            Assert.AreEqual(GenerationMode.LlmFallback, item.Generator.Mode);
            Assert.AreEqual(TemplateAuthor.Source, item.Generator.TextSource);
            Assert.IsTrue(gen.Records[0].UsedFallback);
            Assert.IsTrue(EffectValidator.Validate(item.Effects, PowerBudget.For(Rarity.Legendary, 3)).IsValid);
            Assert.IsNotNull(item.Lore.History);
        }

        [Test]
        public void TransportFailuresCountAsAttempts()
        {
            var fake = new FakeGenerator((req, call) => Good(req));
            fake.FailOnCalls.Add(0);
            fake.FailOnCalls.Add(1);
            var gen = new ItemGenerator(StubWorld.Create(World), fake);
            var item = gen.Generate(RelicSpec());
            Assert.AreEqual(3, item.Generator.Attempts);
            Assert.AreEqual(2, gen.Records[0].TransportFailures);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
        }

        [Test]
        public void OverBudgetProgramsAreScaledToFit()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = GoodObject(req);
                // an enchantment 100× too strong
                o["enchantments"]["rules"][0] = JObject.Parse(EffectJson.Serialize(new EffectProgram
                {
                    Rules = { new EffectRule { Name = "Too Much", Trigger = new TriggerSpec { Kind = TriggerKind.OnHit }, Actions = { new ActionSpec { Kind = ActionKind.Damage, Amount = 5000f } } } },
                }))["rules"][0];
                return o.ToString();
            });
            var item = new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            Assert.Less(item.Generator.ScaleApplied, 1f);
            var v = EffectValidator.Validate(item.Effects, PowerBudget.For(Rarity.Legendary, 3));
            Assert.IsTrue(v.IsValid, v.ToString());
        }

        [Test]
        public void UltimateIsCappedAndAlwaysCostsAFullResonanceBar()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = GoodObject(req);
                o["ultimate"] = JObject.Parse(EffectJson.Serialize(new EffectProgram
                {
                    Rules = { new EffectRule { Name = "Cheap Nuke", Trigger = new TriggerSpec { Kind = TriggerKind.Active }, Cooldown = 0.5f, CostResource = ResourceId.Mana, CostAmount = 1f,
                        Actions = { new ActionSpec { Kind = ActionKind.Emit, Emitter = new EmitterSpec { Shape = EmitShape.Ring, Count = 20 }, Payload = new List<ActionSpec> { new ActionSpec { Kind = ActionKind.Damage, Amount = 900f } } } } } },
                }))["rules"][0];
                return o.ToString();
            });
            var item = new ItemGenerator(StubWorld.Create(World), fake).Generate(RelicSpec());
            var ult = item.Effects.Rules.Last();
            Assert.AreEqual(ResourceId.Resonance, ult.CostResource);
            Assert.AreEqual(EffectTemplates.UltimateCooldown, ult.Cooldown);
            Assert.LessOrEqual(EffectFitter.UnrelievedCost(ult), 0.4f * PowerBudget.For(Rarity.Legendary, 3) + 1e-3f);
        }

        [Test]
        public void EpicsGetOneWrittenEnchantmentAndRaresOnlyNameAndFlavor()
        {
            var fake = new FakeGenerator((req, call) => Good(req));
            var gen = new ItemGenerator(StubWorld.Create(World), fake);
            var epic = gen.Generate(new ItemSpec { WorldSeed = World, Index = 1, Tier = 4, Rarity = Rarity.Epic, BaseType = BaseType.Staff });
            Assert.AreEqual(2, epic.Effects.Rules.Count, "weapon skill + 1 enchantment");
            Assert.IsNotNull(epic.Lore.History);
            var rare = gen.Generate(new ItemSpec { WorldSeed = World, Index = 2, Tier = 4, Rarity = Rarity.Rare, BaseType = BaseType.Sword });
            Assert.IsNotNull(rare.Lore.Flavor);
            Assert.IsNull(rare.Lore.History);
            Assert.AreEqual("rare_name", fake.Requests.Last().Purpose);
            var common = gen.Generate(new ItemSpec { WorldSeed = World, Index = 3, Tier = 4, Rarity = Rarity.Common, BaseType = BaseType.Sword });
            Assert.AreEqual(2, fake.Calls, "commons never call the LLM");
            Assert.AreEqual(GenerationMode.Tables, common.Generator.Mode);
        }

        [Test]
        public void OverusedNameWordsAreRejected()
        {
            var freshness = new NameFreshness();
            for (int i = 0; i < 30; i++) freshness.Add("Lantern " + i);
            var fake = new FakeGenerator((req, call) => { var o = GoodObject(req); if (call == 0) o["name"] = "The Lantern of Rue"; return o.ToString(); });
            var gen = new ItemGenerator(StubWorld.Create(World), fake, null, freshness);
            gen.Generate(RelicSpec());
            StringAssert.Contains("\"lantern\"", fake.Requests[1].Messages.Last().Content);
            StringAssert.Contains("lantern", fake.Requests[0].Messages[1].Content); // overused list is in the prompt
        }

        [Test]
        public void TemplateDraftsConformToTheLlmSchema()
        {
            var world = StubWorld.Create(World);
            var gen = new ItemGenerator(world);
            var fake = new FakeGenerator((req, call) =>
            {
                string json = Good(req);
                var errors = new List<string>();
                SchemaCheck.Validate(JObject.Parse(req.JsonSchema), JToken.Parse(json), "$", errors);
                Assert.IsEmpty(errors, string.Join("\n", errors));
                return json;
            });
            var g2 = new ItemGenerator(world, fake);
            for (int i = 0; i < 60; i++)
                g2.Generate(new ItemSpec { WorldSeed = World, Index = i, Tier = i % 11, Rarity = i % 3 == 0 ? Rarity.Rare : i % 3 == 1 ? Rarity.Epic : Rarity.Legendary });
            Assert.AreEqual(60, fake.Calls);
        }

        [Test]
        public void BenchmarkHarnessReportsRates()
        {
            var fake = new FakeGenerator((req, call) => call % 3 == 0 ? "oops" : Good(req));
            var report = LlmBenchmark.Run(fake, StubWorld.Create(World), World, 6);
            Assert.AreEqual(6, report.Relics);
            Assert.Greater(report.Attempts, 6);
            Assert.Less(report.ValidJsonRate, 1.0);
            Assert.AreEqual(0, report.Fallbacks);
            StringAssert.Contains("valid JSON", report.ToMarkdownRow("fake") + LlmBenchmark.MarkdownHeader);
        }
    }

    /// <summary>A tiny JSON-schema checker for the subset Prompts.Schema uses.</summary>
    static class SchemaCheck
    {
        public static void Validate(JObject schema, JToken value, string path, List<string> errors)
        {
            var type = schema["type"]?.Value<string>();
            if (schema["enum"] is JArray en && !en.Any(x => JToken.DeepEquals(x, value))) errors.Add($"{path}: {value} not in enum");
            switch (type)
            {
                case "object":
                    if (!(value is JObject o)) { errors.Add($"{path}: expected object"); return; }
                    var props = schema["properties"] as JObject ?? new JObject();
                    foreach (var p in o.Properties())
                    {
                        if (props[p.Name] == null) { if (schema["additionalProperties"]?.Value<bool>() == false) errors.Add($"{path}.{p.Name}: not in schema"); continue; }
                        Validate((JObject)props[p.Name], p.Value, path + "." + p.Name, errors);
                    }
                    if (schema["required"] is JArray req)
                        foreach (var r in req.Values<string>()) if (o[r] == null) errors.Add($"{path}.{r}: required");
                    break;
                case "array":
                    if (!(value is JArray a)) { errors.Add($"{path}: expected array"); return; }
                    if (schema["maxItems"] != null && a.Count > schema["maxItems"].Value<int>()) errors.Add($"{path}: too many items");
                    if (schema["minItems"] != null && a.Count < schema["minItems"].Value<int>()) errors.Add($"{path}: too few items");
                    for (int i = 0; i < a.Count; i++) Validate((JObject)schema["items"], a[i], $"{path}[{i}]", errors);
                    break;
                case "string": if (value.Type != JTokenType.String) errors.Add($"{path}: expected string"); break;
                case "number": if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) errors.Add($"{path}: expected number"); break;
                case "integer": if (value.Type != JTokenType.Integer) errors.Add($"{path}: expected integer"); break;
                case "boolean": if (value.Type != JTokenType.Boolean) errors.Add($"{path}: expected boolean"); break;
            }
        }
    }

    public class LlamaServerGeneratorTests
    {
        sealed class StubHandler : HttpMessageHandler
        {
            public readonly List<(string path, string body)> Seen = new List<(string, string)>();
            public Func<HttpRequestMessage, string, HttpResponseMessage> Respond;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                string body = request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                Seen.Add((request.RequestUri.AbsolutePath, body));
                return Respond(request, body);
            }
        }

        /// <summary>Blocks off the caller's synchronization context (Unity's main thread would deadlock on continuations).</summary>
        static T Sync<T>(Func<Task<T>> f) => Task.Run(f).GetAwaiter().GetResult();

        static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
            new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        static TextRequest Request() => new TextRequest
        {
            Purpose = "relic",
            Messages = { new TextMessage("system", "sys"), new TextMessage("user", "hi") },
            JsonSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},\"required\":[\"name\"]}",
            MaxTokens = 50,
            Seed = 7,
        };

        [Test]
        public void ChatCompletionsSendsSchemaAndParsesContent()
        {
            var h = new StubHandler { Respond = (r, b) => Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"name\\\":\\\"Isel's Weir\\\"}\"}}],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8}}") };
            using (var g = new LlamaServerGenerator(new LlamaServerOptions { BaseUrl = "http://127.0.0.1:9999" }, h))
            {
                var res = Sync(() => g.GenerateAsync(Request()));
                Assert.AreEqual("{\"name\":\"Isel's Weir\"}", res.Text);
                Assert.AreEqual(12, res.PromptTokens);
                Assert.AreEqual(8, res.CompletionTokens);
            }
            Assert.AreEqual("/v1/chat/completions", h.Seen[0].path);
            var body = JObject.Parse(h.Seen[0].body);
            Assert.AreEqual("json_schema", body["response_format"]["type"].Value<string>());
            Assert.AreEqual("object", body["response_format"]["json_schema"]["schema"]["type"].Value<string>());
            Assert.AreEqual(2, ((JArray)body["messages"]).Count);
            Assert.AreEqual(50, body["max_tokens"].Value<int>());
            Assert.AreEqual(7, body["seed"].Value<int>());
        }

        [Test]
        public void CompletionEndpointSendsJsonSchemaAndChatMlPrompt()
        {
            var h = new StubHandler { Respond = (r, b) => Json("{\"content\":\"{\\\"name\\\":\\\"x\\\"}\",\"tokens_predicted\":5,\"tokens_evaluated\":40}") };
            using (var g = new LlamaServerGenerator(new LlamaServerOptions { Endpoint = LlamaEndpoint.Completion }, h))
            {
                var res = Sync(() => g.GenerateAsync(Request()));
                Assert.AreEqual("{\"name\":\"x\"}", res.Text);
                Assert.AreEqual(5, res.CompletionTokens);
            }
            Assert.AreEqual("/completion", h.Seen[0].path);
            var body = JObject.Parse(h.Seen[0].body);
            Assert.IsNotNull(body["json_schema"]);
            StringAssert.Contains("<|im_start|>system\nsys<|im_end|>", body["prompt"].Value<string>());
            StringAssert.EndsWith("<|im_start|>assistant\n", body["prompt"].Value<string>());
            Assert.AreEqual(50, body["n_predict"].Value<int>());
        }

        [Test]
        public void ServerErrorsAndGarbageBecomeTextGenerationExceptions()
        {
            var h = new StubHandler { Respond = (r, b) => Json("{\"error\":\"model not loaded\"}", HttpStatusCode.ServiceUnavailable) };
            using (var g = new LlamaServerGenerator(null, h))
                Assert.Throws<TextGenerationException>(() => Sync(() => g.GenerateAsync(Request())));
            var h2 = new StubHandler { Respond = (r, b) => Json("<html>proxy</html>") };
            using (var g = new LlamaServerGenerator(null, h2))
                Assert.Throws<TextGenerationException>(() => Sync(() => g.GenerateAsync(Request())));
            var h3 = new StubHandler { Respond = (r, b) => throw new HttpRequestException("connection refused") };
            using (var g = new LlamaServerGenerator(null, h3))
                Assert.Throws<TextGenerationException>(() => Sync(() => g.GenerateAsync(Request())));
        }

        [Test]
        public void TimeoutBecomesTextGenerationException()
        {
            var slow = new SlowHandler();
            using (var g = new LlamaServerGenerator(new LlamaServerOptions { Timeout = TimeSpan.FromMilliseconds(100) }, slow))
            {
                var ex = Assert.Throws<TextGenerationException>(() => Sync(() => g.GenerateAsync(Request())));
                StringAssert.Contains("timed out", ex.Message);
            }
        }

        sealed class SlowHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                await Task.Delay(5000, ct).ConfigureAwait(false);
                return Json("{}");
            }
        }

        [Test]
        public void HealthCheck()
        {
            var ok = new StubHandler { Respond = (r, b) => Json("{\"status\":\"ok\"}") };
            using (var g = new LlamaServerGenerator(null, ok)) Assert.IsTrue(Sync(() => g.IsReadyAsync()));
            var loading = new StubHandler { Respond = (r, b) => Json("{\"status\":\"loading model\"}", HttpStatusCode.ServiceUnavailable) };
            using (var g = new LlamaServerGenerator(null, loading)) Assert.IsFalse(Sync(() => g.IsReadyAsync()));
        }

        [Test]
        public void WorksEndToEndInThePipeline()
        {
            var world = StubWorld.Create(5);
            var h = new StubHandler
            {
                Respond = (r, b) =>
                {
                    // play the model: answer with a valid draft for the brief implied by the prompt
                    var brief = ((RelicBriefHolder.Last));
                    string content = ItemJson.Serialize(TemplateAuthor.Compose(brief));
                    return Json(new JObject { ["choices"] = new JArray(new JObject { ["message"] = new JObject { ["content"] = content } }) }.ToString());
                },
            };
            using (var llama = new LlamaServerGenerator(null, h))
            {
                var spy = new RelicBriefHolder(llama);
                var item = new ItemGenerator(world, spy).Generate(new ItemSpec { WorldSeed = 5, Index = 0, Tier = 2, Rarity = Rarity.Legendary });
                Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode);
                StringAssert.StartsWith("llama-server", item.Generator.TextSource);
            }
        }

        /// <summary>Passes requests through to the real generator, remembering the brief so the stub server can answer it.</summary>
        sealed class RelicBriefHolder : ITextGenerator
        {
            public static RelicBrief Last;
            readonly ITextGenerator _inner;
            public RelicBriefHolder(ITextGenerator inner) { _inner = inner; }
            public string Name => _inner.Name;
            public Task<TextResult> GenerateAsync(TextRequest request, CancellationToken ct = default)
            {
                Last = (RelicBrief)request.Context;
                return _inner.GenerateAsync(request, ct);
            }
        }
    }
}
