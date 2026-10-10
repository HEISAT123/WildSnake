namespace Snake3D
{
    /// <summary>Откуда пришёл урон — для экрана итогов («отчего погибла»).</summary>
    public enum DamageSource
    {
        /// <summary>Удар о стену, камень, дерево или своё тело.</summary>
        Collision,
        /// <summary>Столкновение со змеёй-соперницей.</summary>
        Rival,
        /// <summary>Удар орла.</summary>
        Eagle,
        /// <summary>Голод.</summary>
        Starvation,
        /// <summary>Укус лисы.</summary>
        Fox,
        /// <summary>Колючки свернувшегося ежа.</summary>
        Hedgehog,
        /// <summary>Птица, защищающая гнездо.</summary>
        Bird
    }
}
