using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class PauseMenuController
    {
        private static readonly string[] PauseSettingsNames = { "Audio", "Display", "Controller", "Accessibility" };
        private readonly GameObject[] pauseSettingsPages = new GameObject[4];
        private readonly Button[] pauseSettingsTabs = new Button[4];
        private readonly Selectable[][] pauseSettingsRows = new Selectable[4][];
        private int pauseSettingsSection;
        private Button pauseResetButton, pauseControllerStyleButton;
        private Toggle pauseRumbleToggle, pauseTriggersToggle, pauseLightbarToggle, pauseSpeakerToggle;
        private Slider pauseDeadzoneSlider;
        private GameObject lastHomeSelection;
        public int CurrentSettingsSection => pauseSettingsSection;

        private void BuildPauseSettingsWorkspace()
        {
            var tabs = CreateAnchoredRect("Pause Settings Tabs", settingsPanel.transform,
                new Vector2(1040, 56), Vector2.one * .5f, new Vector2(0, 232));
            for (int i = 0; i < PauseSettingsNames.Length; i++)
            {
                int index = i;
                var page = CreateAnchoredRect(PauseSettingsNames[i] + " settings", settingsPanel.transform,
                    new Vector2(900, 500), Vector2.one * .5f, Vector2.zero);
                pauseSettingsPages[i] = page.gameObject;
                pauseSettingsTabs[i] = CreateEtherealActionButton("SettingsTab_" + i, tabs,
                    PauseSettingsNames[i], new Vector2((i - 1.5f) * 245, 0), () => SelectSettingsSection(index), false, 228);
                MenuReadability.CompactDeviceTab(pauseSettingsTabs[i]);
                var label = pauseSettingsTabs[i].GetComponentInChildren<TMP_Text>();
                UsePauseBodyFont(label); label.fontSize = 22;
            }
            ReparentSetting(masterSlider.transform.parent, 0);
            ReparentSetting(musicSlider.transform.parent, 0);
            ReparentSetting(effectsSlider.transform.parent, 0);
            ReparentSetting(fullscreenToggle.transform, 1);
            ReparentSetting(vsyncToggle.transform, 1);
            ReparentSetting(reducedMotionToggle.transform, 3);
            pauseSettingsRows[0] = new Selectable[] { masterSlider, musicSlider, effectsSlider };
            pauseSettingsRows[1] = new Selectable[] { fullscreenToggle, vsyncToggle };
            pauseSettingsRows[3] = new Selectable[] { reducedMotionToggle };

            var root = (RectTransform)pauseSettingsPages[2].transform;
            var styleRow = CreateAnchoredRect("Controller prompt style", root, new Vector2(900, 64), Vector2.one * .5f, Vector2.zero);
            CreateLabel("Title", styleRow, Vector2.zero, new Vector2(500, 32), "BUTTON SYMBOLS", 24, TextAlignmentOptions.Left, radiantIvory);
            CreateLabel("Subtitle", styleRow, Vector2.zero, new Vector2(500, 30), "Button symbols shown during play", 20, TextAlignmentOptions.Left, moonlightSilver);
            pauseControllerStyleButton = CreateEtherealActionButton("ControllerStyleButton", styleRow, "Automatic", new Vector2(284, 0), () =>
            {
                MenuPreferences.SetControllerStyle((MenuPreferences.ControllerStyle + 1) % 4);
                MarkPauseSettingsChanged(); SyncPauseControllerSettings();
            }, false, 300);
            var styleText = pauseControllerStyleButton.GetComponentInChildren<TMP_Text>();
            UsePauseBodyFont(styleText); styleText.fontSize = 22;
            pauseRumbleToggle = CreateToggle("ControllerVibration", root, Vector2.zero, "Vibration", "Contextual rumble during play", value =>
            {
                MenuPreferences.SetVibration(value); ControllerSettingChanged(pauseRumbleToggle);
            });
            pauseTriggersToggle = CreateToggle("ControllerAdaptiveTriggers", root, Vector2.zero, "Adaptive triggers", "Trigger resistance on supported DualSense", value =>
            {
                MenuPreferences.SetAdaptiveTriggers(value); ControllerGlyphService.UpdateAdaptiveTriggers(true);
                ControllerSettingChanged(pauseTriggersToggle);
            });
            pauseLightbarToggle = CreateToggle("ControllerLightbar", root, Vector2.zero, "DualSense lightbar", "Match the controller light to the lantern", value =>
            {
                MenuPreferences.SetControllerLightbar(value); ControllerSettingChanged(pauseLightbarToggle);
            });
            pauseSpeakerToggle = CreateToggle("ControllerSpeaker", root, Vector2.zero, "DualSense speaker", "Lantern sounds on supported DualSense", value =>
            {
                MenuPreferences.SetControllerSpeaker(value); ControllerSettingChanged(pauseSpeakerToggle);
            });
            pauseDeadzoneSlider = CreateSlider("ControllerDeadzone", root, Vector2.zero, "Stick deadzone", "Ignore small stick movement and drift", null);
            pauseDeadzoneSlider.minValue = .05f; pauseDeadzoneSlider.maxValue = .4f;
            pauseDeadzoneSlider.SetValueWithoutNotify(MenuPreferences.StickDeadzone);
            pauseDeadzoneSlider.onValueChanged.AddListener(value =>
            {
                MenuPreferences.SetStickDeadzone(value); MarkPauseSettingsChanged(); UpdateSliderPercentLabel(pauseDeadzoneSlider);
            });
            ((MenuSlider)pauseDeadzoneSlider).NavigationStep = .01f;
            pauseSettingsRows[2] = new Selectable[] { pauseControllerStyleButton, pauseRumbleToggle, pauseTriggersToggle,
                pauseLightbarToggle, pauseSpeakerToggle, pauseDeadzoneSlider };
            pauseResetButton = settingsPanel.transform.Find("ResetDefaultsButton").GetComponent<Button>();
            SelectSettingsSection(0);
        }

        private void ReparentSetting(Transform row, int page) => row.SetParent(pauseSettingsPages[page].transform, false);

        private void ControllerSettingChanged(Toggle toggle)
        {
            MarkPauseSettingsChanged(); UpdateSwitchVisual(toggle);
        }

        private void SyncPauseControllerSettings()
        {
            if (pauseControllerStyleButton == null) return;
            pauseControllerStyleButton.GetComponentInChildren<TMP_Text>().text = new[] { "Automatic", "Xbox", "PlayStation", "Keyboard & mouse" }[Mathf.Clamp(MenuPreferences.ControllerStyle, 0, 3)];
            pauseRumbleToggle.SetIsOnWithoutNotify(MenuPreferences.Vibration);
            pauseTriggersToggle.SetIsOnWithoutNotify(MenuPreferences.AdaptiveTriggers);
            pauseLightbarToggle.SetIsOnWithoutNotify(MenuPreferences.ControllerLightbar);
            pauseSpeakerToggle.SetIsOnWithoutNotify(MenuPreferences.ControllerSpeaker);
            foreach (var toggle in new[] { pauseRumbleToggle, pauseTriggersToggle, pauseLightbarToggle, pauseSpeakerToggle }) UpdateSwitchVisual(toggle);
            pauseDeadzoneSlider.SetValueWithoutNotify(MenuPreferences.StickDeadzone);
            UpdateSliderPercentLabel(pauseDeadzoneSlider);
        }

        private void LayoutPauseSettingsWorkspace()
        {
            for (int page = 0; page < pauseSettingsRows.Length; page++)
            {
                var rows = pauseSettingsRows[page];
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i] is Slider || rows[i] == pauseControllerStyleButton ? (RectTransform)rows[i].transform.parent : (RectTransform)rows[i].transform;
                    MenuReadability.Row(row);
                    row.anchoredPosition = new Vector2(0, 160 - i * (page == 2 ? 66 : 86));
                    if (page == 2) row.sizeDelta = new Vector2(900, 64);
                    foreach (var text in row.GetComponentsInChildren<TMP_Text>())
                        if (text.name == "Subtitle") UsePauseBodyFont(text);
                }
            }
            foreach (var tab in pauseSettingsTabs)
            {
                ((RectTransform)tab.transform).sizeDelta = new Vector2(228, 56);
                tab.GetComponentInChildren<TMP_Text>().fontSize = 22;
            }
            SelectSettingsSection(pauseSettingsSection);
        }

        public void SelectSettingsSection(int section)
        {
            pauseSettingsSection = Mathf.Clamp(section, 0, pauseSettingsPages.Length - 1);
            for (int i = 0; i < pauseSettingsPages.Length; i++)
            {
                pauseSettingsPages[i].SetActive(i == pauseSettingsSection);
                var tab = pauseSettingsTabs[i];
                var rule = tab.transform.Find("Selection rule");
                if (rule != null) rule.gameObject.SetActive(i == pauseSettingsSection);
                tab.GetComponentInChildren<TMP_Text>().color = i == pauseSettingsSection ? amberGold : radiantIvory;
                tab.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = pauseSettingsTabs[(i + 3) % 4], selectOnRight = pauseSettingsTabs[(i + 1) % 4],
                    selectOnDown = pauseSettingsRows[pauseSettingsSection][0], selectOnUp = settingsBackButton };
            }
            SyncPauseControllerSettings();
            var activeRows = pauseSettingsRows[pauseSettingsSection];
            for (int i = 0; i < activeRows.Length; i++)
                activeRows[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? activeRows[i - 1] : pauseSettingsTabs[pauseSettingsSection],
                    selectOnDown = i + 1 < activeRows.Length ? activeRows[i + 1] : pauseResetButton };
            pauseResetButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = activeRows[activeRows.Length - 1], selectOnRight = settingsBackButton,
                selectOnDown = pauseSettingsTabs[pauseSettingsSection] };
            settingsBackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = activeRows[activeRows.Length - 1], selectOnLeft = pauseResetButton,
                selectOnDown = pauseSettingsTabs[pauseSettingsSection] };
            if (IsPaused && currentView == "settings" && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(activeRows[0].gameObject);
        }

        public void ShowControllerSettings()
        {
            ShowSettingsView(); SelectSettingsSection(2);
        }

        private Selectable FirstPauseSetting => pauseSettingsRows[pauseSettingsSection]?[0];

        private void RememberHomeSelection()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (currentView == "main" && selected != null && selected.transform.IsChildOf(mainPanel.transform)) lastHomeSelection = selected;
        }
    }
}
