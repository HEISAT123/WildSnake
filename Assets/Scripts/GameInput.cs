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
        public static bool Up => Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        public static bool Down => Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        public static bool Left => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        public static bool Right => Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
        public static bool Pause => Input.GetKeyDown(KeyCode.Escape);
        public static bool Restart => Input.GetKeyDown(KeyCode.R);
        public static bool ToggleCamera => Input.GetKeyDown(KeyCode.C);
        public static bool Confirm => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#elif ENABLE_INPUT_SYSTEM
        private static Keyboard K => Keyboard.current;
        public static bool Up => K != null && (K.wKey.wasPressedThisFrame || K.upArrowKey.wasPressedThisFrame);
        public static bool Down => K != null && (K.sKey.wasPressedThisFrame || K.downArrowKey.wasPressedThisFrame);
        public static bool Left => K != null && (K.aKey.wasPressedThisFrame || K.leftArrowKey.wasPressedThisFrame);
        public static bool Right => K != null && (K.dKey.wasPressedThisFrame || K.rightArrowKey.wasPressedThisFrame);
        public static bool Pause => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool Restart => K != null && K.rKey.wasPressedThisFrame;
        public static bool ToggleCamera => K != null && K.cKey.wasPressedThisFrame;
        public static bool Confirm => K != null && (K.spaceKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame);
#else
        public static bool Up => false;
        public static bool Down => false;
        public static bool Left => false;
        public static bool Right => false;
        public static bool Pause => false;
        public static bool Restart => false;
        public static bool ToggleCamera => false;
        public static bool Confirm => false;
#endif
    }
}
