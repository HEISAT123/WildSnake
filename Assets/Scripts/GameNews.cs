namespace WildSnake
{
    /// <summary>События соперничества, трофея и новых умений змеи — для баннеров (GameManager.News).</summary>
    public enum GameNews
    {
        /// <summary>Соперница побеждена и уползла зализывать раны.</summary>
        RivalDefeated,
        /// <summary>Соперница вернулась — не с нуля, а почти прежней длины.</summary>
        RivalReturned,
        /// <summary>Соперница идёт отбивать добычу, которую душит игрок.</summary>
        StealStarted,
        /// <summary>Соперница отбила добычу.</summary>
        RivalStole,
        /// <summary>Шипение отогнало соперницу.</summary>
        RivalYielded,
        /// <summary>Соперница затаила обиду и охотится на игрока.</summary>
        RivalAngered,
        /// <summary>Игрок поймал добычу у соперницы под носом.</summary>
        PreyStolen,
        /// <summary>Появился трофей дня.</summary>
        TrophyAppeared,
        /// <summary>Трофей дня достался игроку.</summary>
        TrophyPlayer,
        /// <summary>Трофей дня перехватила соперница.</summary>
        TrophyRival,
        /// <summary>Трофей дня ушёл.</summary>
        TrophyLost,
        /// <summary>Игрок выиграл дневное состязание.</summary>
        ContestWon,
        /// <summary>Соперница выиграла дневное состязание.</summary>
        ContestLost,
        /// <summary>Дневное состязание — ничья.</summary>
        ContestTie,
        /// <summary>Хищник обманут «мёртвой змеёй».</summary>
        PredatorFooled,
        /// <summary>Шипение отогнало лису.</summary>
        FoxHissed
    }
}
