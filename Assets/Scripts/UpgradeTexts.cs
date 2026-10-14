using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Тексты улучшений для экрана линьки и окна «Улучшения» (U): название, что именно меняется, чем платишь.
    /// Строки собираются только при открытии экрана, не каждый кадр.
    /// </summary>
    public static class UpgradeTexts
    {
        private static readonly string[] Names =
        {
            "ТОЛСТАЯ ЧЕШУЯ", "МЕДЛЕННЫЙ ОБМЕН", "ДЛИННЫЙ БРОСОК", "ОСТРЫЙ НЮХ",
            "БЕСШУМНАЯ ЧЕШУЯ", "КРЕПКОЕ СЕРДЦЕ", "ГИБКОЕ ТЕЛО", "СЫТНАЯ ОХОТА",
            "ГЛУБОКОЕ ДЫХАНИЕ", "ТЁПЛАЯ КРОВЬ", "СТАЛЬНАЯ ХВАТКА", "ТЕРПЕЛИВЫЙ ОХОТНИК", "ПЛОВЕЦ"
        };

        // {0} — сила эффекта: «22%» или «22% (было 12%)».
        private static readonly string[] Effects =
        {
            "Любой урон, кроме голода, меньше на {0}",
            "Сытость убывает на {0} медленнее",
            "Бросок летит на {0} дальше и тратит меньше выносливости",
            "Нюх (E) чует добычу на {0} дальше и перезаряжается быстрее",
            "Добыча замечает змею с расстояния на {0} меньше",
            "Запас здоровья больше на {0} (обычно 100)",
            "Змея ползает на {0} быстрее",
            "Каждая добыча утоляет голод на {0} сильнее",
            "Выносливость восстанавливается на {0} быстрее",
            "Тело остывает на {0} медленнее: ночь и дождь не так страшны",
            "Кролик в кольцах (F) сдаётся на {0} быстрее",
            "Засада (замереть на месте) наступает на {0} быстрее",
            "В воде змея плывёт на {0} быстрее"
        };

        // Плата сильных улучшений: {0} — сколько всего на этом уровне. Пусто — улучшение бесплатное.
        private static readonly string[] Costs =
        {
            "скорость −{0}", "заживление ран −{0}", "", "", "", "голод +{0}", "голод +{0}", "", "", "", "", "", ""
        };

        public static string Name(UpgradeType type) => Names[(int)type];

        /// <summary>Что даёт улучшение на уровне level (1..3).</summary>
        public static string Effect(UpgradeType type, int level)
        {
            return string.Format(Effects[(int)type], Value(type, level));
        }

        /// <summary>Плата на уровне level: «Плата: голод +8%»; бесплатное улучшение или уровень 0 — пустая строка.</summary>
        public static string Cost(UpgradeType type, int level)
        {
            string template = Costs[(int)type];
            if (template.Length == 0 || level <= 0) return "";
            return "Плата: " + string.Format(template, CostPercent(type, level) + "%");
        }

        /// <summary>
        /// Предложение на линьке: эффект следующего уровня рядом с нынешним («22% (было 12%)») — видно, что отдача убывает,
        /// и плата так же.
        /// </summary>
        public static string Offer(UpgradeType type, int level)
        {
            string value = Value(type, level + 1);
            if (level > 0) value += " (было " + Value(type, level) + ")";
            string text = string.Format(Effects[(int)type], value);
            string template = Costs[(int)type];
            if (template.Length == 0) return text;
            string cost = CostPercent(type, level + 1) + "%";
            if (level > 0) cost += " (было " + CostPercent(type, level) + "%)";
            return text + ". Плата: " + string.Format(template, cost);
        }

        private static string Value(UpgradeType type, int level)
        {
            int v = Mathf.RoundToInt(UpgradeSet.EffectPercent(type, level));
            return type == UpgradeType.StrongHeart ? v + " ед." : v + "%";
        }

        private static int CostPercent(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.ThickScales: return Mathf.RoundToInt(UpgradeSet.THICK_SCALES_SLOWDOWN * 100f * level);
                case UpgradeType.SlowMetabolism: return Mathf.RoundToInt(UpgradeSet.REGEN_PENALTY * 100f * level);
                default: return Mathf.RoundToInt(UpgradeSet.HUNGER_PENALTY * 100f * level);
            }
        }
    }
}
