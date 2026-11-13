using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinement
{
    static void Rect(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;}
    static RectTransform Container(string name,Transform parent){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
    static void Label(Button button,string value,int size=22)
    {var labels=button.GetComponentsInChildren<TMP_Text>(true);for(int i=0;i<labels.Length;i++){labels[i].gameObject.SetActive(i==0);if(i==0){labels[i].text=value;labels[i].fontSize=size;labels[i].enableAutoSizing=false;labels[i].alignment=TextAlignmentOptions.Center;labels[i].raycastTarget=false;Rect(labels[i].transform,new Vector2(.035f,.03f),new Vector2(.965f,.97f));}}}
    static Button Copy(Button template,Transform parent,string name,string text)
    {var b=Object.Instantiate(template,parent);b.name=name;b.gameObject.SetActive(true);b.onClick=new Button.ButtonClickedEvent();b.GetComponent<Image>().raycastTarget=true;Label(b,text);return b;}
    static void ApplyLayout()
    {
        var scene=Main();var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();if(ui.stageButtons!=null&&ui.stageButtons.Length==6)throw new Exception("Refinement layout already applied; not duplicating UI");
        var canvas=ui.breadcrumb.GetComponentInParent<Canvas>();var root=canvas.GetComponentsInChildren<Transform>(true).First(t=>t.name=="VRBimAiDashboard_Contextual");
        var left=ui.listTitle.transform.parent;var asset=ui.assetPanel.transform;var tabRoot=ui.tabs[0].transform.parent;var commandRoot=ui.detailButton.transform.parent;var sourceTemplate=ui.itemTemplate;
        var removed=new List<string>{"Node\tReason\tReferences checked"};
        foreach(var button in new[]{ui.focusButton,ui.measureButton,ui.statsButton}.Concat(ui.tabs.Skip(3)))if(button!=null){removed.Add(PathOf(button.transform)+"\tExplicitly removed action/top-level tab; data logic retained\tSerialized dashboard field cleared; runtime listener removed in adapter");Object.DestroyImmediate(button.gameObject);}
        ui.focusButton=ui.measureButton=ui.statsButton=null;ui.tabs=ui.tabs.Take(3).ToArray();Label(ui.backButton,"Quay lại");Label(ui.hideButton,"Cô lập tài sản");
        var leftBody=Container("DanhSach_Content",left);var leftChildren=left.Cast<Transform>().Where(t=>t!=leftBody).ToArray();foreach(var child in leftChildren)child.SetParent(leftBody,false);Rect(leftBody,Vector2.zero,Vector2.one);ui.leftNav=left.gameObject;ui.leftBody=leftBody.gameObject;
        var assetBody=Container("HoSo_Content",asset);var assetChildren=asset.Cast<Transform>().Where(t=>t!=assetBody).ToArray();foreach(var child in assetChildren)child.SetParent(assetBody,false);Rect(assetBody,Vector2.zero,Vector2.one);ui.assetBody=assetBody.gameObject;
        Rect(left,new Vector2(.012f,.10f),new Vector2(.235f,.865f));Rect(asset,new Vector2(.64f,.10f),new Vector2(.988f,.865f));Rect(ui.title.transform,new Vector2(.04f,.91f),new Vector2(.79f,.995f));ui.title.fontSize=25;
        tabRoot.SetParent(assetBody,false);tabRoot.name="AssetPrimaryTabs";Rect(tabRoot,new Vector2(.025f,.83f),new Vector2(.975f,.905f));
        foreach(var layout in tabRoot.GetComponents<LayoutGroup>())Object.DestroyImmediate(layout);var primary=tabRoot.gameObject.AddComponent<HorizontalLayoutGroup>();primary.spacing=6;primary.childControlWidth=primary.childControlHeight=true;primary.childForceExpandWidth=primary.childForceExpandHeight=true;
        string[] names={"Tổng quan","Thông số","Vòng đời"};for(int i=0;i<3;i++){Label(ui.tabs[i],names[i]);var le=ui.tabs[i].GetComponent<LayoutElement>()??ui.tabs[i].gameObject.AddComponent<LayoutElement>();le.preferredWidth=180;le.preferredHeight=52;}
        var stageRoot=Container("LifecycleSixStages",assetBody);Rect(stageRoot,new Vector2(.035f,.65f),new Vector2(.965f,.815f));var grid=stageRoot.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.cellSize=new Vector2(195,60);grid.spacing=new Vector2(9,9);ui.lifecycleNavigation=stageRoot;
        var reference=EditorSceneManager.OpenScene(ReferencePath,OpenSceneMode.Additive);var source=reference.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Button>(true)).First(b=>b.name=="Stg1");
        ui.stageButtons=RailwayBimStages.All.Select((stage,i)=>Copy(source,stageRoot,"Stg"+(i+1),stage.name)).ToArray();EditorSceneManager.CloseScene(reference,true);
        var secondary=Container("StageSections",assetBody);Rect(secondary,new Vector2(.035f,.575f),new Vector2(.965f,.635f));var sectionLayout=secondary.gameObject.AddComponent<HorizontalLayoutGroup>();sectionLayout.spacing=7;sectionLayout.childControlWidth=sectionLayout.childControlHeight=true;sectionLayout.childForceExpandWidth=sectionLayout.childForceExpandHeight=true;
        ui.stageProfileButton=Copy(sourceTemplate,secondary,"StageProfile","Hồ sơ");ui.stageRecordsButton=Copy(sourceTemplate,secondary,"StageRecords","Kiểm tra");ui.stageHistoryButton=Copy(sourceTemplate,secondary,"StageHistory","Nhật ký");
        foreach(var button in secondary.GetComponentsInChildren<Button>()){var le=button.GetComponent<LayoutElement>()??button.gameObject.AddComponent<LayoutElement>();le.preferredWidth=180;le.preferredHeight=44;}
        Rect(ui.fieldsContent.GetComponentInParent<ScrollRect>().transform,new Vector2(.025f,.12f),new Vector2(.975f,.815f));
        ui.collapseNavButton=Copy(sourceTemplate,left,"CollapseNavigation","Thu gọn");ui.collapseAssetButton=Copy(sourceTemplate,asset,"CollapseAsset","Thu gọn");foreach(var button in new[]{ui.collapseNavButton,ui.collapseAssetButton})Rect(button.transform,new Vector2(.80f,.936f),new Vector2(.98f,.995f));
        Rect(ui.listTitle.transform,new Vector2(.04f,.91f),new Vector2(.79f,.99f));ui.listTitle.fontSize=21;
        foreach(var layout in commandRoot.GetComponents<LayoutGroup>())Object.DestroyImmediate(layout);
        ui.commandPanel=(RectTransform)commandRoot;Rect(commandRoot,new Vector2(.248f,.10f),new Vector2(.625f,.285f));
        var vertical=commandRoot.gameObject.AddComponent<VerticalLayoutGroup>();vertical.spacing=8;vertical.childControlWidth=vertical.childControlHeight=true;vertical.childForceExpandWidth=vertical.childForceExpandHeight=true;
        var buttons=new[]{ui.detailButton,ui.backButton,ui.resetButton,ui.hideButton,ui.exportButton,ui.importButton,ui.applyButton};
        for(int row=0;row<2;row++)
        {var holder=Container("CommandRow"+row,commandRoot);var layout=holder.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=7;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;var height=holder.gameObject.AddComponent<LayoutElement>();height.preferredHeight=72;
            foreach(var b in buttons.Skip(row==0?0:4).Take(row==0?4:3)){b.transform.SetParent(holder,false);var le=b.GetComponent<LayoutElement>()??b.gameObject.AddComponent<LayoutElement>();le.preferredWidth=180;le.preferredHeight=70;Label(b,b.GetComponentInChildren<TMP_Text>().text,20);}}
        var header=ui.breadcrumb.transform.parent;var tools=Container("PresentationControls",header);Rect(tools,new Vector2(.70f,.03f),new Vector2(.99f,.38f));var toolLayout=tools.gameObject.AddComponent<HorizontalLayoutGroup>();toolLayout.spacing=8;toolLayout.childControlWidth=toolLayout.childControlHeight=true;toolLayout.childForceExpandWidth=toolLayout.childForceExpandHeight=true;
        ui.toggleUiButton=Copy(sourceTemplate,tools,"HideManagementUI","Ẩn giao diện · F2");ui.recenterUiButton=Copy(sourceTemplate,tools,"RecenterManagementUI","Đặt lại bảng VR");Rect(ui.breadcrumb.transform,new Vector2(.015f,.03f),new Vector2(.69f,.38f));
        ui.status.text="Chọn tài sản để tự focus · F: về lựa chọn · F2: ẩn UI / camera tự do · VR: X giao diện, Y đổi không gian; ẩn UI để di chuyển";
        // Classify and delete only maximal inactive clone remnants with no external semantic serialized reference.
        var protectedObjects=new HashSet<GameObject>();foreach(var mb in new MonoBehaviour[]{ui,ui.controller,ui.GetComponent<RailwayBimVrPresentation>()})
        {var so=new SerializedObject(mb);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference){var obj=p.objectReferenceValue;var go=obj as GameObject??(obj as Component)?.gameObject;if(go!=null)protectedObjects.Add(go);}}
        void Clean(Transform parent)
        {
            foreach(var child in parent.Cast<Transform>().ToArray())
            {
                bool protect=protectedObjects.Any(g=>g!=null&&(g.transform==child||g.transform.IsChildOf(child)))||child.name=="ReusableTemplates";
                if(!child.gameObject.activeSelf&&!protect)
                {
                    var blockers=new List<string>();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&!(c is Transform)&&!c.transform.IsChildOf(child));
                    foreach(var owner in all){try{var so=new SerializedObject(owner);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference){var target=p.objectReferenceValue as GameObject??(p.objectReferenceValue as Component)?.gameObject;if(target!=null&&(target.transform==child||target.transform.IsChildOf(child)))blockers.Add(owner.GetType().Name+"."+p.propertyPath);}}catch(Exception){}}
                    if(blockers.Count==0){removed.Add(PathOf(child)+"\tInactive legacy clone; production code uses explicit references, no legacy dashboard controller\tNo inbound semantic reference; template/conditional objects protected; prefab source unchanged");Object.DestroyImmediate(child.gameObject);continue;}
                    removed.Add(PathOf(child)+"\tKEPT: insufficient evidence\t"+string.Join(";",blockers));
                }
                if(child.name!="ReusableTemplates")Clean(child);
            }
        }
        Clean(canvas.transform);File.WriteAllLines(Dir+"/Cleanup.tsv",removed);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        File.WriteAllText(Dir+"/Layout.txt","Saved and reopened. Three primary tabs in one asset panel; six source Stg buttons cloned with original image/font styling and 1.04 active scale. Seven action buttons relaid without placeholder slots. Legacy data logic retained; unused inactive UI clone branches removed only after reference checks. Source prefabs untouched.");
    }
}
