using System;

namespace Snake3D
{
    /// <summary>
    /// Переваривание: каждая съеденная добыча — бугор, который ползёт по телу от головы к хвосту.
    /// Пока бугры не прошли, змея медленнее ползает и ниже прыгает (сильнее — чем крупнее добыча).
    /// Обычный C#-класс без Unity API — легко тестировать. Массивы фиксированного размера, без аллокаций.
    /// </summary>
    public class Digestion
    {
        public const int MAX_MEALS = 4;

        /// <summary>Насколько замедляет ползание бугор размера 1 (кролик).</summary>
        public float SlowPerSize { get; set; } = 0.35f;

        /// <summary>Насколько снижает прыжок бугор размера 1.</summary>
        public float JumpPenaltyPerSize { get; set; } = 0.5f;

        /// <summary>Предел замедления (суммарный размер бугров дальше не считается).</summary>
        public float MaxLoad { get; set; } = 1.2f;

        public int Count { get; private set; }
        public bool IsDigesting => Count > 0;

        /// <summary>Суммарная «тяжесть» бугров; уходя к хвосту, бугор тяжелит змею всё меньше.</summary>
        public float Load
        {
            get
            {
                float load = 0f;
                for (int i = 0; i < Count; i++) load += _size[i] * (1f - 0.5f * _pos[i]);
                return Math.Min(MaxLoad, load);
            }
        }

        public float SpeedMultiplier => Math.Max(0.1f, 1f - SlowPerSize * Load);
        public float JumpMultiplier => Math.Max(0.1f, 1f - JumpPenaltyPerSize * Load);

        private readonly float[] _pos = new float[MAX_MEALS];
        private readonly float[] _size = new float[MAX_MEALS];
        private readonly float[] _rate = new float[MAX_MEALS];

        public void Reset()
        {
            Count = 0;
        }

        /// <summary>Проглотить добычу размера size; бугор пройдёт тело за duration секунд.</summary>
        public void Add(float size, float duration)
        {
            if (size <= 0f) return;
            if (Count == MAX_MEALS) RemoveAt(0);
            _pos[Count] = 0f;
            _size[Count] = size;
            _rate[Count] = duration > 0f ? 1f / duration : 1f;
            Count++;
        }

        public void Tick(float dt)
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                _pos[i] += _rate[i] * dt;
                if (_pos[i] >= 1f) RemoveAt(i);
            }
        }

        /// <summary>Бугор i: где он (0 — у головы, 1 — у хвоста) и его размер.</summary>
        public void GetMeal(int index, out float position01, out float size)
        {
            position01 = _pos[index];
            size = _size[index];
        }

        private void RemoveAt(int index)
        {
            for (int i = index; i < Count - 1; i++)
            {
                _pos[i] = _pos[i + 1];
                _size[i] = _size[i + 1];
                _rate[i] = _rate[i + 1];
            }
            Count--;
        }
    }
}
