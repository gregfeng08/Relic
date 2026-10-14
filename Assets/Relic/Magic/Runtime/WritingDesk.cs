using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Relic.Magic.Desk
{
    /// <summary>
    /// The Writing Desk: draw a spell circle with the mouse on parchment (or
    /// compose it from the glyph palette), see each glyph recognized and
    /// colored by layer, and read the compiled spell: name, mana, cooldown,
    /// misfire, power, plain-language notes and the effect-grammar JSON. The
    /// arena preview plays the spell's shapes.
    ///
    /// Built entirely in code (IMGUI) by <see cref="WritingDeskBootstrap"/>,
    /// so the scene file holds a single GameObject.
    /// </summary>
    public sealed class WritingDesk : MonoBehaviour
    {
        // Layout in a 1600×900 reference space, scaled to the screen.
        const float RefW = 1600f, RefH = 900f;
        public const int CanvasSize = 600;
        static readonly Rect CanvasRect = new Rect(20, 50, CanvasSize, CanvasSize);
        static readonly Rect ToolsRect = new Rect(20, 658, 600, 234);
        static readonly Rect SpellRect = new Rect(636, 50, 440, 842);
        static readonly Rect PagesRect = new Rect(1092, 50, 488, 168);
        public static readonly Rect ArenaRect = new Rect(1092, 226, 488, 360);
        static readonly Rect JsonRect = new Rect(1092, 594, 488, 298);

        static readonly Vec2 PageCenter = new Vec2(CanvasSize * 0.5f, CanvasSize * 0.5f);
        const float PageRadius = 250f;
        const float InkRadius = 2.2f;

        // Ink colors by layer.
        static readonly Color32 InkColor = new Color32(45, 32, 26, 255);
        static readonly Color32 CircleInk = new Color32(70, 52, 40, 255);
        static readonly Color32 CoreInk = new Color32(196, 120, 16, 255);
        static readonly Color32 InnerInk = new Color32(24, 112, 140, 255);
        static readonly Color32 RingInk = new Color32(160, 34, 44, 255);
        static readonly Color32 UnknownInk = new Color32(150, 138, 120, 255);
        static readonly Color32 GhostInk = new Color32(206, 188, 152, 255);
        static readonly Color32 GuideInk = new Color32(214, 198, 164, 255);

        enum Mode { Draw, Palette }

        GlyphRegistry _registry;
        CircleParser _parser;
        InkCanvas _canvas;
        SpellPreview _preview;
        Camera _arenaCamera;
        readonly SpellCompileOptions _options = new SpellCompileOptions();

        readonly Drawing _drawing = new Drawing();
        Stroke _current;
        SpellRecipe _palette = new SpellRecipe();
        Emphasis _nextEmphasis = Emphasis.Normal;
        string _recipeText = "";
        Mode _mode = Mode.Draw;
        bool _trace = true;
        bool _autoCast = true;
        bool _loop;
        float _nextLoop;

        Drawing _shown;
        SpellCircle _circle;
        CompiledSpell _spell;
        Vector2 _spellScroll, _jsonScroll;
        string _message = "Draw a large circle, then a core sigil in its middle. Or pick a page on the right.";

        float _scale = 1f;
        Vector2 _offset;
        GUIStyle _title, _label, _small, _bold, _glyphTag, _panel, _json, _good, _bad, _warn, _info;
        Texture2D _panelTex, _tagTex;

        public CompiledSpell Spell => _spell;
        public SpellCircle Circle => _circle;
        public Drawing Ink => _drawing;
        public Texture2D CanvasTexture => _canvas?.Texture;

        /// <summary>Switches between drawing with the pen and placing glyphs from the palette.</summary>
        public void UsePalette(bool palette)
        {
            var mode = palette ? Mode.Palette : Mode.Draw;
            if (mode == _mode) return;
            _mode = mode;
            _preview?.Stop();
            Recompile(cast: _mode == Mode.Palette);
        }

        /// <summary>Sets the palette recipe (also the trace guide in Draw mode).</summary>
        public void SetRecipe(SpellRecipe recipe)
        {
            _palette = recipe ?? new SpellRecipe();
            Recompile(cast: _mode == Mode.Palette);
        }

        public void Init(GlyphRegistry registry, SpellPreview preview, Camera arenaCamera)
        {
            _registry = registry;
            _parser = new CircleParser(registry);
            _preview = preview;
            _arenaCamera = arenaCamera;
            _canvas?.Dispose();
            _canvas = new InkCanvas(CanvasSize);
            Recompile(cast: false);
        }

        void OnDestroy()
        {
            _canvas?.Dispose();
            _canvas = null;
            Release(_panelTex);
            Release(_tagTex);
        }

        static void Release(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // ---------------- State changes ----------------

        public void AddStroke(Stroke s)
        {
            _drawing.Strokes.Add(s);
            Recompile(cast: true);
        }

        public void Undo()
        {
            if (_mode == Mode.Palette)
            {
                RemoveLastPaletteGlyph();
                return;
            }
            if (_drawing.Strokes.Count == 0) return;
            _drawing.Strokes.RemoveAt(_drawing.Strokes.Count - 1);
            Recompile(cast: false);
        }

        public void ClearPage()
        {
            if (_mode == Mode.Palette) _palette = new SpellRecipe();
            else _drawing.Strokes.Clear();
            _preview?.Stop();
            Recompile(cast: false);
        }

        /// <summary>Opens a pre-written page: its drawing goes on the desk as ink, and the palette (trace guide) holds its recipe.</summary>
        public void LoadPage(PrewrittenSpells.Page page)
        {
            _palette = SpellRecipe.Parse(page.Recipe);
            _drawing.Strokes.Clear();
            _drawing.Strokes.AddRange(RecipeDrawer.Draw(_palette, _registry, PageCenter, PageRadius).Strokes);
            _message = $"\"{page.Title}\": {page.Note} Its glyphs are on the page; Clear and trace them to practice.";
            Recompile(cast: true);
        }

        void AddPaletteGlyph(GlyphDefinition g)
        {
            switch (g.Role)
            {
                case GlyphRole.Core: _palette.Core = g.Id; break;
                case GlyphRole.Inner: if (_palette.Inner.Count < 8) _palette.Inner.Add(new RecipeGlyph(g.Id, _nextEmphasis)); break;
                default: if (_palette.Ring.Count < 8) _palette.Ring.Add(new RecipeGlyph(g.Id, _nextEmphasis)); break;
            }
            _message = $"{g.Name}: {g.Hint}";
            Recompile(cast: _mode == Mode.Palette);
        }

        void RemoveLastPaletteGlyph()
        {
            if (_palette.Ring.Count > 0) _palette.Ring.RemoveAt(_palette.Ring.Count - 1);
            else if (_palette.Inner.Count > 0) _palette.Inner.RemoveAt(_palette.Inner.Count - 1);
            else _palette.Core = null;
            Recompile(cast: false);
        }

        void Recompile(bool cast)
        {
            if (_registry == null) return;
            if (_mode == Mode.Palette)
            {
                _shown = RecipeDrawer.Draw(_palette, _registry, PageCenter, PageRadius);
                _circle = _parser.Parse(_shown);
                // Placed glyphs are stamped, not drawn: they count as clean ink.
                foreach (var g in _circle.AllGlyphs) g.Precision = SpellTuning.PalettePrecision;
            }
            else
            {
                _shown = _drawing;
                _circle = _parser.Parse(_drawing);
            }
            _spell = SpellCompiler.Compile(_circle, _options);
            _recipeText = _palette.ToString();
            RenderCanvas();
            if (cast && _autoCast && _spell.Success) Cast();
        }

        void Cast()
        {
            if (_spell?.Program == null || _preview == null) return;
            _preview.Play(_spell.Program);
            _nextLoop = Time.time + Mathf.Max(2.5f, _spell.Cooldown);
        }

        // ---------------- Canvas ----------------

        void RenderCanvas()
        {
            if (_canvas == null) return;
            _canvas.Clear();

            if (_mode == Mode.Draw && _trace && _palette.GlyphCount > 0)
                foreach (var s in RecipeDrawer.Draw(_palette, _registry, PageCenter, PageRadius).Strokes)
                    _canvas.Polyline(s.Points, GhostInk, 3.2f);

            if (_circle != null && _circle.HasCircle)
            {
                var c = _circle.Center;
                float inner = _circle.GuideRadius > 0f ? _circle.GuideRadius : CircleParser.InnerBand;
                _canvas.Circle(c, CircleParser.CenterBand * _circle.Radius, GuideInk, 1f, dashed: true);
                if (_circle.GuideRadius <= 0f) _canvas.Circle(c, inner * _circle.Radius, GuideInk, 1f, dashed: true);
            }

            var colors = StrokeColors();
            for (int i = 0; i < _shown.Strokes.Count; i++)
                _canvas.Polyline(_shown.Strokes[i].Points, colors.TryGetValue(i, out var col) ? col : InkColor, InkRadius);
            _canvas.Apply();
        }

        Dictionary<int, Color32> StrokeColors()
        {
            var map = new Dictionary<int, Color32>();
            if (_circle == null) return map;
            foreach (int i in _circle.CircleStrokeIndices) map[i] = CircleInk;
            foreach (var g in _circle.AllGlyphs)
            {
                var col = g.Glyph.Role == GlyphRole.Core ? CoreInk : g.Glyph.Role == GlyphRole.Inner ? InnerInk : RingInk;
                foreach (int i in g.StrokeIndices) map[i] = col;
            }
            foreach (var m in _circle.Unrecognized)
                foreach (int i in m.StrokeIndices) map[i] = UnknownInk;
            return map;
        }

        Vec2 ToCanvas(Vector2 gui) => new Vec2(
            Mathf.Clamp(gui.x - CanvasRect.x, 0f, CanvasSize - 1),
            Mathf.Clamp(CanvasRect.yMax - gui.y, 0f, CanvasSize - 1));

        Rect FromCanvas(Bounds2 b) =>
            new Rect(CanvasRect.x + b.MinX, CanvasRect.yMax - b.MaxY, b.Width, b.Height);

        // ---------------- Unity ----------------

        void Update()
        {
            ComputeScale();
            if (_arenaCamera != null)
            {
                var r = ArenaRect;
                _arenaCamera.pixelRect = new Rect(
                    _offset.x + r.x * _scale,
                    Screen.height - (_offset.y + (r.y + r.height) * _scale),
                    r.width * _scale,
                    r.height * _scale);
            }
            if (_loop && _spell != null && _spell.Success && Time.time >= _nextLoop) Cast();
        }

        void ComputeScale()
        {
            _scale = Mathf.Max(0.3f, Mathf.Min(Screen.width / RefW, Screen.height / RefH));
            _offset = new Vector2((Screen.width - RefW * _scale) * 0.5f, (Screen.height - RefH * _scale) * 0.5f);
        }

        void OnGUI()
        {
            if (_registry == null || _canvas == null) return;
            EnsureStyles();
            ComputeScale();
            GUI.matrix = Matrix4x4.TRS(new Vector3(_offset.x, _offset.y, 0f), Quaternion.identity, new Vector3(_scale, _scale, 1f));

            GUI.Label(new Rect(20, 10, 600, 34), "Writing Desk", _title);
            GUI.Label(new Rect(250, 18, 380, 24), _mode == Mode.Draw ? "drawing with the pen" : "placing glyphs from the palette", _small);

            HandleKeys();
            DrawCanvas();
            DrawTools();
            DrawSpellPanel();
            DrawPages();
            DrawArenaFrame();
            DrawJson();

            // Status line: the hovered control's hint, else the latest message.
            string tip = string.IsNullOrEmpty(GUI.tooltip) ? _message : GUI.tooltip;
            GUI.Label(new Rect(ToolsRect.x + 8, ToolsRect.yMax - 40, ToolsRect.width - 16, 36), tip, _small);
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0) return;
            if ((e.control || e.command) && e.keyCode == KeyCode.Z) { Undo(); e.Use(); }
            else if (e.keyCode == KeyCode.Space) { Cast(); e.Use(); }
            else if (e.keyCode == KeyCode.Delete) { ClearPage(); e.Use(); }
        }

        void DrawCanvas()
        {
            GUI.DrawTexture(CanvasRect, _canvas.Texture, ScaleMode.StretchToFill, false);

            int id = GUIUtility.GetControlID(FocusType.Passive, CanvasRect);
            var e = Event.current;
            if (_mode == Mode.Draw)
            {
                switch (e.GetTypeForControl(id))
                {
                    case EventType.MouseDown:
                        if (e.button == 0 && CanvasRect.Contains(e.mousePosition))
                        {
                            GUIUtility.hotControl = id;
                            GUIUtility.keyboardControl = 0;
                            _current = new Stroke();
                            _current.Points.Add(ToCanvas(e.mousePosition));
                            _canvas.Stamp(_current.Points[0], InkColor, InkRadius);
                            _canvas.Apply();
                            e.Use();
                        }
                        break;
                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == id && _current != null)
                        {
                            var p = ToCanvas(e.mousePosition);
                            var last = _current.Points[_current.Points.Count - 1];
                            if (Vec2.Distance(p, last) >= 1.5f)
                            {
                                _current.Points.Add(p);
                                _canvas.Line(last, p, InkColor, InkRadius);
                                _canvas.Apply();
                            }
                            e.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == id)
                        {
                            GUIUtility.hotControl = 0;
                            if (_current != null) AddStroke(_current);
                            _current = null;
                            e.Use();
                        }
                        break;
                }
            }

            // Labels on recognized glyphs.
            if (_circle == null) return;
            int order = 1;
            foreach (var g in _circle.Ring) LabelGlyph(g, $"{order++}. ");
            foreach (var g in _circle.Inner) LabelGlyph(g, "");
            foreach (var g in _circle.Cores) LabelGlyph(g, "");
            foreach (var m in _circle.Unrecognized)
            {
                if (m.Bounds.IsEmpty) continue;
                var r = FromCanvas(m.Bounds);
                GUI.Label(new Rect(r.x, r.y - 17, 90, 16), "?", _glyphTag);
            }
        }

        void LabelGlyph(GlyphInstance g, string prefix)
        {
            if (g.Bounds.IsEmpty) return;
            var r = FromCanvas(g.Bounds);
            string text = _mode == Mode.Palette
                ? prefix + g.Label
                : $"{prefix}{g.Label} {Mathf.RoundToInt(g.Precision * 100f)}%";
            var size = _glyphTag.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(r.center.x - size.x * 0.5f, r.y - size.y - 2f, size.x, size.y), text, _glyphTag);
        }

        void DrawTools()
        {
            GUI.Box(ToolsRect, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(ToolsRect.x + 8, ToolsRect.y + 6, ToolsRect.width - 16, ToolsRect.height - 12));

            GUILayout.BeginHorizontal();
            var newMode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Draw", "Palette" }, GUILayout.Width(150));
            if (newMode != _mode) UsePalette(newMode == Mode.Palette);
            if (GUILayout.Button(new GUIContent("Undo", "Remove the last stroke (Ctrl+Z)"), GUILayout.Width(60))) Undo();
            if (GUILayout.Button(new GUIContent("Clear", "Start a fresh page (Delete)"), GUILayout.Width(60))) ClearPage();
            bool trace = GUILayout.Toggle(_trace, new GUIContent("Trace", "Show the palette recipe faintly under your ink, to trace"), GUILayout.Width(62));
            if (trace != _trace) { _trace = trace; RenderCanvas(); }
            GUILayout.FlexibleSpace();
            GUI.enabled = _spell != null && _spell.Success;
            if (GUILayout.Button(new GUIContent("Cast", "Play the spell in the arena (Space)"), GUILayout.Width(60))) Cast();
            GUI.enabled = true;
            _autoCast = GUILayout.Toggle(_autoCast, new GUIContent("Auto", "Cast automatically when the spell changes"), GUILayout.Width(52));
            _loop = GUILayout.Toggle(_loop, new GUIContent("Loop", "Recast every cooldown"), GUILayout.Width(52));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            PaletteRow("Core", GlyphRole.Core);
            PaletteRow("Inner", GlyphRole.Inner);
            PaletteRow("Ring", GlyphRole.Ring);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Recipe", _bold, GUILayout.Width(50));
            GUI.SetNextControlName("recipe");
            string text = GUILayout.TextField(_recipeText, GUILayout.Width(300));
            if (text != _recipeText)
            {
                _recipeText = text;
                try
                {
                    var parsed = SpellRecipe.Parse(text);
                    _palette = parsed;
                    Recompile(cast: false);
                    _recipeText = text; // keep the player's typing as-is
                }
                catch (FormatException) { }
            }
            if (GUILayout.Button(new GUIContent("Remove last", "Remove the last placed glyph"), GUILayout.Width(90))) RemoveLastPaletteGlyph();
            GUILayout.Label("Size", _small, GUILayout.Width(28));
            _nextEmphasis = (Emphasis)GUILayout.Toolbar((int)_nextEmphasis, new[] { "S", "M", "L" }, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void PaletteRow(string label, GlyphRole role)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _bold, GUILayout.Width(50));
            foreach (var g in _registry.WithRole(role))
                if (GUILayout.Button(new GUIContent(g.Name, g.Hint), GUILayout.Width(64), GUILayout.Height(26)))
                    AddPaletteGlyph(g);
            GUILayout.EndHorizontal();
        }

        void DrawSpellPanel()
        {
            GUI.Box(SpellRect, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(SpellRect.x + 10, SpellRect.y + 8, SpellRect.width - 20, SpellRect.height - 16));
            _spellScroll = GUILayout.BeginScrollView(_spellScroll);

            if (_spell == null) { GUILayout.EndScrollView(); GUILayout.EndArea(); return; }

            string name = _spell.Flavor?.Name ?? (_circle?.Core == null ? "An empty circle" : "An unfinished spell");
            GUILayout.Label(name, _title);
            if (!string.IsNullOrEmpty(_spell.Flavor?.Description)) GUILayout.Label(_spell.Flavor.Description, _label);
            GUILayout.Space(4);
            GUILayout.Label(_spell.Success ? "Ready to cast." : "Not castable yet: see the notes below.", _spell.Success ? _good : _bad);
            GUILayout.Space(6);

            if (_spell.Draft != null)
            {
                Stat("Mana", Fmt(_spell.Mana, "0"));
                Stat("Cooldown", Fmt(_spell.Cooldown, "0.0") + " s");
                Stat("Misfire chance", (_spell.MisfireChance * 100f).ToString("0", CultureInfo.InvariantCulture) + "%");
                Stat("Ink quality (precision)", (_spell.Quality * 100f).ToString("0", CultureInfo.InvariantCulture) + "%");
                Stat("Power", Fmt(_spell.Power, "0.0") + $" of {Fmt(SpellTuning.MaxPower, "0")} a circle holds");
                Stat("Budget check", $"{Fmt(_spell.Cost, "0.0")} / {Fmt(_spell.Budget, "0.0")} pp");
                Stat("Ink to write it", _spell.Ink.ToString(CultureInfo.InvariantCulture));
                GUILayout.Space(8);
            }

            GUILayout.Label("Glyphs", _bold);
            if (_circle != null)
            {
                if (_circle.Cores.Count == 0) GUILayout.Label("Core: (none)", _label);
                foreach (var g in _circle.Cores) GUILayout.Label($"Core: {Describe(g)}", _label);
                GUILayout.Label("Inner: " + (_circle.Inner.Count == 0 ? "(none)" : Join(_circle.Inner)), _label);
                if (_circle.Ring.Count == 0) GUILayout.Label("Ring: (none)", _label);
                else
                {
                    GUILayout.Label("Ring, clockwise from the top:", _label);
                    for (int i = 0; i < _circle.Ring.Count; i++) GUILayout.Label($"   {i + 1}. {Describe(_circle.Ring[i])}", _label);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("Notes", _bold);
            if (_spell.Diagnostics.Count == 0) GUILayout.Label("Nothing to add. Clean work.", _info);
            foreach (var d in _spell.Diagnostics)
                GUILayout.Label("• " + d.Message, d.Severity == Severity.Error ? _bad : d.Severity == Severity.Warning ? _warn : _info);

            GUILayout.Space(10);
            GUILayout.Label("How to write a spell", _bold);
            GUILayout.Label(
                "1. Draw one large closed circle.\n" +
                "2. Draw a core sigil in the middle (what the spell is made of).\n" +
                "3. Draw modifiers in the inner ring (how much, how far, how long).\n" +
                "4. Draw shapes on the outer ring; they happen clockwise from the top.\n" +
                "Draw each glyph's strokes one after another. Bigger glyphs mean more. " +
                "Hover a palette button to see how its glyph is drawn; switch on Trace to follow it.",
                _small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string Describe(GlyphInstance g) =>
            _mode == Mode.Palette ? g.Label : $"{g.Label}  ({Mathf.RoundToInt(g.Precision * 100f)}% clean)";

        string Join(List<GlyphInstance> list)
        {
            var parts = new List<string>();
            foreach (var g in list) parts.Add(_mode == Mode.Palette ? g.Label : $"{g.Label} {Mathf.RoundToInt(g.Precision * 100f)}%");
            return string.Join(", ", parts);
        }

        void Stat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _label, GUILayout.Width(190));
            GUILayout.Label(value, _bold);
            GUILayout.EndHorizontal();
        }

        void DrawPages()
        {
            GUI.Box(PagesRect, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(PagesRect.x + 8, PagesRect.y + 6, PagesRect.width - 16, PagesRect.height - 12));
            GUILayout.Label("Pages from old tomes (click to open)", _bold);
            var pages = PrewrittenSpells.All;
            for (int i = 0; i < pages.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + 2, pages.Count); j++)
                    if (GUILayout.Button(new GUIContent(pages[j].Title, pages[j].Note + "  [" + pages[j].Recipe + "]"), GUILayout.Width(230)))
                    {
                        _mode = Mode.Draw;
                        LoadPage(pages[j]);
                    }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        void DrawArenaFrame()
        {
            // The arena camera renders inside ArenaRect; just caption it.
            GUI.Label(new Rect(ArenaRect.x + 6, ArenaRect.y + 4, ArenaRect.width - 12, 20),
                "Test arena: you (center), three drifting dummies. Hits flash the status color.", _small);
        }

        void DrawJson()
        {
            GUI.Box(JsonRect, GUIContent.none, _panel);
            GUILayout.BeginArea(new Rect(JsonRect.x + 8, JsonRect.y + 6, JsonRect.width - 16, JsonRect.height - 12));
            GUILayout.Label(_spell != null && _spell.Program == null && _spell.Draft != null ? "Effect program (draft, not castable)" : "Effect program (JSON)", _bold);
            _jsonScroll = GUILayout.BeginScrollView(_jsonScroll);
            GUILayout.Label(_spell?.Json ?? "", _json);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string Fmt(float v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        // ---------------- Styles ----------------

        void EnsureStyles()
        {
            if (_title != null) return;
            _panelTex = Solid(new Color(0.16f, 0.12f, 0.1f, 0.94f));
            _tagTex = Solid(new Color(0.12f, 0.09f, 0.07f, 0.8f));
            var text = new Color(0.93f, 0.89f, 0.8f);

            _panel = new GUIStyle(GUI.skin.box) { normal = { background = _panelTex } };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.98f, 0.9f, 0.7f) }, wordWrap = true };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = text } };
            _small = new GUIStyle(_label) { fontSize = 11, normal = { textColor = new Color(0.8f, 0.76f, 0.68f) } };
            _bold = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
            _good = new GUIStyle(_bold) { normal = { textColor = new Color(0.55f, 0.9f, 0.5f) } };
            _bad = new GUIStyle(_label) { normal = { textColor = new Color(1f, 0.5f, 0.45f) } };
            _warn = new GUIStyle(_label) { normal = { textColor = new Color(1f, 0.8f, 0.4f) } };
            _info = new GUIStyle(_label) { normal = { textColor = new Color(0.75f, 0.8f, 0.85f) } };
            _json = new GUIStyle(_label) { fontSize = 11, wordWrap = false, normal = { textColor = new Color(0.8f, 0.85f, 0.75f) } };
            _glyphTag = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 1, 1), normal = { background = _tagTex, textColor = new Color(1f, 0.95f, 0.85f) },
            };
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
