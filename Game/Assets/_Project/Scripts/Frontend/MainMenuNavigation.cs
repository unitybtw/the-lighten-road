using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class MainMenuController
    {
        private GameObject optionsIndex;
        private Button keyboardSettingsButton;
        private TMP_Text optionsHeading,optionsSubheading;
        private bool controlsFromOptions;
        private GameObject controllerGuide;
        private ControllerSchematic controllerDiagram;
        private TMP_Text[] diagramLabels;
        public bool IsOptionsIndex => optionsIndex!=null && optionsIndex.activeSelf;

        private void BuildOptionsIndex(RectTransform surface)
        {
            optionsHeading=surface.Find("Heading").GetComponent<TMP_Text>();
            optionsSubheading=surface.Find("Subheading").GetComponent<TMP_Text>();
            var index=CreateAnchoredRect("Options index",surface,new Vector2(560,380),Vector2.one*.5f,Vector2.zero);
            optionsIndex=index.gameObject;
            string[] titles={"Audio","Video","Controller","Accessibility"};
            for(int i=0;i<4;i++)
            {
                settingsTabs[i].transform.SetParent(index,false);
                var r=(RectTransform)settingsTabs[i].transform;r.anchoredPosition=new Vector2(0,130-i*62);r.sizeDelta=new Vector2(440,52);
                settingsTabLabels[i].text=titles[i];settingsTabLabels[i].fontSize=26;
                if(ArchiveMenuStyle.Serif!=null)settingsTabLabels[i].font=ArchiveMenuStyle.Serif;
            }
            keyboardSettingsButton=SettingsButton(index,"Keyboard & guide",new Vector2(0,-118),new Vector2(440,52),OpenKeyboardSettings);
            keyboardSettingsButton.GetComponentInChildren<TMP_Text>().fontSize=26;
            foreach(var button in new[]{settingsDoneButton,resetSettingsButton})
            {
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=22;label.fontStyle=FontStyles.Normal;label.characterSpacing=1.4f;
            }
            settingsDoneButton.onClick.RemoveAllListeners();settingsDoneButton.onClick.AddListener(BackFromSettings);
            ((RectTransform)settingsDoneButton.transform).anchoredPosition=new Vector2(0,-282);
            ((RectTransform)resetSettingsButton.transform).anchoredPosition=new Vector2(0,-230);
            settingsSaveStatus.rectTransform.anchoredPosition=new Vector2(0,-189);
            settingsFooterBar.anchoredPosition=new Vector2(0,-336);
            // The index replaces dense always-visible tabs. Bumpers still provide optional page-to-page shortcuts.
            leftBumperImg.gameObject.SetActive(false);rightBumperImg.gameObject.SetActive(false);
            var rule=surface.Find("Top tab rule");if(rule!=null)rule.gameObject.SetActive(false);
            var crest=surface.Find("Settings Bottom Filigree");if(crest!=null)crest.gameObject.SetActive(false);
            var footerRule=surface.Find("Footer rule");if(footerRule!=null)footerRule.gameObject.SetActive(false);
            ConfigureOptionsIndexNavigation();
        }
        private void ConfigureOptionsIndexNavigation()
        {
            Selectable[] choices={settingsTabs[0],settingsTabs[1],settingsTabs[2],settingsTabs[3],keyboardSettingsButton,settingsDoneButton};
            for(int i=0;i<choices.Length;i++)choices[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnUp=choices[(i+choices.Length-1)%choices.Length],selectOnDown=choices[(i+1)%choices.Length]};
        }
        private void UpdateOptionsFooterLayout()
        {
            if(settingsFooterBar==null)return;
            bool index=IsOptionsIndex;
            var reset=settingsFooterBar.Find("Slot_Reset");if(reset!=null)reset.gameObject.SetActive(!index);
            var tabs=settingsFooterBar.Find("Slot_Tab");if(tabs!=null)tabs.gameObject.SetActive(!index);
            var select=settingsFooterBar.Find("Slot_Select");if(select!=null)((RectTransform)select).anchoredPosition=new Vector2(index?-110:-320,0);
            var back=settingsFooterBar.Find("Slot_Back");if(back!=null)((RectTransform)back).anchoredPosition=new Vector2(index?110:-110,0);
        }
        private void OpenOptionsIndex()
        {
            settingsSection=-1;optionsIndex.SetActive(true);settingsResetPanel.SetActive(false);
            foreach(var section in settingsSections)section.SetActive(false);
            foreach(var tab in settingsTabs){tab.interactable=true;var focus=tab.GetComponent<SettingsLightFocus>();if(focus!=null)focus.IsCurrent=false;}
            settingsDoneButton.interactable=true;resetSettingsButton.gameObject.SetActive(false);settingsSaveStatus.gameObject.SetActive(false);
            optionsHeading.text="Options";optionsSubheading.gameObject.SetActive(false);
            ConfigureOptionsIndexNavigation();RefreshSettingsFooterPrompts();
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(settingsTabs[0].gameObject);
        }
        private void ShowOptionsPage(int page)
        {
            if(optionsIndex==null)return;
            optionsIndex.SetActive(false);
            optionsHeading.text=new[]{"Audio","Video","Controller","Accessibility"}[page];
            optionsSubheading.gameObject.SetActive(false);
            resetSettingsButton.gameObject.SetActive(true);settingsSaveStatus.gameObject.SetActive(true);
        }
        public void BackFromSettings()
        {
            if(IsSettingsResetOpen){CancelSettingsReset();return;}
            FlushSettingsSave();
            if(IsOptionsIndex)ShowHome();else OpenOptionsIndex();
        }
        private void OpenKeyboardSettings(){ShowControls();controlsFromOptions=true;SetControlsDeviceTab(0);}
        private void BackFromControls(){if(controlsFromOptions)ShowSettings();else ShowHome();}
        private void BuildControllerGuide(RectTransform surface)
        {
            var root=CreateAnchoredRect("Controller schematic guide",surface,new Vector2(940,350),Vector2.one*.5f,new Vector2(0,18));
            controllerGuide=root.gameObject;
            var shape=CreateAnchoredRect("Archive controller drawing",root,new Vector2(800,360),Vector2.one*.5f,Vector2.zero);
            controllerDiagram=shape.gameObject.AddComponent<ControllerSchematic>();controllerDiagram.color=new Color(.80f,.83f,.82f,.78f);controllerDiagram.raycastTarget=false;
            diagramLabels=new TMP_Text[6];
            Vector2[] pos={new Vector2(-432,65),new Vector2(-432,-15),new Vector2(432,100),new Vector2(432,48),new Vector2(432,-8),new Vector2(432,-65)};
            for(int i=0;i<6;i++)
            {
                diagramLabels[i]=CreateLabel("Controller callout "+i,root,"",16,moonlightSilver,new Vector2(240,42),Vector2.one*.5f,pos[i],Vector2.one*.5f);
                diagramLabels[i].alignment=i<2?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
            }
            controllerGuide.SetActive(false);
        }
        private void UpdateControllerGuide(int tab)
        {
            if(controllerGuide==null)return;
            bool pad=tab!=0;controllerGuide.SetActive(pad);
            for(int i=0;i<controlsRowKeyLabels.Length;i++)if(controlsRowKeyLabels[i]!=null)controlsRowKeyLabels[i].transform.parent.gameObject.SetActive(!pad);
            if(!pad)return;
            bool ps=tab==2;controllerDiagram.SetSymmetric(ps);
            string[] text={"Move  /  Left stick or D-pad",ps?"Sprint  /  L3":"Sprint  /  Left stick click",ps?"Pause  /  Options":"Pause  /  Menu",ps?"Interact  /  Triangle":"Interact  /  Y",ps?"Flare  /  Square":"Flare  /  X",ps?"Jump  /  Cross":"Jump  /  A"};
            for(int i=0;i<6;i++)diagramLabels[i].text=text[i];
        }
    }
}
