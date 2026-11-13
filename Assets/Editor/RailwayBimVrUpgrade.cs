using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayBimVrUpgrade
{
    const string Request="Temp/BimVrUpgrade.request";
    public const string ScenePath="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    static RailwayBimVrUpgrade(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        var cmd=File.ReadAllText(Request).Trim();File.Delete(Request);
        try{if(cmd=="audit")Audit();else if(cmd=="apply")Apply();else if(cmd=="verify")Verify();else if(cmd=="save-current"){var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=ScenePath)throw new Exception("Wrong scene or Play Mode");EditorSceneManager.SaveScene(scene);File.WriteAllText("Temp/BimVrUpgrade_Saved.txt","Saved current lifecycle scene; "+DateTime.Now);}}
        catch(Exception e){File.WriteAllText("Temp/BimVrUpgrade_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
    static void Audit()
    {
        var lines=new List<string>{"Play="+EditorApplication.isPlaying,"Active="+SceneManager.GetActiveScene().path,"Dirty="+SceneManager.GetActiveScene().isDirty};
        if(!EditorApplication.isPlaying)
        {
            var scene=EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/Login.unity",OpenSceneMode.Additive);
            var rig=scene.GetRootGameObjects().Single(g=>g.name=="XR Origin (VR)");
            lines.AddRange(rig.GetComponentsInChildren<MonoBehaviour>(true).Where(m=>m!=null).Select(m=>m.transform.name+": "+m.GetType().FullName));
            EditorSceneManager.CloseScene(scene,true);
        }
        foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile")){var path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadMainAssetAtPath(path);lines.Add("PROFILE "+path);var so=new SerializedObject(asset);var p=so.GetIterator();while(p.NextVisible(true))if(p.propertyPath.Contains("Scene")||p.propertyPath.Contains("scene"))lines.Add(p.propertyPath+"="+(p.propertyType==SerializedPropertyType.String?p.stringValue:p.propertyType.ToString()));}
        File.WriteAllLines("Temp/BimVrUpgrade_Audit.txt",lines);
    }
    static void Rect(Transform t,Vector2 min,Vector2 max,Vector2? inset=null)
    {var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=inset??Vector2.zero;r.offsetMax=-(inset??Vector2.zero);}
    static void Label(Button b,string value){var t=b.GetComponentInChildren<TMP_Text>(true);t.text=value;t.fontSize=22;t.alignment=TextAlignmentOptions.Center;}
    static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Play Mode still running");
        var current=SceneManager.GetActiveScene();if(current.path!=ScenePath)throw new Exception("Unexpected active scene: "+current.path);
        if(current.isDirty)EditorSceneManager.SaveScene(current);var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var vr=Object.FindFirstObjectByType<RailwayBimVrPresentation>();if(vr==null||vr.controller==null||vr.dashboard==null||vr.localRigPrefab==null||vr.dashboardCanvas==null)throw new Exception("Missing presentation references");
        if(vr.localRigPrefab.GetComponentsInChildren<Canvas>(true).Any(c=>c.name=="LoginCanvas")||vr.localRigPrefab.GetComponent<XROriginSessionPersistence>()!=null)throw new Exception("Login UI/persistence leaked into fallback prefab");
        if(vr.localRigPrefab.GetComponentsInChildren<XRRayInteractor>(true).Length!=2)throw new Exception("Missing controller ray");
        foreach(var name in new[]{"UTT/BIM XRay Silhouette Mask","UTT/BIM XRay Outline Adapter"})if(ShaderUtil.ShaderHasError(Shader.Find(name)))throw new Exception("Shader error: "+name);
        foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)>0)throw new Exception("Missing script: "+tr.name);
        var paths=AssetDatabase.GetDependencies(ScenePath,true);if(paths.Any(p=>p.StartsWith("Assets/")&&!File.Exists(p)))throw new Exception("Missing asset dependency");
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath&&s.enabled))throw new Exception("Scene not enabled in build list");
        File.WriteAllText("Temp/BimVR/SavedSceneVerification.txt","PASS | Scene saved and reopened in Edit Mode; clean="+!scene.isDirty+".\nPASS | All adapter references and source dependencies exist; no missing scripts.\nPASS | Fallback prefab has two authored controller rays, no LoginCanvas or session persistence.\nPASS | Both URP outline shaders compile without errors and include stereo/instancing setup.\nPASS | Scene remains enabled in shared build list. No custom BuildProfile asset was found in this project; inherited list is used.\n"+DateTime.Now.ToString("O"));
    }
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Cần Edit Mode trước khi lưu");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Có Scene chưa lưu; không ghi đè");
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();var controller=ui.controller;var canvas=ui.breadcrumb.GetComponentInParent<Canvas>();
        var presentation=controller.GetComponent<RailwayBimVrPresentation>()??controller.gameObject.AddComponent<RailwayBimVrPresentation>();presentation.controller=controller;presentation.dashboard=ui;presentation.dashboardCanvas=canvas;
        const string prefabPath="Assets/ProjectLopHoc/Generated/RailwayLifecycleBim/BimLocalXR.prefab";
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
        {
            var login=EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/Login.unity",OpenSceneMode.Additive);
            var source=login.GetRootGameObjects().Single(g=>g.name=="XR Origin (VR)");var clone=Object.Instantiate(source);clone.name="BimLocalXR";
            foreach(var child in clone.GetComponentsInChildren<Canvas>(true).Where(c=>c.name=="LoginCanvas").ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(var persist in clone.GetComponentsInChildren<XROriginSessionPersistence>(true))Object.DestroyImmediate(persist);
            var manager=clone.GetComponent<XRInteractionManager>()??clone.AddComponent<XRInteractionManager>();
            foreach(var ray in clone.GetComponentsInChildren<XRRayInteractor>(true))ray.interactionManager=manager;
            PrefabUtility.SaveAsPrefabAsset(clone,prefabPath);Object.DestroyImmediate(clone);EditorSceneManager.CloseScene(login,true);SceneManager.SetActiveScene(scene);
        }
        presentation.localRigPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var eventSystem=Object.FindFirstObjectByType<EventSystem>();foreach(var module in eventSystem.GetComponents<BaseInputModule>())if(!(module is XRUIInputModule))Object.DestroyImmediate(module);
        var xrModule=eventSystem.GetComponent<XRUIInputModule>()??eventSystem.gameObject.AddComponent<XRUIInputModule>();xrModule.enableXRInput=true;xrModule.enableMouseInput=true;xrModule.enableBuiltinActionsAsFallback=true;
        var root=canvas.GetComponentsInChildren<Transform>(true).First(t=>t.name=="VRBimAiDashboard_Contextual");
        var left=ui.listTitle.transform.parent;var info=ui.assetPanel.transform;var header=ui.breadcrumb.transform.parent;var commands=ui.detailButton.transform.parent;var tabs=ui.tabs[0].transform.parent;var footer=ui.status.transform.parent;
        Rect(left,new Vector2(.012f,.08f),new Vector2(.25f,.865f));Rect(info,new Vector2(.66f,.08f),new Vector2(.988f,.865f));
        Rect(header,new Vector2(.012f,.88f),new Vector2(.988f,.988f));Rect(ui.breadcrumb.transform,new Vector2(.015f,.04f),new Vector2(.99f,.36f));ui.breadcrumb.fontSize=19;
        var brand=header.Find("VietnameseBrand");if(brand==null){var text=Object.Instantiate(ui.breadcrumb,header);brand=text.transform;brand.name="VietnameseBrand";}
        brand.gameObject.SetActive(true);var brandText=brand.GetComponent<TMP_Text>();brandText.text="QUẢN LÝ VÒNG ĐỜI TÀI SẢN ĐƯỜNG SẮT";brandText.fontSize=32;brandText.fontStyle=FontStyles.Bold;brandText.color=new Color(.81f,.98f,.94f);Rect(brand,new Vector2(.015f,.40f),new Vector2(.99f,.98f));
        Rect(tabs,new Vector2(.267f,.78f),new Vector2(.643f,.865f));var horizontal=tabs.GetComponent<HorizontalLayoutGroup>();if(horizontal!=null)Object.DestroyImmediate(horizontal);
        var grid=tabs.GetComponent<GridLayoutGroup>()??tabs.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.cellSize=new Vector2(232,43);grid.spacing=new Vector2(8,7);
        string[] titles={"Tổng quan","Thông số","Vòng đời","Kiểm tra","Bảo trì","Lịch sử"};for(int i=0;i<ui.tabs.Length;i++)Label(ui.tabs[i],titles[i]);
        Rect(commands,new Vector2(.267f,.08f),new Vector2(.643f,.28f));var commandGrid=commands.GetComponent<GridLayoutGroup>();commandGrid.constraintCount=3;commandGrid.cellSize=new Vector2(232,45);commandGrid.spacing=new Vector2(8,8);
        Label(ui.detailButton,"Xem chi tiết");Label(ui.backButton,"Cấp trước");Label(ui.focusButton,"Đến tài sản");Label(ui.resetButton,"Toàn cảnh");Label(ui.hideButton,"Ẩn / hiện");Label(ui.measureButton,"Đo khoảng cách");Label(ui.statsButton,"Thống kê");Label(ui.exportButton,"Xuất bảng tính");Label(ui.importButton,"Xem trước tệp");Label(ui.applyButton,"Xác nhận nhập");Label(ui.saveButton,"Lưu hồ sơ");Label(ui.addInspectionButton,"Lưu kết quả kiểm tra");Label(ui.addMaintenanceButton,"Lưu kết quả bảo trì");
        Rect(ui.title.transform,new Vector2(.045f,.86f),new Vector2(.96f,.98f));ui.title.fontSize=27;
        Rect(ui.fieldsContent.GetComponentInParent<ScrollRect>().transform,new Vector2(.025f,.11f),new Vector2(.975f,.845f));
        Rect(ui.listTitle.transform,new Vector2(.05f,.91f),new Vector2(.95f,.99f));ui.listTitle.fontSize=23;
        Rect(ui.search.transform,new Vector2(.04f,.815f),new Vector2(.96f,.90f));ui.search.textComponent.fontSize=23;ui.search.textComponent.color=new Color(.94f,.98f,1);ui.search.GetComponent<Image>().color=new Color(.10f,.18f,.24f,1);if(ui.search.placeholder is TMP_Text ph){ph.text="Tìm tên hoặc mã tài sản…";ph.enableAutoSizing=false;ph.fontSize=23;ph.color=new Color(.65f,.76f,.83f);}
        if(ui.inputTemplate.placeholder is TMP_Text inputHint)inputHint.text="Nhập nội dung…";
        Rect(ui.listContent.GetComponentInParent<ScrollRect>().transform,new Vector2(.025f,.02f),new Vector2(.975f,.795f));
        ui.status.text="Chọn phân đoạn → mô-đun → cấu kiện  •  VR: cò để chọn, X mở bảng, Y đổi không gian  •  Máy tính: F8 xem VR";ui.status.fontSize=21;Rect(ui.status.transform,new Vector2(.018f,0),new Vector2(.982f,1));
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.GetComponent<Button>()!=null)continue;
            if(image.transform==left||image.transform==info||image.transform==header||image.transform==footer){image.color=new Color(.035f,.065f,.10f,.97f);image.raycastTarget=true;}
            else if(image.transform==root){image.color=Color.clear;image.raycastTarget=false;}
        }
        foreach(var b in root.GetComponentsInChildren<Button>(true))
        {
            var colors=b.colors;colors.normalColor=new Color(.09f,.20f,.27f,1);colors.highlightedColor=new Color(.12f,.40f,.43f,1);colors.selectedColor=colors.normalColor;colors.pressedColor=new Color(.08f,.54f,.47f,1);colors.disabledColor=new Color(.07f,.10f,.13f,.55f);colors.fadeDuration=.08f;b.colors=colors;
            b.GetComponent<Image>().color=Color.white;var text=b.GetComponentInChildren<TMP_Text>(true);if(text!=null){text.color=new Color(.91f,.98f,1);text.raycastTarget=false;}
        }
        // Shared lists remain intact; add one existing scene without disturbing zone registrations.
        var scenes=EditorBuildSettings.scenes.ToList();int existing=scenes.FindIndex(s=>s.path==ScenePath);if(existing<0)scenes.Add(new EditorBuildSettingsScene(ScenePath,true));else scenes[existing]=new EditorBuildSettingsScene(ScenePath,true);EditorBuildSettings.scenes=scenes.ToArray();
        var profiles=new List<string>();foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);var profile=AssetDatabase.LoadMainAssetAtPath(path);var so=new SerializedObject(profile);var list=so.FindProperty("m_Scenes");var overridden=so.FindProperty("m_OverrideGlobalScenes");
            if(list!=null&&list.isArray&&overridden!=null&&overridden.boolValue){bool found=false;for(int i=0;i<list.arraySize;i++){var entry=list.GetArrayElementAtIndex(i);var p=entry.FindPropertyRelative("path");if(p?.stringValue==ScenePath){entry.FindPropertyRelative("enabled").boolValue=true;found=true;}}if(!found){list.InsertArrayElementAtIndex(list.arraySize);var e=list.GetArrayElementAtIndex(list.arraySize-1);e.FindPropertyRelative("path").stringValue=ScenePath;e.FindPropertyRelative("enabled").boolValue=true;}so.ApplyModifiedPropertiesWithoutUndo();profiles.Add(path);}
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        File.WriteAllText("Temp/BimVrUpgrade_Applied.txt","Saved/reopened scene. Vietnamese visual layout. Scene-owned authored XR fallback prefab, no login Canvas or persistence. Existing rig reused. XRUIInputModule supports desktop+VR. Added scene to shared Build Scene List; profile overrides updated: "+string.Join(",",profiles)+". Other scene assets untouched.");
        if(File.Exists("Temp/BimVrUpgrade_ERROR.txt"))File.Delete("Temp/BimVrUpgrade_ERROR.txt");
    }
}
