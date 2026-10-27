using TMPro;
using UnityEngine;

namespace TheLightenRoad.Frontend
{
    public sealed partial class MainMenuController
    {
        private void ApplySettingsReadability(RectTransform surface)
        {
            MenuReadability.EnlargeText(surface);
            foreach(var section in settingsSections)
            {
                var r=(RectTransform)section.transform;
                MenuReadability.Place(r,Vector2.zero,new Vector2(900,500));
                var description=r.GetChild(0) as RectTransform;
                if(description!=null)description.anchoredPosition=new Vector2(0,194);
            }
            var sliders=new[]{masterSlider,musicSlider,effectsSlider,deadzoneSlider};
            foreach(var slider in sliders)MenuReadability.Row((RectTransform)slider.transform.parent);
            foreach(var toggle in new[]{fullscreenToggle,vsyncToggle,motionToggle,vibrationToggle,adaptiveTriggersToggle,controllerSpeakerToggle,controllerLightbarToggle})
                MenuReadability.Row((RectTransform)toggle.transform);
            float[] rows={130,52,-26,-104};
            for(int i=0;i<3;i++)((RectTransform)sliders[i].transform.parent).anchoredPosition=new Vector2(0,rows[i]);
            ((RectTransform)controllerSpeakerToggle.transform).anchoredPosition=new Vector2(0,rows[3]);
            var speakerPill=(RectTransform)controllerSpeakerToggle.transform.Find("Pill");
            MenuReadability.Place(speakerPill,new Vector2(-180,0),new Vector2(140,52));
            MenuReadability.Place((RectTransform)speakerTestButton.transform,new Vector2(366,rows[3]),new Vector2(140,56));
            MenuReadability.Place(speakerTestStatus.rectTransform,new Vector2(0,-166),new Vector2(900,40));
            var style=(RectTransform)controllerStyleButton.transform.parent;MenuReadability.Row(style);style.anchoredPosition=new Vector2(0,130);
            MenuReadability.Place((RectTransform)controllerStyleButton.transform,new Vector2(284,0),new Vector2(300,56));
            controllerStyleValueText.fontSize=20;
            var controlRows=new[]{vibrationToggle.transform,adaptiveTriggersToggle.transform,controllerLightbarToggle.transform,deadzoneSlider.transform.parent};
            for(int i=0;i<controlRows.Length;i++)((RectTransform)controlRows[i]).anchoredPosition=new Vector2(0,52-i*78);
            ((RectTransform)fullscreenToggle.transform).anchoredPosition=new Vector2(0,116);
            ((RectTransform)vsyncToggle.transform).anchoredPosition=new Vector2(0,16);
            var frame=(RectTransform)settingsSections[1].transform.Find("Target Frame Rate");MenuReadability.Row(frame);frame.anchoredPosition=new Vector2(0,-96);
            for(int i=0;i<frameRateOptions.Length;i++)
            {
                MenuReadability.Place((RectTransform)frameRateOptions[i].transform,new Vector2(100+i*140,0),new Vector2(132,56));
                frameRateLabels[i].fontSize=20;
            }
            frame.Find("Title").GetComponent<RectTransform>().sizeDelta=new Vector2(400,32);
            frameRateHint.rectTransform.sizeDelta=new Vector2(420,32);
            var comfort=settingsSections[3].transform.Find("Motion Comfort Card");
            foreach(var text in comfort.GetComponentsInChildren<TMP_Text>())text.textWrappingMode=TextWrappingModes.Normal;
            var note=comfort.GetChild(1) as RectTransform;if(note!=null)note.sizeDelta=new Vector2(660,72);
            MenuReadability.Place(settingsSaveStatus.rectTransform,new Vector2(0,-252),new Vector2(900,32));
            MenuReadability.Place((RectTransform)resetSettingsButton.transform,new Vector2(-196,-306),new Vector2(360,56));
            MenuReadability.Place((RectTransform)settingsDoneButton.transform,new Vector2(196,-306),new Vector2(360,56));
            MenuReadability.Footer(settingsFooterBar);settingsFooterBar.anchoredPosition=new Vector2(0,-378);
            for(int i=0;i<settingsTabs.Length;i++)
                MenuReadability.Place((RectTransform)settingsTabs[i].transform,new Vector2(0,140-i*68),new Vector2(480,60));
            MenuReadability.Place((RectTransform)keyboardSettingsButton.transform,new Vector2(0,-132),new Vector2(480,60));
            // The reset dialog is a modal: keep its buttons comfortably sized without changing focus ownership.
            foreach(var button in settingsResetPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            { var r=(RectTransform)button.transform;r.sizeDelta=new Vector2(290,56); }
            MenuReadability.RefreshFocus(surface);
        }
        private void ApplyControlsReadability()
        {
            MenuReadability.EnlargeText(controls);
            var surface=(RectTransform)controllerDiagram.transform.parent.parent;
            var tabs=(RectTransform)surface.Find("Controls Device Tabs");
            MenuReadability.Place(tabs,new Vector2(0,205),new Vector2(1040,56));
            var heading=surface.Find("Heading") as RectTransform;
            if(heading!=null)heading.anchoredPosition=new Vector2(0,312);
            var ornament=surface.Find("Header Bottom Filigree") as RectTransform;
            if(ornament!=null)ornament.anchoredPosition=new Vector2(0,260);
            var spark=surface.Find("Header Lantern Spark") as RectTransform;
            if(spark!=null)spark.anchoredPosition=new Vector2(0,266);
            for(int i=0;i<controlsDeviceTabs.Length;i++)
            {
                MenuReadability.Place((RectTransform)controlsDeviceTabs[i].transform,new Vector2((i-1)*300,0),new Vector2(260,56));
                controlsDeviceTabLabels[i].fontSize=22;
                controlsDeviceTabLabels[i].rectTransform.sizeDelta=new Vector2(240,36);
                MenuReadability.CompactDeviceTab(controlsDeviceTabs[i]);
            }
            for(int i=0;i<controlsRowKeyLabels.Length;i++)
            {
                var key=controlsRowKeyLabels[i];var row=(RectTransform)key.transform.parent;
                MenuReadability.Place(row,new Vector2(0,130-i*60),new Vector2(960,60));
                var description=row.Find("Description") as RectTransform;if(description!=null)description.sizeDelta=new Vector2(520,30);
                key.rectTransform.sizeDelta=new Vector2(280,36);
                var title=row.Find("Action")?.GetComponent<TMP_Text>();if(title!=null)title.fontSize=22;
            }
            var guide=(RectTransform)controllerGuide.transform;guide.anchoredPosition=new Vector2(0,-16);
            MenuReadability.Place((RectTransform)controllerDiagram.transform,Vector2.zero,new Vector2(960,432));
            var artwork=controllerDiagram.transform.Find("Device artwork") as RectTransform;
            if(artwork!=null)artwork.sizeDelta=new Vector2(552,552f*940f/1452f);
            controllerDiagram.SetVerticesDirty();
            Vector2[] positions={new Vector2(-490,78),new Vector2(-490,-18),new Vector2(490,120),new Vector2(490,57.6f),new Vector2(490,-9.6f),new Vector2(490,-78)};
            if(diagramLabels!=null)for(int i=0;i<diagramLabels.Length;i++)
            {
                var label=diagramLabels[i];label.fontSize=20;label.textWrappingMode=TextWrappingModes.Normal;
                MenuReadability.Place(label.rectTransform,positions[i],new Vector2(220,i<2?64:50));
            }
            MenuReadability.RefreshFocus(controls);
            SetControlsDeviceTab(selectedControlsDeviceTab);
        }
    }
}
