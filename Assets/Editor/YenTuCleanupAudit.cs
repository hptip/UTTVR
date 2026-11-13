using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

// Explicit request only. Audit never saves a scene or deletes objects/assets.
[InitializeOnLoad]
public static class YenTuCleanupAudit
{
    const string Main="Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Request="Temp/YenTuCleanup.request";
    const string Out="Temp/YenTuCleanup";
    static bool busy;
    static double nextRefreshCheck;
    static DateTime sourceStamp;
    const string TestKey="UTT.Cleanup.StreamingTest";
    static TrainSplineMover testTrain;
    static RailwayZoneStreamingManager testManager;
    static RailwayMicroChunkStreamer testMicro;
    static bool seated,started,ending;
    static float firstDistance,lastDistance,goal;
    static double testStart,lastProgress;
    static HashSet<string> loaded=new HashSet<string>(),unloaded=new HashSet<string>(),seen=new HashSet<string>();
    static List<string> testErrors=new List<string>();
    [Serializable] public class Node
    {
        public string path,category,reason,id;
        public bool active;
        public int objects,renderers;
        public string[] components, blockers;
    }
    [Serializable] public class Zone
    {
        public string path;
        public bool registered;
        public int objects,renderers,cameras,listeners,missingScripts,visibleMissingMeshes,managers;
    }
    [Serializable] public class State
    {
        public string status,scene;
        public bool dirty;
        public int objects,renderers,activeRenderers,missingScripts,visibleMissingMeshes;
        public float ahead,behind;
        public int poolCapacity;
        public Node[] roots,candidates;
        public Zone[] zones;
        public string[] registeredZones,protectedScenes;
    }
    static YenTuCleanupAudit()
    {
        sourceStamp=File.GetLastWriteTimeUtc("Assets/Editor/YenTuCleanupAudit.cs");
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;
        Application.logMessageReceived+=(message,stack,type)=>{if(EditorApplication.isPlaying&&SessionState.GetBool(TestKey,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))testErrors.Add(type+": "+message);};
        InputSystem.onAfterUpdate+=DriveInput;
    }
    static void Tick()
    {
        if(SessionState.GetBool(TestKey,false)&&EditorApplication.isPlaying){TestTick();return;}
        if(EditorApplication.timeSinceStartup>nextRefreshCheck&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
        {nextRefreshCheck=EditorApplication.timeSinceStartup+2;var stamp=File.GetLastWriteTimeUtc("Assets/Editor/YenTuCleanupAudit.cs");if(stamp!=sourceStamp){sourceStamp=stamp;AssetDatabase.Refresh();return;}}
        if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        Directory.CreateDirectory(Out);
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {File.WriteAllText(Out+"/Status.txt","BLOCKED: Editor is in Play Mode; no scene changes performed.");return;}
        string command=File.ReadAllText(Request).Trim();if(command!="audit"&&command!="plan"&&command!="applytest"&&command!="test"&&command!="post")return;
        File.Delete(Request);busy=true;
        try{if(command=="audit")Audit();else if(command=="plan")Plan(false);else if(command=="post")Post();else{if(command=="applytest")Plan(true);else SessionState.SetInt("UTT.Cleanup.UndoGroup",-1);BeginTest();}}catch(Exception e){File.WriteAllText(Out+"/ERROR.txt",e.ToString());Debug.LogException(e);}finally{busy=false;}
    }
    static void Post()
    {
        var scene=SceneManager.GetSceneByPath(Main);if(!scene.isLoaded)throw new InvalidOperationException("Expected reopened main scene");
        var gos=Objects(scene).ToArray();int scripts=gos.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
        int meshes=gos.Sum(g=>g.GetComponents<MeshFilter>().Count(m=>m.sharedMesh==null&&m.GetComponent<MeshRenderer>()!=null&&m.GetComponent<MeshRenderer>().enabled&&g.activeInHierarchy));
        int missingPrefab=gos.Count(g=>PrefabUtility.GetPrefabAssetType(g)==PrefabAssetType.MissingAsset);
        File.WriteAllLines(Out+"/PostCheck.txt",new[]{"Scene="+scene.path,"GameObjects="+gos.Length,"Renderers="+gos.Sum(g=>g.GetComponents<Renderer>().Length),"Active renderers="+gos.Sum(g=>g.GetComponents<Renderer>().Count(r=>r.enabled&&g.activeInHierarchy)),"Missing scripts="+scripts,"Active renderer missing mesh="+meshes,"Missing prefab objects="+missingPrefab,"Functional EventSystem="+gos.Count(g=>g.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null),"Empty EventSystem roots="+scene.GetRootGameObjects().Count(g=>g.name=="EventSystem"&&g.GetComponents<Component>().Length==1&&g.transform.childCount==0),"Scene dirty after reload="+scene.isDirty});
    }
    static void Plan(bool apply)
    {
        var scene=SceneManager.GetSceneByPath(Main);if(!scene.isLoaded||scene.isDirty)throw new InvalidOperationException("Main scene must be loaded and clean before cleanup.");
        var real=Objects(scene).Count(g=>g.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null);
        var empty=scene.GetRootGameObjects().Where(g=>g.name=="EventSystem"&&g.transform.childCount==0&&g.GetComponents<Component>().Length==1&&!PrefabUtility.IsPartOfPrefabInstance(g)).ToArray();
        var lines=new List<string>{"Proposed removal: transform-only duplicate EventSystem roots. Functional XR EventSystem retained="+real};
        var safe=new List<GameObject>();
        foreach(var go in empty){var refs=Incoming(go.transform,scene);lines.Add(GlobalObjectId.GetGlobalObjectIdSlow(go)+" | "+PathOf(go.transform)+" | external refs="+refs.Length);lines.AddRange(refs);if(real==1&&refs.Length==0)safe.Add(go);}
        // Runtime find-by-name calls would require manual review even for an empty node.
        foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.EndsWith(".cs")&&!p.Contains("/Editor/")&&File.Exists(p)))
        {string text=File.ReadAllText(path);if(text.Contains("Find(\"EventSystem\")")){lines.Add("BLOCK runtime name lookup: "+path);safe.Clear();}}
        lines.Add("Approved by safety predicates="+safe.Count+"; no renderer/script/asset removal.");
        File.WriteAllLines(Out+"/ProposedDeletionPlan.txt",lines);
        if(!apply)return;
        if(safe.Count==0){File.WriteAllText(Out+"/Changes.txt","No objects removed: no candidate passed all safety predicates.");return;}
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("YenTu safe cleanup - empty duplicate EventSystem");
        SessionState.SetInt("UTT.Cleanup.UndoGroup",group);
        foreach(var go in safe)Undo.DestroyObjectImmediate(go);
        Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Out+"/Changes.txt","Removed "+safe.Count+" empty duplicate EventSystem root(s) with Undo. Removed components: Transform only. Renderer/script/asset files removed=0.");
    }
    static void BeginTest()
    {
        var scene=SceneManager.GetSceneByPath(Main);if(!scene.isLoaded||scene.isDirty)throw new InvalidOperationException("Refusing Play Mode with unsaved/missing main scene.");
        SessionState.SetBool(TestKey,true);SessionState.SetString("UTT.Cleanup.Start",DateTime.UtcNow.ToString("O"));
        EditorApplication.isPlaying=true;
    }
    static void PlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Cleanup.Finalize",false))
        {
            SessionState.SetBool("UTT.Cleanup.Finalize",false);bool pass=SessionState.GetBool("UTT.Cleanup.Pass",false);
            var scene=SceneManager.GetSceneByPath(Main);
            if(!pass&&SessionState.GetInt("UTT.Cleanup.UndoGroup",-1)>=0)
            {Undo.RevertAllDownToGroup(SessionState.GetInt("UTT.Cleanup.UndoGroup",-1));EditorSceneManager.SaveScene(scene);File.AppendAllText(Out+"/Changes.txt","\nRuntime verification failed; cleanup Undo group reverted and main scene saved.");}
            if(pass){EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);File.AppendAllText(Out+"/RuntimeTest.txt","\nMain scene closed/reopened after successful test. No scene changes saved during Play Mode.");}
            File.WriteAllText(Out+"/Status.txt",pass?"CLEANUP_TEST_PASS":"TEST_FAILED_CLEANUP_REVERTED_OR_NO_CHANGES");
        }
    }
    static void DriveInput()
    {
        if(!SessionState.GetBool(TestKey,false)||!EditorApplication.isPlaying||testTrain==null||!seated)return;
        foreach(var d in InputSystem.devices.Where(d=>d.layout.Contains("Simulated")&&d.usages.Any(u=>u.ToString()=="LeftHand")))
        {var axis=d.TryGetChildControl<Vector2Control>("primary2DAxis");if(axis!=null)InputState.Change(axis,Vector2.up);}
        testTrain.SetVRControlInput(Vector2.up);
    }
    static void TestTick()
    {
        if(ending)return;
        if(testStart==0)testStart=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-testStart>720){FinishTest(false,"Timeout waiting for genuine train traversal");return;}
        if(testTrain==null)testTrain=Object.FindFirstObjectByType<TrainSplineMover>();
        if(testManager==null)testManager=Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        if(testMicro==null)testMicro=Object.FindFirstObjectByType<RailwayMicroChunkStreamer>();
        if(testTrain==null||testManager==null||testMicro==null||testTrain.totalRouteLength<=0)return;
        if(!started){started=true;firstDistance=lastDistance=testTrain.currentDistanceMeters;goal=Mathf.Min(testTrain.totalRouteLength-1,1600);foreach(var f in testManager.fixedInfrastructure)if(f.kind=="Bridge")goal=Mathf.Min(testTrain.totalRouteLength-1,Mathf.Max(goal,f.endChainage+testManager.keepBehindMeters+100));}
        foreach(var zone in testManager.zones)
        {bool isLoaded=SceneManager.GetSceneByPath(zone.scenePath).isLoaded;if(isLoaded){if(!seen.Contains(zone.zoneId)){loaded.Add(zone.zoneId);seen.Add(zone.zoneId);}}else if(seen.Contains(zone.zoneId))unloaded.Add(zone.zoneId);}
        if(!seated)
        {
            var player=Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).FirstOrDefault(p=>p.Object!=null&&p.Object.IsValid&&p.HasStateAuthority);
            var seat=Object.FindFirstObjectByType<TrainSeatInteractable>();
            if(player!=null&&seat!=null){seat.RequestOccupy(player);seated=player.isDriving;if(seated)testTrain.AdjustSpeedLevel(4);}
        }
        DriveInput();float distance=testTrain.currentDistanceMeters;
        if(distance+0.1f<lastDistance)testErrors.Add("Unexpected backward chainage");lastDistance=distance;
        if(EditorApplication.timeSinceStartup-lastProgress>1)
        {
            lastProgress=EditorApplication.timeSinceStartup;
            File.WriteAllText(Out+"/RuntimeProgress.txt",$"distance={distance:F1}, goal={goal:F1}, speed={testTrain.currentSpeedKmh:F1}, seated={seated}, loaded={testManager.LoadedZoneCount}, visited={seen.Count}, unloaded={unloaded.Count}, railPairs={testMicro.ActiveChunks}, essential={testMicro.EssentialReady}, errors={testErrors.Count}");
        }
        if(testErrors.Count>0){FinishTest(false,"Runtime console or traversal error");return;}
        if(distance>=goal&&seen.Count>=3&&unloaded.Count>0)FinishTest(true,"Genuine TrainSplineMover.Update movement using seated throttle; no manual chainage/currentSpeed/targetSpeed assignment");
    }
    static void FinishTest(bool pass,string reason)
    {
        ending=true;
        var lines=new List<string>{"PASS="+pass,reason,"Start="+firstDistance+", reached="+(testTrain!=null?testTrain.currentDistanceMeters:0),"Loaded/visited="+string.Join(",",loaded),"Unloaded="+string.Join(",",unloaded),"Pool configured="+(testMicro!=null?testMicro.poolCapacity:0)+", peak active pairs="+(testMicro!=null?testMicro.PeakActiveChunks:0),"Console errors="+testErrors.Count,"XR/URP/train/spline files unchanged; runtime throttle only. Visual continuity/FPS/headset not confirmed by this telemetry."};lines.AddRange(testErrors.Take(15));
        File.WriteAllLines(Out+"/RuntimeTest.txt",lines);SessionState.SetBool(TestKey,false);SessionState.SetBool("UTT.Cleanup.Pass",pass);SessionState.SetBool("UTT.Cleanup.Finalize",true);EditorApplication.isPlaying=false;
    }
    static IEnumerable<GameObject> Objects(Scene scene)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject);
    static string PathOf(Transform t){string p=t.name;while(t.parent!=null){t=t.parent;p=t.name+"/"+p;}return p;}
    static bool Under(Object o,Transform root)
    {Transform t=(o as GameObject)?.transform??(o as Component)?.transform;return t!=null&&(t==root||t.IsChildOf(root));}
    static string[] Incoming(Transform target,Scene scene)
    {
        var hits=new List<string>();
        foreach(var go in Objects(scene))
        {
            if(Under(go,target))continue;
            foreach(var c in go.GetComponents<Component>())
            {
                if(c==null||c is Transform)continue;
                try{var it=new SerializedObject(c).GetIterator();while(it.Next(true))
                    if(it.propertyType==SerializedPropertyType.ObjectReference&&Under(it.objectReferenceValue,target))hits.Add(PathOf(go.transform)+"/"+c.GetType().Name+"."+it.propertyPath);
                }catch(Exception e){hits.Add("Unverified serialized references: "+c.GetType().Name+": "+e.GetType().Name);}
            }
        }
        return hits.Distinct().ToArray();
    }
    static bool Protected(Transform t,TrainSplineMover train)
    {
        if(train!=null)foreach(var anchor in new[]{train.transform,train.frontCar,train.rearCar,train.splineRoute!=null?train.splineRoute.transform:null})
            if(anchor!=null&&(t==anchor||t.IsChildOf(anchor)||anchor.IsChildOf(t)))return true;
        return t.GetComponentsInChildren<Camera>(true).Length>0||t.GetComponentsInChildren<Canvas>(true).Length>0||
            t.GetComponentsInChildren<TrainSeatInteractable>(true).Length>0||t.name.Contains("Manual");
    }
    static Node Describe(GameObject go)
    {
        var all=go.GetComponentsInChildren<Transform>(true);
        return new Node{path=PathOf(go.transform),id=GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(),active=go.activeInHierarchy,objects=all.Length,
            renderers=go.GetComponentsInChildren<Renderer>(true).Length,components=all.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(c=>c!=null).Select(c=>c.GetType().Name+(c.enabled?" [enabled]":" [disabled]")).Distinct().ToArray()};
    }
    static void Audit()
    {
        File.WriteAllText(Out+"/Status.txt","Auditing hierarchy and dependencies; no changes.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty&&SceneManager.GetSceneAt(i).path!=Main)
            throw new InvalidOperationException("Unsaved other scene: "+SceneManager.GetSceneAt(i).path+". Refusing to discard or switch it.");
        var scene=SceneManager.GetSceneByPath(Main);
        if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);
        var objects=Objects(scene).ToArray();var manager=objects.Select(g=>g.GetComponent<RailwayZoneStreamingManager>()).FirstOrDefault(c=>c!=null);
        var micro=objects.Select(g=>g.GetComponent<RailwayMicroChunkStreamer>()).FirstOrDefault(c=>c!=null);
        var train=objects.Select(g=>g.GetComponent<TrainSplineMover>()).FirstOrDefault(c=>c!=null);
        if(manager==null||micro==null||train==null)throw new InvalidOperationException("Core streaming/train references missing. No cleanup permitted.");
        var registered=manager.zones.Select(z=>z.scenePath).Distinct().ToArray();
        var roots=scene.GetRootGameObjects().Select(Describe).ToArray();
        foreach(var root in roots){root.category=root.active?"A":"B";root.reason=root.active?"Active; preserve until role verified":"Disabled alone is not evidence for deletion; preserve";}
        var candidates=new List<Node>();
        var scripts=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&p.EndsWith(".cs")&&!p.Contains("/Editor/")&&File.Exists(p)).Select(p=>new{path=p,text=File.ReadAllText(p)}).ToArray();
        foreach(var go in objects.Where(g=>g.name=="DiagnosticPreview"||g.name.StartsWith("Preview_A_Step_")||g.name.StartsWith("Preview_B_Step_")||g.name=="Preview_C_Custom"||g.name=="PrototypeTrack"))
        {
            if(go.transform.parent!=null&&go.transform.parent.name=="DiagnosticPreview")continue;
            var node=Describe(go);var blocked=Incoming(go.transform,scene).ToList();
            if(Protected(go.transform,train))blocked.Add("Protected train/spline/XR/camera/UI/manual subtree");
            foreach(var code in scripts)if(code.text.Contains("\""+go.name+"\""))blocked.Add("Runtime code name lookup/string: "+code.path);
            node.blockers=blocked.Distinct().ToArray();node.category=node.blockers.Length==0?"D - proposed only":"B - retain";
            node.reason="Former explicitly named diagnostic/prototype group; deletion still requires stored plan and validation.";candidates.Add(node);
        }
        var state=new State{scene=scene.path,dirty=scene.isDirty,status="AUDIT_ONLY",objects=objects.Length,renderers=objects.Sum(g=>g.GetComponents<Renderer>().Length),
            activeRenderers=objects.Sum(g=>g.GetComponents<Renderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)),
            missingScripts=objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),
            visibleMissingMeshes=objects.Sum(g=>g.GetComponents<MeshFilter>().Count(m=>m.sharedMesh==null&&m.GetComponent<MeshRenderer>()!=null&&m.GetComponent<MeshRenderer>().enabled&&m.gameObject.activeInHierarchy)),
            ahead=manager.loadAheadMeters,behind=manager.keepBehindMeters,poolCapacity=micro.poolCapacity,roots=roots,candidates=candidates.ToArray(),registeredZones=registered};
        File.WriteAllText(Out+"/MainHierarchy.json",JsonUtility.ToJson(state,true));
        File.WriteAllLines(Out+"/AllHierarchy.tsv",objects.Select(g=>PathOf(g.transform)+"\tactive="+g.activeInHierarchy+"\t"+string.Join(",",g.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))));
        var scenePaths=AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.StartsWith("Assets/")).ToArray();
        state.protectedScenes=scenePaths.Where(p=>p.Contains("ChiTietSaBan")||p.Contains("QuanLyVongDoiTaiSan")).ToArray();
        var zonePaths=scenePaths.Where(p=>p.StartsWith("Assets/ProjectLopHoc/Generated/RuntimeZones/")).OrderBy(p=>p).ToArray();
        var zones=new List<Zone>();
        foreach(var path in zonePaths)
        {
            File.WriteAllText(Out+"/Status.txt","Read-only zone inspection: "+path);
            var zone=SceneManager.GetSceneByPath(path);bool already=zone.isLoaded;
            if(!already)zone=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var gos=Objects(zone).ToArray();
            zones.Add(new Zone{path=path,registered=registered.Contains(path),objects=gos.Length,renderers=gos.Sum(g=>g.GetComponents<Renderer>().Length),
                cameras=gos.Sum(g=>g.GetComponents<Camera>().Length),listeners=gos.Sum(g=>g.GetComponents<AudioListener>().Length),
                missingScripts=gos.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),
                visibleMissingMeshes=gos.Sum(g=>g.GetComponents<MeshFilter>().Count(m=>m.sharedMesh==null&&m.GetComponent<MeshRenderer>()!=null&&m.GetComponent<MeshRenderer>().enabled&&m.gameObject.activeInHierarchy)),
                managers=gos.Sum(g=>g.GetComponents<RailwayZoneStreamingManager>().Length+g.GetComponents<RailwayMicroChunkStreamer>().Length)});
            if(!already)EditorSceneManager.CloseScene(zone,true);
        }
        SceneManager.SetActiveScene(scene);state.zones=zones.ToArray();
        File.WriteAllText(Out+"/MainHierarchy.json",JsonUtility.ToJson(state,true));
        // Serialized dependency closure covers ALL source scenes/prefabs/materials/SOs, not only the live route.
        File.WriteAllText(Out+"/Status.txt","Checking project-wide serialized dependencies and dynamic asset protection.");
        var depRoots=AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject t:Material").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.StartsWith("Assets/")).ToArray();
        var deps=new HashSet<string>(AssetDatabase.GetDependencies(depRoots,true));
        var bundleAssets=new HashSet<string>(AssetDatabase.GetAllAssetBundleNames().SelectMany(b=>AssetDatabase.GetAssetPathsFromAssetBundle(b)));
        var all=AssetDatabase.GetAllAssetPaths();
        var assetRows=new List<string>{"Path\tBytes\tClassification\tEvidence / uncertainty"};
        foreach(var p in all.Where(p=>p.StartsWith("Assets/ProjectLopHoc/Generated/")&&File.Exists(p)))
        {
            string kind=deps.Contains(p)?"Referenced by serialized project assets":p.Contains("/Resources/")||bundleAssets.Contains(p)?"Dynamic protected":"Suspect only - NOT approved for deletion";
            assetRows.Add(p+"\t"+new FileInfo(p).Length+"\t"+kind+"\tEditor generation/manifests/code/dynamic folders must still be reviewed; no file removed");
        }
        File.WriteAllLines(Out+"/AssetCandidates.tsv",assetRows);
        File.WriteAllLines(Out+"/DynamicSystems.txt",new[]{"Resources assets protected; AssetBundle assets="+bundleAssets.Count,"Addressables config paths: "+string.Join(",",all.Where(p=>p.Contains("AddressableAssetsData"))),"Registered runtime scenes="+registered.Length+"; retained zone files="+zonePaths.Length});
        state.status="AUDIT_COMPLETE_NO_CHANGES";File.WriteAllText(Out+"/MainHierarchy.json",JsonUtility.ToJson(state,true));
        File.WriteAllText(Out+"/Status.txt",state.status);
    }
}
