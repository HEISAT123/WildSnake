using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Простой ИИ змеи-соперника: рулит к еде, «щупальцами» проверяет путь впереди,
    /// объезжает стены, высокие препятствия и тела, перепрыгивает низкие стенки.
    /// Выполняется раньше SnakeController, чтобы руль применился в том же кадре.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SnakeAI : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка, которой управляет ИИ")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Змейка игрока (её тело ИИ объезжает)")]
        [SerializeField] private SnakeController _player;

        [Tooltip("Еда — цель ИИ")]
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Header("Поведение")]
        [Tooltip("Дальность дальних щупалец, клеток")]
        [SerializeField, Min(0.5f)] private float _lookAhead = 2.6f;

        [Tooltip("Дальность ближнего щупальца, клеток")]
        [SerializeField, Min(0.3f)] private float _nearLook = 1.2f;

        [Tooltip("Угол боковых щупалец, град")]
        [SerializeField, Range(10f, 80f)] private float _feelerAngle = 35f;

        [Tooltip("Угол до цели, при котором руль в упоре, град")]
        [SerializeField, Range(5f, 90f)] private float _fullSteerAngle = 30f;

        [Tooltip("Отступ от стен арены, клеток")]
        [SerializeField, Min(0f)] private float _wallAvoidMargin = 1f;

        private enum Blocked
        {
            Free,
            Jumpable,
            Solid
        }

        private void Update()
        {
            if (!_snake.IsRunning || !_snake.IsAlive) return;

            Vector3 head = _snake.HeadPosition;
            Vector3 forward = _snake.Forward;
            float cs = _grid.CellSize;

            Vector3 target = _food.HasFood ? _food.Position : _grid.Center;
            Vector3 toTarget = target - head;
            toTarget.y = 0f;
            float angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
            float steer = Mathf.Clamp(angle / _fullSteerAngle, -1f, 1f);

            Vector3 leftDir = Quaternion.Euler(0f, -_feelerAngle, 0f) * forward;
            Vector3 rightDir = Quaternion.Euler(0f, _feelerAngle, 0f) * forward;

            Blocked near = Probe(head + forward * (_nearLook * cs));
            Blocked center = Probe(head + forward * (_lookAhead * cs));
            Blocked left = Probe(head + leftDir * (_lookAhead * cs));
            Blocked right = Probe(head + rightDir * (_lookAhead * cs));

            if (near == Blocked.Jumpable)
            {
                _snake.AiJump = true;
            }
            else if (near == Blocked.Solid || center == Blocked.Solid)
            {
                bool leftFree = left != Blocked.Solid;
                bool rightFree = right != Blocked.Solid;
                if (leftFree && !rightFree) steer = -1f;
                else if (rightFree && !leftFree) steer = 1f;
                else steer = steer >= 0f ? 1f : -1f;
            }
            else
            {
                if (left == Blocked.Solid) steer = Mathf.Max(steer, 0.6f);
                if (right == Blocked.Solid) steer = Mathf.Min(steer, -0.6f);
            }

            _snake.AiSteer = steer;
        }

        private Blocked Probe(Vector3 point)
        {
            float cs = _grid.CellSize;
            Vector2 half = _grid.ArenaHalfExtents;
            Vector3 local = point - _grid.Center;
            float margin = _wallAvoidMargin * cs;
            if (Mathf.Abs(local.x) > half.x - margin || Mathf.Abs(local.z) > half.y - margin) return Blocked.Solid;

            Vector2Int cell = _grid.WorldToCell(point);
            if (_grid.IsInside(cell))
            {
                float h = _grid.GetObstacleHeight(cell);
                if (h > 0f) return h < _snake.JumpHeightWorld * 0.6f ? Blocked.Jumpable : Blocked.Solid;
            }

            float radius = _snake.HeadRadiusWorld;
            if (_player != null && _player.IsAlive && _player.BodyHits(point, radius, 0)) return Blocked.Solid;
            // Своё тело проверяем, пропустив участок у головы, иначе ИИ «боится» сам себя.
            if (_snake.BodyHits(point, radius, 6)) return Blocked.Solid;
            return Blocked.Free;
        }
    }
}
