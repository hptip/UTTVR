using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class ShowroomPackageMergeTools
{
    static void Place(RectTransform r, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
    {
        r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=lo;r.offsetMax=hi;
    }
    static void FixTrainPopup()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if(!scene.isLoaded)throw new Exception("Open Showroom before applying the popup layout");
        var ui=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TrainInfoPopupUI>(true)).Single();
        var panel=(RectTransform)ui.popupPanel.transform;
        Undo.RegisterFullObjectHierarchyUndo(ui.gameObject,"Fix train information popup layout");
        // Fixed-size panel: independent of the desktop/Game View resolution in XR.
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);
        panel.sizeDelta=new Vector2(760,620);panel.anchoredPosition=Vector2.zero;panel.localScale=Vector3.one;
        foreach(string name in new[]{"Background_Solid","Frame_Border"})
        {
            var t=panel.Find(name) as RectTransform;if(t==null)continue;
            Place(t,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var image=t.GetComponent<Image>();if(image!=null)image.raycastTarget=false;
        }
        var title=ui.titleText;Place(title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-82),new Vector2(-28,-20));
        title.enableAutoSizing=true;title.fontSizeMin=22;title.fontSizeMax=30;
        title.overflowMode=TextOverflowModes.Ellipsis;title.raycastTarget=false;
        var scroll=ui.scrollRect;
        if(scroll==null)scroll=ui.popupPanel.GetComponentInChildren<ScrollRect>(true);
        if(scroll==null)throw new Exception("Train popup has no ScrollRect");
        ui.scrollRect=scroll;
        if(scroll.viewport==null)scroll.viewport=scroll.transform.Find("Viewport") as RectTransform;
        if(scroll.content==null)scroll.content=scroll.viewport.Find("Content") as RectTransform;
        if(scroll.verticalScrollbar==null)scroll.verticalScrollbar=scroll.GetComponentInChildren<Scrollbar>(true);
        Place((RectTransform)scroll.transform,Vector2.zero,Vector2.one,new Vector2(28,104),new Vector2(-28,-98));
        scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity=35;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        var viewport=scroll.viewport;
        Place(viewport,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-34,0));
        if(viewport.GetComponent<RectMask2D>()==null)Undo.AddComponent<RectMask2D>(viewport.gameObject);
        var content=scroll.content;
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
        content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;content.localScale=Vector3.one;
        var layout=content.GetComponent<VerticalLayoutGroup>();
        if(layout==null)layout=Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        layout.padding=new RectOffset(8,8,8,8);
        var fitter=content.GetComponent<ContentSizeFitter>();if(fitter==null)fitter=Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var details=ui.detailsText;details.fontSize=24;details.enableAutoSizing=false;
        details.textWrappingMode=TextWrappingModes.Normal;details.overflowMode=TextOverflowModes.Overflow;
        details.alignment=TextAlignmentOptions.TopLeft;details.raycastTarget=false;details.maskable=true;
        var imageBg=scroll.GetComponent<Image>();if(imageBg==null)imageBg=Undo.AddComponent<Image>(scroll.gameObject);
        imageBg.color=new Color(.06f,.09f,.15f,.4f);imageBg.raycastTarget=true;
        if(scroll.verticalScrollbar!=null)
        {
            var bar=scroll.verticalScrollbar;
            Place((RectTransform)bar.transform,new Vector2(1,0),Vector2.one,new Vector2(-26,0),Vector2.zero);
            bar.gameObject.SetActive(true);
            Place((RectTransform)bar.handleRect.parent,Vector2.zero,Vector2.one,new Vector2(3,3),new Vector2(-3,-3));
        }
        var close=ui.closeButton;var rect=(RectTransform)close.transform;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.sizeDelta=new Vector2(146,68);
        rect.anchoredPosition=new Vector2(-24,20);rect.localScale=Vector3.one;rect.SetAsLastSibling();
        close.interactable=true;close.navigation=new Navigation{mode=Navigation.Mode.None};
        var bg=close.GetComponent<Image>();bg.sprite=null;bg.color=new Color(.7f,.13f,.17f,1);bg.raycastTarget=true;close.targetGraphic=bg;
        var colors=close.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.8f,.35f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.6f,.6f,.6f);close.colors=colors;
        var caption=close.GetComponentInChildren<TMP_Text>(true);caption.text="X  Đóng";caption.fontSize=26;caption.color=Color.white;caption.alignment=TextAlignmentOptions.Center;caption.raycastTarget=false;
        var canvas=ui.GetComponent<Canvas>();canvas.sortingOrder=200;
        foreach(var component in ui.GetComponentsInChildren<Component>(true))
            if(component!=null){EditorUtility.SetDirty(component);if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Dir+"/TrainPopupLayout.txt","SAVED | 760x620 panel, masked scrolling details, automatic content height, reserved footer, 146x68 close button bottom-right. Existing content and interactions retained.");
    }
    static void CheckPopupLayout()
    {
        var popup=TrainInfoPopupUI.Instance;
        CheckTest(popup!=null,"Train popup singleton initialized");
        CheckTest(popup.scrollRect!=null,"Saved popup ScrollRect reference resolves");
        foreach(var train in Object.FindObjectsByType<TrainInteractable>(FindObjectsSortMode.None))
        {
            train.OnInteract();Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(popup.scrollRect.content);Canvas.ForceUpdateCanvases();
            CheckTest(popup.popupPanel.activeSelf&&popup.detailsText.text==train.trainSpecifications,"Train popup preserves full specifications: "+train.name);
            CheckTest(popup.scrollRect.viewport.rect.width>500&&popup.scrollRect.viewport.rect.height>350,"Viewport has positive usable dimensions");
            CheckTest(popup.scrollRect.viewport.GetComponent<RectMask2D>()!=null,"Information is clipped to the scrolling viewport");
            CheckTest(popup.scrollRect.content.rect.height+1>=popup.detailsText.preferredHeight,"Content height accommodates full train information");
            var panel=(RectTransform)popup.popupPanel.transform;var corners=new Vector3[4];popup.closeButton.GetComponent<RectTransform>().GetWorldCorners(corners);
            CheckTest(corners.All(c=>panel.rect.Contains(panel.InverseTransformPoint(c))),"Close button entirely inside panel");
            popup.closeButton.onClick.Invoke();CheckTest(!popup.popupPanel.activeSelf,"Close callback works: "+train.name);
        }
        PrepareXrTest();Object.FindObjectsByType<TrainInteractable>(FindObjectsSortMode.None).First().OnInteract();
        // The head-relative canvas updates in LateUpdate after the test camera moves.
        WaitTest(.8,29);
    }
}
