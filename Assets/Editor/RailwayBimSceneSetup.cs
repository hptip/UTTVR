using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailwayBimBuild
{
    static void Seed(RailwayBimCatalog catalog)
    {
        const string seedPath="Assets/StreamingAssets/RailwayLifecycle/Initial.db";Directory.CreateDirectory(Path.GetDirectoryName(seedPath));
        using(var store=new RailwayBimStore(seedPath))
        {
            if(store.Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version=?",catalog.seedVersion)>0)return;
            int ordinal=0;
            store.Db.RunInTransaction(()=>
            {
                void Add(BimAsset a,Bounds bounds)
                {
                    if(store.Assets.ContainsKey(a.AssetID))return;
                    store.Db.Execute("INSERT INTO Assets VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",a.AssetID,a.ParentAssetID,a.AssetType,a.DisplayName,a.ZoneID,a.TrackID,a.SegmentID,a.BlockID,a.ComponentType,a.ChainageStart,a.ChainageEnd,a.ModelReference);store.Assets.Add(a.AssetID,a);
                    foreach(var p in new[]{("ModelWidth",bounds.size.x),("ModelHeight",bounds.size.y),("ModelLength",bounds.size.z)})store.Property(a.AssetID,p.Item1,p.Item2.ToString("G7",CultureInfo.InvariantCulture),"m","Number",false);
                    store.Property(a.AssetID,"DataProvenance","DEMO — kích thước hình học từ model; hồ sơ vòng đời giả lập phục vụ học tập.","","Text",false);
                    store.Property(a.AssetID,"ModelSource",a.ModelReference,"","Text",false);
                    int scenario=ordinal++%6;string[] grades={"Tốt","Cần theo dõi","Cần kiểm tra","Cần bảo trì","Đã bảo trì","Đề xuất thay thế"};
                    string finding=scenario==0?"DEMO: kiểm tra định kỳ, chưa ghi nhận hư hỏng.":a.ComponentType=="Bolt"?"DEMO: cần kiểm tra lực siết và liên kết, không phải kết luận hiện trường.":a.ComponentType=="Rail"?"DEMO: theo dõi mài mòn bề mặt lăn; không áp dụng ngưỡng an toàn giả định.":"DEMO: quan sát tình trạng bề mặt và liên kết theo kịch bản học tập.";
                    var life=new BimLifecycle{AssetID=a.AssetID,InstallDate="2022-04-15",CommissioningDate="2022-06-01",DesignLifeYears=a.ComponentType=="Bolt"?15:30,ConditionGrade=grades[scenario],OperationalStatus="Đang vận hành — DEMO",LastInspectionDate="2026-06-10",NextInspectionDate="2027-01-10",MaintenanceStatus=scenario==4?"Đã bảo trì — DEMO":scenario==3?"Có kế hoạch — DEMO":"Theo dõi định kỳ — DEMO",ReplacementPlanDate=scenario==5?"2027-09-01":"",Notes="DEMO: tuổi thọ, ngày và chi phí là giả định, không phải tiêu chuẩn hay hồ sơ đo thực tế."};store.SaveLifecycle(life,false);
                    store.Db.Execute("INSERT INTO AssetInspections VALUES(?,?,?,?,?,?,?)","DEMO-I-"+a.AssetID,a.AssetID,"2026-06-10",grades[scenario],finding,"Nhóm mô phỏng UTT","Dữ liệu DEMO, không phải kiểm định thật.");
                    if(scenario==4||scenario==5)store.Db.Execute("INSERT INTO AssetMaintenance VALUES(?,?,?,?,?,?,?)","DEMO-M-"+a.AssetID,a.AssetID,"2026-08-15","Bảo trì mô phỏng",a.ComponentType=="Rail"?"DEMO: vệ sinh và kiểm tra bề mặt ray":"DEMO: vệ sinh, kiểm tra liên kết và ghi nhận hiện trạng",a.ComponentType=="Bolt"?120000:750000,"Hoàn thành theo kịch bản DEMO");
                    store.Db.Execute("INSERT INTO AssetLifecycleEvents(EventID,AssetID,EventType,EventDate,Description,PreviousValue,NewValue) VALUES(?,?,?,?,?,?,?)","DEMO-E-"+a.AssetID,a.AssetID,"DemoSeed","2026-11-12","Khởi tạo hồ sơ mô phỏng phiên bản "+catalog.seedVersion,"",grades[scenario]);
                }
                foreach(var s in catalog.segments)Add(new BimAsset{AssetID=s.id,AssetType="Segment",DisplayName=s.id,ZoneID=catalog.zoneID,TrackID=s.track,SegmentID=s.id,ChainageStart=s.start,ChainageEnd=s.end,ModelReference="Zone_021/"+s.id},s.bounds);
                foreach(var b in catalog.blocks)
                {
                    Add(new BimAsset{AssetID=b.id,ParentAssetID=b.segmentID,AssetType="Block",DisplayName="Module "+b.id,ZoneID=catalog.zoneID,TrackID=b.track,SegmentID=b.segmentID,BlockID=b.id,ChainageStart=b.start,ChainageEnd=b.end,ModelReference="EBS module 3m / raw physical model"},b.overview.bounds);
                    foreach(var p in catalog.parts)Add(new BimAsset{AssetID=b.id+"-"+p.key,ParentAssetID=b.id,AssetType="Component",DisplayName=p.name,ZoneID=catalog.zoneID,TrackID=b.track,SegmentID=b.segmentID,BlockID=b.id,ComponentType=p.type,ChainageStart=b.start,ChainageEnd=b.end,ModelReference=p.sourcePath},p.bounds);
                }
                store.Db.Execute("INSERT INTO SeedVersions VALUES(?,?)",catalog.seedVersion,DateTime.UtcNow.ToString("O"));
            });
            File.WriteAllText("Temp/BimSeedSummary.txt",$"Assets={store.Assets.Count}; components={store.Assets.Values.Count(a=>a.AssetType=="Component")}; FK violations={store.Db.Query<ForeignKeyError>("PRAGMA foreign_key_check").Count}; path={seedPath}; no legacy DB opened.");
        }
        AssetDatabase.Refresh();
    }
    public class ForeignKeyError{public string table{get;set;}}
    static Transform Find(Transform parent,string name)=>parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    static void Rect(Transform tr,Vector2 min,Vector2 max,Vector2? offsetMin=null,Vector2? offsetMax=null)
    {var r=(RectTransform)tr;r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin??Vector2.zero;r.offsetMax=offsetMax??Vector2.zero;r.localScale=Vector3.one;}
    static RectTransform UiRoot(string name,Transform parent){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
    static void Setup()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<RailwayBimCatalog>(Folder+"/Catalog.asset");if(catalog==null)throw new Exception("Missing catalog");Seed(catalog);
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);if(Object.FindFirstObjectByType<RailwayBimController>()!=null)throw new Exception("BIM already configured; refusing duplicate setup");
        var env=scene.GetRootGameObjects().Single(g=>g.name=="Zone_021_Editable");env.transform.Find("Track_Right").gameObject.SetActive(false);env.transform.Find("Track_Left").gameObject.SetActive(false);env.transform.Find("DetailedModule_Right_Z021_M001").gameObject.SetActive(false);
        var camera=Object.FindFirstObjectByType<Camera>();foreach(var old in camera.GetComponents<MonoBehaviour>().Where(m=>m is DetailOrbitZoom||m is RailwayLifecycleInspector).ToArray())Object.DestroyImmediate(old);
        var cam=camera.gameObject.AddComponent<RailwayBimCamera>();camera.stereoTargetEye=StereoTargetEyeMask.None;
        var runtime=new GameObject("RailwayLifecycleSystem");var controller=runtime.AddComponent<RailwayBimController>();controller.catalog=catalog;controller.cameraController=cam;
        var overview=new GameObject("BimSegmentOverview");controller.overviewRoot=overview.transform;
        foreach(var s in catalog.segments){var g=new GameObject(s.id);g.transform.SetParent(overview.transform,false);g.AddComponent<MeshFilter>().sharedMesh=s.overview;g.AddComponent<MeshRenderer>().sharedMaterials=s.materials;g.AddComponent<MeshCollider>().sharedMesh=s.overview;g.AddComponent<BimAssetProxy>().assetID=s.id;}
        var support=new GameObject("PreservedOriginalGirderGeometry");support.transform.SetParent(overview.transform,false);support.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/ExistingGirderSupportGeometry.asset");support.AddComponent<MeshRenderer>().sharedMaterials=catalog.blocks[0].materials;
        var outlineObject=new GameObject("SelectionOutlinePool");var outline=outlineObject.AddComponent<RailwayBimOutline>();outline.maskMaterial=new Material(Shader.Find("UTT/BIM XRay Silhouette Mask"));outline.outlineMaterial=new Material(Shader.Find("UTT/BIM XRay Outline Adapter"));AssetDatabase.CreateAsset(outline.maskMaterial,Folder+"/OutlineMask.mat");AssetDatabase.CreateAsset(outline.outlineMaterial,Folder+"/OutlineShell.mat");controller.outline=outline;
        var reference=EditorSceneManager.OpenScene("Assets/ChiTietSaBan/ChiTiet/ChiTietSaBan.unity",OpenSceneMode.Additive);var originalCanvas=reference.GetRootGameObjects().Single(g=>g.GetComponent<Canvas>()!=null);var canvasObject=Object.Instantiate(originalCanvas);canvasObject.name="RailwayLifecycleDashboard";SceneManager.MoveGameObjectToScene(canvasObject,scene);SceneManager.SetActiveScene(scene);
        foreach(var mb in canvasObject.GetComponentsInChildren<MonoBehaviour>(true).Where(m=>m!=null&&!(m is UnityEngine.EventSystems.UIBehaviour)).ToArray())Object.DestroyImmediate(mb);
        foreach(var b in canvasObject.GetComponentsInChildren<Button>(true)){b.onClick=new Button.ButtonClickedEvent();b.gameObject.SetActive(false);}
        foreach(var text in canvasObject.GetComponentsInChildren<TMP_Text>(true))text.raycastTarget=false;
        foreach(var image in canvasObject.GetComponentsInChildren<Image>(true))image.raycastTarget=false;
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.transform.localScale=Vector3.one;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=Find(canvas.transform,"VRBimAiDashboard_Contextual");Rect(root,Vector2.zero,Vector2.one);
        foreach(Transform child in root)if(child.name!="LeftNav"&&child.name!="AssetPanel"&&child.name!="LifecycleBar")child.gameObject.SetActive(false);
        var sourceButton=Find(root,"BtnNavOverview").GetComponent<Button>();var sourceInput=canvasObject.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault();if(sourceInput==null)throw new Exception("No TMP input template in reference canvas");
        var templates=UiRoot("ReusableTemplates",canvas.transform);templates.gameObject.SetActive(false);
        var item=Object.Instantiate(sourceButton,templates);item.onClick=new Button.ButtonClickedEvent();item.gameObject.name="AssetItemTemplate";
        var input=Object.Instantiate(sourceInput,templates);input.gameObject.name="InputTemplate";input.onValueChanged=new TMP_InputField.OnChangeEvent();input.onEndEdit=new TMP_InputField.SubmitEvent();input.onSubmit=new TMP_InputField.SubmitEvent();input.text="";input.contentType=TMP_InputField.ContentType.Standard;input.textComponent.raycastTarget=false;input.GetComponent<Image>().raycastTarget=true;
        var ui=runtime.AddComponent<RailwayBimDashboard>();ui.controller=controller;ui.itemTemplate=item;ui.inputTemplate=input;ui.rowTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChiTietSaBan/ChiTiet/ElementInfoRow 1.prefab").GetComponent<ElementInfoRow>();
        TMP_Text Label(string name,Transform parent,string value,int size=18)
        {var sample=sourceButton.GetComponentInChildren<TMP_Text>(true);var label=Object.Instantiate(sample,parent);label.name=name;label.gameObject.SetActive(true);label.text=value;label.fontSize=size;label.enableWordWrapping=true;label.raycastTarget=false;Rect(label.transform,Vector2.zero,Vector2.one);return label;}
        Button Button(string name,Transform parent,string text)
        {var b=Object.Instantiate(sourceButton,parent);b.name=name;b.gameObject.SetActive(true);b.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();b.GetComponent<Image>().raycastTarget=true;var label=b.GetComponentInChildren<TMP_Text>(true);label.gameObject.SetActive(true);label.text=text;label.fontSize=17;label.raycastTarget=false;Rect(label.transform,Vector2.zero,Vector2.one,new Vector2(5,2),new Vector2(-5,-2));return b;}
        var left=Find(root,"LeftNav");left.gameObject.SetActive(true);Rect(left,new Vector2(0,.08f),new Vector2(.22f,.91f),new Vector2(12,0),new Vector2(-8,0));left.GetComponent<Image>().raycastTarget=true;
        foreach(Transform child in left)child.gameObject.SetActive(false);
        ui.listTitle=Label("ListTitle",left,"SEGMENTS",18);Rect(ui.listTitle.transform,new Vector2(.03f,.91f),new Vector2(.97f,.99f));
        ui.search=Object.Instantiate(input,left);ui.search.gameObject.SetActive(true);ui.search.name="AssetSearch";Rect(ui.search.transform,new Vector2(.03f,.83f),new Vector2(.97f,.90f));ui.search.textComponent.fontSize=17;if(ui.search.placeholder is TMP_Text ph)ph.text="Tìm ID hoặc tên toàn Zone...";
        RectTransform Scroll(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var scroll=UiRoot(name,parent);Rect(scroll,min,max);var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;
            var viewport=UiRoot("Viewport",scroll);Rect(viewport,Vector2.zero,Vector2.one);viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.01f);viewport.gameObject.AddComponent<RectMask2D>();
            var content=UiRoot("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=content.offsetMax=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;layout.spacing=5;layout.padding=new RectOffset(6,6,6,6);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;sr.viewport=viewport;sr.content=content;return content;
        }
        ui.listContent=Scroll("DynamicAssetList",left,new Vector2(.02f,.02f),new Vector2(.98f,.81f));
        var asset=Find(root,"AssetPanel");asset.gameObject.SetActive(true);Rect(asset,new Vector2(.74f,.08f),new Vector2(1,.91f),new Vector2(8,0),new Vector2(-12,0));asset.GetComponent<Image>().raycastTarget=true;ui.assetPanel=asset.gameObject;foreach(Transform child in asset)child.gameObject.SetActive(false);
        ui.title=Label("AssetTitle",asset,"CHỌN TÀI SẢN",20);Rect(ui.title.transform,new Vector2(.03f,.91f),new Vector2(.97f,.99f));ui.fieldsContent=Scroll("DynamicAssetFields",asset,new Vector2(.02f,.10f),new Vector2(.98f,.89f));
        var save=Button("SaveAsset",asset,"Lưu SQLite");Rect(save.transform,new Vector2(.05f,.015f),new Vector2(.95f,.08f));ui.saveButton=save;
        ui.addInspectionButton=Button("AddInspection",asset,"Lưu kết quả kiểm tra");Rect(ui.addInspectionButton.transform,new Vector2(.05f,.015f),new Vector2(.95f,.08f));ui.addMaintenanceButton=Button("AddMaintenance",asset,"Lưu bảo trì");Rect(ui.addMaintenanceButton.transform,new Vector2(.05f,.015f),new Vector2(.95f,.08f));
        var header=UiRoot("BimHeader",root);Rect(header,new Vector2(0,.92f),Vector2.one,new Vector2(12,0),new Vector2(-12,-8));header.gameObject.AddComponent<Image>().color=new Color(.045f,.09f,.15f,.96f);ui.breadcrumb=Label("Breadcrumb",header,"Zone_021",18);Rect(ui.breadcrumb.transform,new Vector2(.01f,0),new Vector2(.7f,1));
        var commands=UiRoot("BimCommands",root);Rect(commands,new Vector2(.225f,.08f),new Vector2(.735f,.15f));var commandsLayout=commands.gameObject.AddComponent<GridLayoutGroup>();commandsLayout.cellSize=new Vector2(143,32);commandsLayout.spacing=new Vector2(5,5);commandsLayout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;commandsLayout.constraintCount=5;
        ui.detailButton=Button("InspectDetail",commands,"Xem chi tiết");ui.backButton=Button("BackLevel",commands,"Quay lại");ui.focusButton=Button("FocusAsset",commands,"Focus (F)");ui.resetButton=Button("ResetView",commands,"Tổng quan");ui.hideButton=Button("ShowHide",commands,"Ẩn / hiện");ui.measureButton=Button("Measure",commands,"Đo khoảng cách");ui.statsButton=Button("Statistics",commands,"Thống kê");ui.exportButton=Button("ExportWorkbook",commands,"Export Excel");ui.importButton=Button("PreviewWorkbook",commands,"Preview Excel");ui.applyButton=Button("ApplyWorkbook",commands,"Apply import");ui.applyButton.interactable=false;
        var life=Find(root,"LifecycleBar");life.gameObject.SetActive(true);Rect(life,new Vector2(.225f,.84f),new Vector2(.735f,.91f));var oldLayout=life.GetComponent<HorizontalLayoutGroup>();if(oldLayout!=null){oldLayout.childControlWidth=true;oldLayout.childForceExpandWidth=true;oldLayout.spacing=3;}foreach(Transform child in life)child.gameObject.SetActive(false);
        string[] tabNames={"General","Technical","Lifecycle","Inspection","Maintenance","History"};ui.tabs=tabNames.Select(name=>Button("Tab_"+name,life,name)).ToArray();foreach(var b in ui.tabs){var le=b.GetComponent<LayoutElement>()??b.gameObject.AddComponent<LayoutElement>();le.preferredWidth=130;le.preferredHeight=42;b.GetComponentInChildren<TMP_Text>().fontSize=15;}
        var footer=UiRoot("StatusFooter",root);Rect(footer,Vector2.zero,new Vector2(1,.07f));footer.gameObject.AddComponent<Image>().color=new Color(.04f,.075f,.12f,.97f);ui.status=Label("RuntimeStatus",footer,"DEMO — Chọn Segment → Xem chi tiết → Block → cấu kiện. RMB xoay, wheel zoom, Alt+click chọn lớp.",16);
        var eventSystem=Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).FirstOrDefault(e=>e.gameObject.scene==scene);if(eventSystem==null){var g=new GameObject("EventSystem");eventSystem=g.AddComponent<EventSystem>();g.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();}
        EditorSceneManager.CloseScene(reference,true);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/BimSetup.txt",$"Saved scene; segments={catalog.segments.Length}, blocks={catalog.blocks.Length}, parts={catalog.parts.Length}, logical components={catalog.parts.Length*catalog.blocks.Length}; source canvas cloned; legacy callbacks removed only in clone; old Phase1 rail/detail layers inactive; environment retained. UI and runtime controllers wired; needs Play tests.");
    }
}
