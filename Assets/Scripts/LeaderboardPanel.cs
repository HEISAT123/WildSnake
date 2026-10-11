using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Экран рекордов: топ-10 жизней змеи в колонках «место, сутки, добыча, длина, линьки» (заголовки — в разметке).
    /// Закрывается кнопкой «Готово» и по Esc.
    /// </summary>
    // Раньше GameManager: Esc забирается панелью и не снимает игру с паузы.
    [DefaultExecutionOrder(-100)]
    public class LeaderboardPanel : MonoBehaviour
    {
        private const int COLUMN_COUNT = 5;

        [Header("Ссылки")]
        [SerializeField] private LeaderboardService _leaderboard;
        [SerializeField] private AudioService _audio;

        [Tooltip("Длина суток — чтобы перевести секунды в сутки")]
        [SerializeField] private GameConfig _config;

        [Header("Элементы")]
        [Tooltip("Колонки: место, сутки, добыча, длина, линьки")]
        [SerializeField] private Text[] _columns = new Text[COLUMN_COUNT];

        [Tooltip("Надпись для пустой таблицы")]
        [SerializeField] private Text _emptyText;

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

        private void Update()
        {
            if (!GameInput.Pause) return;
            GameInput.ConsumePause();
            OnBackClicked();
        }

        /// <summary>Строится при открытии экрана, а не каждый кадр.</summary>
        private void Refresh()
        {
            Leaderboard board = _leaderboard.Board;
            float dayLength = _config.DaySeconds + _config.NightSeconds;
            var sb = new StringBuilder();
            for (int c = 0; c < _columns.Length; c++)
            {
                sb.Clear();
                for (int i = 0; i < board.Count; i++)
                {
                    LeaderboardEntry e = board.Entries[i];
                    switch (c)
                    {
                        case 0: sb.Append((i + 1).ToString("00")); break;
                        case 1: sb.Append(TimeFormat.Days(e.Seconds, dayLength)); break;
                        case 2: sb.Append(e.Prey); break;
                        case 3: sb.Append(e.Length.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')).Append(" м"); break;
                        default: sb.Append(e.Molts); break;
                    }
                    sb.Append('\n');
                }
                _columns[c].text = sb.ToString();
            }
            _emptyText.gameObject.SetActive(board.Count == 0);
        }

        private void OnBackClicked()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
