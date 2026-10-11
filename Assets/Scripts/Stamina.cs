using System;

namespace WildSnake
{
    /// <summary>
    /// Выносливость змеи. Обычный C#-класс без Unity API — легко тестировать.
    /// Тратится на быстрое ползание (постепенно), бросок и обвивание (разом или постепенно), восстанавливается
    /// после короткой паузы; стоя — быстрее. Голод замедляет восстановление (RegenMultiplier ставит GameManager).
    /// Опустошённая до нуля змея «выдохлась»: быстро ползти не может, пока запас не восстановится до порога.
    /// </summary>
    public class Stamina
    {
        public float Value { get; private set; }
        public float Max { get; private set; } = 100f;

        /// <summary>Восстановление в секунду.</summary>
        public float RegenPerSecond { get; set; } = 18f;

        /// <summary>Пауза после траты, прежде чем запас начнёт восстанавливаться, сек.</summary>
        public float RegenDelay { get; set; } = 0.6f;

        /// <summary>Во сколько раз быстрее восстановление, когда змея стоит.</summary>
        public float StillBonus { get; set; } = 1.5f;

        /// <summary>Доля запаса, до которой нужно восстановиться, чтобы перестать быть «выдохшейся».</summary>
        public float RecoverThreshold01 { get; set; } = 0.3f;

        /// <summary>Внешний множитель восстановления (голод, холод).</summary>
        public float RegenMultiplier { get; set; } = 1f;

        public bool IsExhausted { get; private set; }
        public float Value01 => Max > 0f ? Value / Max : 0f;

        private float _delay;

        public Stamina()
        {
            Reset(100f);
        }

        public void Reset(float max)
        {
            Max = Math.Max(1f, max);
            Value = Max;
            IsExhausted = false;
            _delay = 0f;
        }

        public bool CanSpend(float amount)
        {
            return !IsExhausted && Value >= amount;
        }

        /// <summary>Потратить разом (бросок). Если не хватает — ничего не тратит и возвращает false.</summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true;
            if (!CanSpend(amount)) return false;
            Value -= amount;
            _delay = RegenDelay;
            if (Value <= 0f) Exhaust();
            return true;
        }

        /// <summary>Тратить постепенно (быстрое ползание, обвивание): забирает сколько есть.</summary>
        public void Drain(float amount)
        {
            if (amount <= 0f) return;
            Value = Math.Max(0f, Value - amount);
            _delay = RegenDelay;
            if (Value <= 0f) Exhaust();
        }

        /// <summary>Течение времени: после паузы запас восстанавливается (стоя — быстрее).</summary>
        public void Tick(float dt, bool still)
        {
            if (dt <= 0f) return;
            if (_delay > 0f)
            {
                _delay -= dt;
                return;
            }

            float regen = RegenPerSecond * Math.Max(0f, RegenMultiplier) * (still ? StillBonus : 1f);
            Value = Math.Min(Max, Value + regen * dt);
            if (IsExhausted && Value01 >= RecoverThreshold01) IsExhausted = false;
        }

        private void Exhaust()
        {
            Value = 0f;
            IsExhausted = true;
        }
    }
}
