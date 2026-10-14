using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WildSnake
{
    /// <summary>
    /// Единая точка чтения клавиатуры, мыши и геймпада. Бэкенд выбирается по настройке Active Input Handling,
    /// чтобы код компилировался и со старым Input Manager, и с Input System.
    /// Движение — оси WASD/стрелок/левого стика (x — вправо, y — вперёд), взгляд — дельта мыши.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>Оси Horizontal/Vertical Input Manager уже включают WASD, стрелки и левый стик геймпада.</summary>
        public static Vector2 Move => Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);

        /// <summary>Дельта мыши за кадр (единицы Input Manager: ~0.1 на пиксель).</summary>
        public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static float Zoom => Input.GetAxisRaw("Mouse ScrollWheel");

        public static bool SneakHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C);
        public static bool SprintHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.JoystickButton8);
        public static bool Strike => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton5);
        public static bool CoilHeld => Input.GetMouseButton(1) || Input.GetKey(KeyCode.JoystickButton4);
        public static bool ConstrictHeld => Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.JoystickButton2);
        public static bool Jump => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool Interact => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3);
        public static bool Hiss => Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool PlayDeadHeld => Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.JoystickButton9);
        public static bool MapHeld => Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.JoystickButton6);
        public static bool Upgrades => Input.GetKeyDown(KeyCode.U);
        public static bool ToggleHud => Input.GetKeyDown(KeyCode.H);
        private static bool PausePressed => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool Restart => Input.GetKeyDown(KeyCode.R);
        public static bool ToggleFullscreen => Input.GetKeyDown(KeyCode.F11);
        public static bool Confirm => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        public static bool MenuUp => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        public static bool MenuDown => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        public static bool MenuLeft => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        public static bool MenuRight => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#elif ENABLE_INPUT_SYSTEM
        private static Keyboard K => Keyboard.current;
        private static Mouse M => Mouse.current;
        private static Gamepad G => Gamepad.current;

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (K != null)
                {
                    if (K.aKey.isPressed || K.leftArrowKey.isPressed) v.x -= 1f;
                    if (K.dKey.isPressed || K.rightArrowKey.isPressed) v.x += 1f;
                    if (K.sKey.isPressed || K.downArrowKey.isPressed) v.y -= 1f;
                    if (K.wKey.isPressed || K.upArrowKey.isPressed) v.y += 1f;
                }
                if (G != null) v += G.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Дельта мыши в пикселях × 0.1 — тот же масштаб, что у осей Input Manager; правый стик — в тех же единицах.</summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 v = M != null ? M.delta.ReadValue() * 0.1f : Vector2.zero;
                if (G != null) v += G.rightStick.ReadValue() * (Time.unscaledDeltaTime * 30f);
                return v;
            }
        }

        public static float Zoom => M != null ? M.scroll.ReadValue().y / 1200f : 0f;
        public static bool SneakHeld => K != null && (K.leftCtrlKey.isPressed || K.rightCtrlKey.isPressed || K.cKey.isPressed);
        public static bool SprintHeld => (K != null && (K.leftShiftKey.isPressed || K.rightShiftKey.isPressed)) || (G != null && G.leftStickButton.isPressed);
        public static bool Strike => (M != null && M.leftButton.wasPressedThisFrame) || (G != null && G.rightShoulder.wasPressedThisFrame);
        public static bool CoilHeld => (M != null && M.rightButton.isPressed) || (G != null && G.leftShoulder.isPressed);
        public static bool ConstrictHeld => (K != null && K.fKey.isPressed) || (G != null && G.buttonWest.isPressed);
        public static bool Jump => (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool Interact => (K != null && K.eKey.wasPressedThisFrame) || (G != null && G.buttonNorth.wasPressedThisFrame);
        public static bool Hiss => (K != null && K.qKey.wasPressedThisFrame) || (G != null && G.buttonEast.wasPressedThisFrame);
        public static bool PlayDeadHeld => (K != null && K.xKey.isPressed) || (G != null && G.rightStickButton.isPressed);
        public static bool MapHeld => (K != null && K.tabKey.isPressed) || (G != null && G.selectButton.isPressed);
        public static bool Upgrades => K != null && K.uKey.wasPressedThisFrame;
        public static bool ToggleHud => K != null && K.hKey.wasPressedThisFrame;
        private static bool PausePressed => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool Restart => K != null && K.rKey.wasPressedThisFrame;
        public static bool ToggleFullscreen => K != null && K.f11Key.wasPressedThisFrame;
        public static bool Confirm => K != null && (K.spaceKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame);
        public static bool MenuUp => K != null && (K.upArrowKey.wasPressedThisFrame || K.wKey.wasPressedThisFrame);
        public static bool MenuDown => K != null && (K.downArrowKey.wasPressedThisFrame || K.sKey.wasPressedThisFrame);
        public static bool MenuLeft => K != null && (K.leftArrowKey.wasPressedThisFrame || K.aKey.wasPressedThisFrame);
        public static bool MenuRight => K != null && (K.rightArrowKey.wasPressedThisFrame || K.dKey.wasPressedThisFrame);
#else
        public static Vector2 Move => Vector2.zero;
        public static Vector2 Look => Vector2.zero;
        public static float Zoom => 0f;
        public static bool SneakHeld => false;
        public static bool SprintHeld => false;
        public static bool Strike => false;
        public static bool CoilHeld => false;
        public static bool ConstrictHeld => false;
        public static bool Jump => false;
        public static bool Interact => false;
        public static bool Hiss => false;
        public static bool PlayDeadHeld => false;
        public static bool MapHeld => false;
        public static bool Upgrades => false;
        public static bool ToggleHud => false;
        private static bool PausePressed => false;
        public static bool Restart => false;
        public static bool ToggleFullscreen => false;
        public static bool Confirm => false;
        public static bool MenuUp => false;
        public static bool MenuDown => false;
        public static bool MenuLeft => false;
        public static bool MenuRight => false;
#endif

        private static int _pauseConsumedFrame = -1;

        /// <summary>Esc / Start в этом кадре, если его ещё не забрал открытый раздел меню.</summary>
        public static bool Pause => PausePressed && _pauseConsumedFrame != Time.frameCount;

        /// <summary>
        /// Раздел меню (настройки, рекорды, справочник) закрывается по Esc и забирает нажатие себе,
        /// чтобы та же клавиша не сняла игру с паузы. Разделы обновляются раньше GameManager (DefaultExecutionOrder).
        /// </summary>
        public static void ConsumePause()
        {
            _pauseConsumedFrame = Time.frameCount;
        }
    }
}
