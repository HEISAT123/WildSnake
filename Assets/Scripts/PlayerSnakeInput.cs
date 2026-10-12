using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Ввод игрока → намерение змеи: WASD задаёт направление относительно камеры, Ctrl/Shift — темп,
    /// ПКМ — замах с прицелом по взгляду камеры, ЛКМ — бросок, Space — прыжок,
    /// Q — шипение, X (держать) — притвориться мёртвой.
    /// Включается только во время партии (GameManager); в меню змею ведёт SnakeAI через тот же интерфейс.
    /// Выполняется раньше SnakeController, чтобы намерение применилось в том же кадре.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerSnakeInput : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Камера: от её поворота считается направление WASD и прицел")]
        [SerializeField] private CameraRig _camera;

        [Header("Ввод")]
        [Tooltip("Мёртвая зона стика/клавиш, ниже которой змея стоит")]
        [SerializeField, Range(0f, 0.5f)] private float _deadZone = 0.15f;

        /// <summary>Внешний запрет на движение (змея в норе, обвивает добычу): ввод читается, но змея стоит.</summary>
        public bool MovementLocked { get; set; }

        private void OnDisable()
        {
            if (_snake == null) return;
            _snake.SetMoveIntent(Vector3.zero, SnakePace.Walk);
            _snake.SetCoil(false, Vector3.zero);
            _snake.SetPlayDead(false);
        }

        private void Update()
        {
            if (!_snake.IsRunning || !_snake.IsAlive) return;

            Vector3 forward = _camera.PlanarForward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector2 move = GameInput.Move;
            if (move.sqrMagnitude < _deadZone * _deadZone || MovementLocked) move = Vector2.zero;

            SnakePace pace = GameInput.SneakHeld ? SnakePace.Sneak : GameInput.SprintHeld ? SnakePace.Sprint : SnakePace.Walk;
            _snake.SetMoveIntent(right * move.x + forward * move.y, pace);

            _snake.SetCoil(GameInput.CoilHeld && !MovementLocked, forward);
            _snake.SetPlayDead(GameInput.PlayDeadHeld && !MovementLocked);
            // Шипеть можно и душа добычу — отгонять соперницу, которая пришла её отбить.
            if (GameInput.Hiss) _snake.TryHiss();
            if (MovementLocked) return;
            if (GameInput.Strike) _snake.TryStrike(forward);
            if (GameInput.Jump) _snake.RequestJump();
        }
    }
}
