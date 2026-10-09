using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Snake3D
{
    /// <summary>
    /// Единая точка чтения клавиатуры. Бэкенд выбирается по настройке Active Input Handling,
    /// чтобы код компилировался и со старым Input Manager, и с Input System.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        public static bool LeftHeld => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        public static bool RightHeld => Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        public static bool Jump => Input.GetKeyDown(KeyCode.Space);
        public static bool Dash => Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        public static bool Pause => Input.GetKeyDown(KeyCode.Escape);
        public static bool Restart => Input.GetKeyDown(KeyCode.R);
        public static bool ToggleFullscreen => Input.GetKeyDown(KeyCode.F11);
        public static bool Confirm => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#elif ENABLE_INPUT_SYSTEM
        private static Keyboard K => Keyboard.current;
        public static bool LeftHeld => K != null && (K.aKey.isPressed || K.leftArrowKey.isPressed);
        public static bool RightHeld => K != null && (K.dKey.isPressed || K.rightArrowKey.isPressed);
        public static bool Jump => K != null && K.spaceKey.wasPressedThisFrame;
        public static bool Dash => K != null && (K.leftShiftKey.wasPressedThisFrame || K.rightShiftKey.wasPressedThisFrame);
        public static bool Pause => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool Restart => K != null && K.rKey.wasPressedThisFrame;
        public static bool ToggleFullscreen => K != null && K.f11Key.wasPressedThisFrame;
        public static bool Confirm => K != null && (K.spaceKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame);
#else
        public static bool LeftHeld => false;
        public static bool RightHeld => false;
        public static bool Jump => false;
        public static bool Dash => false;
        public static bool Pause => false;
        public static bool Restart => false;
        public static bool ToggleFullscreen => false;
        public static bool Confirm => false;
#endif
    }
}
