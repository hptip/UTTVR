using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class ShowroomPackageMergeTools
{
    const string Dir="Temp/ShowroomMerge";
    const string ScenePath="Assets/ProjectLopHoc/Scenes/Showroom.unity";
    static ShowroomPackageMergeTools(){EditorApplication.update+=Tick;InstallPlayTests();}
    static void Tick()
    {
        const string request=Dir+"/request.txt";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string command=File.ReadAllText(request).Trim();
        if(command!="status"&&EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);Directory.CreateDirectory(Dir);
        try
        {
            if(command=="status")Status();
            else if(command=="testperformance"){SessionState.SetBool(TestKey+".Performance",true);SessionState.SetBool(TestKey+".Popup",false);SessionState.SetBool(TestKey+".XRonly",false);BeginPlayTests();}
            else if(command=="testperformanceray"){SessionState.SetBool(TestKey+".Performance",true);SessionState.SetBool(TestKey+".Popup",false);SessionState.SetBool(TestKey+".XRonly",true);BeginPlayTests();}
            else if(command=="fixpopup")FixTrainPopup();
            else if(command=="testpopup"){SessionState.SetBool(TestKey+".Popup",true);SessionState.SetBool(TestKey+".XRonly",false);BeginPlayTests();}
            else if(command=="audit")AuditCurrent();
            else if(command=="incoming")AuditIncoming();
            else if(command=="merge")MergeIncoming();
            else if(command=="test"){SessionState.SetBool(TestKey+".XRonly",false);BeginPlayTests();}
            else if(command=="testray"){SessionState.SetBool(TestKey+".XRonly",true);BeginPlayTests();}
            else if(command=="validate")ValidateMergedScene();
            else if(command=="checkdirty")CheckNeutralDirtyScene();
            else if(command=="cleanupstaging")CleanupStaging();
            else if(command=="focus")FocusMergedScene();
        }
        catch(Exception e){File.WriteAllText(Dir+"/Error_"+command+".txt",e.ToString());Debug.LogException(e);}
    }
    static void Status()
    {
        var scenes=new List<string>();for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);scenes.Add(s.path+" | loaded="+s.isLoaded+" | dirty="+s.isDirty+" | active="+(s==SceneManager.GetActiveScene()));}
        File.WriteAllLines(Dir+"/EditorStatus.txt",new[]{"Playing="+EditorApplication.isPlaying,"Compiling="+EditorApplication.isCompiling}.Concat(scenes));
    }
    static void FocusMergedScene()
    {
        var main=SceneManager.GetSceneByPath(ScenePath);if(!main.IsValid()||!main.isLoaded)throw new Exception("Showroom not loaded");
        var others=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=main).ToArray();
        if(others.Any(s=>s.isDirty))throw new Exception("Other scene has real unsaved changes; preserving it");
        foreach(var other in others)EditorSceneManager.CloseScene(other,true);
        SceneManager.SetActiveScene(main);Status();
    }
    static string Id(Object value)=>value==null?"":GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
    static string Hierarchy(Transform t)=>t.parent==null?t.name:Hierarchy(t.parent)+"/"+t.name;
    [Serializable] public class RefRow{public string property,target,assetPath;}
    [Serializable] public class ComponentRow{public string id,type,script,json;public RefRow[] references;}
    [Serializable] public class Node{public string id,path,parent,prefab,tag;public int layer;public bool active;public ComponentRow[] components;}
    [Serializable] public class Audit{public string scene;public bool dirty;public Node[] nodes;public string[] roots,dependencies;public string[] counts;public int missingScripts,missingMeshes,missingMaterials;}
    static Audit Inspect(Scene scene)
    {
        var nodes=new List<Node>();var counts=new Dictionary<string,int>();var result=new Audit{scene=scene.path,dirty=scene.isDirty};
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            result.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            var components=new List<ComponentRow>();
            foreach(var component in t.GetComponents<Component>().Where(c=>c!=null))
            {
                string name=component.GetType().FullName;counts[name]=counts.TryGetValue(name,out int n)?n+1:1;
                var refs=new List<RefRow>();var so=new SerializedObject(component);var p=so.GetIterator();
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null)refs.Add(new RefRow{property=p.propertyPath,target=Id(p.objectReferenceValue),assetPath=AssetDatabase.GetAssetPath(p.objectReferenceValue)});
                components.Add(new ComponentRow{id=Id(component),type=name,script=component is MonoBehaviour mb?AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(mb)):"",json=EditorJsonUtility.ToJson(component),references=refs.ToArray()});
                if(component is MeshFilter mf&&mf.sharedMesh==null)result.missingMeshes++;
                if(component is Renderer renderer)result.missingMaterials+=renderer.sharedMaterials.Count(m=>m==null);
            }
            nodes.Add(new Node{id=Id(t.gameObject),path=Hierarchy(t),parent=Id(t.parent),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),tag=t.tag,layer=t.gameObject.layer,active=t.gameObject.activeSelf,components=components.ToArray()});
        }
        result.nodes=nodes.ToArray();result.roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray();result.counts=counts.OrderByDescending(p=>p.Value).Select(p=>p.Key+"="+p.Value).ToArray();result.dependencies=AssetDatabase.GetDependencies(scene.path,true);return result;
    }
    static void AuditCurrent()
    {
        Status();var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try{var audit=Inspect(scene);File.WriteAllText(Dir+"/CurrentSceneAudit.json",JsonUtility.ToJson(audit,true));File.WriteAllLines(Dir+"/CurrentSceneSummary.txt",new[]{"Scene="+scene.path,"Dirty="+scene.isDirty,"Nodes="+audit.nodes.Length,"MissingScripts="+audit.missingScripts,"MissingMeshes="+audit.missingMeshes,"MissingMaterials="+audit.missingMaterials,"Roots="+string.Join(" | ",audit.roots)}.Concat(audit.counts));}
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    const string IncomingPath="Assets/ShowroomIntegration/_MergeStaging/Showroom_Incoming.unity";
    [Serializable] public class GuidRequest{public string[] guids;}
    static void AuditIncoming()
    {
        Status();var scene=EditorSceneManager.OpenScene(IncomingPath,OpenSceneMode.Additive);
        try
        {
            var audit=Inspect(scene);File.WriteAllText(Dir+"/IncomingSceneAudit.json",JsonUtility.ToJson(audit,true));
            var lines=new List<string>{"Nodes="+audit.nodes.Length,"MissingScripts="+audit.missingScripts,"MissingMeshes="+audit.missingMeshes,"MissingMaterials="+audit.missingMaterials};
            foreach(var root in scene.GetRootGameObjects())
            {
                var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(root.transform.position,Vector3.zero);if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);}
                lines.Add(root.name+" | position="+root.transform.position.ToString("F3")+" | boundsCenter="+bounds.center.ToString("F2")+" | boundsSize="+bounds.size.ToString("F2")+" | prefab="+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));
            }
            lines.AddRange(audit.counts);File.WriteAllLines(Dir+"/IncomingSceneSummary.txt",lines);
            var request=JsonUtility.FromJson<GuidRequest>(File.ReadAllText(Dir+"/ReferencedGuids.json"));
            File.WriteAllLines(Dir+"/GuidResolution.tsv",new[]{"GUID\tPath"}.Concat(request.guids.Select(g=>g+"\t"+AssetDatabase.GUIDToAssetPath(g))));
            var methods=typeof(GameObjectUtility).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Where(m=>m.Name.Contains("Duplicate"));File.WriteAllLines(Dir+"/DuplicateApi.txt",methods.Select(m=>m.ToString()));
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
    }
}
