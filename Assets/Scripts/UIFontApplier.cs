using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Ставит всем текстам интерфейса системный шрифт (Bahnschrift в Windows, DIN-подобный) — он современнее
    /// встроенного Arial и поддерживает кириллицу. Шрифт берётся из ОС при запуске, поэтому
    /// это не сторонний ассет; если его нет, остаётся встроенный.
    /// </summary>
    public class UIFontApplier : MonoBehaviour
    {
        [Header("Шрифт")]
        [Tooltip("Системные шрифты по приоритету: берётся первый установленный")]
        [SerializeField] private string[] _fontNames = { "Bahnschrift", "Segoe UI", "Helvetica Neue", "Arial" };

        [Tooltip("Базовый размер для растеризации")]
        [SerializeField, Min(8)] private int _baseSize = 32;

        private void Awake()
        {
            Font font = Font.CreateDynamicFontFromOSFont(_fontNames, _baseSize);
            if (font == null) return;

            // Один раз при старте, включая неактивные окна (настройки, рекорды).
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) texts[i].font = font;
        }
    }
}
