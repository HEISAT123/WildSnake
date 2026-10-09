using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>Пользовательские настройки: громкость, руль, графика, экран. Хранятся в PlayerPrefs.</summary>
    public class SettingsService : MonoBehaviour
    {
        private const string KEY_MASTER = "Snake3D.Settings.Master";
        private const string KEY_MUSIC = "Snake3D.Settings.Music";
        private const string KEY_SFX = "Snake3D.Settings.Sfx";
        private const string KEY_STEER = "Snake3D.Settings.Steer";
        private const string KEY_QUALITY = "Snake3D.Settings.Quality";
        private const string KEY_FULLSCREEN = "Snake3D.Settings.Fullscreen";
        private const string KEY_MINIMAP = "Snake3D.Settings.Minimap";
        private const string KEY_RIVAL = "Snake3D.Settings.Rival";

        public event Action Changed;

        public float MasterVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float SteerSensitivity { get; private set; }
        public int QualityLevel { get; private set; }
        public bool Fullscreen { get; private set; }
        public bool ShowMinimap { get; private set; }
        public bool RivalEnabled { get; private set; }

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
            SteerSensitivity = PlayerPrefs.GetFloat(KEY_STEER, 1f);
            QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
            Fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            ShowMinimap = PlayerPrefs.GetInt(KEY_MINIMAP, 1) == 1;
            RivalEnabled = PlayerPrefs.GetInt(KEY_RIVAL, 1) == 1;
            Apply();
        }

        private void Update()
        {
            if (GameInput.ToggleFullscreen) SetFullscreen(!Fullscreen);
        }

        public void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MASTER, MasterVolume); Commit(); }
        public void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MUSIC, MusicVolume); Commit(); }
        public void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_SFX, SfxVolume); Commit(); }
        public void SetSteerSensitivity(float value) { SteerSensitivity = Mathf.Clamp(value, 0.5f, 1.5f); PlayerPrefs.SetFloat(KEY_STEER, SteerSensitivity); Commit(); }
        public void SetFullscreen(bool value) { Fullscreen = value; PlayerPrefs.SetInt(KEY_FULLSCREEN, value ? 1 : 0); Commit(); }
        public void SetShowMinimap(bool value) { ShowMinimap = value; PlayerPrefs.SetInt(KEY_MINIMAP, value ? 1 : 0); Commit(); }
        public void SetRivalEnabled(bool value) { RivalEnabled = value; PlayerPrefs.SetInt(KEY_RIVAL, value ? 1 : 0); Commit(); }

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
            // В редакторе Screen.fullScreen ничего не делает — работает в собранной игре.
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }
    }
}
