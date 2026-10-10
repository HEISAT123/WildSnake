using System;

namespace Snake3D
{
    /// <summary>
    /// Здоровье и сытость змеи игрока. Обычный C#-класс без Unity API — легко тестировать.
    /// Сытость убывает со временем и от действий; пока змея сыта, здоровье восстанавливается.
    /// Голодание (сытость на нуле) обрабатывает GameManager: сначала худеет тело, потом идёт урон.
    /// </summary>
    public class SurvivalStats
    {
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Satiety { get; private set; }
        public float MaxSatiety { get; private set; }

        /// <summary>Порог сытости (доля), с которого идёт восстановление здоровья.</summary>
        public float WellFedThreshold { get; set; } = 0.6f;

        /// <summary>Восстановление здоровья в секунду, пока змея сыта.</summary>
        public float RegenPerSecond { get; set; } = 2.5f;

        public bool IsDead => Health <= 0f;
        public bool IsStarving => Satiety <= 0f;
        public float Health01 => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public float Satiety01 => MaxSatiety > 0f ? Satiety / MaxSatiety : 0f;

        public SurvivalStats(float maxHealth, float maxSatiety, float startSatiety)
        {
            Reset(maxHealth, maxSatiety, startSatiety);
        }

        public void Reset(float maxHealth, float maxSatiety, float startSatiety)
        {
            MaxHealth = Math.Max(1f, maxHealth);
            MaxSatiety = Math.Max(1f, maxSatiety);
            Health = MaxHealth;
            Satiety = Clamp(startSatiety, 0f, MaxSatiety);
        }

        /// <summary>Течение времени: голод и восстановление здоровья у сытой змеи.</summary>
        public void Tick(float dt, float hungerPerSecond)
        {
            if (IsDead || dt <= 0f) return;
            Satiety = Math.Max(0f, Satiety - hungerPerSecond * dt);
            if (Satiety01 >= WellFedThreshold) Heal(RegenPerSecond * dt);
        }

        /// <summary>Потратить сытость (бросок, прыжок).</summary>
        public void Spend(float amount)
        {
            if (amount > 0f) Satiety = Math.Max(0f, Satiety - amount);
        }

        public void Feed(float amount)
        {
            if (amount > 0f && !IsDead) Satiety = Math.Min(MaxSatiety, Satiety + amount);
        }

        public void Heal(float amount)
        {
            if (amount > 0f && !IsDead) Health = Math.Min(MaxHealth, Health + amount);
        }

        /// <summary>Нанести урон. Возвращает true, если змея погибла от этого удара.</summary>
        public bool Damage(float amount)
        {
            if (IsDead || amount <= 0f) return false;
            Health = Math.Max(0f, Health - amount);
            return IsDead;
        }

        /// <summary>Поднять максимум здоровья (улучшение); текущее растёт на ту же величину.</summary>
        public void AddMaxHealth(float amount)
        {
            MaxHealth = Math.Max(1f, MaxHealth + amount);
            Health = Clamp(Health + amount, 0f, MaxHealth);
        }

        public void HealFull()
        {
            if (!IsDead) Health = MaxHealth;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }
}
