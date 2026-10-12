using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Сценарий видео-обучения: главы-ролики по механикам, нарисованные сверху, как на карте. Каждый кадр — чистая
    /// функция времени главы: никакого накопленного состояния, поэтому перемотка и пауза работают сами собой.
    /// Рисует в TutorialCanvas (сцена 16×9); субтитры — по таймкодам. Строки — константы, кадр без аллокаций.
    /// </summary>
    public static class TutorialFilm
    {
        public static readonly string[] Titles =
        {
            "ДВИЖЕНИЕ", "ОХОТА", "ЗАСАДА", "КРУПНАЯ ДОБЫЧА", "ЁЖ", "ХИЩНИКИ", "ОСИНОЕ ГНЕЗДО", "ТЕПЛО И НОРЫ", "СОПЕРНИЦА",
            "ПРОДОЛЖИТЬ РОД"
        };

        public static readonly float[] Durations = { 10f, 12f, 11f, 10f, 15f, 20f, 13f, 12f, 14f, 13f };

        private static readonly float[][] CueTimes =
        {
            new[] { 0f, 3.5f, 6.5f },
            new[] { 0f, 4.5f, 9.2f },
            new[] { 0f, 2.5f, 4.5f, 8.1f },
            new[] { 0f, 3.2f, 7.6f },
            new[] { 0f, 3.5f, 9f, 11.5f },
            new[] { 0f, 3f, 6.5f, 9.2f, 13f, 16.4f },
            new[] { 0f, 2.4f, 6.5f, 10.5f },
            new[] { 0f, 2.2f, 4.5f, 8.5f },
            new[] { 0f, 2.3f, 4.5f, 10f },
            new[] { 0f, 4f, 9f, 11.8f }
        };

        private static readonly string[][] Cues =
        {
            new[]
            {
                "WASD — ползти. Змея поворачивает дугой, а не на месте; мышь — камера.",
                "CTRL — красться: медленно, зато добыча вас не слышит.",
                "SHIFT — быстрее, но это тратит выносливость. SPACE — прыжок через бревно."
            },
            new[]
            {
                "Спешащую змею добыча замечает издалека и убегает.",
                "Подкрадитесь (CTRL), потом держите ПКМ — замах: бросок полетит дальше.",
                "ЛКМ — бросок. Жука хватит просто коснуться."
            },
            new[]
            {
                "Заползите в траву или камыш — там вас почти не видно.",
                "Замрите на пару секунд: змея сливается с местностью.",
                "Затаившаяся змея подманивает добычу кончиком хвоста.",
                "Подпустите ближе — и бросок."
            },
            new[]
            {
                "Кролик сытный, но одного укуса мало.",
                "Бросок только хватает его — держите F, чтобы обвить и задушить.",
                "Отпустите F раньше — кролик вырвется. Сжатие тратит выносливость."
            },
            new[]
            {
                "Ёж, заметив змею, сворачивается — укус в колючки ранит вас.",
                "Подползите не спеша и толкайте шар носом. Скатился в воду — ёж беззащитен.",
                "Хватайте, пока он не выбрался на берег и не отряхнулся.",
                "Или ждите рядом: перед тем как развернуться, ёж выглядывает — кусайте в этот миг."
            },
            new[]
            {
                "Днём кружит орёл: по земле скользит тень, а когда он рядом — метка на экране.",
                "Уходите в траву, под кроны или в полое бревно — орёл промахнётся.",
                "Ночью приходит лиса и идёт по вашему следу.",
                "В ежевику и воду лиса не полезет, а бросок в морду отпугнёт её, пока вы здоровы.",
                "Q — шипение: лиса отпрянет и на время отступит — и от вас, и от кладки.",
                "X (держать) — притвориться мёртвой: лиса и орёл теряют интерес. Но силы тают."
            },
            new[]
            {
                "Осы злятся от шума: шагом — понемногу, бегом или броском — сразу.",
                "Рой летит быстрее вас. Спасают вода, нора или полое бревно.",
                "Зато лису можно привести к гнезду: осы жалят и её.",
                "Крадитесь (CTRL) мимо гнезда — и осы вас не заметят."
            },
            new[]
            {
                "Змея холоднокровна: замёрзнув, она медленнее ползает и бросается.",
                "Грейтесь на солнечных камнях — днём это быстрее всего.",
                "Ночью греет тёплый ключ — пятно парящей земли.",
                "E у норы — переждать ночь и сохраниться. Ночь в норе идёт быстрее."
            },
            new[]
            {
                "Соперница — живой враг: днём идёт счёт, кто больше добыл. Итог — на закате.",
                "Трофей дня — золотая метка. Его ищете вы обе: успейте первой.",
                "Пока вы душите добычу, соперница попробует её отбить — шипите (Q).",
                "Укусите её — разозлится и начнёт охоту на вас. Победите — шаг к линьке."
            },
            new[]
            {
                "Ешьте и растите. Каждые несколько добыч — линька и улучшение на выбор.",
                "Повзрослев, ночью и сытой отложите яйца в гнезде — клавиша E.",
                "Охраняйте кладку: лиса придёт за яйцами.",
                "Вылупятся змеёныши — и вы играете следующим поколением."
            }
        };

        public static int ChapterCount => Titles.Length;

        // ---------------- Палитра ----------------
        private static readonly Color Meadow = new Color(0.27f, 0.37f, 0.19f);
        private static readonly Color MeadowDark = new Color(0.23f, 0.32f, 0.16f);
        private static readonly Color GrassC = new Color(0.52f, 0.66f, 0.28f);
        private static readonly Color WaterC = new Color(0.2f, 0.42f, 0.6f);
        private static readonly Color WaterRim = new Color(0.62f, 0.82f, 0.9f, 0.55f);
        private static readonly Color SnakeC = new Color(0.56f, 0.8f, 0.22f);
        private static readonly Color SnakeDark = new Color(0.3f, 0.47f, 0.12f);
        private static readonly Color EyeC = new Color(0.98f, 0.82f, 0.25f);
        private static readonly Color TongueC = new Color(0.85f, 0.12f, 0.2f);
        private static readonly Color MouseC = new Color(0.64f, 0.62f, 0.6f);
        private static readonly Color PinkC = new Color(0.96f, 0.66f, 0.7f);
        private static readonly Color FrogC = new Color(0.42f, 0.72f, 0.3f);
        private static readonly Color RabbitC = new Color(0.74f, 0.62f, 0.47f);
        private static readonly Color WhiteC = new Color(0.96f, 0.95f, 0.92f);
        private static readonly Color DarkC = new Color(0.06f, 0.05f, 0.05f);
        private static readonly Color HedgehogC = new Color(0.52f, 0.42f, 0.32f);
        private static readonly Color SpikeC = new Color(0.24f, 0.17f, 0.12f);
        private static readonly Color FaceC = new Color(0.86f, 0.76f, 0.6f);
        private static readonly Color FoxC = new Color(0.95f, 0.5f, 0.16f);
        private static readonly Color EagleC = new Color(0.36f, 0.26f, 0.18f);
        private static readonly Color ShadowC = new Color(0f, 0f, 0f, 0.32f);
        private static readonly Color NightC = new Color(0.02f, 0.05f, 0.14f, 0.62f);
        private static readonly Color DirtC = new Color(0.42f, 0.31f, 0.2f);
        private static readonly Color HoleC = new Color(0.07f, 0.05f, 0.04f);
        private static readonly Color StoneC = new Color(0.86f, 0.78f, 0.6f);
        private static readonly Color SpringC = new Color(0.4f, 0.84f, 0.82f);
        private static readonly Color MineralC = new Color(0.9f, 0.87f, 0.76f);
        private static readonly Color BrambleC = new Color(0.13f, 0.21f, 0.11f);
        private static readonly Color BerryC = new Color(0.36f, 0.06f, 0.16f);
        private static readonly Color PaperC = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color WaspC = new Color(0.98f, 0.8f, 0.14f);
        private static readonly Color EggC = new Color(0.96f, 0.93f, 0.83f);
        private static readonly Color Venom = new Color(0.78f, 0.96f, 0.2f);
        private static readonly Color Ember = new Color(1f, 0.42f, 0.3f);
        private static readonly Color Bone = new Color(0.95f, 0.93f, 0.88f);
        private static readonly Color Frost = new Color(0.55f, 0.8f, 1f, 0.55f);
        private static readonly Color WarmthC = new Color(1f, 0.6f, 0.2f, 0.45f);
        private static readonly Color RivalC = new Color(0.86f, 0.52f, 0.2f);
        private static readonly Color RivalDark = new Color(0.5f, 0.27f, 0.1f);
        private static readonly Color BellyC = new Color(0.93f, 0.9f, 0.7f);
        private static readonly Color GoldC = new Color(1f, 0.78f, 0.15f);

        // ---------------- Маршруты змеи ----------------
        private static readonly Vector2[] PathAcross = { new Vector2(-6f, 4.5f), new Vector2(22f, 4.5f) };
        private static readonly Vector2[] PathMove = { new Vector2(-4f, 3f), new Vector2(4f, 3.4f), new Vector2(8f, 5.6f), new Vector2(13f, 5.2f), new Vector2(22f, 3.6f) };
        private static readonly Vector2[] PathToGrass = { new Vector2(-4f, 3f), new Vector2(3f, 3.6f), new Vector2(5.2f, 4.4f), new Vector2(12f, 5.6f) };
        private static readonly Vector2[] PathEagle = { new Vector2(-4f, 2.5f), new Vector2(4f, 3f), new Vector2(9f, 4.5f), new Vector2(14f, 6f) };
        private static readonly Vector2[] PathFox = { new Vector2(-4f, 6f), new Vector2(3f, 6f), new Vector2(6f, 4.5f), new Vector2(10f, 4f) };
        private static readonly Vector2[] PathWasp = { new Vector2(-4f, 5f), new Vector2(5f, 5f), new Vector2(9f, 4.3f), new Vector2(12.4f, 2.6f), new Vector2(16f, 2f) };
        private static readonly Vector2[] PathLure = { new Vector2(-4f, 6.5f), new Vector2(4f, 6.5f), new Vector2(7.5f, 5.2f), new Vector2(10f, 3.2f), new Vector2(12.6f, 2.4f), new Vector2(16f, 2f) };
        private static readonly Vector2[] PathTrophyPlayer = { new Vector2(-6f, 4.3f), new Vector2(22f, 4.3f) };
        private static readonly Vector2[] PathTrophyRival = { new Vector2(24f, 4.9f), new Vector2(-4f, 4.9f) };
        private static readonly Vector2[] PathThief = { new Vector2(22f, 5.4f), new Vector2(-4f, 5.4f) };
        private static readonly Vector2[] PathThiefAway = { new Vector2(10.6f, 5.4f), new Vector2(24f, 6.8f) };
        private static readonly Vector2[] PathRivalCross = { new Vector2(-2f, 5.5f), new Vector2(24f, 5.5f) };
        private static readonly Vector2[] PathBiteUp = { new Vector2(8f, -2f), new Vector2(8f, 10f) };
        private static readonly Vector2[] PathNest = { new Vector2(-4f, 3f), new Vector2(4f, 3.5f), new Vector2(7f, 5.6f), new Vector2(8.2f, 6f), new Vector2(12f, 6.4f) };

        private const int MAX_SEGMENTS = 32;
        private const float GAP = 0.22f;
        private static readonly Vector2[] Pts = new Vector2[MAX_SEGMENTS];

        /// <summary>Субтитр главы на момент t (последняя реплика, чей таймкод уже наступил).</summary>
        public static string Caption(int chapter, float t)
        {
            float[] times = CueTimes[chapter];
            int k = 0;
            for (int i = 0; i < times.Length; i++)
                if (t >= times[i]) k = i;
            return Cues[chapter][k];
        }

        public static void Draw(int chapter, float t, TutorialCanvas c)
        {
            switch (chapter)
            {
                case 0: Movement(c, t); break;
                case 1: Hunting(c, t); break;
                case 2: Ambush(c, t); break;
                case 3: BigPrey(c, t); break;
                case 4: HedgehogChapter(c, t); break;
                case 5: Predators(c, t); break;
                case 6: Wasps(c, t); break;
                case 7: Warmth(c, t); break;
                case 8: Rival(c, t); break;
                default: Lineage(c, t); break;
            }
        }

        // =====================================================================
        // Главы
        // =====================================================================

        private static void Movement(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            Grass(c, new Vector2(3f, 7.2f), 1.1f, 1f, 3);
            Grass(c, new Vector2(12.5f, 2f), 1.3f, 1f, 7);
            // Бревно: SPACE — прыжок.
            c.Line(new Vector2(14.6f, 3.2f), new Vector2(14.6f, 6.4f), 0.5f, DirtC);
            float s = 3f + 1.6f * Mathf.Min(t, 3.5f) + 0.7f * Seg(t, 3.5f, 6.5f) * 3f + 3f * Seg(t, 6.5f, 10f) * 3.5f;
            FillPath(PathMove, s, 22, 0.28f);
            Snake(c, 22, 1f, t, Flick(t));
            int pace = t < 3.5f ? 0 : t < 6.5f ? 1 : 2;
            c.Key(new Vector2(1.2f, 0.75f), "WASD", true);
            c.Key(new Vector2(2.75f, 0.75f), "CTRL", pace == 1);
            c.Key(new Vector2(4.15f, 0.75f), "SHIFT", pace == 2);
            float stamina = 1f - 0.75f * Seg(t, 6.5f, 10f);
            Bar(c, 0.6f, 8.2f, 3f, stamina, Bone);
            c.Label(new Vector2(2.1f, 8.62f), "выносливость", Bone);
            if (pace == 1) c.Label(Pts[0] + new Vector2(0f, 0.8f), "тише", Venom);
            if (pace == 2) c.Label(Pts[0] + new Vector2(0f, 0.8f), "быстрее", Ember);
        }

        private static void Hunting(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            Grass(c, new Vector2(13.5f, 7.4f), 1.2f, 1f, 11);
            if (t < 4.5f)
            {
                // Дубль 1: змея несётся — мышь замечает и убегает.
                float s = 1f + 3.2f * t;
                FillPath(PathAcross, s, 20, 0.28f);
                Vector2 mouse = new Vector2(10f, 4.5f);
                float flee = Seg(t, 1.8f, 4.5f);
                if (t > 1.5f) mouse += new Vector2(flee * 7f, Mathf.Sin(flee * 18f) * 0.4f + flee * 2.5f);
                Mouse(c, mouse, t > 1.5f ? 20f : 180f, t);
                Snake(c, 20, 1f, t, false);
                c.Key(new Vector2(1.6f, 0.75f), "SHIFT", true);
                if (t > 1.4f && t < 3f) c.Label(mouse + new Vector2(0f, 0.75f), "!", Ember);
            }
            else
            {
                float u = t - 4.5f;
                Vector2 mouse = new Vector2(10.2f, 4.5f);
                bool caught = t >= 9.55f;
                // Подкрасться, замах, бросок.
                float s = 7f + 1.7f * Mathf.Min(u, 3.7f);
                bool coiling = t >= 8.2f && t < 9.3f;
                if (t >= 9.3f) s += Smooth(Seg(t, 9.3f, 9.55f)) * 2.4f;
                if (coiling)
                {
                    Vector2 head = PathPoint(PathAcross, s, 0.28f);
                    FillCoil(head, 0f, 20);
                }
                else FillPath(PathAcross, s, 20, t >= 9.3f ? 0.1f : 0.28f);
                if (!caught) Mouse(c, mouse, 180f, t);
                Snake(c, 20, 1f, t, !coiling && Flick(t));
                if (coiling) c.Ring(Pts[1], 0.75f, 0.07f, Seg(t, 8.2f, 9.2f), 90f, Venom);
                c.Key(new Vector2(1.3f, 0.75f), "CTRL", t < 8.2f);
                c.Key(new Vector2(2.6f, 0.75f), "ПКМ", coiling);
                c.Key(new Vector2(3.8f, 0.75f), "ЛКМ", t >= 9.3f && t < 9.7f);
                Bar(c, 0.6f, 8.2f, 3f, caught ? Mathf.Lerp(0.45f, 0.75f, Seg(t, 9.55f, 10.3f)) : 0.45f, Venom);
                c.Label(new Vector2(2.1f, 8.62f), "сытость", Bone);
                if (caught && t < 11.5f) c.Label(mouse + new Vector2(0f, 0.8f), "+ мышь", Venom);
                Cut(c, t, 4.5f);
            }
        }

        private static void Ambush(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            Vector2 grass = new Vector2(5.4f, 4.6f);
            float s = 2f + 2.9f * Mathf.Min(t, 2.5f);
            bool strike = t >= 8.1f;
            if (strike) s += Smooth(Seg(t, 8.1f, 8.35f)) * 2.6f;
            FillPath(PathToGrass, s, 20, strike ? 0.1f : 0.25f);
            float hide = Seg(t, 2.5f, 4.5f) * (strike ? 0f : 1f);
            // Кончик хвоста подрагивает — «червяк».
            if (t > 4.5f && !strike) Pts[19] += new Vector2(Mathf.Sin(t * 16f) * 0.12f, Mathf.Cos(t * 13f) * 0.1f);
            // Лягушка подскакивает всё ближе.
            Vector2 frog = new Vector2(12.5f, 6.6f);
            // Куда придёт лягушка — туда и попадёт бросок.
            var target = new Vector2(7.5f, 4.9f);
            float approach = Seg(t, 4.5f, 8f);
            float hops = approach * 4f;
            float hop01 = hops - Mathf.Floor(hops);
            frog = Vector2.Lerp(frog, target, Mathf.Floor(hops) / 4f + Smooth(hop01) / 4f);
            bool caught = t >= 8.35f;
            if (!caught) Frog(c, frog, Mathf.Sin(hop01 * Mathf.PI) * (approach > 0f && approach < 1f ? 1f : 0f));
            Snake(c, 20, 1f - 0.62f * hide, t, false);
            if (t > 4.5f && !strike) c.Circle(Pts[19], 0.09f, Venom);
            Grass(c, grass, 1.9f, 0.85f, 5);
            if (hide > 0.95f && t < 8f) c.Label(Pts[0] + new Vector2(0f, 0.9f), "засада", Venom);
            if (t > 5f && t < 8f) c.Label(Pts[19] + new Vector2(-0.2f, -0.7f), "манит", Bone);
            c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", strike && t < 8.6f);
            if (caught && t < 10.5f) c.Label(target + new Vector2(0f, 0.8f), "+ лягушка", Venom);
        }

        private static void BigPrey(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            Grass(c, new Vector2(2.5f, 7.5f), 1.2f, 1f, 13);
            Vector2 rabbit = new Vector2(9.6f, 4.5f);
            bool held = t >= 3.4f && t < 7.6f;
            bool coiled = t >= 3.7f && t < 7.6f;
            if (!coiled)
            {
                float s = 4f + 3.1f * Mathf.Min(t, 3.2f);
                if (t >= 3.2f) s += Smooth(Seg(t, 3.2f, 3.4f)) * 1.2f;
                FillPath(PathAcross, s, 24, 0.25f);
            }
            else FillAround(rabbit, 0.75f, 90f + (t - 3.7f) * 12f, 24);
            if (t < 7.6f)
            {
                float shake = held ? Mathf.Sin(t * 40f) * 0.08f : 0f;
                float hop = !held ? Mathf.Abs(Mathf.Sin(t * 3f)) * 0.12f : 0f;
                Rabbit(c, rabbit + new Vector2(shake, hop), 180f);
            }
            Snake(c, 24, 1f, t, false);
            if (coiled)
            {
                c.Ring(rabbit, 1.35f, 0.08f, 1f, 90f, new Color(1f, 1f, 1f, 0.12f));
                c.Ring(rabbit, 1.35f, 0.08f, Seg(t, 3.7f, 7.5f), 90f, Venom);
            }
            c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", t >= 3.2f && t < 3.6f);
            c.Key(new Vector2(2.9f, 0.75f), "F", coiled);
            if (held && t < 4.6f) c.Label(rabbit + new Vector2(0f, 1.1f), "держите F", Bone);
            if (t >= 7.6f && t < 9.6f) c.Label(rabbit + new Vector2(0f, 1f), "+ кролик", Venom);
        }

        private static void HedgehogChapter(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            if (t < 3.5f)
            {
                // Ёж замечает змею, сворачивается; укус — и укол.
                Vector2 hog = new Vector2(9.4f - Mathf.Min(t, 1.2f) * 0.4f, 4.5f);
                float s = 8f + 2.3f * Mathf.Min(t, 2.4f);
                if (t >= 2.4f) s += Smooth(Seg(t, 2.4f, 2.6f)) * 1.2f - Smooth(Seg(t, 2.65f, 3.3f)) * 1.6f;
                FillPath(PathAcross, s, 20, 0.25f);
                Hedgehog(c, hog, 180f, t > 1.2f ? 1 : 0, 0f, t);
                Snake(c, 20, 1f, t, false);
                c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", t >= 2.4f && t < 2.8f);
                if (t > 2.6f && t < 3.5f)
                {
                    c.Label(Pts[0] + new Vector2(0f, 0.85f), "−14", Ember);
                    Flash(c, Ember, 0.25f * (1f - Seg(t, 2.6f, 3.2f)));
                }
                return;
            }
            if (t < 11.5f)
            {
                // Толкнуть носом в пруд; в воде ёж беззащитен.
                Vector2 pond = new Vector2(12.4f, 4.5f);
                Pond(c, pond, 1.7f, 1.4f);
                float ballX = 7.6f + EaseOut(Seg(t, 4.6f, 5.4f)) * 1.6f + EaseOut(Seg(t, 7.4f, 8.2f)) * 2.6f;
                bool inWater = t >= 7.85f;
                float hogX = inWater ? 11.8f - 0.45f * Mathf.Max(0f, t - 8.2f) : ballX;
                float headX = Mathf.Min(6.2f + 0.6f * (t - 3.5f), 9.6f);
                bool strike = t >= 10.2f;
                if (strike) headX += Smooth(Seg(t, 10.2f, 10.4f)) * 1f;
                FillPath(PathAcross, headX + 6f, 20, 0.2f);
                bool caught = t >= 10.4f;
                float roll = (ballX - 7.6f) * 160f;
                if (!caught) Hedgehog(c, new Vector2(hogX, 4.5f), 180f, inWater ? 3 : 1, roll, t);
                if (inWater && t < 9f) c.Ring(new Vector2(11.2f, 4.5f), 0.3f + Seg(t, 7.85f, 9f) * 1.2f, 0.06f, 1f, 0f, new Color(1f, 1f, 1f, 0.6f * (1f - Seg(t, 7.85f, 9f))));
                Snake(c, 20, 1f, t, false);
                c.Key(new Vector2(1.4f, 0.75f), "WASD", !strike);
                c.Key(new Vector2(2.9f, 0.75f), "ЛКМ", strike && t < 10.6f);
                if ((t > 4.4f && t < 5f) || (t > 7.2f && t < 7.8f)) c.Label(new Vector2(ballX - 0.4f, 5.4f), "толк", Bone);
                if (inWater && !caught) c.Label(new Vector2(hogX, 5.4f), "беззащитен", Venom);
                if (caught && t < 11.4f) c.Label(new Vector2(10.6f, 5.4f), "+ ёж", Venom);
                Cut(c, t, 3.5f);
                return;
            }
            // Ждать рядом: ёж выглядывает — укус.
            Vector2 ball = new Vector2(8.4f, 4.5f);
            bool bite = t >= 13.1f;
            float headS = 6.8f + 6f;
            if (bite) headS += Smooth(Seg(t, 13.1f, 13.3f)) * 0.9f;
            if (!bite) FillCoil(PathPoint(PathAcross, headS, 0f), 0f, 20);
            else FillPath(PathAcross, headS, 20, 0.1f);
            bool peek = t >= 12.5f && t < 13.3f;
            if (t < 13.3f) Hedgehog(c, ball, 180f, peek ? 2 : 1, 0f, t);
            Snake(c, 20, 1f, t, false);
            c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", bite && t < 13.5f);
            if (peek) c.Label(ball + new Vector2(0f, 0.9f), "выглядывает!", Venom);
            if (t >= 13.3f && t < 14.8f) c.Label(ball + new Vector2(0f, 0.9f), "+ ёж", Venom);
            Cut(c, t, 11.5f);
        }

        private static void Predators(TutorialCanvas c, float t)
        {
            if (t >= 13f)
            {
                Defence(c, t);
                return;
            }
            if (t < 6.5f)
            {
                Background(c, 0f);
                Vector2 grass = new Vector2(9f, 4.6f);
                float s = 3f + 2.8f * Mathf.Min(t, 3.6f);
                FillPath(PathEagle, s, 20, 0.25f);
                Vector2 head = Pts[0];
                Snake(c, 20, 1f, t, false);
                Grass(c, grass, 1.8f, 0.85f, 17);
                // Тень орла кружит, потом пикирует к траве и промахивается.
                float dive = Seg(t, 3.6f, 4.6f);
                Vector2 circle = head + new Vector2(Mathf.Cos(t * 1.4f), Mathf.Sin(t * 1.4f)) * 2.4f;
                Vector2 shadow = Vector2.Lerp(circle, grass + new Vector2(0.6f, 0.3f), dive);
                float climb = Seg(t, 4.6f, 6.4f);
                shadow += new Vector2(climb * 6f, climb * 3f);
                float altitude = Mathf.Lerp(1.4f, 0.15f, dive) + climb * 1.5f;
                Eagle(c, shadow, altitude, t);
                if (t > 1.2f && t < 3.4f) c.Label(shadow + new Vector2(-altitude, altitude + 0.8f), "!", Ember);
                if (t >= 4.6f && t < 6f) c.Label(grass + new Vector2(0f, 2.1f), "промах", Venom);
                Cut(c, t, 6.5f);
                return;
            }
            // Ночь: лиса идёт по следу, змея ныряет в ежевику.
            Background(c, 1f);
            Vector2 bramble = new Vector2(6.3f, 4.8f);
            float u = t - 6.5f;
            float sn = 4f + 3.1f * Mathf.Min(u, 2.6f);
            FillPath(PathFox, sn, 20, 0.25f);
            // След — точки, по которым идёт лиса.
            for (int i = 0; i < 9; i++)
            {
                Vector2 p = PathPoint(PathFox, sn - 1f - i * 0.7f, 0f);
                c.Circle(p, 0.05f, new Color(0.78f, 0.96f, 0.2f, 0.35f));
            }
            Vector2 fox = PathPoint(PathFox, Mathf.Min(-2f + 2.4f * u, 5f) - 2.6f * Seg(t, 11f, 13f) * 2f, 0f);
            float foxFacing = t < 11f ? -10f : 170f;
            Snake(c, 20, 1f, t, false);
            Bramble(c, bramble, 2.4f, 23);
            Fox(c, fox, foxFacing, t);
            Night(c, 1f);
            if (t > 9.4f && t < 11f) c.Label(fox + new Vector2(0f, 0.9f), "?", Bone);
            if (t > 8.8f) c.Label(bramble + new Vector2(0f, 2.7f), "ежевика", Venom);
            Cut(c, t, 6.5f);
            Cut(c, t, 13f);
        }

        /// <summary>Защита: шипение отгоняет лису, а «мёртвую» змею она обнюхивает и бросает.</summary>
        private static void Defence(TutorialCanvas c, float t)
        {
            float u = t - 13f;
            Background(c, 1f);
            FillPath(PathAcross, 12f, 20, 0.2f);
            Vector2 head = Pts[0];
            bool hiss = u >= 1.5f && u < 2.6f;
            bool dead = u >= 3.4f;
            // Лиса подходит, отпрыгивает от шипения, возвращается, обнюхивает «мёртвую» змею и уходит.
            float x = 15f - 5.4f * Smooth(Seg(u, 0f, 1.5f)) + 3.2f * EaseOut(Seg(u, 1.7f, 2.4f))
                      - 3.6f * Smooth(Seg(u, 3f, 4.2f)) + 9f * Smooth(Seg(u, 5.4f, 7f));
            Vector2 fox = new Vector2(x, 4.6f + Mathf.Sin(u * 7f) * 0.06f * Seg(u, 4.2f, 5.4f));
            float facing = u < 1.7f || (u >= 2.4f && u < 5.4f) ? 180f : 0f;
            Snake(c, 20, 1f, t, false, !dead);
            if (dead)
            {
                // Кверху брюхом: светлое брюхо вдоль тела и глаза-крестики.
                for (int i = 1; i < 20; i += 2) c.Circle(Pts[i], Mathf.Lerp(0.12f, 0.04f, i / 19f), BellyC);
                c.Ellipse(head, 0.32f, 0.22f, 0f, BellyC);
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 eye = head + new Vector2(0.1f, 0.12f * k);
                    c.Line(eye + new Vector2(-0.06f, -0.06f), eye + new Vector2(0.06f, 0.06f), 0.03f, DarkC);
                    c.Line(eye + new Vector2(-0.06f, 0.06f), eye + new Vector2(0.06f, -0.06f), 0.03f, DarkC);
                }
            }
            if (hiss) HissRings(c, head, Seg(u, 1.5f, 2.6f));
            Fox(c, fox, facing, t);
            Night(c, 0.85f);
            float stamina = 1f - 0.55f * Seg(u, 3.4f, 7f) - 0.15f * Seg(u, 1.5f, 1.7f);
            Bar(c, 0.6f, 8.2f, 3f, stamina, Bone);
            c.Label(new Vector2(2.1f, 8.62f), "выносливость", Bone);
            c.Key(new Vector2(1.4f, 0.75f), "Q", hiss);
            c.Key(new Vector2(2.5f, 0.75f), "X", dead && u < 5.6f);
            if (hiss) c.Label(head + new Vector2(0.2f, 1f), "ш-ш-ш!", Venom);
            if (u > 2f && u < 3f) c.Label(fox + new Vector2(0f, 1f), "отпрянула", Venom);
            if (u > 4.2f && u < 5.4f) c.Label(fox + new Vector2(0f, 1f), "?", Bone);
            if (u > 5.4f && u < 6.8f) c.Label(fox + new Vector2(0f, 1f), "потеряла интерес", Venom);
            Cut(c, t, 13f);
        }

        /// <summary>Расходящиеся кольца шипения вокруг головы.</summary>
        private static void HissRings(TutorialCanvas c, Vector2 head, float k)
        {
            for (int i = 0; i < 3; i++)
            {
                float r = 0.4f + (k * 1.6f + i * 0.45f) % 1.6f;
                c.Ring(head, r, 0.05f, 1f, 0f, new Color(0.78f, 0.96f, 0.2f, 0.8f * (1f - r / 2f)));
            }
        }

        private static void Rival(TutorialCanvas c, float t)
        {
            if (t < 4.5f)
            {
                // Гонка за трофеем: обе змеи ползут к золотой мыши, игрок успевает первой.
                Background(c, 0f);
                Vector2 trophy = new Vector2(9.3f, 4.6f);
                bool caught = t >= 3.85f;
                FillPath(PathTrophyRival, 9f + 0.95f * Mathf.Min(t, 3.85f), 20, 0.25f);
                Snake(c, 20, 1f, t, false, true, RivalC, RivalDark);
                float s = 11f + 0.9f * Mathf.Min(t, 3.6f) + Smooth(Seg(t, 3.6f, 3.85f)) * 1.1f;
                FillPath(PathTrophyPlayer, s, 20, t >= 3.6f ? 0.1f : 0.25f);
                if (!caught)
                {
                    c.Ring(trophy, 0.55f + Mathf.Sin(t * 5f) * 0.06f, 0.07f, 1f, 0f, GoldC);
                    Mouse(c, trophy, 0f, t);
                    c.Label(trophy + new Vector2(0f, 0.95f), "трофей дня", GoldC);
                }
                Snake(c, 20, 1f, t, false);
                c.Label(new Vector2(8f, 8.4f), caught ? "охота:  вы 3 : 1 она" : "охота:  вы 2 : 1 она", Bone);
                c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", t >= 3.6f && t < 3.95f);
                if (caught) c.Label(trophy + new Vector2(0f, 0.95f), "+ трофей", Venom);
                Cut(c, t, 4.5f);
                return;
            }
            if (t < 10f)
            {
                // Кража: игрок душит кролика, соперница ползёт отбить, шипение её прогоняет.
                float u = t - 4.5f;
                Background(c, 0f);
                Vector2 rabbit = new Vector2(7f, 4.4f);
                bool away = u >= 3f;
                if (!away) FillPath(PathThief, 6f + 2f * Mathf.Min(u, 2.7f), 20, 0.25f);
                else FillPath(PathThiefAway, 1f + 3.2f * (u - 3f), 20, 0.25f);
                Vector2 rivalHead = Pts[0];
                Snake(c, 20, 1f, t, false, true, RivalC, RivalDark);
                FillAround(rabbit, 0.75f, 90f + u * 12f, 24);
                Rabbit(c, rabbit + new Vector2(Mathf.Sin(t * 40f) * 0.06f, 0f), 180f);
                Snake(c, 24, 1f, t, false);
                c.Ring(rabbit, 1.35f, 0.08f, 1f, 90f, new Color(1f, 1f, 1f, 0.12f));
                c.Ring(rabbit, 1.35f, 0.08f, Seg(u, 0f, 5.5f), 90f, Venom);
                bool hiss = u >= 2.5f && u < 3.4f;
                if (hiss) HissRings(c, Pts[0], Seg(u, 2.5f, 3.4f));
                c.Key(new Vector2(1.4f, 0.75f), "F", true);
                c.Key(new Vector2(2.5f, 0.75f), "Q", hiss);
                if (u > 0.8f && u < 2.6f) c.Label(rivalHead + new Vector2(0f, 1f), "отбивает!", Ember);
                if (u > 3.2f && u < 5f) c.Label(rivalHead + new Vector2(0f, 1f), "отступила", Venom);
                Cut(c, t, 4.5f);
                Cut(c, t, 10f);
                return;
            }
            // Укус соперницы: она злится и разворачивается за вами.
            float w = t - 10f;
            Background(c, 0f);
            FillPath(PathRivalCross, 8f + 1.5f * Mathf.Min(w, 1.45f), 20, 0.25f);
            bool bitten = w >= 1.45f;
            Vector2 rival = Pts[0];
            Snake(c, 20, 1f, t, false, true, RivalC, RivalDark);
            if (bitten) c.Ring(rival, 0.5f + Mathf.Sin(t * 9f) * 0.08f, 0.06f, 1f, 0f, Ember);
            float sp = 4.6f + 1.2f * Mathf.Min(w, 1.2f) + Smooth(Seg(w, 1.2f, 1.45f)) * 1.2f - 1.4f * Seg(w, 1.6f, 3.5f);
            FillPath(PathBiteUp, sp, 20, w >= 1.2f && w < 1.6f ? 0.1f : 0.2f);
            Snake(c, 20, 1f, t, false);
            c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", w >= 1.2f && w < 1.6f);
            if (bitten && w < 2.4f)
            {
                c.Label(rival + new Vector2(0f, 1f), "укус!", Venom);
                Flash(c, Ember, 0.18f * (1f - Seg(w, 1.45f, 2f)));
            }
            if (w >= 2.4f) c.Label(rival + new Vector2(0f, 1f), "злится — охотится на вас", Ember);
            Cut(c, t, 10f);
        }

        private static void Wasps(TutorialCanvas c, float t)
        {
            Background(c, 0f);
            Vector2 nest = new Vector2(7.6f, 6.6f);
            Vector2 pond = new Vector2(12.8f, 2.4f);
            Pond(c, pond, 1.8f, 1.2f);
            if (t < 6.5f)
            {
                float s = 3.5f + 3.2f * Mathf.Min(t, 4.2f);
                FillPath(PathWasp, s, 20, 0.25f);
                Snake(c, 20, 1f, t, false);
                WaspNest(c, nest);
                float anger = Seg(t, 0.8f, 2.2f);
                if (t < 2.2f) Guards(c, nest, anger, t);
                else
                {
                    // Рой гонится за змеёй, у воды отстаёт и возвращается.
                    Vector2 chase = Vector2.Lerp(nest, Pts[0] + new Vector2(0f, 0.3f), Smooth(Seg(t, 2.2f, 3.6f)));
                    Vector2 swarm = Vector2.Lerp(chase, nest, Smooth(Seg(t, 4.6f, 6.3f)));
                    Swarm(c, swarm, t);
                    if (t < 4.5f) c.Label(swarm + new Vector2(0f, 0.9f), "осы!", Ember);
                }
                c.Key(new Vector2(1.6f, 0.75f), "SHIFT", t < 4.2f);
                if (t > 4.3f && t < 6.3f) c.Label(pond + new Vector2(0f, 1.6f), "в воде не достанут", Venom);
                return;
            }
            // Привести лису к гнезду.
            float u = t - 6.5f;
            const float lureSpeed = 3.5f;
            float sn = 3f + lureSpeed * u;
            FillPath(PathLure, sn, 20, 0.25f);
            bool stung = t >= 9.6f;
            // Где была лиса, когда её настиг рой (в 3.1 с от начала сцены).
            Vector2 stungAt = PathPoint(PathLure, 3f + lureSpeed * 3.1f - 5.2f, 0f);
            Vector2 fox = PathPoint(PathLure, Mathf.Max(0f, sn - 5.2f), 0f);
            float foxFacing = -20f;
            if (stung)
            {
                Vector2 at = stungAt;
                fox = at + new Vector2(-1f, 0.8f) * (Seg(t, 9.6f, 12f) * 6f);
                foxFacing = 150f;
            }
            Snake(c, 20, 1f, t, false);
            WaspNest(c, nest);
            if (t < 9f) Guards(c, nest, Seg(t, 7.4f, 9f), t);
            else
            {
                Vector2 swarm = Vector2.Lerp(nest, stungAt + new Vector2(0f, 0.4f), Smooth(Seg(t, 9f, 9.6f)));
                if (stung) swarm = Vector2.Lerp(swarm, fox + new Vector2(0.2f, 0.3f), 0.7f * (1f - Seg(t, 11f, 12.6f)));
                Swarm(c, swarm, t);
            }
            Fox(c, fox, foxFacing, t);
            c.Key(new Vector2(1.6f, 0.75f), "ЛКМ", t >= 8.9f && t < 9.3f);
            if (stung && t < 12.4f) c.Label(fox + new Vector2(0f, 1f), "осы прогнали лису", Venom);
            Cut(c, t, 6.5f);
        }

        private static void Warmth(TutorialCanvas c, float t)
        {
            if (t < 4.5f)
            {
                Background(c, 0f);
                Vector2 stone = new Vector2(8f, 4.6f);
                c.Ellipse(stone, 1.3f, 0.9f, 10f, StoneC);
                FillPath(PathAcross, 10f + 1.1f * Mathf.Min(t, 3f), 20, 0.22f);
                Snake(c, 20, 1f, t, false);
                float warm = Seg(t, 2.6f, 4.3f);
                Vignette(c, Color.Lerp(Frost, WarmthC, warm), 1f);
                c.Label(new Vector2(8f, 7.6f), warm < 0.5f ? "холодно — медленнее" : "прогрелась", warm < 0.5f ? Frost : Venom);
                Cut(c, t, 4.5f);
                return;
            }
            if (t < 8.5f)
            {
                Background(c, 1f);
                Vector2 spring = new Vector2(8.6f, 4.5f);
                HotSpring(c, spring, t);
                FillPath(PathAcross, 10.6f + 1.4f * Mathf.Min(t - 4.5f, 2.6f), 20, 0.22f);
                Snake(c, 20, 1f, t, false);
                Night(c, 1f);
                // Пар и тепло ключа видно и ночью.
                c.Ellipse(spring, 1.6f, 1.2f, 0f, new Color(1f, 0.7f, 0.35f, 0.12f));
                Steam(c, spring, t);
                Vignette(c, Color.Lerp(Frost, WarmthC, Seg(t, 6.2f, 8f)), 1f);
                if (t > 6.6f) c.Label(spring + new Vector2(0f, 1.9f), "тёплый ключ", Venom);
                Cut(c, t, 4.5f);
                Cut(c, t, 8.5f);
                return;
            }
            Background(c, 0.4f);
            Vector2 burrow = new Vector2(9f, 4.5f);
            Burrow(c, burrow);
            float enter = Seg(t, 9.8f, 11f);
            FillPath(PathAcross, 13.5f + 1.2f * Mathf.Min(t - 8.5f, 1.3f) + enter * 4.5f, 20, 0.2f);
            // Ушедшая в нору часть тела не видна.
            int hidden = HideBeyond(burrow.x - 0.2f, 20);
            Snake(c, 20 - hidden, 1f, t, false, hidden == 0);
            c.Ellipse(burrow + new Vector2(0.1f, 0f), 0.34f, 0.22f, 0f, HoleC);
            Night(c, 0.4f);
            c.Key(new Vector2(1.6f, 0.75f), "E", t >= 9.6f && t < 10.2f);
            if (t > 10.8f) c.Label(burrow + new Vector2(0f, 1.3f), "сохранено", Venom);
            Cut(c, t, 8.5f);
        }

        private static void Lineage(TutorialCanvas c, float t)
        {
            if (t < 4f)
            {
                Background(c, 0f);
                // Жуки один за другим исчезают — змея растёт; потом линька: старая кожа остаётся лежать.
                int segments = 14 + Mathf.FloorToInt(Seg(t, 0.3f, 2.6f) * 10f);
                // Голова идёт от x = 3 к x = 8, после линьки ползёт дальше, а старая кожа остаётся на месте.
                const float moltS = 3f + 1.8f * 2.8f + 6f;
                float grow = 9f + 1.8f * Mathf.Min(t, 2.8f) + 1.2f * Mathf.Max(0f, t - 2.8f);
                for (int i = 0; i < 5; i++)
                {
                    float bx = 4.4f + i * 0.9f;
                    if (PathPoint(PathAcross, grow, 0f).x < bx) c.Circle(new Vector2(bx, 4.5f + Mathf.Sin(i * 2.1f) * 0.15f), 0.11f, DarkC);
                }
                if (t >= 2.8f)
                {
                    FillPath(PathAcross, moltS, segments, 0.25f);
                    Snake(c, segments, 0.35f * (1f - Seg(t, 3.6f, 4f)), t, false);
                }
                FillPath(PathAcross, grow, segments, 0.25f);
                Snake(c, segments, 1f, t, false);
                if (t > 2.8f)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 0.785f + t * 2f;
                        c.Circle(Pts[3] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.4f + Seg(t, 2.8f, 3.8f) * 0.8f), 0.05f, new Color(0.9f, 1f, 0.5f, 1f - Seg(t, 2.8f, 4f)));
                    }
                    c.Label(Pts[0] + new Vector2(0f, 1f), "линька: улучшение", Venom);
                }
                Cut(c, t, 4f);
                return;
            }
            Background(c, 1f);
            Vector2 nest = new Vector2(9f, 4.5f);
            bool laid = t >= 6.2f;
            int eggs = t < 12f ? 5 : 0;
            c.Ring(nest, 0.75f, 0.28f, 1f, 0f, DirtC);
            c.Circle(nest, 0.62f, new Color(0.3f, 0.22f, 0.14f));
            if (laid && eggs > 0)
                for (int i = 0; i < eggs; i++)
                    c.Ellipse(nest + Dir(i * 72f + 20f) * 0.3f, 0.16f, 0.12f, i * 72f, EggC);
            if (!laid) c.Ring(nest, 1.05f + Mathf.Sin(t * 4f) * 0.08f, 0.06f, 1f, 0f, new Color(0.78f, 0.96f, 0.2f, 0.7f));
            // Подползти к гнезду, свернуться у кладки, броском встретить лису.
            // Свернувшись рядом, а не на яйцах: бросок — выпад головы из колец в сторону лисы.
            float s = 6.9f + 3f * Mathf.Min(t - 4f, 2f);
            if (t >= 6f)
            {
                Vector2 coil = PathPoint(PathNest, 12.9f, 0f);
                const float facing = -17f;
                FillCoil(coil, facing, 20);
                float lunge = Smooth(Seg(t, 10.1f, 10.3f)) * (1f - Seg(t, 10.8f, 11.4f));
                Vector2 f = Dir(facing);
                Pts[0] += f * (lunge * 1.3f);
                Pts[1] += f * (lunge * 0.9f);
                Pts[2] += f * (lunge * 0.4f);
            }
            else FillPath(PathNest, s, 20, 0.25f);
            // Лиса приходит за яйцами и убегает от броска.
            if (t >= 9f && t < 12.5f)
            {
                float away = Seg(t, 10.35f, 12f);
                Vector2 fox = Vector2.Lerp(new Vector2(16f, 5.2f), new Vector2(10.3f, 5.5f), Smooth(Seg(t, 9f, 10.2f))) + new Vector2(away * 6f, away * 1.5f);
                Fox(c, fox, away > 0f ? 10f : 190f, t);
                if (away > 0f && away < 0.9f) c.Label(fox + new Vector2(0f, 1f), "отогнана", Venom);
            }
            Snake(c, 20, 1f, t, false);
            // Змеёныши расползаются из гнезда.
            if (t >= 12f)
            {
                float k = Seg(t, 12f, 13f);
                for (int i = 0; i < 5; i++)
                {
                    Vector2 dir = Dir(i * 72f + 40f);
                    Vector2 p = nest + dir * (0.2f + k * 1.6f);
                    for (int j = 0; j < 4; j++)
                        c.Circle(p - dir * (j * 0.1f) + new Vector2(-dir.y, dir.x) * Mathf.Sin(t * 12f + j) * 0.04f, 0.07f - j * 0.012f, SnakeC);
                }
                c.Label(nest + new Vector2(0f, 1.6f), "новое поколение", Venom);
            }
            Night(c, 0.85f);
            c.Key(new Vector2(1.4f, 0.75f), "E", t >= 6f && t < 6.6f);
            c.Key(new Vector2(2.7f, 0.75f), "ЛКМ", t >= 10.1f && t < 10.5f);
            if (laid && t < 8.6f) c.Label(nest + new Vector2(0f, 1.4f), "кладка", Venom);
            Cut(c, t, 4f);
        }

        // =====================================================================
        // Змея
        // =====================================================================

        /// <summary>Точка маршрута на расстоянии s от начала; змейка виляет поперёк маршрута с амплитудой amp.</summary>
        private static Vector2 PathPoint(Vector2[] path, float s, float amp)
        {
            Vector2 basePoint;
            Vector2 tangent;
            if (s <= 0f)
            {
                tangent = (path[1] - path[0]).normalized;
                basePoint = path[0] + tangent * s;
            }
            else
            {
                float remaining = s;
                basePoint = path[path.Length - 1];
                tangent = (path[path.Length - 1] - path[path.Length - 2]).normalized;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 d = path[i + 1] - path[i];
                    float len = d.magnitude;
                    if (remaining <= len || i == path.Length - 2)
                    {
                        tangent = d / len;
                        basePoint = path[i] + tangent * remaining;
                        break;
                    }
                    remaining -= len;
                }
            }
            return basePoint + new Vector2(-tangent.y, tangent.x) * (amp * Mathf.Sin(s * 2.2f));
        }

        private static void FillPath(Vector2[] path, float s, int count, float amp)
        {
            for (int i = 0; i < count; i++) Pts[i] = PathPoint(path, s - i * GAP, amp);
        }

        /// <summary>Змея свернулась для замаха: голова приподнята к цели, тело — спиралью.</summary>
        private static void FillCoil(Vector2 center, float facing, int count)
        {
            Vector2 f = Dir(facing);
            Pts[0] = center + f * 0.42f;
            Pts[1] = center + f * 0.18f;
            for (int i = 2; i < count; i++) Pts[i] = center + Dir(facing + 200f + (i - 2) * 38f) * (0.24f + (i - 2) * 0.02f);
        }

        /// <summary>Кольца вокруг добычи (обвивание).</summary>
        private static void FillAround(Vector2 center, float radius, float start, int count)
        {
            for (int i = 0; i < count; i++) Pts[i] = center + Dir(start - i * 26f) * (radius + i * 0.018f);
        }

        /// <summary>
        /// Змея вползает в нору: сегменты правее x скрыты. Видимые сдвигаются в начало буфера;
        /// возвращает, сколько сегментов (считая от головы) уже под землёй.
        /// </summary>
        private static int HideBeyond(float x, int count)
        {
            int hidden = 0;
            while (hidden < count && Pts[hidden].x > x) hidden++;
            for (int i = hidden; i < count; i++) Pts[i - hidden] = Pts[i];
            if (hidden > 0 && hidden < count) Pts[0] = new Vector2(x, Pts[0].y);
            return hidden;
        }

        private static void Snake(TutorialCanvas c, int count, float alpha, float t, bool tongue, bool drawHead = true)
        {
            Snake(c, count, alpha, t, tongue, drawHead, SnakeC, SnakeDark);
        }

        /// <summary>Змея своего цвета (соперница — рыжая).</summary>
        private static void Snake(TutorialCanvas c, int count, float alpha, float t, bool tongue, bool drawHead, Color bodyColor, Color darkColor)
        {
            if (alpha <= 0.01f || count < 2) return;
            Color body = A(bodyColor, alpha), dark = A(darkColor, alpha);
            for (int i = count - 1; i >= 1; i--)
            {
                float k = (float)i / (count - 1);
                float r = Mathf.Lerp(0.2f, 0.06f, k);
                c.Line(Pts[i], Pts[i - 1], r * 2f, body);
                c.Circle(Pts[i], r, body);
                if (i % 3 == 0) c.Circle(Pts[i], r * 0.5f, dark);
            }
            if (!drawHead)
            {
                c.Circle(Pts[0], 0.2f, body);
                return;
            }
            Vector2 dir = Pts[0] - Pts[1];
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (tongue)
            {
                Vector2 tip = Pts[0] + dir * 0.6f;
                c.Line(Pts[0] + dir * 0.25f, tip, 0.04f, A(TongueC, alpha));
                c.Line(tip, tip + Rotate(dir, 32f) * 0.13f, 0.035f, A(TongueC, alpha));
                c.Line(tip, tip + Rotate(dir, -32f) * 0.13f, 0.035f, A(TongueC, alpha));
            }
            c.Ellipse(Pts[0], 0.32f, 0.22f, angle, body);
            Vector2 side = new Vector2(-dir.y, dir.x);
            c.Circle(Pts[0] + dir * 0.1f + side * 0.12f, 0.055f, A(EyeC, alpha));
            c.Circle(Pts[0] + dir * 0.1f - side * 0.12f, 0.055f, A(EyeC, alpha));
        }

        private static bool Flick(float t)
        {
            float k = t * 0.9f;
            return k - Mathf.Floor(k) < 0.18f;
        }

        // =====================================================================
        // Звери и места
        // =====================================================================

        private static void Mouse(TutorialCanvas c, Vector2 p, float facing, float t)
        {
            Vector2 f = Dir(facing);
            Vector2 side = new Vector2(-f.y, f.x);
            c.Line(p - f * 0.2f, p - f * 0.55f + side * Mathf.Sin(t * 8f) * 0.1f, 0.04f, PinkC);
            c.Ellipse(p, 0.28f, 0.17f, facing, MouseC);
            c.Circle(p + f * 0.12f + side * 0.12f, 0.08f, PinkC);
            c.Circle(p + f * 0.12f - side * 0.12f, 0.08f, PinkC);
            c.Circle(p + f * 0.27f, 0.035f, DarkC);
        }

        private static void Frog(TutorialCanvas c, Vector2 p, float hop)
        {
            float s = 1f + hop * 0.25f;
            c.Ellipse(p + new Vector2(0.06f, -0.12f), 0.24f * s, 0.12f * s, 0f, ShadowC);
            c.Ellipse(p, 0.26f * s, 0.2f * s, 0f, FrogC);
            c.Circle(p + new Vector2(-0.12f, 0.15f) * s, 0.07f * s, FrogC);
            c.Circle(p + new Vector2(0.12f, 0.15f) * s, 0.07f * s, FrogC);
            c.Circle(p + new Vector2(-0.12f, 0.16f) * s, 0.035f * s, DarkC);
            c.Circle(p + new Vector2(0.12f, 0.16f) * s, 0.035f * s, DarkC);
        }

        private static void Rabbit(TutorialCanvas c, Vector2 p, float facing)
        {
            Vector2 f = Dir(facing);
            Vector2 side = new Vector2(-f.y, f.x);
            c.Circle(p - f * 0.42f, 0.11f, WhiteC);
            c.Ellipse(p, 0.45f, 0.28f, facing, RabbitC);
            c.Circle(p + f * 0.38f, 0.2f, RabbitC);
            c.Ellipse(p + f * 0.2f + side * 0.16f, 0.26f, 0.06f, facing + 160f, RabbitC);
            c.Ellipse(p + f * 0.2f - side * 0.16f, 0.26f, 0.06f, facing - 160f, RabbitC);
            c.Circle(p + f * 0.5f + side * 0.08f, 0.035f, DarkC);
            c.Circle(p + f * 0.5f - side * 0.08f, 0.035f, DarkC);
        }

        /// <summary>Ёж: mode 0 — идёт, 1 — свёрнут (roll — поворот шара), 2 — выглядывает, 3 — плывёт.</summary>
        private static void Hedgehog(TutorialCanvas c, Vector2 p, float facing, int mode, float roll, float t)
        {
            Vector2 f = Dir(facing);
            if (mode == 3)
            {
                c.Ring(p, 0.45f + Mathf.Sin(t * 5f) * 0.05f, 0.04f, 1f, 0f, WaterRim);
                c.Circle(p + f * 0.28f, 0.11f, FaceC);
                c.Ellipse(p, 0.32f, 0.24f, facing, HedgehogC);
                Spikes(c, p, 0.26f, 10, facing + 180f, 180f, t);
                return;
            }
            if (mode == 1 || mode == 2)
            {
                if (mode == 2) c.Circle(p + f * (0.34f + Mathf.Sin(t * 20f) * 0.02f), 0.12f, FaceC);
                if (mode == 2) c.Circle(p + f * 0.44f, 0.04f, DarkC);
                c.Circle(p, 0.33f, SpikeC);
                Spikes(c, p, 0.3f, 16, roll, 360f, t);
                c.Circle(p, 0.22f, HedgehogC);
                return;
            }
            c.Circle(p + f * 0.32f, 0.13f, FaceC);
            c.Circle(p + f * 0.45f, 0.04f, DarkC);
            c.Ellipse(p, 0.36f, 0.27f, facing, HedgehogC);
            Spikes(c, p, 0.3f, 12, facing + 180f, 200f, t);
        }

        private static void Spikes(TutorialCanvas c, Vector2 p, float r, int count, float center, float span, float t)
        {
            for (int i = 0; i < count; i++)
            {
                float a = center - span * 0.5f + span * (i + 0.5f) / count;
                Vector2 d = Dir(a);
                Vector2 side = new Vector2(-d.y, d.x) * 0.05f;
                c.Triangle(p + d * (r * 0.6f) + side, p + d * (r * 0.6f) - side, p + d * (r + 0.14f), SpikeC);
            }
        }

        private static void Fox(TutorialCanvas c, Vector2 p, float facing, float t)
        {
            Vector2 f = Dir(facing);
            Vector2 side = new Vector2(-f.y, f.x);
            float sway = Mathf.Sin(t * 6f) * 0.12f;
            Vector2 tail = p - f * 0.75f + side * sway;
            c.Ellipse(tail, 0.36f, 0.14f, facing + sway * 60f, FoxC);
            c.Circle(tail - f * 0.3f, 0.09f, WhiteC);
            c.Ellipse(p, 0.55f, 0.24f, facing, FoxC);
            Vector2 head = p + f * 0.55f;
            c.Circle(head, 0.22f, FoxC);
            c.Triangle(head + side * 0.18f, head + side * 0.06f, head + side * 0.2f - f * 0.22f, DarkC);
            c.Triangle(head - side * 0.18f, head - side * 0.06f, head - side * 0.2f - f * 0.22f, DarkC);
            c.Ellipse(head + f * 0.2f, 0.14f, 0.09f, facing, WhiteC);
            c.Circle(head + f * 0.32f, 0.04f, DarkC);
        }

        /// <summary>Орёл: тень на земле в точке ground и сама птица выше-левее, тем дальше, чем выше летит.</summary>
        private static void Eagle(TutorialCanvas c, Vector2 ground, float altitude, float t)
        {
            float flap = Mathf.Sin(t * 5f) * 0.1f;
            c.Ellipse(ground, 0.9f, 0.3f, 0f, ShadowC);
            Vector2 p = ground + new Vector2(-altitude * 0.6f, altitude);
            c.Ellipse(p + new Vector2(-0.45f, flap), 0.55f, 0.16f, 8f, EagleC);
            c.Ellipse(p + new Vector2(0.45f, flap), 0.55f, 0.16f, -8f, EagleC);
            c.Ellipse(p, 0.2f, 0.3f, 0f, EagleC);
            c.Circle(p + new Vector2(0f, 0.3f), 0.12f, WhiteC);
        }

        private static void WaspNest(TutorialCanvas c, Vector2 p)
        {
            c.Circle(p + new Vector2(0.05f, -0.45f), 0.32f, new Color(0.36f, 0.26f, 0.16f));
            c.Ellipse(p, 0.34f, 0.44f, 0f, PaperC);
            for (int i = -1; i <= 1; i++) c.Line(p + new Vector2(-0.3f, i * 0.14f), p + new Vector2(0.3f, i * 0.14f), 0.04f, new Color(0.45f, 0.41f, 0.35f));
            c.Circle(p + new Vector2(0f, -0.3f), 0.07f, HoleC);
        }

        private static void Guards(TutorialCanvas c, Vector2 nest, float anger, float t)
        {
            float speed = Mathf.Lerp(2.2f, 8f, anger), radius = Mathf.Lerp(0.55f, 1.1f, anger);
            for (int i = 0; i < 2; i++)
            {
                float a = t * speed + i * Mathf.PI;
                Wasp(c, nest + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.6f));
            }
        }

        private static void Swarm(TutorialCanvas c, Vector2 center, float t)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = t * (7f + i * 0.4f) + i * 1.7f;
                Wasp(c, center + new Vector2(Mathf.Sin(a) * 0.55f, Mathf.Cos(a * 1.3f) * 0.4f));
            }
        }

        private static void Wasp(TutorialCanvas c, Vector2 p)
        {
            c.Circle(p, 0.075f, WaspC);
            c.Circle(p + new Vector2(0.05f, 0f), 0.035f, DarkC);
        }

        private static void Pond(TutorialCanvas c, Vector2 center, float rx, float ry)
        {
            c.Ellipse(center, rx + 0.12f, ry + 0.12f, 0f, WaterRim);
            c.Ellipse(center, rx, ry, 0f, WaterC);
        }

        private static void Grass(TutorialCanvas c, Vector2 center, float radius, float alpha, int seed)
        {
            Color g = A(GrassC, alpha);
            Color shade = A(new Color(0.2f, 0.3f, 0.12f), alpha * 0.7f);
            c.Ellipse(center, radius, radius * 0.8f, 0f, A(MeadowDark, alpha * 0.6f));
            for (int i = 0; i < 26; i++)
            {
                float a = Hash(i, seed) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Hash(seed, i + 7)) * radius;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
                c.Triangle(p + new Vector2(-0.14f, 0f), p + new Vector2(-0.02f, 0f), p + new Vector2(-0.18f, 0.32f), shade);
                c.Triangle(p + new Vector2(-0.06f, 0f), p + new Vector2(0.06f, 0f), p + new Vector2(0f, 0.42f), g);
                c.Triangle(p + new Vector2(0.02f, 0f), p + new Vector2(0.14f, 0f), p + new Vector2(0.2f, 0.3f), g);
            }
        }

        private static void Bramble(TutorialCanvas c, Vector2 center, float radius, int seed)
        {
            for (int i = 0; i < 22; i++)
            {
                float a = Hash(i, seed) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Hash(seed, i + 3)) * radius;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
                c.Circle(p, 0.32f + Hash(i, seed + 1) * 0.15f, BrambleC);
            }
            for (int i = 0; i < 14; i++)
            {
                float a = Hash(i + 40, seed) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Hash(seed, i + 50)) * radius;
                c.Circle(center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f), 0.07f, BerryC);
            }
        }

        private static void HotSpring(TutorialCanvas c, Vector2 p, float t)
        {
            c.Ellipse(p, 1.3f, 0.95f, 0f, MineralC);
            c.Ellipse(p, 0.85f, 0.6f, 0f, SpringC);
        }

        private static void Steam(TutorialCanvas c, Vector2 p, float t)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = t * 0.4f + i / 6f;
                k -= Mathf.Floor(k);
                Vector2 q = p + new Vector2(Mathf.Sin(i * 2.3f + t) * 0.4f, k * 2f);
                c.Circle(q, 0.2f + k * 0.35f, new Color(1f, 1f, 1f, 0.28f * (1f - k)));
            }
        }

        private static void Burrow(TutorialCanvas c, Vector2 p)
        {
            c.Ellipse(p + new Vector2(0.35f, 0f), 0.95f, 0.75f, 0f, DirtC);
        }

        // =====================================================================
        // Фон, оверлеи, шкалы
        // =====================================================================

        private static void Background(TutorialCanvas c, float night)
        {
            c.Rect(0f, 0f, TutorialCanvas.WIDTH, TutorialCanvas.HEIGHT, Meadow);
            // Пятна более тёмной травы — чтобы луг не был плоской заливкой.
            for (int i = 0; i < 16; i++)
            {
                var p = new Vector2(Hash(i, 3) * 16f, Hash(7, i) * 9f);
                c.Ellipse(p, 0.8f + Hash(i, 11) * 1.2f, 0.5f + Hash(13, i) * 0.6f, Hash(i, 17) * 180f, MeadowDark);
            }
        }

        private static void Night(TutorialCanvas c, float amount)
        {
            c.Rect(0f, 0f, TutorialCanvas.WIDTH, TutorialCanvas.HEIGHT, A(NightC, NightC.a * amount));
        }

        private static void Vignette(TutorialCanvas c, Color color, float amount)
        {
            Color edge = A(color, color.a * amount);
            Color none = A(color, 0f);
            const float w = 2.2f;
            c.RectGradient(0f, 0f, 16f, w, edge, none);
            c.RectGradient(0f, 9f - w, 16f, 9f, none, edge);
            c.RectGradientH(0f, 0f, w, 9f, edge, none);
            c.RectGradientH(16f - w, 0f, 16f, 9f, none, edge);
        }

        private static void Flash(TutorialCanvas c, Color color, float alpha)
        {
            if (alpha > 0f) c.Rect(0f, 0f, 16f, 9f, A(color, alpha));
        }

        /// <summary>Монтажная склейка: короткое затемнение вокруг момента at.</summary>
        private static void Cut(TutorialCanvas c, float t, float at)
        {
            float a = 1f - Mathf.Abs(t - at) / 0.3f;
            if (a > 0f) c.Rect(0f, 0f, 16f, 9f, new Color(0f, 0f, 0f, a));
        }

        private static void Bar(TutorialCanvas c, float x, float y, float width, float fill, Color color)
        {
            c.Rect(x, y, x + width, y + 0.12f, new Color(1f, 1f, 1f, 0.18f));
            c.Rect(x, y, x + width * Mathf.Clamp01(fill), y + 0.12f, color);
        }

        // =====================================================================
        // Математика
        // =====================================================================

        private static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
        private static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>Детерминированный «случайный» 0..1: кадр одинаков при каждом показе.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
