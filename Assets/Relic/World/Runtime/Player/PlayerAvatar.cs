using System.Collections.Generic;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Placeholder hero built from primitives with the toon + outline material, and a small
    /// procedural animation (run cycle, lean, dash stretch, landing squash, climb, swim, dive), blended between states. The silhouette
    /// is the hitbox in combat, so it is kept simple and readable.
    /// </summary>
    public sealed class PlayerAvatar : MonoBehaviour
    {
        public PlayerController Player;

        Transform _body, _torso, _head, _legL, _legR, _armL, _armR, _cape;
        float _phase;
        float _lean;
        float _stretch;
        readonly List<Material> _materials = new List<Material>();

        public void Build(Material toon)
        {
            var root = new GameObject("Avatar").transform;
            root.SetParent(transform, false);
            _body = root;

            Material tunic = Tint(toon, new Color(0.22f, 0.42f, 0.80f));
            Material skin = Tint(toon, new Color(1.00f, 0.84f, 0.71f));
            Material hair = Tint(toon, new Color(0.28f, 0.18f, 0.13f));
            Material trousers = Tint(toon, new Color(0.20f, 0.18f, 0.24f));
            Material cape = Tint(toon, new Color(0.86f, 0.26f, 0.22f));

            _torso = Part(PrimitiveType.Capsule, root, new Vector3(0f, 1.08f, 0f), new Vector3(0.52f, 0.42f, 0.40f), tunic);
            _head = Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.66f, 0f), new Vector3(0.40f, 0.42f, 0.40f), skin);
            Part(PrimitiveType.Sphere, _head, new Vector3(0f, 0.22f, -0.06f), new Vector3(1.08f, 0.7f, 1.08f), hair);
            Part(PrimitiveType.Sphere, _head, new Vector3(0f, -0.05f, -0.42f), new Vector3(0.55f, 0.55f, 0.4f), hair);

            _legL = Pivot(root, new Vector3(-0.13f, 0.74f, 0f));
            _legR = Pivot(root, new Vector3(0.13f, 0.74f, 0f));
            Part(PrimitiveType.Capsule, _legL, new Vector3(0f, -0.36f, 0f), new Vector3(0.19f, 0.37f, 0.19f), trousers);
            Part(PrimitiveType.Capsule, _legR, new Vector3(0f, -0.36f, 0f), new Vector3(0.19f, 0.37f, 0.19f), trousers);

            _armL = Pivot(root, new Vector3(-0.33f, 1.36f, 0f));
            _armR = Pivot(root, new Vector3(0.33f, 1.36f, 0f));
            Part(PrimitiveType.Capsule, _armL, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.3f, 0.14f), skin);
            Part(PrimitiveType.Capsule, _armR, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.3f, 0.14f), skin);

            _cape = Pivot(root, new Vector3(0f, 1.42f, -0.2f));
            Part(PrimitiveType.Cube, _cape, new Vector3(0f, -0.36f, 0f), new Vector3(0.46f, 0.72f, 0.04f), cape);

        }

        Material Tint(Material baseMat, Color c)
        {
            var m = new Material(baseMat) { name = "Avatar " + c };
            m.SetColor(ShaderIds.BaseColor, c);
            _materials.Add(m);
            return m;
        }

        static Transform Pivot(Transform parent, Vector3 pos)
        {
            var t = new GameObject("Pivot").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.gameObject.layer = 2;
            return t;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2; // Ignore Raycast: camera and ground probes see through the player
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // Smoothed pose (degrees / meters), so switching states blends instead of snapping.
        float _poseSwing, _poseArmSwing, _poseArmSpread, _poseArmUp, _poseBob, _poseCape, _poseTwist, _waterPivot;

        void LateUpdate()
        {
            if (Player == null || _body == null) return;
            float dt = Time.deltaTime;
            MoveState s = Player.State;
            float speed = Player.HorizontalSpeed;
            float runAmount = Mathf.Clamp01(speed / 9.5f);

            float swing = 0f, armSwing = 0f, bob = 0f, targetLean = 0f, armSpread = 0f, armUp = 0f, twist = 0f;
            float poseRate = 14f;
            switch (s)
            {
                case MoveState.Grounded:
                    _phase += dt * (4f + speed * 1.1f);
                    swing = Mathf.Sin(_phase) * 38f * runAmount;
                    armSwing = -swing * 0.8f;
                    bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * runAmount;
                    targetLean = runAmount * (Player.IsSprinting ? 14f : 8f);
                    poseRate = 20f;
                    break;
                case MoveState.Airborne:
                    swing = 18f;
                    armSwing = -25f;
                    armSpread = 25f;
                    poseRate = 10f;
                    break;
                case MoveState.Climbing:
                    _phase += dt * Player.Velocity.magnitude * 3f;
                    armUp = 140f + Mathf.Sin(_phase) * 25f;
                    swing = Mathf.Sin(_phase + 1.5f) * 25f;
                    poseRate = 12f;
                    break;
                case MoveState.Swimming:
                    _phase += dt * (2f + speed * 1.5f);
                    targetLean = 60f;
                    armUp = 90f + Mathf.Sin(_phase) * 70f;
                    swing = Mathf.Sin(_phase * 2f) * 20f;
                    poseRate = 8f;
                    break;
                case MoveState.Diving:
                    // Streamlined: body along the swim direction, arms sweeping a breaststroke,
                    // legs fluttering. Idle underwater: a slow tread.
                    float effort = Mathf.Clamp01(Player.Velocity.magnitude / 3f);
                    _phase += dt * (1.6f + effort * 3.2f);
                    targetLean = Mathf.Lerp(25f, 85f, effort) + Player.DivePitch * effort;
                    armUp = Mathf.Lerp(60f, 150f, effort) - Mathf.Max(0f, Mathf.Sin(_phase)) * 70f * effort;
                    armSpread = 20f + Mathf.Max(0f, Mathf.Sin(_phase)) * 45f * effort;
                    swing = Mathf.Sin(_phase * 2.4f) * (12f + 14f * effort);
                    twist = Mathf.Sin(_phase * 0.5f) * 4f;
                    poseRate = 6f;
                    break;
                case MoveState.Flying:
                    targetLean = 30f;
                    armSpread = 70f;
                    break;
            }

            float k = 1f - Mathf.Exp(-poseRate * dt);
            _poseSwing = Mathf.Lerp(_poseSwing, swing, k);
            _poseArmSwing = Mathf.Lerp(_poseArmSwing, armSwing, k);
            _poseArmSpread = Mathf.Lerp(_poseArmSpread, armSpread, k);
            _poseArmUp = Mathf.Lerp(_poseArmUp, armUp, k);
            _poseBob = Mathf.Lerp(_poseBob, bob, k);
            _poseTwist = Mathf.Lerp(_poseTwist, twist, k);
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-10f * dt));
            _stretch = Mathf.Lerp(_stretch, Player.IsDashing ? 1f : 0f, 1f - Mathf.Exp(-25f * dt));

            // Landing squash (from the controller's spring) and the step/snap smoothing offset.
            float squash = Player.LandingSquash;
            // In water the body pitches around its middle, not its feet.
            bool inWater = s == MoveState.Swimming || s == MoveState.Diving;
            _waterPivot = Mathf.Lerp(_waterPivot, inWater ? 0.9f : 0f, 1f - Mathf.Exp(-6f * dt));
            Quaternion bodyRot = Quaternion.Euler(_lean, _poseTwist, 0f);
            Vector3 pivot = new Vector3(0f, _waterPivot, 0f);
            _body.localPosition = new Vector3(0f, _poseBob + Player.VisualYOffset - squash * 0.25f, 0f) + pivot - bodyRot * pivot;
            _body.localRotation = bodyRot;
            _body.localScale = new Vector3(
                (1f - 0.12f * _stretch) * (1f + 0.35f * squash),
                (1f - 0.08f * _stretch) * (1f - 0.45f * squash),
                (1f + 0.35f * _stretch) * (1f + 0.35f * squash));

            _legL.localRotation = Quaternion.Euler(_poseSwing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(-_poseSwing, 0f, 0f);
            float armUpBlend = Mathf.Clamp01(_poseArmUp / 40f);
            Quaternion swingL = Quaternion.Euler(_poseArmSwing, 0f, -_poseArmSpread - 6f);
            Quaternion swingR = Quaternion.Euler(-_poseArmSwing, 0f, _poseArmSpread + 6f);
            Quaternion upL = Quaternion.Euler(-_poseArmUp, 0f, -10f - _poseArmSpread * 0.5f);
            Quaternion upR = Quaternion.Euler(-_poseArmUp + (s == MoveState.Climbing ? 50f : 0f), 0f, 10f + _poseArmSpread * 0.5f);
            _armL.localRotation = Quaternion.Slerp(swingL, upL, armUpBlend);
            _armR.localRotation = Quaternion.Slerp(swingR, upR, armUpBlend);

            float capeTarget = s == MoveState.Diving ? 15f + Mathf.Sin(_phase) * 8f
                : Mathf.Clamp(speed * 5f, 0f, 70f) + (s == MoveState.Airborne ? 30f : 0f);
            _poseCape = Mathf.Lerp(_poseCape, capeTarget, 1f - Mathf.Exp(-8f * dt));
            _cape.localRotation = Quaternion.Euler(_poseCape + Mathf.Sin(Time.time * 7f) * 3f * runAmount, 0f, 0f);
        }

        void OnDestroy()
        {
            foreach (Material m in _materials) if (m != null) Destroy(m);
        }
    }
}
