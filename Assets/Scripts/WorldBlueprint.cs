using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Авторский план мира: где лежат регионы, вода, укрытия, норы, цели и опасности. GridManager строит по нему поле,
    /// а сид решает только мелочи (форма берегов, брёвна, камни, пучки травы), поэтому карта всегда спроектирована, а не случайна.
    /// Координаты — в клетках от центра мира (центр — старт), x — на восток, y — на север (вверх на карте). Мир — круг
    /// радиусом ~78 клеток в кольце гор; всё важное лежит не дальше ~65 клеток, чтобы не упираться в склоны.
    ///
    /// Замысел:
    /// • Старт — тихий луг: рядом нора с камнем, рощица-укрытие от орла, ежевика и птичье гнездо — всё «учебное» под рукой.
    /// • Река с северо-востока в озеро на северо-западе — рубеж мира. За ней Северное поле с гнездом для кладки: туда ведут
    ///   три моста (у среднего — осы), брод вплавь (медленно и холодно) и долгий обход вокруг озера.
    /// • У гнезда собрано всё для финала: нора (сохраниться перед кладкой), тёплый ключ (ночью охранять кладку не замёрзнув),
    ///   мышиный городок (наесться до нужной сытости) и треугольник ежевики — убежище, куда лиса не полезет.
    /// • Восток — каменная гряда: солнечные камни, но открыто — земля орла; на подходе — осы, мышиный городок на открытом лугу.
    /// • Юг — Старый лес: кроны прячут от орла, по опушкам птичьи гнёзда, лесное озерцо; ночью здесь хозяйничает лиса.
    /// • Запад — болото у озера (лягушки, рыба, тёплый ключ на краю) и березняк.
    /// • Между укрытиями — тропы высокой травы: тихий путь длиннее, открытая прямая — быстрее, но под орлом.
    /// • Норы — сеть безопасных точек примерно через 25–35 клеток, у каждой — утренний солнечный камень.
    /// </summary>
    public static class WorldBlueprint
    {
        /// <summary>Меняется вместе с планом — поле пересобирается.</summary>
        public const int VERSION = 2;

        /// <summary>Большое озеро (x, y, радиус): в него впадает река.</summary>
        public static readonly Vector3 Lake = new Vector3(-44f, 26f, 11f);

        /// <summary>Пруды (x, y, радиус): водопой у старта, лесное озерцо, юго-восточный пруд, пруд Северного поля.</summary>
        public static readonly Vector3[] Ponds =
        {
            new Vector3(20f, -14f, 4.2f), new Vector3(-14f, -36f, 4f), new Vector3(46f, -32f, 4.5f), new Vector3(-6f, 52f, 3.8f), new Vector3(-58f, 2f, 3.5f)
        };

        /// <summary>Опорные точки русла: от подножия гор на северо-востоке к озеру.</summary>
        public static readonly Vector2[] River =
        {
            new Vector2(52f, 68f), new Vector2(45f, 51f), new Vector2(33f, 37f), new Vector2(17f, 31f), new Vector2(2f, 34f),
            new Vector2(-12f, 31f), new Vector2(-26f, 28f), new Vector2(-36f, 27f), new Vector2(-44f, 26f)
        };

        /// <summary>Где через реку лежат брёвна-мосты (ближайшие точки русла).</summary>
        public static readonly Vector2[] Bridges = { new Vector2(30f, 35f), new Vector2(2f, 34f), new Vector2(-20f, 29f) };

        /// <summary>Болото у озера (x, y, радиус).</summary>
        public static readonly Vector3 Swamp = new Vector3(-28f, 6f, 12f);

        /// <summary>Каменная гряда (x, y, радиус).</summary>
        public static readonly Vector3 Ridge = new Vector3(44f, 2f, 11f);

        /// <summary>Рощи (x, y, радиус, вид: 0 — смешанная, 1 — березняк, 2 — густой Старый лес). Три южные сливаются в один лес.</summary>
        public static readonly Vector4[] Groves =
        {
            new Vector4(-4f, -46f, 12f, 2f), new Vector4(-26f, -40f, 10f, 1f), new Vector4(16f, -50f, 9f, 2f),
            new Vector4(-56f, -16f, 9f, 1f), new Vector4(-22f, 52f, 9f, 0f), new Vector4(62f, -12f, 7f, 1f),
            new Vector4(40f, 58f, 7f, 0f), new Vector4(-14f, 14f, 6f, 0f)
        };

        /// <summary>Норы: у старта, по краям регионов и у гнезда — не дальше ~30 клеток друг от друга.</summary>
        public static readonly Vector2[] Burrows =
        {
            new Vector2(8f, 6f), new Vector2(-18f, -14f), new Vector2(24f, -26f), new Vector2(2f, -30f), new Vector2(30f, 16f),
            new Vector2(-44f, 46f), new Vector2(6f, 44f), new Vector2(46f, 38f), new Vector2(56f, -42f), new Vector2(-50f, -34f)
        };

        /// <summary>Гнездо для кладки — Северное поле, за рекой.</summary>
        public static readonly Vector2 Nest = new Vector2(20f, 48f);

        /// <summary>Тёплые ключи: у гнезда (ночная охрана кладки), на краю болота, за грядой.</summary>
        public static readonly Vector2[] HotSprings = { new Vector2(11f, 53f), new Vector2(-40f, -6f), new Vector2(58f, 8f) };

        /// <summary>Мышиные городки: еда у гнезда, на открытом лугу у гряды, на юго-западном лугу, на севере.</summary>
        public static readonly Vector2[] MouseColonies = { new Vector2(30f, 54f), new Vector2(22f, 2f), new Vector2(-36f, -24f), new Vector2(-4f, 61f) };

        /// <summary>Осиные гнёзда (одинокие пни): у моста, на короткой дороге к гряде, у юго-западного городка, у Северного поля.</summary>
        public static readonly Vector2[] WaspNests = { new Vector2(8f, 24f), new Vector2(34f, -6f), new Vector2(-38f, -32f), new Vector2(34f, 46f) };

        /// <summary>Заросли ежевики (x, y, радиус): треугольник у гнезда, островки на открытых местах, опушки.</summary>
        public static readonly Vector3[] Brambles =
        {
            new Vector3(14f, 43f, 2.4f), new Vector3(27f, 44f, 2.4f), new Vector3(22f, 55f, 2.2f), new Vector3(-14f, 41f, 2.6f),
            new Vector3(-30f, -28f, 2.5f), new Vector3(24f, -38f, 2.5f), new Vector3(-9f, 4f, 2f), new Vector3(33f, -18f, 2.4f),
            new Vector3(52f, 22f, 2.3f)
        };

        /// <summary>Птичьи гнёзда — по опушкам и в траве (ищется ближайшее укрытое место).</summary>
        public static readonly Vector2[] BirdNests =
        {
            new Vector2(-7f, 17f), new Vector2(8f, -37f), new Vector2(-38f, -36f), new Vector2(-48f, -10f),
            new Vector2(-24f, 43f), new Vector2(55f, -6f), new Vector2(38f, 24f), new Vector2(-18f, -28f)
        };

        /// <summary>Тропы высокой травы между укрытиями (ломаные).</summary>
        public static readonly Vector2[][] CoverTrails =
        {
            new[] { new Vector2(-1f, 7f), new Vector2(-5f, 16f), new Vector2(-2f, 25f), new Vector2(0f, 30f) },
            new[] { new Vector2(-2f, -7f), new Vector2(-6f, -18f), new Vector2(-3f, -30f) },
            new[] { new Vector2(-7f, -3f), new Vector2(-15f, -10f) },
            new[] { new Vector2(10f, 10f), new Vector2(20f, 14f), new Vector2(28f, 19f) },
            new[] { new Vector2(4f, 38f), new Vector2(9f, 42f) },
            new[] { new Vector2(-34f, 40f), new Vector2(-30f, 47f), new Vector2(-26f, 44f) },
            new[] { new Vector2(28f, -22f), new Vector2(38f, -26f), new Vector2(50f, -38f) },
            new[] { new Vector2(-24f, -18f), new Vector2(-40f, -26f), new Vector2(-48f, -32f) }
        };

        /// <summary>Открытые поля (x, y, радиус): там почти нет травы и брёвен — быстро, но под орлом.</summary>
        public static readonly Vector3[] OpenFields =
        {
            new Vector3(24f, -2f, 11f), new Vector3(16f, 50f, 9f), new Vector3(40f, -18f, 8f), new Vector3(-34f, -22f, 7f)
        };

        /// <summary>Подписи регионов на карте журнала (x, y).</summary>
        public static readonly (string name, Vector2 at)[] Regions =
        {
            ("СТАРТОВЫЙ ЛУГ", new Vector2(0f, -8f)), ("ОЗЕРО", new Vector2(-44f, 19f)), ("СЕВЕРНОЕ ПОЛЕ", new Vector2(6f, 66f)),
            ("БОЛОТО", new Vector2(-28f, 0f)), ("КАМЕННАЯ ГРЯДА", new Vector2(46f, -15f)), ("СТАРЫЙ ЛЕС", new Vector2(-2f, -60f)),
            ("БЕРЕЗНЯК", new Vector2(-58f, -27f)), ("РЕКА", new Vector2(26f, 26f))
        };
    }
}
