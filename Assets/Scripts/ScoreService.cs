using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>Счёт текущей партии с множителем (бонус «x2»). Рекорды хранит LeaderboardService.</summary>
    public class ScoreService : MonoBehaviour
    {
        /// <summary>(счёт, рекорд)</summary>
        public event Action<int, int> ScoreChanged;

        public int Score { get; private set; }
        public int Best { get; private set; }
        public bool IsNewBest { get; private set; }
        public int Multiplier { get; set; } = 1;

        /// <summary>Начать партию; best — лучший результат в текущем режиме.</summary>
        public void ResetScore(int best)
        {
            Score = 0;
            Best = best;
            IsNewBest = false;
            Multiplier = 1;
            ScoreChanged?.Invoke(Score, Best);
        }

        public void AddPoints(int basePoints)
        {
            Score += basePoints * Multiplier;
            if (Score > Best)
            {
                Best = Score;
                IsNewBest = true;
            }
            ScoreChanged?.Invoke(Score, Best);
        }

        public void RemovePoints(int points)
        {
            Score = Mathf.Max(0, Score - points);
            ScoreChanged?.Invoke(Score, Best);
        }
    }
}
