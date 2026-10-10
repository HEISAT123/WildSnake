using System;

namespace Snake3D
{
    /// <summary>
    /// Цели суток: с каждым рассветом 1–2 простые задачи. GameManager сообщает о событиях (Report),
    /// выполненная цель приносит награду — очко линьки или полное восстановление.
    /// Обычный C#-класс без Unity API — легко тестировать. Без аллокаций после создания.
    /// </summary>
    public class DailyGoals
    {
        public const int MAX_GOALS = 2;
        public const int TYPE_COUNT = 7;

        private readonly DailyGoal[] _goals = new DailyGoal[MAX_GOALS];
        private readonly int[] _pool = new int[TYPE_COUNT];

        public int Count { get; private set; }
        public int Day { get; private set; }

        public DailyGoal Get(int index) => _goals[index];

        /// <summary>Сколько целей выполнено сегодня.</summary>
        public int DoneCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++)
                    if (_goals[i].Done) n++;
                return n;
            }
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Новые цели на сутки day: в первый день одна (чтобы освоиться), дальше две разные.</summary>
        public void Roll(int day, Random rng)
        {
            Day = day;
            for (int i = 0; i < TYPE_COUNT; i++) _pool[i] = i;
            Count = day <= 1 ? 1 : MAX_GOALS;
            for (int i = 0; i < Count; i++)
            {
                int j = i + rng.Next(TYPE_COUNT - i);
                int tmp = _pool[i];
                _pool[i] = _pool[j];
                _pool[j] = tmp;

                var type = (GoalType)_pool[i];
                _goals[i] = new DailyGoal
                {
                    Type = type,
                    Target = TargetFor(type),
                    Progress = 0,
                    Done = false,
                    MoltReward = GivesMolt(type)
                };
            }
        }

        public bool Has(GoalType type)
        {
            for (int i = 0; i < Count; i++)
                if (_goals[i].Type == type && !_goals[i].Done) return true;
            return false;
        }

        /// <summary>
        /// Событие для целей. Возвращает индекс цели, которую это событие выполнило, или -1.
        /// </summary>
        public int Report(GoalType type, int amount = 1)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_goals[i].Type != type || _goals[i].Done) continue;
                _goals[i].Progress = Math.Min(_goals[i].Target, _goals[i].Progress + amount);
                if (_goals[i].Progress < _goals[i].Target) return -1;
                _goals[i].Done = true;
                return i;
            }
            return -1;
        }

        public static int TargetFor(GoalType type) => type == GoalType.CatchMice ? 3 : 1;

        /// <summary>Охотничьи цели дают очко линьки, остальные — полное восстановление.</summary>
        public static bool GivesMolt(GoalType type)
        {
            return type == GoalType.AmbushBird || type == GoalType.ConstrictRabbit || type == GoalType.CatchMice;
        }
    }
}
