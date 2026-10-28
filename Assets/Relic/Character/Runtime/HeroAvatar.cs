using UnityEngine;
using UnityEngine.Rendering;

namespace Relic.Character
{
    /// <summary>
    /// The procedural hero (docs/design/14-character.md): builds one skinned mesh from code on a
    /// rest-pose rig, dresses it from the equipped items, and animates it every frame (pose, then
    /// spring chains). Rebuilt from scratch whenever the kit or armor weight changes.
    /// </summary>
    public sealed class HeroAvatar : MonoBehaviour
    {
        public Material ToonMaterial;
        public HeroKit Kit = new HeroKit();
        /// <summary>Overrides the armor weight shown (F9 debug), or null for the kit's own.</summary>
        public ArmorWeight? ArmorOverride;
        public bool WeaponDrawn;
        /// <summary>Driven by the player when present; tools can set it directly.</summary>
        public HeroAnimInput Input;

        public HeroRig Rig { get; private set; }
        public HeroLook Look { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        public SpringChains Chains { get; private set; }
        public HeroAnimator Animator { get; private set; }
        public int TriangleCount { get; private set; }
        public int MaterialCount { get; private set; }
        /// <summary>Triangles per part group, for budgeting.</summary>
        public string Breakdown { get; private set; }

        HeroMaterials _mats;
        Mesh _mesh;

        public void Rebuild()
        {
            Teardown();
            Look = EquipmentMapper.Map(Kit);
            if (ArmorOverride.HasValue) Look.Armor.Weight = ArmorOverride.Value;

            Rig = new HeroRig(transform);
            _mats = new HeroMaterials(ToonMaterial);
            var b = new HeroBuild(Rig, _mats, Look, WeaponDrawn);
            var sb = new System.Text.StringBuilder();
            int last = 0;
            void Mark(string part) { int t = b.Kit.TriangleCount; sb.Append(part).Append(' ').Append(t - last).Append(' '); last = t; }
            HeroBody.Build(b); Mark("body");
            HeroCoat.Build(b); Mark("coat");
            HeroArmor.Build(b); Mark("armor");
            HeroScarf.Build(b); Mark("scarf");
            HeroHair.Build(b); Mark("hair");
            HeroGear.Build(b); Mark("gear");
            HeroWeapon.Build(b); Mark("weapon");
            Breakdown = sb.ToString();

            _mesh = b.Kit.Build("Hero", out Material[] materials);
            _mesh.bindposes = Rig.BindPoses();
            TriangleCount = b.Kit.TriangleCount;
            MaterialCount = materials.Length;

            var go = new GameObject("HeroMesh");
            go.layer = gameObject.layer;
            go.transform.SetParent(Rig.RootTransform, false);
            Renderer = go.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = _mesh;
            Renderer.sharedMaterials = materials;
            Renderer.bones = Rig.Bones.ToArray();
            Renderer.rootBone = Rig.Bones[HeroRig.Hips];
            Renderer.localBounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(2.4f, 2.6f, 2.4f));
            Renderer.updateWhenOffscreen = false;
            Renderer.shadowCastingMode = ShadowCastingMode.On;
            Renderer.quality = SkinQuality.Bone4;

            Animator = new HeroAnimator(Rig);
            Animator.Snap(Input);
            Chains = new SpringChains(Rig, b.Chains);
            AddColliders();
            Chains.Reset();
        }

        void AddColliders()
        {
            Transform B(int i) => Rig.Bones[i];
            Chains.AddCollider(B(HeroRig.Head), new Vector3(0f, 0.08f, -0.005f), new Vector3(0f, 0.13f, -0.005f), 0.11f);
            Chains.AddCollider(B(HeroRig.Neck), new Vector3(0f, -0.02f, 0f), new Vector3(0f, 0.06f, 0f), 0.06f);
            Chains.AddCollider(B(HeroRig.Chest), new Vector3(-0.07f, 0.06f, -0.01f), new Vector3(0.07f, 0.06f, -0.01f), 0.12f);
            Chains.AddCollider(B(HeroRig.Spine), new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.1f, 0f), 0.13f);
            Chains.AddCollider(B(HeroRig.Hips), new Vector3(-0.05f, -0.04f, 0f), new Vector3(0.05f, -0.04f, 0f), 0.13f);
            // The backpack keeps the hair and scarf off the back.
            Chains.AddCollider(B(HeroRig.Chest), new Vector3(-0.06f, -0.06f, -0.20f), new Vector3(0.06f, -0.06f, -0.20f), 0.10f);
            for (int s = -1; s <= 1; s += 2)
            {
                Chains.AddCollider(B(HeroRig.Thigh(s)), new Vector3(0f, -0.06f, 0.0f), new Vector3(0f, -0.40f, 0.01f), 0.075f, SpringChains.Legs);
                Chains.AddCollider(B(HeroRig.Shin(s)), new Vector3(0f, 0f, 0.01f), new Vector3(0f, -0.3f, 0f), 0.075f, SpringChains.Legs);
                Chains.AddCollider(B(HeroRig.UpperArm(s)), new Vector3(0f, 0f, 0f), new Vector3(0f, -0.26f, 0f), 0.065f);
            }
        }

        /// <summary>Poses the hero and steps secondary motion. Called every frame by the driver (or tools).</summary>
        public void Animate(float dt, Vector3 worldVelocity)
        {
            if (Rig == null) return;
            Animator.Apply(Input, dt);
            // Air drag on cloth and hair: things trail behind when moving, more so when dashing.
            Chains.Wind = -worldVelocity * (Input.Dash ? 2.2f : 1.1f);
            // Underwater, hair and scarf drift slowly upward; at the surface they float.
            Chains.GravityScale = Input.Motion == HeroMotion.Dive ? -0.12f : Input.Motion == HeroMotion.Swim ? 0.3f : 1f;
            if (Input.Motion == HeroMotion.Dive || Input.Motion == HeroMotion.Swim) Chains.Wind *= 0.4f;
            Chains.Step(dt);
        }

        void Teardown()
        {
            if (Rig != null) DestroyObj(Rig.RootTransform.gameObject);
            if (_mesh != null) DestroyObj(_mesh);
            _mats?.Destroy();
            Rig = null; _mesh = null; _mats = null; Renderer = null; Chains = null; Animator = null;
        }

        static void DestroyObj(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnDestroy() => Teardown();
    }
}
