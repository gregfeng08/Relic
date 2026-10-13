using Relic.Core.Stats;
using Relic.World.Chunks;
using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Minimal honest HUD (HP and mana stubs, stamina) plus the F3 debug overlay: seed, position,
    /// region, tier, biome, fps and streaming stats. IMGUI keeps it dependency-free for M1.
    /// </summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        public PlayerController Player;
        public ChunkStreamer Streamer;
        public FloatingOrigin Origin;
        public PerfMonitor Perf;
        public TerrainGenerator Generator;
        public ulong Seed;
        public bool ShowOverlay = true;
        public bool Loading = true;

        GUIStyle _label;
        GUIStyle _big;
        float _staminaAlpha;

        void OnGUI()
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                _label.normal.textColor = Color.white;
                _big = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                _big.normal.textColor = Color.white;
            }
            float scale = Screen.height / 1080f;
            if (Loading)
            {
                Rect r = new Rect(0, Screen.height * 0.45f, Screen.width, 40);
                GUI.Label(r, "Generating terrain...", _big);
                return;
            }
            DrawBars(scale);
            DrawReticle();
            if (ShowOverlay) DrawOverlay();
            if (Perf != null && Perf.BenchmarkRunning)
            {
                GUI.Label(new Rect(0, 40 * scale, Screen.width, 30), "Benchmark: auto-sprint " + (Perf.BenchmarkProgress * 100).ToString("0") + "%  (F8 to stop)", _big);
            }
        }

        void DrawBars(float scale)
        {
            if (Player == null || Player.Config == null) return;
            float w = 260f * scale, h = 8f * scale;
            float x = 24f * scale, y = Screen.height - 40f * scale;
            Bar(new Rect(x, y, w, h), 1f, new Color(0.86f, 0.25f, 0.25f), 1f);          // HP (stub)
            Bar(new Rect(x, y + h + 5f * scale, w, h), 1f, new Color(0.30f, 0.55f, 0.95f), 1f); // mana (stub)

            // Stamina: a bar under the character, fading out when full (like Genshin's wheel).
            float frac = Player.Stamina / Player.Config.MaxStamina;
            bool show = frac < 0.999f;
            _staminaAlpha = Mathf.MoveTowards(_staminaAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (_staminaAlpha > 0.01f)
            {
                Color c = Player.Exhausted ? new Color(0.95f, 0.35f, 0.25f) : new Color(0.98f, 0.85f, 0.3f);
                float sw = 180f * scale, sh = 9f * scale;
                Bar(new Rect((Screen.width - sw) * 0.5f, Screen.height * 0.62f, sw, sh), frac, c, _staminaAlpha);
            }
        }

        static void Bar(Rect r, float frac, Color c, float alpha)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
            GUI.color = new Color(c.r, c.g, c.b, alpha);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void DrawReticle()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        string _overlayText;
        GUIContent _overlayContent;
        float _overlayNext;

        void DrawOverlay()
        {
            if (Player == null || Origin == null || Generator == null) return;
            if (Time.unscaledTime >= _overlayNext || _overlayContent == null)
            {
                _overlayNext = Time.unscaledTime + 0.2f; // rebuild text 5x per second: avoids per-frame garbage
                _overlayContent = new GUIContent(BuildOverlayText());
            }
            DrawOverlayBox(_overlayContent);
        }

        string BuildOverlayText()
        {
            Vector3 p = Player.transform.position;
            Origin.ToWorld(p, out double wx, out double wz);
            TerrainSample s = Generator.Sample(wx, wz);
            ChunkCoord chunk = ChunkMath.ChunkAt(wx, wz);
            int tier = Difficulty.TierAt(wx, wz);
            float fps = Perf != null ? 1000f / Mathf.Max(0.01f, Perf.SmoothedMs) : 0f;
            float ms = Perf != null ? Perf.SmoothedMs : 0f;

            string text =
                "<b>Relic terrain sandbox</b>   F3 overlay · F4 fly · F5 new seed · F6 day cycle · F8 benchmark (3 km) · Esc cursor\n" +
                "Seed <b>" + Seed + "</b>    " + fps.ToString("0") + " fps (" + ms.ToString("0.0") + " ms)\n" +
                "World x " + wx.ToString("0.0") + "  z " + wz.ToString("0.0") + "  y " + p.y.ToString("0.0") +
                "   (distance " + System.Math.Sqrt(wx * wx + wz * wz).ToString("0") + " m)\n" +
                "Scene " + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + "   origin " + Origin.OriginX.ToString("0") + ", " + Origin.OriginZ.ToString("0") + "   chunk " + chunk + "\n" +
                "Region " + s.Region + " <b>" + s.Family + "</b>   border " + s.BorderFactor.ToString("0.00") + "   tier <b>" + tier + "</b>\n" +
                "Biome <b>" + s.Biome + "</b>   ground " + s.Height.ToString("0.0") + " m   water " + s.WaterLevel.ToString("0.0") + " m   temp " + s.Temperature.ToString("0.00") + "  humid " + s.Humidity.ToString("0.00") + "\n" +
                "State " + Player.State + (Player.IsSprinting ? " (sprint)" : "") + (Player.IsDashing ? " (dash)" : "") +
                "   speed " + Player.HorizontalSpeed.ToString("0.0") + " m/s   stamina " + Player.Stamina.ToString("0") + "/" + Player.Config.MaxStamina.ToString("0") + "\n";
            if (Streamer != null)
            {
                double avg = Streamer.TotalBuilt > 0 ? Streamer.TotalBuildMs / Streamer.TotalBuilt : 0;
                text += "Chunks " + Streamer.VisibleChunks + "/" + Streamer.LoadedChunks + "   pending " + Streamer.PendingWork + "   in flight " + Streamer.InFlight +
                        "   colliders " + Streamer.CollidersActive + "   built " + Streamer.TotalBuilt + " (avg " + avg.ToString("0.0") + " ms)   upload " +
                        Streamer.LastUploadMs.ToString("0.0") + " ms";
            }
            if (Perf != null && !string.IsNullOrEmpty(Perf.LastSummary)) text += "\n" + Perf.LastSummary.Replace("[Relic.Perf] ", "");
            return text;
        }

        void DrawOverlayBox(GUIContent content)
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            _label.fontSize = Mathf.RoundToInt(13 * scale);
            Vector2 size = _label.CalcSize(content);
            Rect r = new Rect(10, 10, size.x + 16, size.y + 10);
            Color old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 8, r.y + 5, size.x, size.y), content, _label);
        }
    }
}
