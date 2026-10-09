using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Таймеры активных бонусов. Тикает по реальному времени (замедление времени не должно
    /// замедлять само себя), но только пока идёт игра (IsTicking).
    /// Щит — без таймера, его хранит SnakeController.HasShield.
    /// </summary>
    public class PowerUpService : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса (длительности)")]
        [SerializeField] private GameConfig _config;

        private const int TYPE_COUNT = 5;

        public event Action<PowerUpType> Activated;

        public bool IsTicking { get; set; }

        private readonly float[] _remaining = new float[TYPE_COUNT];
        private readonly float[] _duration = new float[TYPE_COUNT];

        public void Activate(PowerUpType type)
        {
            int i = (int)type;
            float duration = _config.GetPowerUpDuration(type);
            _duration[i] = duration;
            _remaining[i] = duration;
            Activated?.Invoke(type);
        }

        public bool IsActive(PowerUpType type)
        {
            return _remaining[(int)type] > 0f;
        }

        /// <summary>Сколько осталось от бонуса: 1 — только что взят, 0 — закончился.</summary>
        public float Remaining01(PowerUpType type)
        {
            int i = (int)type;
            return _duration[i] > 0f ? Mathf.Clamp01(_remaining[i] / _duration[i]) : 0f;
        }

        public void ResetAll()
        {
            Array.Clear(_remaining, 0, TYPE_COUNT);
        }

        private void Update()
        {
            if (!IsTicking) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < TYPE_COUNT; i++)
                if (_remaining[i] > 0f) _remaining[i] -= dt;
        }
    }
}
