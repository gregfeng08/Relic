using UnityEngine;

namespace Relic.Magic.Desk
{
    /// <summary>
    /// The only component in WritingDesk.unity: builds the cameras, light, test
    /// arena and the desk UI at runtime, so the scene file stays trivial and
    /// merge-friendly.
    /// </summary>
    public sealed class WritingDeskBootstrap : MonoBehaviour
    {
        public WritingDesk Desk { get; private set; }
        public SpellPreview Preview { get; private set; }

        void Awake() => Build();

        /// <summary>Builds everything as children of this object (safe to call once; tests call it in edit mode).</summary>
        public WritingDesk Build()
        {
            if (Desk != null) return Desk;
            var registry = GlyphLibrary.Load();

            // A camera that only paints the desk's background color behind the UI.
            var bg = new GameObject("Desk Background Camera").AddComponent<Camera>();
            bg.transform.SetParent(transform, false);
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = new Color(0.1f, 0.075f, 0.06f);
            bg.cullingMask = 0;
            bg.depth = -10;

            Preview = SpellPreview.Build(new Vector3(0f, 0f, 0f));
            Preview.transform.SetParent(transform, true);

            var cam = new GameObject("Arena Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.position = new Vector3(0f, 19f, -6f);
            cam.transform.LookAt(new Vector3(0f, 0f, 7f));
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.15f, 0.13f);
            cam.depth = 0;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Desk = gameObject.AddComponent<WritingDesk>();
            Desk.Init(registry, Preview, cam);
            return Desk;
        }
    }
}
