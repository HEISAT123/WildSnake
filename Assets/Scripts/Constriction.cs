namespace WildSnake
{
    /// <summary>
    /// Обвивание крупной добычи: пока игрок держит F и у змеи есть силы, шкала борьбы растёт.
    /// Отпустил — шкала тает, а если отпустил надолго или кончилась выносливость, добыча вырывается.
    /// Обычный C#-класс без Unity API — легко тестировать.
    /// </summary>
    public class Constriction
    {
        /// <summary>Сколько секунд непрерывно сжимать, чтобы победить.</summary>
        public float Duration { get; set; } = 2.6f;

        /// <summary>Сколько шкалы теряется в секунду, пока змея не сжимает.</summary>
        public float Decay { get; set; } = 0.25f;

        /// <summary>Сколько секунд добыча терпит без сжатия, прежде чем вырваться.</summary>
        public float Patience { get; set; } = 1.5f;

        public bool IsActive { get; private set; }
        public float Progress01 { get; private set; }

        private float _idle;

        public void Begin()
        {
            IsActive = true;
            Progress01 = 0f;
            _idle = 0f;
        }

        public void Cancel()
        {
            IsActive = false;
            Progress01 = 0f;
        }

        /// <summary>Шаг борьбы. squeezing — зажата F, hasStamina — у змеи ещё есть силы.</summary>
        public ConstrictResult Tick(float dt, bool squeezing, bool hasStamina)
        {
            if (!IsActive) return ConstrictResult.None;

            if (squeezing && !hasStamina)
            {
                IsActive = false;
                return ConstrictResult.Escaped;
            }

            if (squeezing)
            {
                _idle = 0f;
                Progress01 += Duration > 0f ? dt / Duration : 1f;
                if (Progress01 >= 1f)
                {
                    Progress01 = 1f;
                    IsActive = false;
                    return ConstrictResult.Won;
                }
                return ConstrictResult.Ongoing;
            }

            _idle += dt;
            Progress01 -= Decay * dt;
            if (Progress01 < 0f) Progress01 = 0f;
            if (_idle >= Patience)
            {
                IsActive = false;
                return ConstrictResult.Escaped;
            }
            return ConstrictResult.Ongoing;
        }
    }
}
