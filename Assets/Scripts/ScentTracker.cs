using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Нюх: змея высовывает язык — на несколько секунд от головы к ближайшей добыче тянется
    /// светящийся след из «облачков» запаха, а вся добыча в радиусе подсвечивается метками.
    /// Облачка — заранее созданный пул объектов, двигаются только позиции и масштаб.
    /// </summary>
    public class ScentTracker : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры нюха (радиус, длительность, перезарядка)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Живность")]
        [SerializeField] private WildlifeManager _wildlife;

        [Tooltip("Облачка следа")]
        [SerializeField] private Transform[] _wisps = new Transform[0];

        [Header("След")]
        [Tooltip("Размер облачка, мировых единиц")]
        [SerializeField, Min(0.01f)] private float _wispSize = 0.17f;

        [Tooltip("Высота следа над землёй")]
        [SerializeField] private float _wispHeight = 0.35f;

        [Tooltip("Скорость «течения» следа к добыче, долей пути в секунду")]
        [SerializeField] private float _flowSpeed = 0.35f;

        [Tooltip("Как часто пересчитывать ближайшую добычу, сек")]
        [SerializeField, Min(0.05f)] private float _retargetInterval = 0.25f;

        /// <summary>Нюх сработал; true — добыча рядом есть.</summary>
        public event Action<bool> Sniffed;

        public bool IsActive => _activeTimer > 0f;

        /// <summary>Готовность нюха: 1 — можно нюхать.</summary>
        public float Charge01 => _cooldownTotal > 0f ? 1f - Mathf.Clamp01(_cooldownTimer / _cooldownTotal) : 1f;

        /// <summary>Есть ли сейчас цель следа и где она.</summary>
        public bool HasTarget => IsActive && _target != null && _target.IsActive;
        public Vector3 TargetPosition => _target != null ? _target.Position : _snake.HeadPosition;

        /// <summary>Множители от улучшений.</summary>
        public float RadiusMultiplier { get; set; } = 1f;
        public float CooldownMultiplier { get; set; } = 1f;

        private PreyAnimal _target;
        private float _activeTimer;
        private float _cooldownTimer;
        private float _cooldownTotal = 1f;
        private float _retargetTimer;
        private float _flow;

        private float Radius => _config.SmellRadius * RadiusMultiplier * _snake.CellSize;

        private void Awake()
        {
            HideWisps();
        }

        /// <summary>Погасить след и сбросить перезарядку (новая партия, меню).</summary>
        public void ResetScent()
        {
            _activeTimer = 0f;
            _cooldownTimer = 0f;
            _target = null;
            _wildlife.SetScentMarks(false, Vector3.zero, 0f);
            HideWisps();
        }

        /// <summary>Понюхать. Возвращает false, если нюх ещё не восстановился.</summary>
        public bool TrySniff()
        {
            if (_cooldownTimer > 0f || !_snake.IsAlive) return false;
            _cooldownTotal = _config.SmellCooldown * CooldownMultiplier;
            _cooldownTimer = _cooldownTotal;
            _activeTimer = _config.SmellDuration;
            _retargetTimer = 0f;
            _flow = 0f;
            bool found = _wildlife.TryGetNearest(_snake.HeadPosition, Radius, out _target);
            Sniffed?.Invoke(found);
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            if (_activeTimer <= 0f) return;

            _activeTimer -= dt;
            if (_activeTimer <= 0f || !_snake.IsAlive)
            {
                _activeTimer = 0f;
                _target = null;
                _wildlife.SetScentMarks(false, Vector3.zero, 0f);
                HideWisps();
                return;
            }

            Vector3 head = _snake.HeadPosition;
            _retargetTimer -= dt;
            if (_retargetTimer <= 0f || _target == null || !_target.IsActive)
            {
                _retargetTimer = _retargetInterval;
                _wildlife.TryGetNearest(head, Radius, out _target);
                _wildlife.SetScentMarks(true, head, Radius);
            }

            if (_target == null || !_target.IsActive)
            {
                HideWisps();
                return;
            }
            UpdateWisps(head, _target.Position, dt);
        }

        /// <summary>Облачки текут от головы к добыче, слегка виляя; у концов следа и в конце нюха гаснут.</summary>
        private void UpdateWisps(Vector3 from, Vector3 to, float dt)
        {
            _flow = Mathf.Repeat(_flow + _flowSpeed * dt, 1f);
            float ground = from.y - _snake.HeightAboveGround;
            Vector3 delta = to - from;
            delta.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, delta.normalized);
            float fadeOut = Mathf.Clamp01(_activeTimer / 0.6f);
            int n = _wisps.Length;
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Repeat((i + _flow * n) / n, 1f);
                Vector3 p = from + delta * t;
                p += side * (Mathf.Sin(t * 9f + Time.time * 3f) * 0.35f);
                p.y = ground + _wispHeight + Mathf.Sin(t * 13f + Time.time * 4f) * 0.08f;
                float ends = Mathf.Sin(t * Mathf.PI);
                _wisps[i].position = p;
                _wisps[i].localScale = Vector3.one * (_wispSize * (0.4f + 0.6f * ends) * fadeOut);
                if (!_wisps[i].gameObject.activeSelf) _wisps[i].gameObject.SetActive(true);
            }
        }

        private void HideWisps()
        {
            for (int i = 0; i < _wisps.Length; i++)
                if (_wisps[i] != null && _wisps[i].gameObject.activeSelf) _wisps[i].gameObject.SetActive(false);
        }
    }
}
