using System;

namespace Snake3D
{
    /// <summary>
    /// Главная цель жизни змеи — «Продолжить род»: сначала повзрослеть (длина и линьки), потом найти гнездо
    /// и ночью, сытой, отложить в нём яйца. Обычный C#-класс без Unity API — легко тестировать.
    /// </summary>
    public class MainQuest
    {
        /// <summary>Какой длины (клеток) должна стать взрослая змея.</summary>
        public float TargetLength { get; set; } = 11f;

        /// <summary>Сколько линек нужно пережить.</summary>
        public int TargetMolts { get; set; } = 2;

        /// <summary>Сытость (доля), без которой кладка невозможна.</summary>
        public float NestSatiety { get; set; } = 0.6f;

        public QuestStage Stage { get; private set; }

        public void Reset()
        {
            Stage = QuestStage.Grow;
        }

        /// <summary>Восстановить этап из сохранения.</summary>
        public void Restore(QuestStage stage)
        {
            Stage = stage;
        }

        /// <summary>Насколько змея повзрослела, 0..1 (среднее длины и линек).</summary>
        public float GrowProgress01(float length, int molts)
        {
            float l = TargetLength > 0f ? Math.Min(1f, length / TargetLength) : 1f;
            float m = TargetMolts > 0 ? Math.Min(1f, (float)molts / TargetMolts) : 1f;
            return (l + m) * 0.5f;
        }

        /// <summary>Проверить рост. Возвращает true, если змея только что повзрослела (этап сменился на «Гнездо»).</summary>
        public bool Update(float length, int molts)
        {
            if (Stage != QuestStage.Grow) return false;
            if (length + 1e-3f < TargetLength || molts < TargetMolts) return false;
            Stage = QuestStage.Nest;
            return true;
        }

        /// <summary>Можно ли отложить яйца (змея уже у гнезда).</summary>
        public NestCheck CanLay(bool isNight, float satiety01)
        {
            if (Stage == QuestStage.Done) return NestCheck.AlreadyDone;
            if (Stage != QuestStage.Nest) return NestCheck.NotGrown;
            if (!isNight) return NestCheck.NeedNight;
            if (satiety01 < NestSatiety) return NestCheck.NeedFood;
            return NestCheck.Ok;
        }

        /// <summary>Отложить яйца. Возвращает true, если цель выполнена этим вызовом.</summary>
        public bool TryLay(bool isNight, float satiety01)
        {
            if (CanLay(isNight, satiety01) != NestCheck.Ok) return false;
            Stage = QuestStage.Done;
            return true;
        }
    }
}
