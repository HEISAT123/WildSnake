using UnityEngine;

namespace WildSnake
{
    /// <summary>Мелкая «жизнь» головы: змея периодически высовывает раздвоенный язык и моргает.</summary>
    public class SnakeHeadAnimator : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Змейка (анимация только пока жива)")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Корень языка: масштаб по Z выдвигает язык")]
        [SerializeField] private Transform _tongue;

        [Tooltip("Левый глаз")]
        [SerializeField] private Transform _leftEye;

        [Tooltip("Правый глаз")]
        [SerializeField] private Transform _rightEye;

        [Header("Язык")]
        [Tooltip("Пауза между движениями языка, сек (мин, макс)")]
        [SerializeField] private Vector2 _tongueInterval = new Vector2(1.2f, 3.5f);

        [Tooltip("Длительность одного движения языка, сек")]
        [SerializeField, Min(0.05f)] private float _tongueDuration = 0.4f;

        [Header("Моргание")]
        [Tooltip("Пауза между морганиями, сек (мин, макс)")]
        [SerializeField] private Vector2 _blinkInterval = new Vector2(2f, 5f);

        [Tooltip("Длительность моргания, сек")]
        [SerializeField, Min(0.02f)] private float _blinkDuration = 0.14f;

        private float _tongueWait;
        private float _tongueTime = -1f;
        private float _blinkWait;
        private float _blinkTime = -1f;
        private Vector3 _leftEyeScale;
        private Vector3 _rightEyeScale;
        private Vector3 _tongueScale;

        private void Awake()
        {
            _leftEyeScale = _leftEye.localScale;
            _rightEyeScale = _rightEye.localScale;
            _tongueScale = _tongue.localScale;
            _tongueWait = Random.Range(_tongueInterval.x, _tongueInterval.y);
            _blinkWait = Random.Range(_blinkInterval.x, _blinkInterval.y);
            SetTongue(0f);
        }

        /// <summary>Высунуть язык прямо сейчас (змея нюхает).</summary>
        public void Flick()
        {
            if (_tongueTime < 0f) _tongueTime = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!_snake.IsAlive)
            {
                SetTongue(0f);
                return;
            }

            UpdateTongue(dt);
            UpdateBlink(dt);
        }

        private void UpdateTongue(float dt)
        {
            if (_tongueTime < 0f)
            {
                _tongueWait -= dt;
                if (_tongueWait <= 0f) _tongueTime = 0f;
                return;
            }

            _tongueTime += dt;
            float k = _tongueTime / _tongueDuration;
            if (k >= 1f)
            {
                SetTongue(0f);
                _tongueTime = -1f;
                _tongueWait = Random.Range(_tongueInterval.x, _tongueInterval.y);
                return;
            }

            // Выдвинуть, дважды дрогнуть кончиком и спрятать.
            float extend = Mathf.Sin(k * Mathf.PI);
            float flick = 1f + 0.15f * Mathf.Sin(k * Mathf.PI * 8f);
            SetTongue(extend * flick);
        }

        private void UpdateBlink(float dt)
        {
            if (_blinkTime < 0f)
            {
                _blinkWait -= dt;
                if (_blinkWait <= 0f) _blinkTime = 0f;
                return;
            }

            _blinkTime += dt;
            float k = _blinkTime / _blinkDuration;
            if (k >= 1f)
            {
                _leftEye.localScale = _leftEyeScale;
                _rightEye.localScale = _rightEyeScale;
                _blinkTime = -1f;
                _blinkWait = Random.Range(_blinkInterval.x, _blinkInterval.y);
                return;
            }

            float squeeze = 1f - 0.85f * Mathf.Sin(k * Mathf.PI);
            _leftEye.localScale = new Vector3(_leftEyeScale.x, _leftEyeScale.y * squeeze, _leftEyeScale.z);
            _rightEye.localScale = new Vector3(_rightEyeScale.x, _rightEyeScale.y * squeeze, _rightEyeScale.z);
        }

        private void SetTongue(float amount)
        {
            // Нулевой масштаб даёт вырожденную матрицу, поэтому оставляем крошечную длину.
            _tongue.localScale = new Vector3(_tongueScale.x, _tongueScale.y, _tongueScale.z * Mathf.Max(0.001f, amount));
        }
    }
}
