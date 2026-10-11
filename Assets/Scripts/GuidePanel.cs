using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Справочник «Как выжить»: статичный текст из разметки и кнопка «Готово»; закрывается и по Esc.</summary>
    // Раньше GameManager: Esc забирается панелью и не снимает игру с паузы.
    [DefaultExecutionOrder(-100)]
    public class GuidePanel : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private AudioService _audio;
        [SerializeField] private Button _backButton;

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBackClicked);
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

        private void Close()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
