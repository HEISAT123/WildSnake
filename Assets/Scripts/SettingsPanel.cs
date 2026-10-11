using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Экран настроек: связывает элементы UI с SettingsService; закрывается кнопкой «Готово» и по Esc.</summary>
    // Раньше GameManager: Esc забирается панелью и не снимает игру с паузы.
    [DefaultExecutionOrder(-100)]
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private SettingsService _settings;
        [SerializeField] private AudioService _audio;
        [Tooltip("Контекстные подсказки: «Показать обучение» сбрасывает их счётчики")]
        [SerializeField] private ContextHints _hints;

        [Header("Элементы")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _mouseSlider;
        [SerializeField] private Toggle _invertYToggle;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Toggle _hudToggle;
        [SerializeField] private Toggle _rivalToggle;
        [SerializeField] private Toggle _minimapToggle;
        [SerializeField] private Button _qualityButton;
        [SerializeField] private Text _qualityLabel;
        [SerializeField] private Button _tutorialButton;
        [SerializeField] private Button _backButton;

        private void OnEnable()
        {
            Refresh();
            _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            _mouseSlider.onValueChanged.AddListener(OnMouseChanged);
            _invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
            _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            _hudToggle.onValueChanged.AddListener(OnHudChanged);
            _rivalToggle.onValueChanged.AddListener(OnRivalChanged);
            _minimapToggle.onValueChanged.AddListener(OnMinimapChanged);
            _qualityButton.onClick.AddListener(OnQualityClicked);
            _tutorialButton.onClick.AddListener(OnTutorialClicked);
            _backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnDisable()
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            _mouseSlider.onValueChanged.RemoveListener(OnMouseChanged);
            _invertYToggle.onValueChanged.RemoveListener(OnInvertYChanged);
            _fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            _hudToggle.onValueChanged.RemoveListener(OnHudChanged);
            _rivalToggle.onValueChanged.RemoveListener(OnRivalChanged);
            _minimapToggle.onValueChanged.RemoveListener(OnMinimapChanged);
            _qualityButton.onClick.RemoveListener(OnQualityClicked);
            _tutorialButton.onClick.RemoveListener(OnTutorialClicked);
            _backButton.onClick.RemoveListener(OnBackClicked);
        }

        private void Update()
        {
            if (!GameInput.Pause) return;
            GameInput.ConsumePause();
            OnBackClicked();
        }

        /// <summary>Значения ставим без вызова событий, чтобы не сохранять то, что и так сохранено.</summary>
        private void Refresh()
        {
            _masterSlider.SetValueWithoutNotify(_settings.MasterVolume);
            _musicSlider.SetValueWithoutNotify(_settings.MusicVolume);
            _sfxSlider.SetValueWithoutNotify(_settings.SfxVolume);
            _mouseSlider.SetValueWithoutNotify(_settings.MouseSensitivity);
            _invertYToggle.SetIsOnWithoutNotify(_settings.InvertY);
            _fullscreenToggle.SetIsOnWithoutNotify(_settings.Fullscreen);
            _hudToggle.SetIsOnWithoutNotify(_settings.ShowHud);
            _rivalToggle.SetIsOnWithoutNotify(_settings.RivalEnabled);
            _minimapToggle.SetIsOnWithoutNotify(_settings.ShowMinimap);
            _qualityLabel.text = QualityText();
        }

        private string QualityText() => _settings.QualityName.ToUpperInvariant() + "   ›";

        private void OnMasterChanged(float v) => _settings.SetMasterVolume(v);
        private void OnMusicChanged(float v) => _settings.SetMusicVolume(v);
        private void OnSfxChanged(float v) { _settings.SetSfxVolume(v); _audio.Play(SoundId.Click); }
        private void OnMouseChanged(float v) => _settings.SetMouseSensitivity(v);
        private void OnInvertYChanged(bool v) => _settings.SetInvertY(v);
        private void OnFullscreenChanged(bool v) => _settings.SetFullscreen(v);
        private void OnHudChanged(bool v) => _settings.SetShowHud(v);
        private void OnRivalChanged(bool v) => _settings.SetRivalEnabled(v);
        private void OnMinimapChanged(bool v) => _settings.SetShowMinimap(v);

        private void OnQualityClicked()
        {
            _audio.Play(SoundId.Click);
            _settings.CycleQuality();
            _qualityLabel.text = QualityText();
        }

        private void OnTutorialClicked()
        {
            _audio.Play(SoundId.Click);
            _hints.ResetCounters();
        }

        private void OnBackClicked()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
