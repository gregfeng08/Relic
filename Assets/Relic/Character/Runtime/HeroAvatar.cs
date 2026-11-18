using UnityEngine;
using UnityEngine.Rendering;

namespace Relic.Character
{
    /// <summary>
    /// The procedural hero (docs/design/14-character.md): builds one skinned mesh from code on a
    /// rest-pose rig, dresses it from the equipped items, rigs it as a Unity Humanoid and drives it
    /// with the hero AnimatorController (clips baked from the procedural poses), then steps the
    /// spring chains on top. Rebuilt from scratch whenever the look, kit or armor weight changes.
    /// Without a controller (or with ProceduralOnly) the procedural HeroAnimator poses it instead.
    /// </summary>
    public sealed class HeroAvatar : MonoBehaviour
    {
        public Material ToonMaterial;
        public HeroKit Kit = new HeroKit();
        /// <summary>The look (data-driven; sanitized on rebuild). Defaults to Greg's hero.</summary>
        public HeroAppearance Appearance = AppearancePresets.Default();
        /// <summary>Overrides the armor weight shown (F9 debug), or null for the kit's own.</summary>
        public ArmorWeight? ArmorOverride;
        public bool WeaponDrawn;
        /// <summary>Driven by the player when present; tools can set it directly.</summary>
        public HeroAnimInput Input;
        /// <summary>The Animator's controller; null loads Resources/Relic/HeroAnimator.</summary>
        public RuntimeAnimatorController Controller;
        /// <summary>Skip the Animator and pose procedurally (a fallback, and for comparisons).</summary>
        public bool ProceduralOnly;

        public HeroRig Rig { get; private set; }
        public HeroShape Shape { get; private set; }
        public HeroLook Look { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        public SpringChains Chains { get; private set; }
        /// <summary>The procedural poser (used when there is no Animator).</summary>
        public HeroAnimator Procedural { get; private set; }
        /// <summary>The Unity Humanoid avatar built for this rig.</summary>
        public Avatar HumanAvatar { get; private set; }
        /// <summary>The Mecanim Animator on the rig root, or null when posing procedurally.</summary>
        public Animator Mecanim { get; private set; }
        public HeroMecanim Bridge { get; private set; }
        public int TriangleCount { get; private set; }
        public int MaterialCount { get; private set; }
        /// <summary>Triangles per part group, for budgeting.</summary>
        public string Breakdown { get; private set; }

        HeroMaterials _mats;
        Mesh _mesh;
        static RuntimeAnimatorController _defaultController;

        public static RuntimeAnimatorController DefaultController()
        {
            if (_defaultController == null) _defaultController = Resources.Load<RuntimeAnimatorController>(HeroMecanim.ResourcePath);
            return _defaultController;
        }

        public void Rebuild()
        {
            Teardown();
            Look = EquipmentMapper.Map(Kit);
            if (ArmorOverride.HasValue) Look.Armor.Weight = ArmorOverride.Value;

            if (Appearance == null) Appearance = AppearancePresets.Default();
            AppearanceRules.Sanitize(Appearance);
            Shape = new HeroShape(Appearance.Body);
            Rig = new HeroRig(transform, Shape.Remap);
            _mats = new HeroMaterials(ToonMaterial);
            var b = new HeroBuild(Rig, _mats, Look, Appearance, Shape, WeaponDrawn);
            var sb = new System.Text.StringBuilder();
            int last = 0;
            void Mark(string part) { int t = b.Kit.TriangleCount; sb.Append(part).Append(' ').Append(t - last).Append(' '); last = t; }
            HeroBody.Build(b); Mark("body");
            HeroCoat.Build(b); Mark("coat");
            HeroArmor.Build(b); Mark("armor");
            HeroScarf.Build(b); Mark("scarf");
            HeroHair.Build(b); Mark("hair");
            HeroAccessories.Build(b); Mark("accessories");
            HeroGear.Build(b); Mark("gear");
            HeroWeapon.Build(b); Mark("weapon");
            Breakdown = sb.ToString();

            _mesh = b.Kit.Build("Hero");
            _mesh.bindposes = Rig.BindPoses();
            TriangleCount = b.Kit.TriangleCount;
            MaterialCount = 1;

            var go = new GameObject("HeroMesh");
            go.layer = gameObject.layer;
            go.transform.SetParent(Rig.RootTransform, false);
            Renderer = go.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = _mesh;
            Renderer.sharedMaterial = _mats.Material;
            Renderer.bones = Rig.Bones.ToArray();
            Renderer.rootBone = Rig.Bones[HeroRig.Hips];
            Renderer.localBounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(2.4f, 2.6f, 2.4f));
            Renderer.updateWhenOffscreen = false;
            Renderer.shadowCastingMode = ShadowCastingMode.On;
            Renderer.quality = SkinQuality.Bone4;

            // The Humanoid avatar first (it needs the rest pose), then the Animator on the rig root.
            HumanAvatar = HeroHumanoid.Build(Rig);
            Procedural = new HeroAnimator(Rig);
            RuntimeAnimatorController controller = Controller != null ? Controller : DefaultController();
            if (!ProceduralOnly && controller != null && HumanAvatar.isValid && HumanAvatar.isHuman)
            {
                Mecanim = Rig.RootTransform.gameObject.AddComponent<Animator>();
                Mecanim.avatar = HumanAvatar;
                Mecanim.runtimeAnimatorController = controller;
                Mecanim.applyRootMotion = false;
                Mecanim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Bridge = new HeroMecanim(Mecanim);
                Bridge.Snap(Input);
                Rig.RootTransform.localPosition = new Vector3(0f, Input.VisualYOffset, 0f);
            }
            else Procedural.Snap(Input);
            Chains = new SpringChains(Rig, b.Chains);
            AddColliders();
            Chains.Reset();
        }

