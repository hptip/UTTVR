using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SQLite;

[InitializeOnLoad]
public static class RailwayBimAudit
{
    static RailwayBimAudit(){EditorApplication.update+=Tick;}
    static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
    static void Tick()
    {
        const string request="Temp/RailwayBimAudit.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(request);
        try{Run();}catch(Exception e){File.WriteAllText("Temp/RailwayBimAudit_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
    static void Run()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        var setup=EditorSceneManager.GetSceneManagerSetup();var lines=new List<string>{"AUDIT: clean Editor scenes, Edit Mode; no source scene saved."};
        foreach(var path in new[]{"Assets/ChiTietSaBan/ChiTiet/ChiTietSaBan.unity","Assets/ChiTietSaBan/ChiTiet/SaBan.unity"})
        {
            var s=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var nodes=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();lines.Add("SCENE "+path+" nodes="+nodes.Length);
            foreach(var t in nodes)
            {
                var cs=t.GetComponents<Component>();if(cs.Any(c=>c is Canvas||c is Button||c is TMP_InputField||c is ScrollRect||c is MonoBehaviour&&!(c is TMP_Text)))
                    lines.Add(PathOf(t)+" | "+string.Join(",",cs.Where(c=>c!=null).Select(c=>c.GetType().Name)));
                var button=t.GetComponent<Button>();if(button!=null)for(int i=0;i<button.onClick.GetPersistentEventCount();i++)lines.Add("CALL "+t.name+" -> "+button.onClick.GetPersistentTarget(i)+"."+button.onClick.GetPersistentMethodName(i));
            }
        }
        foreach(var p in new[]{"Assets/ChiTietSaBan/ChiTiet/MB-EBS-DRW-003_00EBSLR49E1-MS140SklTrack-3DView_Renamed.fbx","Assets/ChiTietSaBan/ChiTiet/DuongRay.prefab"})
        {
            var g=AssetDatabase.LoadAssetAtPath<GameObject>(p);lines.Add("MODEL "+p+" nodes="+g.GetComponentsInChildren<Transform>(true).Length+" filters="+g.GetComponentsInChildren<MeshFilter>(true).Length);
            File.WriteAllLines("Temp/BimModel_"+Path.GetFileNameWithoutExtension(p)+".tsv",g.GetComponentsInChildren<MeshFilter>(true).Select(m=>PathOf(m.transform)+"\t"+m.sharedMesh.name+"\t"+m.sharedMesh.vertexCount+"\t"+m.sharedMesh.bounds.size+"\t"+m.transform.localPosition+"\t"+m.transform.localEulerAngles+"\t"+m.transform.lossyScale));
        }
        string dbPath=Path.Combine(Application.persistentDataPath,"RailwayModels.db");lines.Add("Existing database="+dbPath+" exists="+File.Exists(dbPath));
        if(File.Exists(dbPath))using(var db=new SQLiteConnection(dbPath,SQLiteOpenFlags.ReadOnly))
        {foreach(var row in db.Query<SqlRow>("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL"))lines.Add(row.sql);lines.Add("RailwayElements rows="+db.ExecuteScalar<int>("SELECT count(*) FROM RailwayElements"));}
        EditorSceneManager.RestoreSceneManagerSetup(setup);File.WriteAllLines("Temp/RailwayBimAudit.txt",lines);
        File.WriteAllText("Temp/RailwayBimAudit_DONE.txt",DateTime.Now.ToString("O"));
    }
    public class SqlRow{public string sql{get;set;}}
}
