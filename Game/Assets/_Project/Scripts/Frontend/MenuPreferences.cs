using System.Collections;
using TheLightenRoad.Audio;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.Frontend
{
    /// <summary>Menu-owned preferences, also applied after entering a gameplay scene.</summary>
    public sealed class MenuPreferences : MonoBehaviour
    {
        private const string Prefix = "TLR.Menu.";
        private static MenuPreferences instance;
        public static float Master => PlayerPrefs.GetFloat(Prefix + "Master", 0.8f);
        public static float Music => PlayerPrefs.GetFloat(Prefix + "Music", 1f);
        public static float Effects => PlayerPrefs.GetFloat(Prefix + "Effects", 1f);
        public static bool ReducedMotion => PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) == 1;

        public static void EnsureActive()
        {
            if (instance != null) return;
            instance = new GameObject("Menu preferences").AddComponent<MenuPreferences>();
            DontDestroyOnLoad(instance.gameObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplyAfterStart());
        private IEnumerator ApplyAfterStart() { yield return null; Apply(); }

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
            if (!Application.isEditor) Screen.fullScreen = value;
        }

        public static void ApplySavedDisplay()
        {
            if (PlayerPrefs.HasKey(Prefix + "Fullscreen") && !Application.isEditor)
                Screen.fullScreen = PlayerPrefs.GetInt(Prefix + "Fullscreen") == 1;
        }

        public static void Apply()
        {
            AudioListener.volume = Master;
            var music = BackgroundMusicManager.Instance;
            if (music != null) music.TargetVolume = 0.25f * Music;
            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.MasterSFXVolume = Effects;
            foreach (var hud in Object.FindObjectsByType<LanternHUD>(FindObjectsSortMode.None)) hud.ReduceMotion = ReducedMotion;
            foreach (var hud in Object.FindObjectsByType<StaminaHUD>(FindObjectsSortMode.None)) hud.ReduceMotion = ReducedMotion;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
