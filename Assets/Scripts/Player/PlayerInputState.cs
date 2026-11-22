using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Device-independent snapshot of what the player wants to do this frame.
    /// DesktopInput (keyboard + mouse) and VRRig (XR controllers) both fill the same structure,
    /// so the gameplay code never needs to know which device is used.
    /// </summary>
    public class PlayerInputState
    {
        public Vector2 move;
        public Vector2 look;
        public float snapTurn;          // VR only: degrees to rotate this frame

        public bool fireHeld, firePressed, aimHeld;
        public bool zoomHeld;           // VR left trigger held (archery zoom/steady); desktop/gamepad use aimHeld
        public bool reloadPressed, jumpPressed, dashPressed;
        public bool sprintHeld, crouchHeld;
        public bool grenadePressed, meleePressed, flashlightPressed, medkitPressed;
        public int weaponSelect = -1;   // direct slot select, -1 = none
        public int weaponCycle;         // +1 / -1 = next / previous

        public void Clear()
        {
            move = Vector2.zero; look = Vector2.zero; snapTurn = 0f;
            fireHeld = firePressed = aimHeld = zoomHeld = false;
            reloadPressed = jumpPressed = dashPressed = false;
            sprintHeld = crouchHeld = false;
            grenadePressed = meleePressed = flashlightPressed = medkitPressed = false;
            weaponSelect = -1; weaponCycle = 0;
        }
    }
}
