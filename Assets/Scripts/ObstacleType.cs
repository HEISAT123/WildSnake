namespace Snake3D
{
    /// <summary>Тип препятствия в клетке поля.</summary>
    public enum ObstacleType : byte
    {
        None,
        /// <summary>Низкая каменная стенка — можно перепрыгнуть.</summary>
        LowWall,
        /// <summary>Высокая колонна — только объехать.</summary>
        Pillar,
        /// <summary>Большой блок 2x2 — только объехать.</summary>
        Block
    }
}
