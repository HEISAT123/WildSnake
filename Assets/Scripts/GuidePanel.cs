using UnityEngine;
using UnityEngine.UI;

namespace WildSnake
{
    /// <summary>
    /// Справочник «Как выжить»: статичный текст из разметки, кнопка «Смотреть обучение» (видео-обучение)
    /// и «Готово»; закрывается и по Esc.
    /// </summary>
    // Раньше GameManager: Esc забирается панелью и не снимает игру с паузы.
    [DefaultExecutionOrder(-100)]
    public class GuidePanel : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private AudioService _audio;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _watchButton;
        [SerializeField] private GameObject _tutorialPanel;

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBackClicked);
            _watchButton.onClick.AddListener(OnWatchClicked);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBackClicked);
            _watchButton.onClick.RemoveListener(OnWatchClicked);
        }

        private void Update()
        {
            if (!GameInput.Pause) return;
            GameInput.ConsumePause();
            Close();
        }

        private void OnBackClicked()
        {
            Close();
        }

        private void OnWatchClicked()
        {
            _audio.Play(SoundId.Click);
            _tutorialPanel.SetActive(true);
        }

        private void Close()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
