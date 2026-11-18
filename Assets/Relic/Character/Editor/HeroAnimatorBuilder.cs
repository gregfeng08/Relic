using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Relic.Character.Editor
{
    /// <summary>
    /// Builds the hero's Mecanim assets: bakes the procedural poses into Humanoid clips
    /// (HeroClipBaker), the right-arm AvatarMask, and the AnimatorController the runtime loads
    /// from Resources (HeroMecanim.ResourcePath). Menu: Relic > Character > Bake Hero Animator.
    /// Batchmode: -executeMethod Relic.Character.Editor.HeroAnimatorBuilder.BuildFromCommandLine -quit
    ///
    /// Base layer: Idle; Move (1D blend on Speed: Walk 1.5, Run 6, Sprint 9.5); Dash; Jump (Apex →
    /// Rise on VerticalSpeed) → Fall (Fall → Apex) → Land (Idle → Land on the controller's landing
    /// squash); Climb; Swim (SwimIdle → Swim on Speed); Dive (DiveUp → Dive → DiveDown on
    /// DivePitch); Fly. Mode (int) picks ground/air/climb/swim/fly/dive; Rate scales the cycles to
    /// the procedural cadence. Weapon layer: the drawn-weapon right arm (masked), weight from code.
    /// </summary>
    public static class HeroAnimatorBuilder
    {
        public const string Dir = "Assets/Relic/Character/Animation";
        public const string ClipDir = Dir + "/Clips";
        public const string MaskPath = Dir + "/HeroWeaponArm.mask";
        public const string ControllerPath = "Assets/Relic/Character/Resources/" + HeroMecanim.ResourcePath + ".controller";

        [MenuItem("Relic/Character/Bake Hero Animator")]
        public static void BuildMenu() => Build();

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[Relic.Character] Hero animator baked: " + ControllerPath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string ClipPath(string name) => ClipDir + "/Hero" + name + ".anim";

        public static void Build()
        {
            EnsureFolder("Assets/Relic/Character", "Animation");
            EnsureFolder(Dir, "Clips");
            EnsureFolder("Assets/Relic/Character", "Resources");
            EnsureFolder("Assets/Relic/Character/Resources", "Relic");

            var clips = new Dictionary<string, AnimationClip>();
            HeroRig rig = HeroClipBaker.NewRig(out GameObject go);
            Avatar avatar = HeroHumanoid.Build(rig);
            try
            {
                if (!avatar.isValid || !avatar.isHuman) throw new Exception("hero avatar is not a valid humanoid");
                var handler = new HumanPoseHandler(avatar, rig.RootTransform);
                foreach (HeroClipBaker.ClipSpec s in HeroClipBaker.Specs)
                {
                    string path = ClipPath(s.Name);
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    AnimationClip clip = HeroClipBaker.Bake(s, rig, handler, existing);
                    if (existing == null) AssetDatabase.CreateAsset(clip, path);
                    else EditorUtility.SetDirty(clip);
                    clips[s.Name] = clip;
                }
                handler.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            AvatarMask mask = BuildMask();
            BuildController(clips, mask);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static AvatarMask BuildMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            bool create = mask == null;
            if (create) mask = new AvatarMask();
            for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++)
                mask.SetHumanoidBodyPartActive(p, p == AvatarMaskBodyPart.RightArm || p == AvatarMaskBodyPart.RightFingers);
            if (create) AssetDatabase.CreateAsset(mask, MaskPath);
            else EditorUtility.SetDirty(mask);
            return mask;
        }

        static void BuildController(Dictionary<string, AnimationClip> c, AvatarMask mask)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Param(ctrl, HeroMecanim.Speed, AnimatorControllerParameterType.Float);
            Param(ctrl, HeroMecanim.VerticalSpeed, AnimatorControllerParameterType.Float);
            Param(ctrl, HeroMecanim.Mode, AnimatorControllerParameterType.Int);
            Param(ctrl, HeroMecanim.Dash, AnimatorControllerParameterType.Bool);
            Param(ctrl, HeroMecanim.Rate, AnimatorControllerParameterType.Float, 1f);
            Param(ctrl, HeroMecanim.DivePitch, AnimatorControllerParameterType.Float);
            Param(ctrl, HeroMecanim.Land, AnimatorControllerParameterType.Float);

            AnimatorControllerLayer[] layers = ctrl.layers;
            layers[0].name = "Base";
            ctrl.layers = layers;
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            AnimatorState idle = State(sm, HeroMecanim.SIdle, c["Idle"], 0, 0);
            AnimatorState move = State(sm, HeroMecanim.SMove, Tree(ctrl, "Move", HeroMecanim.Speed, (c["Walk"], HeroMecanim.WalkSpeed), (c["Run"], HeroAnimator.RunSpeed), (c["Sprint"], HeroAnimator.SprintSpeed)), 1, 0, true);
            AnimatorState dash = State(sm, HeroMecanim.SDash, c["Dash"], 2, 0);
            AnimatorState land = State(sm, HeroMecanim.SLand, Tree(ctrl, "Land", HeroMecanim.Land, (c["Idle"], 0f), (c["Land"], 1f)), 0, 1);
            AnimatorState jump = State(sm, HeroMecanim.SJump, Tree(ctrl, "Jump", HeroMecanim.VerticalSpeed, (c["Apex"], 0f), (c["Rise"], 4f)), 1, 1);
            AnimatorState fall = State(sm, HeroMecanim.SFall, Tree(ctrl, "Fall", HeroMecanim.VerticalSpeed, (c["Fall"], -8f), (c["Apex"], 0f)), 2, 1);
            AnimatorState climb = State(sm, HeroMecanim.SClimb, c["Climb"], 0, 2, true);
            AnimatorState swim = State(sm, HeroMecanim.SSwim, Tree(ctrl, "Swim", HeroMecanim.Speed, (c["SwimIdle"], 0f), (c["Swim"], HeroMecanim.BakeSwimSpeed)), 1, 2, true);
            AnimatorState dive = State(sm, HeroMecanim.SDive, Tree(ctrl, "Dive", HeroMecanim.DivePitch, (c["DiveUp"], -55f), (c["Dive"], 0f), (c["DiveDown"], 45f)), 2, 2, true);
            AnimatorState fly = State(sm, HeroMecanim.SFly, c["Fly"], 3, 2);
            sm.defaultState = idle;

            const AnimatorConditionMode Gt = AnimatorConditionMode.Greater, Lt = AnimatorConditionMode.Less, Eq = AnimatorConditionMode.Equals;
            const AnimatorConditionMode If = AnimatorConditionMode.If, IfNot = AnimatorConditionMode.IfNot;
            string speed = HeroMecanim.Speed, vs = HeroMecanim.VerticalSpeed, mode = HeroMecanim.Mode, dashP = HeroMecanim.Dash, landP = HeroMecanim.Land;

            // Ground: idle ↔ move, landing squash.
            T(idle, move, 0.12f, (Gt, 0.3f, speed), (Eq, HeroMecanim.Ground, mode));
            T(move, idle, 0.2f, (Lt, 0.3f, speed), (Eq, HeroMecanim.Ground, mode));
            T(idle, land, 0.05f, (Gt, 0.15f, landP), (Eq, HeroMecanim.Ground, mode));
            T(land, move, 0.12f, (Gt, 0.3f, speed), (Eq, HeroMecanim.Ground, mode));
            T(land, idle, 0.15f, (Lt, 0.05f, landP), (Lt, 0.3f, speed), (Eq, HeroMecanim.Ground, mode));
            // Dash (ground or air) and back.
            Any(sm, dash, 0.05f, (If, 0f, dashP), (Eq, HeroMecanim.Ground, mode));
            Any(sm, dash, 0.05f, (If, 0f, dashP), (Eq, HeroMecanim.Air, mode));
            T(dash, move, 0.15f, (IfNot, 0f, dashP), (Eq, HeroMecanim.Ground, mode), (Gt, 0.3f, speed));
            T(dash, idle, 0.2f, (IfNot, 0f, dashP), (Eq, HeroMecanim.Ground, mode), (Lt, 0.3f, speed));
            T(dash, fall, 0.2f, (IfNot, 0f, dashP), (Eq, HeroMecanim.Air, mode));
            // Air: jump → fall → land.
            foreach (AnimatorState s in new[] { idle, move, land })
            {
                T(s, jump, 0.08f, (Eq, HeroMecanim.Air, mode), (Gt, 0.5f, vs));
                T(s, fall, 0.15f, (Eq, HeroMecanim.Air, mode), (Lt, 0.5f, vs));
            }
            T(jump, fall, 0.25f, (Eq, HeroMecanim.Air, mode), (Lt, 0.5f, vs));
            T(fall, jump, 0.1f, (Eq, HeroMecanim.Air, mode), (Gt, 2f, vs));
            T(jump, land, 0.06f, (Eq, HeroMecanim.Ground, mode));
            T(fall, land, 0.06f, (Eq, HeroMecanim.Ground, mode));
            // Traversal modes from anywhere, and back to the ground or the air.
            Any(sm, climb, 0.18f, (Eq, HeroMecanim.Climb, mode));
            Any(sm, swim, 0.25f, (Eq, HeroMecanim.Swim, mode));
            Any(sm, fly, 0.25f, (Eq, HeroMecanim.Fly, mode));
            Any(sm, dive, 0.3f, (Eq, HeroMecanim.Dive, mode));
            foreach (AnimatorState s in new[] { climb, swim, dive, fly })
            {
                T(s, idle, 0.2f, (Eq, HeroMecanim.Ground, mode), (Lt, 0.3f, speed));
                T(s, move, 0.2f, (Eq, HeroMecanim.Ground, mode), (Gt, 0.3f, speed));
                T(s, jump, 0.15f, (Eq, HeroMecanim.Air, mode), (Gt, 0.5f, vs));
                T(s, fall, 0.2f, (Eq, HeroMecanim.Air, mode), (Lt, 0.5f, vs));
            }

            // Weapon layer: the drawn-weapon right arm over everything (weight set by HeroMecanim).
            ctrl.AddLayer("Weapon");
            layers = ctrl.layers;
            layers[HeroMecanim.WeaponLayer].avatarMask = mask;
            layers[HeroMecanim.WeaponLayer].defaultWeight = 0f;
            layers[HeroMecanim.WeaponLayer].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;
            AnimatorStateMachine wsm = ctrl.layers[HeroMecanim.WeaponLayer].stateMachine;
            AnimatorState ready = wsm.AddState("Ready");
            ready.motion = c["Ready"];
            wsm.defaultState = ready;
            EditorUtility.SetDirty(ctrl);
        }

        static void Param(AnimatorController ctrl, string name, AnimatorControllerParameterType type, float def = 0f)
        {
            ctrl.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = def });
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, int col, int row, bool rate = false)
        {
            AnimatorState s = sm.AddState(name, new Vector3(300f + col * 230f, row * 110f, 0f));
            s.motion = motion;
            if (rate)
            {
                s.speedParameterActive = true;
                s.speedParameter = HeroMecanim.Rate;
            }
            return s;
        }

        static BlendTree Tree(AnimatorController ctrl, string name, string param, params (Motion motion, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = param,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            foreach (var ch in children) tree.AddChild(ch.motion, ch.threshold);
            return tree;
        }

        static AnimatorStateTransition T(AnimatorState from, AnimatorState to, float duration, params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            Setup(t, duration, conditions);
            return t;
        }

        static void Any(AnimatorStateMachine sm, AnimatorState to, float duration, params (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.canTransitionToSelf = false;
            Setup(t, duration, conditions);
        }

        static void Setup(AnimatorStateTransition t, float duration, (AnimatorConditionMode mode, float threshold, string param)[] conditions)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.exitTime = 0f;
            foreach (var c in conditions) t.AddCondition(c.mode, c.threshold, c.param);
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
