using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class PauseMenuController
    {
        private void ApplyPauseReadability()
        {
            MenuReadability.EnlargeText(modalRect);
            var settingsRoot=(RectTransform)settingsPanel.transform;
            settingsRoot.sizeDelta=new Vector2(1040,720);
            var settingsTitle=settingsRoot.Find("SettingsTitle")?.GetComponent<TMP_Text>();
            if(settingsTitle!=null){settingsTitle.fontSize=32;settingsTitle.rectTransform.anchoredPosition=new Vector2(0,305);settingsTitle.rectTransform.sizeDelta=new Vector2(800,48);}
            var crown=settingsRoot.Find("Settings Top Filigree") as RectTransform;
            if(crown!=null)crown.anchoredPosition=new Vector2(0,355);
            Transform[] rows={masterSlider.transform.parent,musicSlider.transform.parent,effectsSlider.transform.parent,fullscreenToggle.transform,vsyncToggle.transform,reducedMotionToggle.transform};
            for(int i=0;i<rows.Length;i++)
            { var r=(RectTransform)rows[i];MenuReadability.Row(r);r.anchoredPosition=new Vector2(0,220-i*78); }
            var bottom=settingsRoot.Find("Settings Bottom Filigree");if(bottom!=null)bottom.gameObject.SetActive(false);
            var reset=settingsRoot.Find("ResetDefaultsButton") as RectTransform;
            if(reset!=null)MenuReadability.Place(reset,new Vector2(-196,-286),new Vector2(360,56));
            MenuReadability.Place((RectTransform)settingsBackButton.transform,new Vector2(196,-286),new Vector2(360,56));
            var controlsRoot=(RectTransform)controlsPanel.transform;controlsRoot.sizeDelta=new Vector2(1040,760);
            var title=controlsRoot.Find("ControlsTitle")?.GetComponent<TMP_Text>();
            if(title!=null){title.fontSize=32;MenuReadability.Place(title.rectTransform,new Vector2(0,305),new Vector2(800,48));}
            var ornament=controlsRoot.Find("Controls Top Filigree") as RectTransform;if(ornament!=null)ornament.anchoredPosition=new Vector2(0,355);
            var tabs=(RectTransform)controlsRoot.Find("Pause Controls Tabs");tabs.anchoredPosition=new Vector2(0,232);tabs.sizeDelta=new Vector2(1040,56);
            for(int i=0;i<3;i++)
            {
                MenuReadability.Place((RectTransform)pauseControlsDeviceTabs[i].transform,new Vector2((i-1)*300,0),new Vector2(260,56));
                pauseControlsDeviceTabLabels[i].fontSize=20;
                pauseControlsDeviceTabLabels[i].rectTransform.sizeDelta=new Vector2(240,36);
                MenuReadability.CompactDeviceTab(pauseControlsDeviceTabs[i]);
            }
            foreach(var name in new[]{"Left Bumper Badge","Right Bumper Badge"})
            {
                var r=tabs.Find(name) as RectTransform;
                if(r!=null)
                {
                    MenuReadability.Place(r,new Vector2(name.StartsWith("Left")?-500:500,0),new Vector2(64,52));
                    r.GetComponent<Image>().raycastTarget=true;
                }
            }
            for(int i=0;i<pauseControlsRowKeyLabels.Length;i++)
            {
                var key=pauseControlsRowKeyLabels[i];var badge=(RectTransform)key.transform.parent;var row=(RectTransform)badge.parent;
                MenuReadability.Place(row,new Vector2(0,150-i*60),new Vector2(960,58));
                badge.anchorMin=badge.anchorMax=new Vector2(0,.5f);badge.pivot=new Vector2(0,.5f);
                MenuReadability.Place(badge,new Vector2(0,0),new Vector2(340,50));
                key.fontSize=20;key.characterSpacing=.5f;
                key.rectTransform.anchorMin=key.rectTransform.anchorMax=new Vector2(0,.5f);key.rectTransform.pivot=new Vector2(0,.5f);
                MenuReadability.Place(key.rectTransform,new Vector2(52,0),new Vector2(280,36));
                var action=row.Find("Action")?.GetComponent<TMP_Text>();
                var description=row.Find("Description")?.GetComponent<TMP_Text>();
                if(action!=null){action.fontSize=22;MenuReadability.Place(action.rectTransform,new Vector2(370,14),new Vector2(560,32));}
                if(description!=null){description.fontSize=20;MenuReadability.Place(description.rectTransform,new Vector2(370,-16),new Vector2(560,30));}
            }
            MenuReadability.Place((RectTransform)pausePromptStyleButton.transform,new Vector2(-196,-232),new Vector2(360,56));
            MenuReadability.Place((RectTransform)pauseVibrationButton.transform,new Vector2(196,-232),new Vector2(360,56));
            MenuReadability.Place((RectTransform)controlsBackButton.transform,new Vector2(0,-300),new Vector2(360,56));
            MenuReadability.Footer(pauseFooterBar);pauseFooterBar.anchoredPosition=new Vector2(0,-378);
            MenuReadability.RefreshFocus(modalRect);
            SetPauseControlsDeviceTab(selectedPauseControlsDeviceTab);
        }
    }
}
