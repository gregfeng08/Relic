using UnityEngine;
using UnityEngine.InputSystem;

namespace Relic.World.Runtime
{
    /// <summary>One frame of player input (keyboard and mouse; controller later).</summary>
    public struct PlayerInputState
    {
        public Vector2 Move;
        public Vector2 Look;          // mouse delta in pixels
        public float Zoom;           // scroll notches, + = zoom in
        public bool JumpPressed;
        public bool JumpHeld;
        public bool SprintPressed;   // Shift went down this frame (dash)
        public bool SprintHeld;      // Shift held (sprint)
        public bool DescendHeld;     // Ctrl (fly mode down)
    }

    /// <summary>Reads devices directly through the new Input System (the project uses it exclusively).</summary>
    public static class PlayerInput
    {
        public static PlayerInputState Read()
        {
            var s = new PlayerInputState();
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if (k != null)
            {
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                s.Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
                s.JumpPressed = k.spaceKey.wasPressedThisFrame;
                s.JumpHeld = k.spaceKey.isPressed;
                s.SprintPressed = k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame;
                s.SprintHeld = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                s.DescendHeld = k.leftCtrlKey.isPressed || k.cKey.isPressed;
            }
            if (m != null)
            {
                if (Cursor.lockState == CursorLockMode.Locked) s.Look = m.delta.ReadValue();
                s.Zoom = m.scroll.ReadValue().y / 120f;
                if (Mathf.Abs(s.Zoom) > 0f && Mathf.Abs(s.Zoom) < 0.05f) s.Zoom = Mathf.Sign(s.Zoom); // some devices report 1 per notch
            }
            return s;
        }

        public static bool KeyDown(Key key)
        {
            Keyboard k = Keyboard.current;
            return k != null && k[key].wasPressedThisFrame;
        }

        /// <summary>Click to capture the mouse, Escape to release it.</summary>
        public static void UpdateCursorLock()
        {
            Mouse m = Mouse.current;
            if (KeyDown(Key.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
