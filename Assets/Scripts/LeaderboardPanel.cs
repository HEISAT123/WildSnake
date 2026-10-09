using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Экран рекордов: топ-10 по каждому режиму в трёх колонках.</summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        private static readonly string[] ModeNames = { "Классика", "На время", "Выживание" };

        [Header("Ссылки")]
        [SerializeField] private LeaderboardService _leaderboard;
        [SerializeField] private AudioService _audio;

        [Header("Элементы")]
        [Tooltip("Колонки по режимам (порядок как в GameMode)")]
        [SerializeField] private Text[] _columns = new Text[3];

        [SerializeField] private Button _backButton;

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBackClicked);
            Refresh();
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBackClicked);
        }

        /// <summary>Строится при открытии экрана, а не каждый кадр.</summary>
        private void Refresh()
        {
            var sb = new StringBuilder();
            for (int m = 0; m < _columns.Length && m < ModeNames.Length; m++)
            {
                sb.Clear();
                sb.Append(ModeNames[m]).Append("\n\n");
                Leaderboard board = _leaderboard.GetBoard((GameMode)m);
                if (board.Count == 0) sb.Append("пока пусто");
                for (int i = 0; i < board.Count; i++)
                {
                    LeaderboardEntry e = board.Entries[i];
                    sb.Append(i + 1).Append(". ").Append(e.Score)
                      .Append("   дл. ").Append(e.Length.ToString("0.0", CultureInfo.InvariantCulture))
                      .Append('\n');
                }
                _columns[m].text = sb.ToString();
            }
        }

        private void OnBackClicked()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