        void AddColliders()
        {
            Transform B(int i) => Rig.Bones[i];
            // Offsets are in actual (HeroShape-mapped) bone space.
            Chains.AddCollider(B(HeroRig.Head), new Vector3(0f, 0.09f, -0.01f), new Vector3(0f, 0.15f, -0.01f), 0.125f);
            Chains.AddCollider(B(HeroRig.Neck), new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.05f, 0f), 0.055f);
            Chains.AddCollider(B(HeroRig.Chest), new Vector3(-0.06f, 0.05f, -0.01f), new Vector3(0.06f, 0.05f, -0.01f), 0.105f);
            Chains.AddCollider(B(HeroRig.Spine), new Vector3(0f, -0.05f, 0f), new Vector3(0f, 0.09f, 0f), 0.11f);
            Chains.AddCollider(B(HeroRig.Hips), new Vector3(-0.04f, -0.04f, 0f), new Vector3(0.04f, -0.04f, 0f), 0.11f);
            // The backpack keeps the hair and scarf off the back.
            if (Appearance.Outfit.Pack)
                Chains.AddCollider(B(HeroRig.Chest), new Vector3(-0.05f, -0.08f, -0.17f), new Vector3(0.05f, -0.08f, -0.17f), 0.065f);
            for (int s = -1; s <= 1; s += 2)
            {
                Chains.AddCollider(B(HeroRig.Thigh(s)), new Vector3(0f, -0.06f, 0.0f), new Vector3(0f, -0.37f, 0.01f), 0.066f, SpringChains.Legs);
                Chains.AddCollider(B(HeroRig.Shin(s)), new Vector3(0f, 0f, 0.01f), new Vector3(0f, -0.28f, 0f), 0.066f, SpringChains.Legs);
                Chains.AddCollider(B(HeroRig.UpperArm(s)), new Vector3(0f, 0f, 0f), new Vector3(0f, -0.24f, 0f), 0.056f);
            }
        }

        /// <summary>
        /// Feeds this frame's movement to the Animator. The driver calls it in Update so Unity
        /// evaluates the Animator with it the same frame; Animate pushes it again (harmless), so
        /// tools that only call Animate still work.
        /// </summary>
        public void SetInput(HeroAnimInput a, float dt)
        {
            Input = a;
            Bridge?.Push(a, dt);
        }

        /// <summary>
        /// Poses the hero and steps secondary motion; called every frame in LateUpdate by the driver
        /// (or by tools). In play mode Unity evaluates the Animator itself; in edit mode (tests,
        /// captures) it is stepped here.
        /// </summary>
        public void Animate(float dt, Vector3 worldVelocity)
        {
            if (Rig == null) return;
            if (Mecanim != null)
            {
                Bridge.Push(Input, dt);
                if (!Application.isPlaying) Mecanim.Update(Mathf.Clamp(dt, 0f, 0.1f));
                // Step smoothing is applied unfiltered so the feet track the controller exactly.
                Rig.RootTransform.localPosition = new Vector3(0f, Input.VisualYOffset, 0f);
            }
            else Procedural.Apply(Input, dt);
            // Air drag on cloth and hair: things trail behind when moving, more so when dashing.
            Chains.Wind = -worldVelocity * (Input.Dash ? 2.2f : 1.1f);
            // Underwater, hair and scarf drift slowly upward; at the surface they float.
            Chains.GravityScale = Input.Underwater || Input.Motion == HeroMotion.Dive ? -0.12f : Input.Motion == HeroMotion.Swim ? 0.3f : 1f;
            if (Input.Motion == HeroMotion.Dive || Input.Motion == HeroMotion.Swim) Chains.Wind *= 0.4f;
            Chains.Step(dt);
        }

        void Teardown()
        {
            if (Rig != null) DestroyObj(Rig.RootTransform.gameObject);
            if (_mesh != null) DestroyObj(_mesh);
            if (HumanAvatar != null) DestroyObj(HumanAvatar);
            _mats?.Destroy();
            Rig = null; _mesh = null; _mats = null; Renderer = null; Chains = null; Procedural = null;
            HumanAvatar = null; Mecanim = null; Bridge = null;
        }

        static void DestroyObj(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnDestroy() => Teardown();
    }
}
