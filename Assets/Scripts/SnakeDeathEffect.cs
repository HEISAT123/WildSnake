using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// При смерти тело рассыпается на шарики: меш и голова прячутся, а вдоль позвоночника
    /// выпускаются частицы-сферы цвета змеи, которые падают и подпрыгивают на полу.
    /// </summary>
    public class SnakeDeathEffect : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Меш тела")]
        [SerializeField] private SnakeBodyMesh _body;

        [Tooltip("Визуал головы (прячется при смерти)")]
        [SerializeField] private GameObject _headVisual;

        [Tooltip("Система частиц со сферами (режим Mesh)")]
        [SerializeField] private ParticleSystem _pieces;

        [Header("Вид")]
        [Tooltip("Цвет кусочков")]
        [SerializeField] private Color _color = new Color(0.35f, 0.5f, 0.2f);

        [Tooltip("Шаг кусочков вдоль тела, клеток")]
        [SerializeField, Min(0.1f)] private float _pieceSpacing = 0.35f;

        [Tooltip("Скорость разлёта кусочков")]
        [SerializeField, Min(0f)] private float _scatterSpeed = 2.5f;

        private void OnEnable()
        {
            _snake.Died += OnDied;
            _snake.Respawned += OnRespawned;
        }

        private void OnDisable()
        {
            _snake.Died -= OnDied;
            _snake.Respawned -= OnRespawned;
        }

        private void OnDied(Vector3 headPosition)
        {
            _body.Visible = false;
            _headVisual.SetActive(false);
            if (_pieces == null) return;

            float length = _snake.BodyLengthWorld;
            float spacing = _pieceSpacing * _snake.CellSize;
            float radius = _snake.BodyRadiusWorld;
            var emit = new ParticleSystem.EmitParams
            {
                startColor = _color,
                applyShapeToPosition = false
            };

            // Голова — кусочек побольше.
            emit.position = headPosition;
            emit.startSize = radius * 2.4f;
            emit.velocity = Vector3.up * _scatterSpeed;
            _pieces.Emit(emit, 1);

            for (float d = spacing; d < length; d += spacing)
            {
                _snake.SampleSpine(d, out Vector3 pos, out _);
                float t = d / length;
                emit.position = pos;
                emit.startSize = radius * 2f * Mathf.Lerp(1f, 0.35f, t);
                emit.velocity = Random.insideUnitSphere * _scatterSpeed + Vector3.up * _scatterSpeed;
                _pieces.Emit(emit, 1);
            }
        }

        private void OnRespawned()
        {
            _body.Visible = true;
            _headVisual.SetActive(true);
        }
    }
}
