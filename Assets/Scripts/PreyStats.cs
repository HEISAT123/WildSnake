using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>Параметры одного вида добычи (настраиваются в GameConfig).</summary>
    [Serializable]
    public class PreyStats
    {
        [Tooltip("Сколько сытости даёт")]
        [Min(0f)] public float Satiety = 10f;

        [Tooltip("На сколько клеток удлиняет змею")]
        [Min(0f)] public float Growth = 0.5f;

        [Tooltip("Скорость спокойного передвижения, клеток в секунду")]
        [Min(0f)] public float WalkSpeed = 1f;

        [Tooltip("Скорость бегства, клеток в секунду")]
        [Min(0f)] public float FleeSpeed = 4f;

        [Tooltip("С какого расстояния замечает змею, клеток (0 — не замечает)")]
        [Min(0f)] public float AwareRadius = 4f;

        [Tooltip("Сколько секунд длится бегство, потом добыча устаёт")]
        [Min(0.1f)] public float FleeTime = 2f;

        [Tooltip("Поймать можно только броском (Shift), касание лишь спугивает")]
        public bool RequiresStrike = true;

        [Tooltip("Сколько особей на поле днём")]
        [Min(0)] public int DayCount = 3;

        [Tooltip("Сколько особей на поле ночью")]
        [Min(0)] public int NightCount = 3;

        public PreyStats() { }

        public PreyStats(float satiety, float growth, float walk, float flee, float aware, float fleeTime, bool strike, int day, int night)
        {
            Satiety = satiety;
            Growth = growth;
            WalkSpeed = walk;
            FleeSpeed = flee;
            AwareRadius = aware;
            FleeTime = fleeTime;
            RequiresStrike = strike;
            DayCount = day;
            NightCount = night;
        }
    }
}
