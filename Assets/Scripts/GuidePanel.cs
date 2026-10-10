using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Справочник «Как выжить»: статичный текст из разметки и кнопка «Готово».</summary>
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
            // Справочник открывается из меню — закрываем и по Esc, как привычно.
            if (GameInput.Pause) Close();
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
