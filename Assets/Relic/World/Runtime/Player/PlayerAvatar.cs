using System.Collections.Generic;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Placeholder hero built from primitives with the toon + outline material, and a small
    /// procedural animation (run cycle, lean, dash stretch, climb, glide, swim). The silhouette
    /// is the hitbox in combat, so it is kept simple and readable.
    /// </summary>
    public sealed class PlayerAvatar : MonoBehaviour
    {
        public PlayerController Player;

        Transform _body, _torso, _head, _legL, _legR, _armL, _armR, _cape, _glider;
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
            Material glider = Tint(toon, new Color(0.96f, 0.88f, 0.62f));

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

            _glider = Pivot(root, new Vector3(0f, 1.9f, -0.05f));
            Part(PrimitiveType.Cube, _glider, new Vector3(0f, 0f, 0f), new Vector3(2.6f, 0.04f, 0.9f), glider);
            Part(PrimitiveType.Cube, _glider, new Vector3(0f, -0.2f, 0f), new Vector3(0.04f, 0.4f, 0.04f), trousers);
            _glider.gameObject.SetActive(false);
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

        void LateUpdate()
        {
            if (Player == null || _body == null) return;
            float dt = Time.deltaTime;
            MoveState s = Player.State;
            float speed = Player.HorizontalSpeed;
            float runAmount = Mathf.Clamp01(speed / 9.5f);

            _glider.gameObject.SetActive(s == MoveState.Gliding);

            float swing = 0f, armSwing = 0f, bob = 0f, targetLean = 0f, armSpread = 0f, armUp = 0f;
            switch (s)
            {
                case MoveState.Grounded:
                    _phase += dt * (4f + speed * 1.1f);
                    swing = Mathf.Sin(_phase) * 38f * runAmount;
                    armSwing = -swing * 0.8f;
                    bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * runAmount;
                    targetLean = runAmount * (Player.IsSprinting ? 14f : 8f);
                    break;
                case MoveState.Airborne:
                    swing = 18f;
                    armSwing = -25f;
                    armSpread = 25f;
                    break;
                case MoveState.Gliding:
                    targetLean = 55f;
                    armUp = 150f;
                    swing = 8f;
                    break;
                case MoveState.Climbing:
                    _phase += dt * Player.Velocity.magnitude * 3f;
                    armUp = 140f + Mathf.Sin(_phase) * 25f;
                    swing = Mathf.Sin(_phase + 1.5f) * 25f;
                    break;
                case MoveState.Swimming:
                    _phase += dt * (2f + speed * 1.5f);
                    targetLean = 60f;
                    armUp = 90f + Mathf.Sin(_phase) * 70f;
                    swing = Mathf.Sin(_phase * 2f) * 20f;
                    break;
                case MoveState.Flying:
                    targetLean = 30f;
                    armSpread = 70f;
                    break;
            }

            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-10f * dt));
            _stretch = Mathf.Lerp(_stretch, Player.IsDashing ? 1f : 0f, 1f - Mathf.Exp(-25f * dt));

            _body.localPosition = new Vector3(0f, bob, 0f);
            _body.localRotation = Quaternion.Euler(_lean, 0f, 0f);
            _body.localScale = new Vector3(1f - 0.12f * _stretch, 1f - 0.08f * _stretch, 1f + 0.35f * _stretch);

            _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (armUp > 0f)
            {
                _armL.localRotation = Quaternion.Euler(-armUp, 0f, -10f);
                _armR.localRotation = Quaternion.Euler(-armUp + (s == MoveState.Climbing ? 50f : 0f), 0f, 10f);
            }
            else
            {
                _armL.localRotation = Quaternion.Euler(armSwing, 0f, -armSpread - 6f);
                _armR.localRotation = Quaternion.Euler(-armSwing, 0f, armSpread + 6f);
            }
            float capeLift = Mathf.Clamp(speed * 5f, 0f, 70f) + (s == MoveState.Airborne ? 30f : 0f);
            _cape.localRotation = Quaternion.Euler(capeLift + Mathf.Sin(Time.time * 7f) * 3f * runAmount, 0f, 0f);
        }

        void OnDestroy()
        {
            foreach (Material m in _materials) if (m != null) Destroy(m);
        }
    }
}
