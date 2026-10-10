using System;

namespace Snake3D
{
    /// <summary>
    /// Главная цель жизни змеи — «Продолжить род», и она повторяется из поколения в поколение:
    /// повзрослеть (длина и линьки), ночью, сытой, отложить яйца в гнезде, затем охранять кладку до вылупления.
    /// Вылупились змеёныши — игра продолжается одним из них: следующее поколение, цель взросления чуть выше.
    /// Обычный C#-класс без Unity API — легко тестировать.
    /// </summary>
    public class MainQuest
    {
        /// <summary>Какой длины (клеток) должна стать взрослая змея первого поколения.</summary>
        public float BaseLength { get; set; } = 11f;

        /// <summary>На сколько клеток длиннее должна вырасти змея каждого следующего поколения.</summary>
        public float LengthStep { get; set; } = 1.5f;

        /// <summary>Сколько линек нужно пережить за поколение.</summary>
        public int TargetMolts { get; set; } = 2;

        /// <summary>Сытость (доля), без которой кладка невозможна.</summary>
        public float NestSatiety { get; set; } = 0.6f;

        /// <summary>Сколько яиц в кладке.</summary>
        public int ClutchSize { get; set; } = 5;

        /// <summary>Сколько секунд яйца зреют до вылупления.</summary>
        public float HatchSeconds { get; set; } = 170f;

        public QuestStage Stage { get; private set; }

        /// <summary>Номер поколения, начиная с 1.</summary>
        public int Generation { get; private set; } = 1;

        /// <summary>Сколько яиц в гнезде (на этапе охраны).</summary>
        public int Eggs { get; private set; }

        /// <summary>Сколько секунд кладка уже зреет.</summary>
        public float HatchTime { get; private set; }

        /// <summary>Длина взрослой змеи для текущего поколения.</summary>
        public float TargetLength => BaseLength + (Generation - 1) * LengthStep;

        public float HatchProgress01 => HatchSeconds > 0f ? Math.Min(1f, HatchTime / HatchSeconds) : 1f;

        public float HatchSecondsLeft => Math.Max(0f, HatchSeconds - HatchTime);

        public void Reset()
        {
            Stage = QuestStage.Grow;
            Generation = 1;
            Eggs = 0;
            HatchTime = 0f;
        }

        /// <summary>Восстановить состояние из сохранения.</summary>
        public void Restore(QuestStage stage, int generation = 1, int eggs = 0, float hatchTime = 0f)
        {
            Stage = stage;
            Generation = Math.Max(1, generation);
            Eggs = stage == QuestStage.Guard ? Math.Max(1, Math.Min(eggs, ClutchSize)) : 0;
            HatchTime = stage == QuestStage.Guard ? Math.Max(0f, hatchTime) : 0f;
        }

        /// <summary>Насколько змея повзрослела, 0..1 (среднее длины и линек этого поколения).</summary>
        public float GrowProgress01(float length, int molts)
        {
            float target = TargetLength;
            float l = target > 0f ? Math.Min(1f, length / target) : 1f;
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
            if (Stage == QuestStage.Guard) return NestCheck.AlreadyDone;
            if (Stage != QuestStage.Nest) return NestCheck.NotGrown;
            if (!isNight) return NestCheck.NeedNight;
            if (satiety01 < NestSatiety) return NestCheck.NeedFood;
            return NestCheck.Ok;
        }

        /// <summary>Отложить яйца: начинается охрана кладки. Возвращает true, если кладка состоялась.</summary>
        public bool TryLay(bool isNight, float satiety01)
        {
            if (CanLay(isNight, satiety01) != NestCheck.Ok) return false;
            Stage = QuestStage.Guard;
            Eggs = Math.Max(1, ClutchSize);
            HatchTime = 0f;
            return true;
        }

        /// <summary>Кладка зреет. Возвращает true, когда пора вылупляться.</summary>
        public bool TickHatch(float dt)
        {
            if (Stage != QuestStage.Guard || Eggs <= 0) return false;
            HatchTime += dt;
            return HatchTime >= HatchSeconds;
        }

        /// <summary>Хищник утащил яйцо. Возвращает true, если кладка погибла целиком — придётся откладывать заново.</summary>
        public bool LoseEgg()
        {
            if (Stage != QuestStage.Guard || Eggs <= 0) return false;
            Eggs--;
            if (Eggs > 0) return false;
            Stage = QuestStage.Nest;
            HatchTime = 0f;
            return true;
        }

        /// <summary>Вылупление: следующее поколение начинает с роста. Возвращает число змеёнышей.</summary>
        public int Hatch()
        {
            if (Stage != QuestStage.Guard || Eggs <= 0) return 0;
            int hatched = Eggs;
            Eggs = 0;
            HatchTime = 0f;
            Generation++;
            Stage = QuestStage.Grow;
            return hatched;
        }
    }
}
