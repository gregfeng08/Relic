using Relic.Core.Stats;
using Relic.Items;
using Relic.World.Runtime;
using UnityEngine;
using Key = UnityEngine.InputSystem.Key;

namespace Relic.Character
{
    /// <summary>
    /// Connects the hero to the World PlayerController: feeds movement state to the animator each
    /// frame, plus the debug keys. F7: roll and equip a random kit for the current tier. F9: cycle
    /// the armor weight. A HUD line lists the equipped items.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class HeroPlayerDriver : MonoBehaviour
    {
        public PlayerController Player;
        public HeroAvatar Hero;
        public bool ShowHud = true;

        KitRoller _roller;
        ulong _rollerSeed;
        int _roll;
        int _tier;
        GUIStyle _style;

        /// <summary>World avatar factory: builds the hero on the player object (registered by the Game layer).</summary>
        public static Component Create(GameObject player, PlayerController controller, Material toon)
        {
            var hero = player.AddComponent<HeroAvatar>();
            hero.ToonMaterial = toon;
            var driver = player.AddComponent<HeroPlayerDriver>();
            driver.Player = controller;
            driver.Hero = hero;
            driver.EquipStarter();
            return hero;
        }

        /// <summary>The reference-sheet look: light coat, the spiral staff on the back.</summary>
        public void EquipStarter()
        {
            Hero.Kit = Roller().Roll(0, 0, BaseType.Staff, BaseType.LightArmor);
            Hero.ArmorOverride = null;
            Hero.Rebuild();
        }

        KitRoller Roller()
        {
            var sandbox = FindAnyObjectByType<TerrainSandbox>();
            ulong seed = sandbox != null ? sandbox.CurrentSeed : 1337UL;
            if (_roller == null || seed != _rollerSeed) { _roller = new KitRoller(seed); _rollerSeed = seed; }
            return _roller;
        }

        int CurrentTier()
        {
            var origin = FindAnyObjectByType<FloatingOrigin>();
            Vector3 p = transform.position;
            double x = p.x, z = p.z;
            if (origin != null) origin.ToWorld(p, out x, out z);
            return Difficulty.TierAt(x, z);
        }

        void Update()
        {
            if (Hero == null) return;
            if (PlayerInput.KeyDown(Key.F7)) RollRandomKit();
            if (PlayerInput.KeyDown(Key.F9)) CycleArmor();
        }

        /// <summary>F7: a random kit for the tier at the player's world position.</summary>
        public void RollRandomKit()
        {
            _tier = CurrentTier();
            Hero.Kit = Roller().Roll(_tier, ++_roll);
            Hero.ArmorOverride = null;
            Hero.Rebuild();
            Debug.Log("[Relic.Character] Kit " + _roll + " (tier " + _tier + "): " + string.Join(" | ", Hero.Look.ItemNames) + " (" + Hero.TriangleCount + " tris)");
        }

        /// <summary>F9: Light → Medium → Heavy → Light (overrides the equipped armor's weight).</summary>
        public void CycleArmor()
        {
            ArmorWeight current = Hero.Look != null ? Hero.Look.Armor.Weight : ArmorWeight.Light;
            Hero.ArmorOverride = (ArmorWeight)(((int)current + 1) % 3);
            Hero.Rebuild();
        }

        void LateUpdate()
        {
            if (Hero == null || Player == null) return;
            var a = new HeroAnimInput
            {
                Speed = Player.HorizontalSpeed,
                Sprint = Player.IsSprinting,
                Dash = Player.IsDashing,
                VerticalSpeed = Player.Velocity.y,
                WeaponDrawn = Hero.WeaponDrawn,
            };
            switch (Player.State)
            {
                case MoveState.Grounded: a.Motion = Player.HorizontalSpeed > 0.25f || Player.IsDashing ? HeroMotion.Run : HeroMotion.Idle; break;
                case MoveState.Airborne: a.Motion = HeroMotion.Air; break;
                case MoveState.Climbing: a.Motion = HeroMotion.Climb; a.ClimbSpeed = Player.Velocity.magnitude; break;
                case MoveState.Swimming: a.Motion = HeroMotion.Swim; break;
                case MoveState.Flying: a.Motion = HeroMotion.Fly; break;
                default:
                    // States added later (diving, underwater...): never throw, swim or dive.
                    string name = Player.State.ToString();
                    a.Motion = name.IndexOf("Div", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Under", System.StringComparison.OrdinalIgnoreCase) >= 0
                        ? HeroMotion.Dive : HeroMotion.Swim;
                    break;
            }
            Hero.Input = a;
            Hero.Animate(Time.deltaTime, Player.Velocity);
        }

        void OnGUI()
        {
            if (!ShowHud || Hero == null || Hero.Look == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };
                _style.normal.textColor = Color.white;
            }
            string items = Hero.Look.ItemNames.Count > 0 ? string.Join("  ·  ", Hero.Look.ItemNames) : "(no items)";
            string line = "<b>Kit</b> " + items + "   <color=#bbbbbb>[" + Hero.Look.Armor.Weight + " · F7 new kit · F9 armor]</color>";
            var r = new Rect(12, Screen.height - 30, Screen.width - 24, 24);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), StripColor(line), _style);
            GUI.color = Color.white;
            GUI.Label(r, line, _style);
        }

        static string StripColor(string s) => s.Replace("<color=#bbbbbb>", "").Replace("</color>", "");
    }
}
