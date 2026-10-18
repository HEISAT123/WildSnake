using System;
using UnityEngine;

namespace WildSnake
{
    /// <summary>Пользовательские настройки: громкость, мышь, графика, экран. Хранятся в PlayerPrefs.</summary>
    public class SettingsService : MonoBehaviour
    {
        private const string KEY_MASTER = "WildSnake.Settings.Master";
        private const string KEY_MUSIC = "WildSnake.Settings.Music";
        private const string KEY_SFX = "WildSnake.Settings.Sfx";
        private const string KEY_MOUSE = "WildSnake.Settings.Mouse";
        private const string KEY_INVERT_Y = "WildSnake.Settings.InvertY";
        private const string KEY_QUALITY = "WildSnake.Settings.Quality";
        private const string KEY_FULLSCREEN = "WildSnake.Settings.Fullscreen";
        private const string KEY_HUD = "WildSnake.Settings.Hud";
        private const string KEY_RIVAL = "WildSnake.Settings.Rival";
        private const string KEY_MINIMAP = "WildSnake.Settings.Minimap";
        private const string KEY_HINTS = "WildSnake.Settings.Hints";

        public event Action Changed;

        private float _screenCheck = 1f;

        public float MasterVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float MouseSensitivity { get; private set; }
        public bool InvertY { get; private set; }
        public int QualityLevel { get; private set; }
        public bool Fullscreen { get; private set; }
        /// <summary>Интерфейс в игре (H): без него остаются только виньетки и звук.</summary>
        public bool ShowHud { get; private set; }
        public bool RivalEnabled { get; private set; }
        /// <summary>Мини-карта в углу экрана во время партии.</summary>
        public bool ShowMinimap { get; private set; }
        /// <summary>Подсказки с клавишей внизу экрана во время партии.</summary>
        public bool ShowHints { get; private set; }

        public string QualityName
        {
            get
            {
                string[] names = QualitySettings.names;
                return QualityLevel >= 0 && QualityLevel < names.Length ? names[QualityLevel] : "?";
            }
        }

        private void Awake()
        {
            MasterVolume = PlayerPrefs.GetFloat(KEY_MASTER, 0.8f);
            MusicVolume = PlayerPrefs.GetFloat(KEY_MUSIC, 0.5f);
            SfxVolume = PlayerPrefs.GetFloat(KEY_SFX, 0.8f);
            MouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE, 1f);
            InvertY = PlayerPrefs.GetInt(KEY_INVERT_Y, 0) == 1;
            QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
            Fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            ShowHud = PlayerPrefs.GetInt(KEY_HUD, 1) == 1;
            RivalEnabled = PlayerPrefs.GetInt(KEY_RIVAL, 1) == 1;
            ShowMinimap = PlayerPrefs.GetInt(KEY_MINIMAP, 1) == 1;
            ShowHints = PlayerPrefs.GetInt(KEY_HINTS, 1) == 1;
            Apply();
        }

        private void Update()
        {
            if (GameInput.ToggleFullscreen) SetFullscreen(!Fullscreen);
            // Alt+Enter переключает режим мимо настроек — подхватываем, чтобы полный экран был без полос.
            // В редакторе экран не переключается, и проверка стёрла бы настройку.
            if (Application.isEditor) return;
            _screenCheck -= Time.unscaledDeltaTime;
            if (_screenCheck > 0f) return;
            _screenCheck = 1f;
            if (Screen.fullScreen != Fullscreen)
            {
                Fullscreen = Screen.fullScreen;
                PlayerPrefs.SetInt(KEY_FULLSCREEN, Fullscreen ? 1 : 0);
                Changed?.Invoke();
            }
            ApplyScreen();
        }

        public void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MASTER, MasterVolume); Commit(); }
        public void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MUSIC, MusicVolume); Commit(); }
        public void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_SFX, SfxVolume); Commit(); }
        public void SetMouseSensitivity(float value) { MouseSensitivity = Mathf.Clamp(value, 0.2f, 3f); PlayerPrefs.SetFloat(KEY_MOUSE, MouseSensitivity); Commit(); }
        public void SetInvertY(bool value) { InvertY = value; PlayerPrefs.SetInt(KEY_INVERT_Y, value ? 1 : 0); Commit(); }
        public void SetFullscreen(bool value) { Fullscreen = value; PlayerPrefs.SetInt(KEY_FULLSCREEN, value ? 1 : 0); Commit(); }
        public void SetShowHud(bool value) { ShowHud = value; PlayerPrefs.SetInt(KEY_HUD, value ? 1 : 0); Commit(); }
        public void SetRivalEnabled(bool value) { RivalEnabled = value; PlayerPrefs.SetInt(KEY_RIVAL, value ? 1 : 0); Commit(); }
        public void SetShowMinimap(bool value) { ShowMinimap = value; PlayerPrefs.SetInt(KEY_MINIMAP, value ? 1 : 0); Commit(); }
        public void SetShowHints(bool value) { ShowHints = value; PlayerPrefs.SetInt(KEY_HINTS, value ? 1 : 0); Commit(); }

        public void CycleQuality()
        {
            int count = QualitySettings.names.Length;
            QualityLevel = count > 0 ? (QualityLevel + 1) % count : 0;
            PlayerPrefs.SetInt(KEY_QUALITY, QualityLevel);
            Commit();
        }

        private void Commit()
        {
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (QualityLevel != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(QualityLevel, true);
            ApplyScreen();
        }

        /// <summary>
        /// Полный экран — всегда в родном разрешении монитора: иначе Unity растягивает прежнее окно (например,
        /// развёрнутое без панели задач) и по краям остаются чёрные полосы. Окно — 80% экрана с теми же пропорциями.
        /// В редакторе ничего не делает — работает в собранной игре.
        /// </summary>
        private void ApplyScreen()
        {
            if (Application.isEditor) return;
            int w = Display.main.systemWidth, h = Display.main.systemHeight;
            if (w <= 0 || h <= 0) return;
            if (Fullscreen)
            {
                if (Screen.fullScreen && Screen.width == w && Screen.height == h) return;
                Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            }
            else
            {
                if (!Screen.fullScreen) return;
                Screen.SetResolution(Mathf.RoundToInt(w * 0.8f), Mathf.RoundToInt(h * 0.8f), FullScreenMode.Windowed);
            }
            // Режим сменится только в следующих кадрах — до тех пор не сверяемся с ним.
            _screenCheck = 1.5f;
        }
    }
}
