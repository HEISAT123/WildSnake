using UnityEngine;

namespace Snake3D
{
    /// <summary>Шлейф за головой, который рисуется только во время рывка.</summary>
    [RequireComponent(typeof(TrailRenderer))]
    public class DashTrail : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка, чей рывок показывает шлейф")]
        [SerializeField] private SnakeController _snake;

        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
        }

        private void OnEnable()
        {
            _snake.Respawned += OnRespawned;
        }

        private void OnDisable()
        {
            _snake.Respawned -= OnRespawned;
        }

        private void LateUpdate()
        {
            bool emit = _snake.IsDashing && _snake.IsAlive;
            if (_trail.emitting != emit) _trail.emitting = emit;
        }

        // Иначе после рестарта шлейф протянется от места смерти к центру поля.
        private void OnRespawned()
        {
            _trail.Clear();
        }
    }
}
