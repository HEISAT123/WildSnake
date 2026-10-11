using System;
using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Осиные гнёзда на старых пнях. Змея рядом с гнездом его «злит»: шагом — понемногу, быстрым ползанием — сильно,
    /// броском — сразу; крадущаяся змея ос не тревожит (ночью они вдвое сонливее). Разозлённое гнездо выпускает рой:
    /// он гонится за змеёй чуть быстрее её бега и жалит, пока змея не нырнёт в воду, нору или полое бревно, не уйдёт
    /// далеко от гнезда или пока рою не надоест. Рой жалит и лису, если та рядом, — её можно навести на гнездо.
    /// Логика в данных, Transform только показывает результат (осы — пул мелких моделек). События забирает GameManager.
    /// </summary>
    public class WaspNests : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля: где гнёзда")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Tooltip("Лиса: рой жалит и её")]
        [SerializeField] private FoxHunter _fox;

        [Tooltip("Поле: где висят гнёзда на пнях")]
        [SerializeField] private BoardView _board;

        [Header("Осы")]
        [Tooltip("Осы роя (пул): летают вокруг центра роя")]
        [SerializeField] private Transform[] _swarm = new Transform[0];

        [Tooltip("Дежурные осы: по две у каждого гнезда, по порядку гнёзд GridManager")]
        [SerializeField] private Transform[] _guards = new Transform[0];

        [Header("Поведение")]
        [Tooltip("Высота гнезда над землёй, если поле не знает точек гнёзд, клеток")]
        [SerializeField, Min(0f)] private float _nestHeight = 1.25f;

        [Tooltip("С какого расстояния рой жалит, клеток")]
        [SerializeField, Min(0.2f)] private float _stingRange = 1f;

        [Tooltip("С какого расстояния рой жалит лису, клеток")]
        [SerializeField, Min(0.2f)] private float _foxStingRange = 2.2f;

        [Tooltip("Насколько рой «размазан» вокруг центра, клеток")]
        [SerializeField, Min(0.05f)] private float _swarmSpread = 0.55f;

        /// <summary>Рой вылетел из гнезда (где гнездо).</summary>
        public event Action<Vector3> SwarmStarted;

        /// <summary>Оса ужалила змею (откуда — центр роя).</summary>
        public event Action<Vector3> Stung;

        /// <summary>Рой ужалил лису — та убежала.</summary>
        public event Action FoxStung;

        /// <summary>Гнёзда живут (идёт партия).</summary>
        public bool IsRunning { get; set; }

        /// <summary>Ночью осы сонные и злятся вдвое медленнее.</summary>
        public bool IsNight { get; set; }

        /// <summary>Рой в воздухе.</summary>
        public bool IsSwarming => _active >= 0;

        /// <summary>Рой гонится за змеёй (а не возвращается к гнезду).</summary>
        public bool IsChasing => _active >= 0 && !_returning;

        public Vector3 SwarmPosition => _swarmPos;

        private int _count;
        private Vector3[] _nests;
        private float[] _anger;
        private float[] _calm;
        private int _active = -1;
        private bool _returning;
        private Vector3 _swarmPos;
        private float _chaseTimer;
        private float _stingTimer;
        private float _anim;
        private bool _initialized;

        private void OnEnable()
        {
            _snake.StrikeStarted += OnStrikeStarted;
        }

        private void OnDisable()
        {
            _snake.StrikeStarted -= OnStrikeStarted;
        }

        private void Start()
        {
            Initialize();
            ResetAll();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _count = _grid.WaspNestCount;
            _nests = new Vector3[_count];
            _anger = new float[_count];
            _calm = new float[_count];
            float cs = _grid.CellSize;
            Vector3[] points = _board != null ? _board.WaspNestPoints : null;
            for (int i = 0; i < _count; i++)
                _nests[i] = points != null && i < points.Length ? points[i] : _grid.GetWaspNest(i) + Vector3.up * (_nestHeight * cs);
        }

        /// <summary>Новая партия: гнёзда спокойны, роя нет.</summary>
        public void ResetAll()
        {
            Initialize();
            for (int i = 0; i < _count; i++)
            {
                _anger[i] = 0f;
                _calm[i] = 0f;
            }
            _active = -1;
            for (int i = 0; i < _swarm.Length; i++) SetActive(_swarm[i], false);
        }

        /// <summary>Злость ближайшего гнезда 0..1 и расстояние до него (мир) — для подсказок.</summary>
        public float NearestAnger(Vector3 point, out float distance)
        {
            distance = float.MaxValue;
            float anger = 0f;
            for (int i = 0; i < _count; i++)
            {
                Vector3 d = _nests[i] - point;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist >= distance) continue;
                distance = dist;
                anger = _anger[i];
            }
            return anger;
        }

        private void OnStrikeStarted(float charge)
        {
            if (!IsRunning || _active >= 0) return;
            // Бросок у гнезда — осы срываются сразу.
            int nest = NearestNest(_snake.HeadPosition, _config.WaspAlertRadius * _grid.CellSize);
            if (nest >= 0 && _calm[nest] <= 0f) StartSwarm(nest);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !_initialized) return;
            _anim += dt;
            AnimateGuards();
            if (!IsRunning)
            {
                if (_active >= 0) EndSwarm();
                return;
            }

            for (int i = 0; i < _count; i++)
            {
                if (_calm[i] > 0f) _calm[i] -= dt;
                if (i == _active) continue;
                _anger[i] = Mathf.Clamp01(_anger[i] + AngerRate(i) * dt);
                if (_anger[i] >= 1f && _active < 0) StartSwarm(i);
            }
            if (_active >= 0) TickSwarm(dt);
        }

        /// <summary>Сколько злости в секунду добавляет змея рядом с гнездом (отрицательное — гнездо остывает).</summary>
        private float AngerRate(int nest)
        {
            if (_calm[nest] > 0f || !SnakePresent()) return -0.3f;
            float radius = _config.WaspAlertRadius * _grid.CellSize;
            Vector3 d = _snake.HeadPosition - _nests[nest];
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > radius) return -0.3f;
            float rate;
            if (!_snake.IsMoving || _snake.Pace == SnakePace.Sneak) rate = 0f;
            else if (_snake.IsHurrying) rate = _config.WaspAngerRate.y;
            else rate = _config.WaspAngerRate.x;
            if (rate <= 0f) return -0.15f;
            // Вплотную к гнезду — злятся быстрее.
            rate *= Mathf.Lerp(1.5f, 0.6f, dist / radius);
            if (IsNight) rate *= 0.5f;
            return rate;
        }

        private void StartSwarm(int nest)
        {
            _active = nest;
            _returning = false;
            _swarmPos = _nests[nest];
            _chaseTimer = _config.WaspChaseTime;
            _stingTimer = 0.4f;
            for (int i = 0; i < _swarm.Length; i++) SetActive(_swarm[i], true);
            SwarmStarted?.Invoke(_nests[nest]);
        }

        private void EndSwarm()
        {
            if (_active >= 0)
            {
                _anger[_active] = 0f;
                _calm[_active] = _config.WaspCalmTime;
            }
            _active = -1;
            for (int i = 0; i < _swarm.Length; i++) SetActive(_swarm[i], false);
        }

        private void TickSwarm(float dt)
        {
            float cs = _grid.CellSize;
            Vector3 nest = _nests[_active];
            _chaseTimer -= dt;
            if (!_returning)
            {
                Vector3 fromNest = _snake.HeadPosition - nest;
                fromNest.y = 0f;
                // Вода, нора и полое бревно спасают; далеко от гнезда или надоело — рой отстаёт.
                bool lost = !SnakePresent() || _snake.IsInWater || _grid.IsHollowAt(_snake.HeadPosition)
                            || _chaseTimer <= 0f || fromNest.magnitude > _config.WaspLeash * cs;
                if (lost) _returning = true;
            }

            Vector3 goal = _returning ? nest : _snake.HeadPosition + Vector3.up * (0.5f * cs);
            _swarmPos = Vector3.MoveTowards(_swarmPos, goal, _config.WaspSpeed * cs * dt);
            // Рой не проваливается в холмы.
            float floor = _grid.GroundHeight(_swarmPos) + 0.3f * cs;
            if (_swarmPos.y < floor) _swarmPos.y = floor;

            if (_returning)
            {
                if ((_swarmPos - nest).sqrMagnitude < 0.25f * cs * cs) EndSwarm();
            }
            else
            {
                _stingTimer -= dt;
                Vector3 d = _snake.HeadPosition - _swarmPos;
                d.y = 0f;
                if (_stingTimer <= 0f && d.magnitude <= _stingRange * cs)
                {
                    _stingTimer = _config.WaspStingInterval;
                    Stung?.Invoke(_swarmPos);
                }
            }

            if (_fox != null && _fox.Sting(_swarmPos, _foxStingRange * cs)) FoxStung?.Invoke();
            if (_active >= 0) AnimateSwarm();
        }

        private int NearestNest(Vector3 point, float maxDistance)
        {
            int best = -1;
            float bestDist = maxDistance;
            for (int i = 0; i < _count; i++)
            {
                Vector3 d = _nests[i] - point;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > bestDist) continue;
                bestDist = dist;
                best = i;
            }
            return best;
        }

        private bool SnakePresent()
        {
            return _snake != null && _snake.IsAlive && _snake.IsRunning && !_snake.InBurrow;
        }

        // ---------------- Визуал ----------------

        /// <summary>Осы роя мечутся вокруг центра по «восьмёркам» с разными частотами.</summary>
        private void AnimateSwarm()
        {
            float r = _swarmSpread * _grid.CellSize;
            for (int i = 0; i < _swarm.Length; i++)
            {
                float t = _anim * (7f + i * 0.37f) + i * 1.7f;
                var offset = new Vector3(Mathf.Sin(t) * r, Mathf.Sin(t * 1.9f + i) * r * 0.5f, Mathf.Cos(t * 1.3f) * r);
                Vector3 velocity = new Vector3(Mathf.Cos(t), 0f, -Mathf.Sin(t * 1.3f));
                _swarm[i].SetPositionAndRotation(_swarmPos + offset, Quaternion.LookRotation(velocity.sqrMagnitude > 1e-4f ? velocity : Vector3.forward));
            }
        }

        /// <summary>Дежурные осы кружат у гнезда; чем злее гнездо, тем шире и быстрее — видно, что пора уходить.</summary>
        private void AnimateGuards()
        {
            float cs = _grid.CellSize;
            for (int i = 0; i < _guards.Length; i++)
            {
                int nest = i / 2;
                if (nest >= _count) break;
                bool visible = nest != _active;
                SetActive(_guards[i], visible);
                if (!visible) continue;
                float anger = _anger[nest];
                float speed = Mathf.Lerp(2.2f, 7f, anger);
                float radius = Mathf.Lerp(0.35f, 0.9f, anger) * cs;
                float t = _anim * speed + i * Mathf.PI;
                var offset = new Vector3(Mathf.Cos(t) * radius, 0.1f * cs + Mathf.Sin(t * 2.3f) * 0.12f * cs, Mathf.Sin(t) * radius);
                Vector3 tangent = new Vector3(-Mathf.Sin(t), 0f, Mathf.Cos(t));
                _guards[i].SetPositionAndRotation(_nests[nest] + offset, Quaternion.LookRotation(tangent));
            }
        }

        private static void SetActive(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
    }
}
