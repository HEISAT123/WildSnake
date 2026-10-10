using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>Экран настроек: связывает элементы UI с SettingsService.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private SettingsService _settings;
        [SerializeField] private AudioService _audio;
        [SerializeField] private TutorialController _tutorial;

        [Header("Элементы")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _steerSlider;
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Toggle _minimapToggle;
        [SerializeField] private Toggle _rivalToggle;
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
            _steerSlider.onValueChanged.AddListener(OnSteerChanged);
            _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            _minimapToggle.onValueChanged.AddListener(OnMinimapChanged);
            _rivalToggle.onValueChanged.AddListener(OnRivalChanged);
            _qualityButton.onClick.AddListener(OnQualityClicked);
            _tutorialButton.onClick.AddListener(OnTutorialClicked);
            _backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnDisable()
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            _steerSlider.onValueChanged.RemoveListener(OnSteerChanged);
            _fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            _minimapToggle.onValueChanged.RemoveListener(OnMinimapChanged);
            _rivalToggle.onValueChanged.RemoveListener(OnRivalChanged);
            _qualityButton.onClick.RemoveListener(OnQualityClicked);
            _tutorialButton.onClick.RemoveListener(OnTutorialClicked);
            _backButton.onClick.RemoveListener(OnBackClicked);
        }

        /// <summary>Значения ставим без вызова событий, чтобы не сохранять то, что и так сохранено.</summary>
        private void Refresh()
        {
            _masterSlider.SetValueWithoutNotify(_settings.MasterVolume);
            _musicSlider.SetValueWithoutNotify(_settings.MusicVolume);
            _sfxSlider.SetValueWithoutNotify(_settings.SfxVolume);
            _steerSlider.SetValueWithoutNotify(_settings.SteerSensitivity);
            _fullscreenToggle.SetIsOnWithoutNotify(_settings.Fullscreen);
            _minimapToggle.SetIsOnWithoutNotify(_settings.ShowMinimap);
            _rivalToggle.SetIsOnWithoutNotify(_settings.RivalEnabled);
            _qualityLabel.text = QualityText();
        }

        private string QualityText() => _settings.QualityName.ToUpperInvariant() + "   ›";

        private void OnMasterChanged(float v) => _settings.SetMasterVolume(v);
        private void OnMusicChanged(float v) => _settings.SetMusicVolume(v);
        private void OnSfxChanged(float v) { _settings.SetSfxVolume(v); _audio.Play(SoundId.Click); }
        private void OnSteerChanged(float v) => _settings.SetSteerSensitivity(v);
        private void OnFullscreenChanged(bool v) => _settings.SetFullscreen(v);
        private void OnMinimapChanged(bool v) => _settings.SetShowMinimap(v);
        private void OnRivalChanged(bool v) => _settings.SetRivalEnabled(v);

        private void OnQualityClicked()
        {
            _audio.Play(SoundId.Click);
            _settings.CycleQuality();
            _qualityLabel.text = QualityText();
        }

        private void OnTutorialClicked()
        {
            _audio.Play(SoundId.Click);
            _tutorial.ResetTutorial();
        }

        private void OnBackClicked()
        {
            _audio.Play(SoundId.Click);
            gameObject.SetActive(false);
        }
    }
}
