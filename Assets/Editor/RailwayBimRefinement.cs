using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class RailwayBimRefinement
{
    public const string ScenePath="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    public const string ReferencePath="Assets/ChiTietSaBan/ChiTiet/ChiTietSaBan.unity";
    const string Request="Temp/BimRefinement.request";public const string Dir="Temp/BimRefinement";
    static RailwayBimRefinement(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        var command=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Dir);
        try{if(command=="audit")Audit();else if(command=="layout")ApplyLayout();else if(command=="finish")FinishLayout();else if(command=="verify")VerifyRefinement();else if(command=="scenario")ApplyBimScenario();else if(command=="freeflight")ApplyFreeFlightStyle();else if(command=="topbar")ApplyTopBar();}
        catch(Exception e){File.WriteAllText(Dir+"/ERROR.txt",e.ToString());Debug.LogException(e);}
    }
    static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
    static Scene Main()
    {
        var scene=SceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Scene has unsaved changes: "+scene.path);
        return scene.path==ScenePath?scene:EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }
    static void Audit()
    {
        var scene=Main();var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();var canvas=ui.breadcrumb.GetComponentInParent<Canvas>();
        var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var refs=new Dictionary<Object,List<string>>();
        foreach(var owner in components)
        {
            try{var so=new SerializedObject(owner);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null){if(!refs.TryGetValue(p.objectReferenceValue,out var list))refs[p.objectReferenceValue]=list=new List<string>();list.Add(PathOf(owner.transform)+":"+owner.GetType().Name+"."+p.propertyPath);}}
            catch(Exception){}
        }
        var inventory=new List<string>{"Path\tActiveSelf\tActiveHierarchy\tComponents\tInbound serialized references\tPrefab source\tClassification"};
        foreach(var t in canvas.GetComponentsInChildren<Transform>(true))
        {
            var inbound=t.GetComponents<Component>().Where(c=>c!=null).SelectMany(c=>refs.TryGetValue(c,out var a)?a:new List<string>()).Concat(refs.TryGetValue(t.gameObject,out var b)?b:new List<string>());
            var category=t.IsChildOf(ui.itemTemplate.transform)||t.IsChildOf(ui.inputTemplate.transform)||t.name=="ReusableTemplates"?"KEEP_TEMPLATE":t==ui.saveButton.transform||t==ui.addInspectionButton.transform||t==ui.addMaintenanceButton.transform?"CONDITIONAL_RUNTIME":t.gameObject.activeSelf?"ACTIVE":"REVIEW_LEGACY_OR_CONDITIONAL";
            inventory.Add(PathOf(t)+"\t"+t.gameObject.activeSelf+"\t"+t.gameObject.activeInHierarchy+"\t"+string.Join(",",t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))+"\t"+string.Join(";",inbound)+"\t"+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)+"\t"+category);
        }
        File.WriteAllLines(Dir+"/UI_Inventory.tsv",inventory);
        var reference=EditorSceneManager.OpenScene(ReferencePath,OpenSceneMode.Additive);var lines=new List<string>{"MAIN "+scene.path,"Scene clean="+!scene.isDirty,"Main UI hierarchy inventory="+Dir+"/UI_Inventory.tsv"};
        foreach(var root in reference.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(t.name.StartsWith("Stg")||t.name=="LifecycleBar"||t.name=="AssetPanel")lines.Add("REFERENCE "+PathOf(t)+" components="+string.Join(",",t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))+" prefab="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject));
            foreach(var mb in t.GetComponents<MonoBehaviour>().Where(m=>m!=null&&m.GetType().Name.Contains("Dashboard")))lines.Add("REFERENCE CONTROLLER "+mb.GetType().Name+" "+AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(mb)));
        }
        EditorSceneManager.CloseScene(reference,true);
        lines.AddRange(new[]{"Existing controller uses explicit Select/Locate/EnterDetails/Back; focus currently manual and min 60/5/1.1m. Current hide toggles selected renderer, no isolate snapshot.","Current dashboard six top tabs; records in AssetInspections/AssetMaintenance/AssetLifecycleEvents must remain.","Current VR binds A focus/B back/right grip detail; one flat 1920x1080 board 2.1m ahead. No BIM locomotion. Source rig contains tracked pose/rays/vignette, no standalone locomotion provider.","Source SelectLifecycleStage: Stg1..6 selection, active scale 1.04, TxtLife + OnLifecycleStageSelected. Source PopulateLifecycleTab placeholders: no real six-stage SQLite persistence. Reuse button/label/sprite/layout selection technique; extend separate adapter with real stage records.","Reference highlight uses Custom/URP/XRayHighlight material replacement; BIM scene already has separate stereo-compatible outline adapter. Keep it and original source material.","Current SQLite schema version1; AssetID identity never regenerated. Add version2 stage tables and per-event StageKey; retain all old tables/data.","Excel Technical dynamic properties remain; six additional validated stage sheets will preserve AssetID+StageKey isolation.","Audit only: no scene/source/prefab was saved or deleted. Existing templates and conditional UI cannot be removed solely because inactive."});
        File.WriteAllLines(Dir+"/Audit.txt",lines);
    }
}
