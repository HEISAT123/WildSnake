using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// Короткие слова-состояния над показателями: «В ТРАВЕ», «ЗАСАДА», «ХОЛОДНО», «ПЕРЕВАРИВАЕТ»…
    /// Объясняют виньетки и замедление без справочника. Пересобираются только при смене набора состояний.
    /// </summary>
    public class HudStatus : MonoBehaviour
    {
        private const int AMBUSH = 1 << 0;
        private const int GRASS = 1 << 1;
        private const int CANOPY = 1 << 2;
        private const int WATER = 1 << 3;
        private const int COLD = 1 << 4;
        private const int WARM = 1 << 5;
        private const int DIGEST = 1 << 6;
        private const int TIRED = 1 << 7;
        private const int EXPOSED = 1 << 8;

        private static readonly int[] Flags = { EXPOSED, TIRED, COLD, AMBUSH, GRASS, CANOPY, WATER, DIGEST, WARM };
        private static readonly string[] Words =
        {
            "ВЫДАЛА СЕБЯ", "НЕТ СИЛ", "ХОЛОДНО — МЕДЛЕННЕЕ", "ЗАСАДА", "В ТРАВЕ — НЕ ВИДНО", "ПОД КРОНАМИ — ОРЁЛ НЕ ВИДИТ",
            "В ВОДЕ", "ПЕРЕВАРИВАЕТ — ТЯЖЕЛЕЕ", "ПРОГРЕЛАСЬ"
        };

        [Header("Источники")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SnakeController _snake;

        [Header("Элементы")]
        [Tooltip("Строки снизу вверх")]
        [SerializeField] private Text[] _lines = new Text[3];

        [Header("Цвета")]
        [SerializeField] private Color _goodColor = new Color(0.78f, 0.96f, 0.2f);
        [SerializeField] private Color _badColor = new Color(1f, 0.42f, 0.3f);
        [SerializeField] private Color _infoColor = new Color(0.95f, 0.93f, 0.88f);
        [SerializeField] private Color _coldColor = new Color(0.62f, 0.82f, 1f);

        private int _shown = -1;

        private void OnEnable()
        {
            _shown = -1;
        }

        private void LateUpdate()
        {
            int state = 0;
            if (!_snake.InBurrow)
            {
                if (_snake.Ambush.IsLuring) state |= AMBUSH;
                else if (_snake.IsHidden) state |= GRASS;
                if (_snake.UnderCanopy && !_snake.IsHidden) state |= CANOPY;
                if (_snake.IsInWater) state |= WATER;
                if (_snake.Temperature.IsCold) state |= COLD;
                else if (_snake.Temperature.Warm01 > 0.5f) state |= WARM;
                if (_snake.Digestion.IsDigesting) state |= DIGEST;
                if (_snake.Stamina.IsExhausted) state |= TIRED;
                if (_snake.IsExposed) state |= EXPOSED;
            }
            if (state == _shown) return;
            _shown = state;

            int line = 0;
            for (int i = 0; i < Flags.Length && line < _lines.Length; i++)
            {
                if ((state & Flags[i]) == 0) continue;
                Text t = _lines[line++];
                t.text = Words[i];
                t.color = ColorFor(Flags[i]);
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
            for (; line < _lines.Length; line++)
                if (_lines[line].gameObject.activeSelf) _lines[line].gameObject.SetActive(false);
        }

        private Color ColorFor(int flag)
        {
            if (flag == EXPOSED || flag == TIRED) return _badColor;
            if (flag == COLD || flag == WATER) return _coldColor;
            if (flag == AMBUSH || flag == GRASS || flag == CANOPY || flag == WARM) return _goodColor;
            return _infoColor;
        }
    }
}
