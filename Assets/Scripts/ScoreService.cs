using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>Счёт текущей партии и рекорд, сохраняемый в PlayerPrefs.</summary>
    public class ScoreService : MonoBehaviour
    {
        [Header("Очки")]
        [Tooltip("Сколько очков даёт одна еда")]
        [SerializeField, Min(1)] private int _pointsPerFood = 10;

        [Header("Сохранение")]
        [Tooltip("Ключ PlayerPrefs для рекорда")]
        [SerializeField] private string _bestScoreKey = "Snake3D.BestScore";

        /// <summary>(счёт, рекорд)</summary>
        public event Action<int, int> ScoreChanged;

        public int Score { get; private set; }
        public int Best { get; private set; }
        public bool IsNewBest { get; private set; }

        private void Awake()
        {
            Best = PlayerPrefs.GetInt(_bestScoreKey, 0);
        }

        public void ResetScore()
        {
            Score = 0;
            IsNewBest = false;
            ScoreChanged?.Invoke(Score, Best);
        }

        public void AddFood()
        {
            Score += _pointsPerFood;
            if (Score > Best)
            {
                Best = Score;
                IsNewBest = true;
            }
            ScoreChanged?.Invoke(Score, Best);
        }

        /// <summary>Пишем на диск только в конце партии, а не на каждую еду.</summary>
        public void SaveBest()
        {
            PlayerPrefs.SetInt(_bestScoreKey, Best);
            PlayerPrefs.Save();
        }
    }
}
