using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinement
{
    static void FinishLayout()
    {
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.path!=ScenePath)throw new Exception("Wrong scene");if(current.isDirty)EditorSceneManager.SaveScene(current);
        var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();
        // Recenter is already available through X hide/show. Remove the desktop-only no-op button.
        if(ui.recenterUiButton!=null){File.AppendAllText(Dir+"/Cleanup.tsv",PathOf(ui.recenterUiButton.transform)+"\tNew desktop-only no-op removed; X hide/show is the explicit VR recenter mechanism\tOwned control created during this refinement; no source asset deleted\n");Object.DestroyImmediate(ui.recenterUiButton.gameObject);ui.recenterUiButton=null;}
        if(ui.lifecycleNavigation.name!="LifecycleSixStages_SourceMatched")
        {
            var reference=EditorSceneManager.OpenScene(ReferencePath,OpenSceneMode.Additive);var originals=reference.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Button>(true)).ToArray();
            for(int i=0;i<6;i++){var original=originals.Single(b=>b.name=="Stg"+(i+1));var replacement=Copy(original,ui.lifecycleNavigation,"Stg"+(i+1),RailwayBimStages.All[i].name);Object.DestroyImmediate(ui.stageButtons[i].gameObject);ui.stageButtons[i]=replacement;}
            EditorSceneManager.CloseScene(reference,true);ui.lifecycleNavigation.name="LifecycleSixStages_SourceMatched";
        }
        if(ui.historyNextButton==null)
        {
            var row=Container("HistoryPaging",ui.assetBody.transform);Rect(row,new Vector2(.04f,.018f),new Vector2(.96f,.095f));var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=layout.childForceExpandHeight=true;
            ui.historyPreviousButton=Copy(ui.itemTemplate,row,"HistoryNewer","Nhật ký mới hơn");ui.historyNextButton=Copy(ui.itemTemplate,row,"HistoryOlder","Nhật ký cũ hơn");
        }
        Label(ui.collapseNavButton,"Thu gọn",18);Label(ui.collapseAssetButton,"Thu gọn",18);ui.lifecycleNavigation.GetComponent<GridLayoutGroup>().childAlignment=TextAnchor.MiddleCenter;
        EditorSceneManager.MarkSceneDirty(current);EditorSceneManager.SaveScene(current);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);File.WriteAllText(Dir+"/FinishedLayout.txt","Saved incremental history paging controls and sidebar typography; six-stage source layout retained.");
    }
    static void VerifyRefinement()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new Exception("Wrong scene");if(scene.isDirty)EditorSceneManager.SaveScene(scene);scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();var c=ui.controller;var vr=c.GetComponent<RailwayBimVrPresentation>();
        if(ui.tabs.Length!=3||ui.stageButtons.Length!=6||ui.focusButton!=null||ui.measureButton!=null||ui.statsButton!=null)throw new Exception("Wrong final UI controls");
        if(ui.leftBody==null||ui.assetBody==null||ui.historyNextButton==null||ui.historyPreviousButton==null||vr.localRigPrefab==null)throw new Exception("Missing UI/VR references");
        int nodes=0,renderers=0;
        foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true))
        {nodes++;if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)>0)throw new Exception("Missing script: "+PathOf(tr));foreach(var r in tr.GetComponents<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy)){renderers++;if(r.GetComponent<MeshFilter>()?.sharedMesh==null||r.sharedMaterials.Any(m=>m==null))throw new Exception("Missing active mesh/material: "+r.name);}}
        if(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length!=1||Object.FindFirstObjectByType<XRUIInputModule>()==null)throw new Exception("Wrong event system/module");
        foreach(var name in new[]{"UTT/BIM XRay Silhouette Mask","UTT/BIM XRay Outline Adapter"})if(ShaderUtil.ShaderHasError(Shader.Find(name)))throw new Exception("Outline shader compile error");
        var dependencies=AssetDatabase.GetDependencies(ScenePath,true);if(dependencies.Any(p=>p.StartsWith("Assets/")&&!File.Exists(p)))throw new Exception("Missing dependency");
        using(var db=new SQLite.SQLiteConnection("UserData/QuanLyVongDoiTaiSan.db",SQLite.SQLiteOpenFlags.ReadOnly))
        {
            int stages=db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages");if(stages!=33434*6)throw new Exception("Wrong stage profile count");if(db.Query<RailwayBimBuild.ForeignKeyError>("PRAGMA foreign_key_check").Count!=0)throw new Exception("Foreign key errors");
            File.WriteAllText(Dir+"/SavedSceneVerification.txt","PASS | Saved/reopened scene clean="+!scene.isDirty+"; nodes="+nodes+"; active mesh renderers="+renderers+"; dependencies="+dependencies.Length+".\nPASS | Three main tabs, six stages, history paging and all required templates/references present; removed buttons absent.\nPASS | No missing scripts, active meshes/materials or source asset dependencies. One EventSystem with XRUIInputModule.\nPASS | Both stereo outline shaders have no compile errors.\nPASS | SQLite assets="+db.ExecuteScalar<int>("SELECT count(*) FROM Assets")+"; stage profiles="+stages+"; stage field rows="+db.ExecuteScalar<int>("SELECT count(*) FROM AssetStageFields")+"; FK check clean.\nEditor only; physical headset and Android build not verified.");
        }
    }
}
