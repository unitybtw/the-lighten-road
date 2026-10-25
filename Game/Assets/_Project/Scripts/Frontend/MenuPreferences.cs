using System.Collections;
using TheLightenRoad.Audio;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Persistent frontend & gameplay preferences manager for audio, display, frame rate,
    /// accessibility, and selected realm/chapter.
    /// </summary>
    public sealed class MenuPreferences : MonoBehaviour
    {
        private const string Prefix = "TLR.Menu.";
        private static MenuPreferences instance;

        public static float Master => PlayerPrefs.GetFloat(Prefix + "Master", 0.85f);
        public static float Music => PlayerPrefs.GetFloat(Prefix + "Music", 0.85f);
        public static float Effects => PlayerPrefs.GetFloat(Prefix + "Effects", 1.0f);
        public static bool ReducedMotion => PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) == 1;
        public static bool Fullscreen => PlayerPrefs.GetInt(Prefix + "Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        public static bool VSync => PlayerPrefs.GetInt(Prefix + "VSync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        public static int TargetFPS => PlayerPrefs.GetInt(Prefix + "TargetFPS", 60);
        public static int SelectedRealmIndex => PlayerPrefs.GetInt(Prefix + "SelectedRealm", 0);
        public static int ControllerStyle => PlayerPrefs.GetInt(Prefix + "ControllerStyle", 0);
        public static bool Vibration => PlayerPrefs.GetInt(Prefix + "VibrationEnabled", 1) == 1;
        public static float StickDeadzone => PlayerPrefs.GetFloat(Prefix + "StickDeadzone", 0.15f);

        public static void EnsureActive()
        {
            if (instance != null) return;
            var go = new GameObject("Menu Preferences");
            instance = go.AddComponent<MenuPreferences>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplyAfterStart());

        private IEnumerator ApplyAfterStart()
        {
            yield return null;
            Apply();
        }

        public static void SetAudio(float master, float music, float effects)
        {
            PlayerPrefs.SetFloat(Prefix + "Master", Mathf.Clamp01(master));
            PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(music));
            PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(effects));
            Apply();
        }

        public static void SetReducedMotion(bool value)
        {
            PlayerPrefs.SetInt(Prefix + "ReducedMotion", value ? 1 : 0);
            Apply();
        }

        public static void SetFullscreen(bool value)
        {
            PlayerPrefs.SetInt(Prefix + "Fullscreen", value ? 1 : 0);
            if (!Application.isEditor)
            {
                Screen.fullScreen = value;
            }
        }

        public static void SetVSync(bool value)
        {
            PlayerPrefs.SetInt(Prefix + "VSync", value ? 1 : 0);
            QualitySettings.vSyncCount = value ? 1 : 0;
        }

        public static void SetTargetFPS(int fps)
        {
            PlayerPrefs.SetInt(Prefix + "TargetFPS", fps);
            Application.targetFrameRate = fps;
        }

        public static void SetSelectedRealm(int index)
        {
            PlayerPrefs.SetInt(Prefix + "SelectedRealm", Mathf.Max(0, index));
        }

        public static void SetControllerStyle(int style)
        {
            PlayerPrefs.SetInt(Prefix + "ControllerStyle", Mathf.Clamp(style, 0, 3));
            ControllerGlyphService.ConfiguredStyle = (ControllerPromptStyle)style;
            Save();
        }

        public static void SetVibration(bool enabled)
        {
            PlayerPrefs.SetInt(Prefix + "VibrationEnabled", enabled ? 1 : 0);
            ControllerGlyphService.VibrationEnabled = enabled;
            Save();
        }

        public static void SetStickDeadzone(float deadzone)
        {
            PlayerPrefs.SetFloat(Prefix + "StickDeadzone", Mathf.Clamp(deadzone, 0.05f, 0.40f));
            ControllerGlyphService.StickDeadzone = deadzone;
            Save();
        }

        public static bool IsRealmCompleted(int index) => PlayerPrefs.GetInt(Prefix + "RealmCompleted." + index, 0) == 1;

        public static void SetRealmCompleted(int index, bool completed = true)
        {
            PlayerPrefs.SetInt(Prefix + "RealmCompleted." + index, completed ? 1 : 0);
            Save();
        }

        public static void ResetToDefaults()
        {
            SetAudio(0.85f, 0.85f, 1.0f);
            SetReducedMotion(false);
            SetFullscreen(true);
            SetVSync(true);
            SetTargetFPS(60);
            SetControllerStyle(0);
            SetVibration(true);
            SetStickDeadzone(0.15f);
            // Restoring presentation preferences must never erase chapter progress.
            Save();
        }

        public static void ApplySavedDisplay()
        {
            if (PlayerPrefs.HasKey(Prefix + "Fullscreen") && !Application.isEditor)
            {
                Screen.fullScreen = Fullscreen;
            }
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = TargetFPS;
        }

        public static void Apply()
        {
            AudioListener.volume = Master;

            var music = BackgroundMusicManager.Instance;
            if (music != null)
            {
                music.TargetVolume = 0.25f * Music;
            }

            if (SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.MasterSFXVolume = Effects;
            }

            foreach (var hud in Object.FindObjectsByType<LanternHUD>(FindObjectsSortMode.None))
            {
                hud.ReduceMotion = ReducedMotion;
            }

            foreach (var hud in Object.FindObjectsByType<StaminaHUD>(FindObjectsSortMode.None))
            {
                hud.ReduceMotion = ReducedMotion;
            }
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
