using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UTT.SaBan;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class ShowroomPerformanceAudit
{
    const string Dir="Temp/ShowroomPerformance";
    const string ScenePath="Assets/ProjectLopHoc/Scenes/Showroom.unity";
    const string Folder="Assets/ProjectLopHoc/Generated/ShowroomPerformance";
    const string ProfileKey="UTT.ShowroomPerformance.Profile";
    static ShowroomPerformanceAudit(){EditorApplication.update+=Tick;EditorApplication.update+=ProfileTick;}
    static void Tick()
    {
        string req=Dir+"/request.txt";if(!File.Exists(req)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string command=File.ReadAllText(req).Trim();if(EditorApplication.isPlayingOrWillChangePlaymode&&command!="status")return;
        File.Delete(req);
        try
        {
            if(command=="survey")Survey();
            else if(command=="status")File.WriteAllText(Dir+"/Status.txt","Playing="+EditorApplication.isPlaying+"; Compiling="+EditorApplication.isCompiling+"; "+string.Join(" | ",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).path+" dirty="+SceneManager.GetSceneAt(i).isDirty)));
            else if(command=="baseline"||command=="after"||command=="afterfinal")BeginProfile(command);
            else if(command=="plan")PlanInstancing();
            else if(command=="verify")VerifyIntegrity();
            else if(command=="checkdirty")CheckSavedState();
            else if(command=="cleardriven")ClearValidatedLayoutDirtiness();
            else if(command.StartsWith("apply_"))ApplyInstancing(command.Substring(6));
        }
        catch(Exception e){File.WriteAllText(Dir+"/Error_"+command+".txt",e.ToString());Debug.LogException(e);}
    }
    static Scene Scene(){var s=SceneManager.GetSceneByPath(ScenePath);if(!s.isLoaded)throw new Exception("Showroom must be open in current Editor");return s;}
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static string Hash(string text){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","");}
    static string FileHash(string path){using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","");}
    static IEnumerable<Transform> Nodes(Scene scene)=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true));
    static SaBanRoot Root()=>Scene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SaBanRoot>(true)).Single();
    static long Tris(Mesh mesh){long n=0;for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)n+=(long)mesh.GetIndexCount(i)/3;return n;}
    static string MeshStats(IEnumerable<Renderer> source)
    {
        var rs=source.ToArray();var fs=rs.Select(r=>r.GetComponent<MeshFilter>()).Where(f=>f!=null&&f.sharedMesh!=null).ToArray();var meshes=fs.Select(f=>f.sharedMesh).Distinct().ToArray();
        return "renderers="+rs.Length+"; enabledActive="+rs.Count(r=>r.enabled&&r.gameObject.activeInHierarchy)+"; meshes="+meshes.Length+"; mats="+rs.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count()+"; vertices(instanced)="+fs.Sum(f=>(long)f.sharedMesh.vertexCount)+"; triangles(instanced)="+fs.Sum(f=>Tris(f.sharedMesh))+"; uniqueMeshBytes="+meshes.Sum(Profiler.GetRuntimeMemorySizeLong)+"; readable="+fs.Count(f=>f.sharedMesh.isReadable);
    }
    static void Survey()
    {
        var scene=Scene();if(scene.isDirty)throw new Exception("Showroom has unsaved changes; survey preserves them, apply is blocked until saved");
        var nodes=Nodes(scene).ToArray();var all=nodes.SelectMany(t=>t.GetComponents<Component>()).Where(c=>c!=null).ToArray();var root=Root();
        var lines=new List<string>{"Scene="+scene.path,"Nodes="+nodes.Length+"; active="+nodes.Count(t=>t.gameObject.activeInHierarchy),"MeshFilters="+all.OfType<MeshFilter>().Count()+"; colliders="+all.OfType<Collider>().Count()+"; meshColliders="+all.OfType<MeshCollider>().Count(),"ALL | "+MeshStats(all.OfType<Renderer>()),"MODEL | "+MeshStats(root.model.GetComponentsInChildren<Renderer>(true)),"Scale="+root.modelScale+"; model="+PathOf(root.model),"Runner="+EditorJsonUtility.ToJson(root.trainRunner)};
        foreach(var layer in root.GetLayers())
        {
            var rs=layer.GetComponentsInChildren<Renderer>(true);lines.Add("LAYER | "+layer.key+" | "+PathOf(layer.transform)+" | visible="+layer.IsVisible+" | "+MeshStats(rs));
            lines.AddRange(rs.GroupBy(r=>r.sharedMaterials.Length==1&&r.sharedMaterial!=null?AssetDatabase.GetAssetPath(r.sharedMaterial):"multi/null").OrderByDescending(g=>g.Count()).Take(7).Select(g=>"  material | "+g.Count()+" | "+g.Key));
            var bs=layer.GetComponentsInChildren<MonoBehaviour>(true).Where(b=>b!=null).GroupBy(b=>b.GetType().FullName).OrderByDescending(g=>g.Count());lines.AddRange(bs.Select(g=>"  script | "+g.Count()+" | "+g.Key));
        }
        lines.AddRange(all.OfType<MonoBehaviour>().GroupBy(b=>b.GetType().FullName).OrderByDescending(g=>g.Count()).Take(55).Select(g=>"SCRIPT | "+g.Count()+" | enabled="+g.Count(b=>b.enabled&&b.gameObject.activeInHierarchy)+" | "+g.Key));
        var mats=all.OfType<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct();
        lines.AddRange(mats.Select(m=>"MAT | "+AssetDatabase.GetAssetPath(m)+" | shader="+m.shader.name+" | instancing="+m.enableInstancing+" | passes="+m.passCount));
        var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
        File.WriteAllLines(Dir+"/AvailableCounters.txt",handles.Select(ProfilerRecorderHandle.GetDescription).Select(d=>d.Category.Name+" | "+d.Name+" | "+d.UnitType));
        File.WriteAllLines(Dir+"/Survey.txt",lines);
        // Source integrity ledger: no scene copies, no branch or backup generation.
        var protectedPaths=Directory.GetFiles("Assets/ProjectLopHoc/Scenes","*.unity",SearchOption.TopDirectoryOnly).Where(p=>p.Replace('\\','/')!=ScenePath).Concat(Directory.GetFiles("Assets/ProjectLopHoc/Generated/RuntimeZones","*.unity",SearchOption.AllDirectories)).Distinct();
        File.WriteAllLines(Dir+"/ProtectedSceneHashes.tsv",protectedPaths.Select(p=>p+"\t"+FileHash(p)));
        File.WriteAllLines(Dir+"/TransformsBefore.tsv",nodes.Select(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject)+"\t"+Hash(EditorJsonUtility.ToJson(t))+"\t"+PathOf(t)+"\t"+t.gameObject.activeSelf));
        File.WriteAllLines(Dir+"/BehavioursBefore.tsv",all.OfType<MonoBehaviour>().Select(b=>GlobalObjectId.GetGlobalObjectIdSlow(b)+"\t"+Hash(EditorJsonUtility.ToJson(b))+"\t"+b.GetType().FullName));
        var assets=all.OfType<MeshFilter>().Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).Concat(mats.Select(AssetDatabase.GetAssetPath)).Where(p=>!string.IsNullOrEmpty(p)&&File.Exists(p)).Distinct();
        File.WriteAllLines(Dir+"/SourceAssetHashes.tsv",assets.Select(p=>p+"\t"+FileHash(p)));
        File.WriteAllLines(Dir+"/Layers.tsv",root.GetLayers().Select(l=>l.key+"\t"+PathOf(l.transform)));
    }

    static List<string> profileLines=new List<string>(),profileErrors=new List<string>();
    static int lastSampleFrame=-1;
    static Dictionary<string,ProfilerRecorder> recorders=new Dictionary<string,ProfilerRecorder>();
    static List<Dictionary<string,double>> samples=new List<Dictionary<string,double>>();
    static SaBanRoot playRoot;static int phase,frame,waitFrames;static string label;static double began;static Camera profileCamera;static Dictionary<Renderer,bool> originalStates;
    static void BeginProfile(string tag)
    {
        // Play measurement preserves the live Editor scene; never save or discard unsaved state here.
        var scene=Scene();
        var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));window.maximized=true;window.Focus();
        SessionState.SetString(ProfileKey+".Tag",tag);SessionState.SetBool(ProfileKey,true);phase=0;began=0;lastSampleFrame=-1;EditorApplication.isPlaying=true;
    }
    static void ProfileTick()
    {
        if(!EditorApplication.isPlaying||!SessionState.GetBool(ProfileKey,false))return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(lastSampleFrame==Time.frameCount)return;
        lastSampleFrame=Time.frameCount;
        try
        {
            if(began==0)began=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-began>900)throw new Exception("Profiling timeout");
            if(phase==0)
            {
                playRoot=Object.FindFirstObjectByType<SaBanRoot>();if(playRoot==null||Time.frameCount<20)return;
                if(SessionState.GetString(ProfileKey+".Tag","")=="baseline")foreach(var group in playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true))group.enabled=false;
                var menu=Object.FindFirstObjectByType<SceneQuickMenu>();if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(menu,new object[]{false});
                foreach(var c in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))c.enabled=false;
                var origin=Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if(origin!=null){foreach(var b in origin.GetComponentsInChildren<Behaviour>(true))if(b.GetType().Name.Contains("PoseDriver")||b.GetType().Name=="QuestRayControllerInput")b.enabled=false;profileCamera=origin.Camera;}
                if(profileCamera==null)profileCamera=Camera.main;
                var panel=playRoot.panel.GetComponent<SaBanControlPanel>();if(panel!=null)panel.faceLocalPlayer=false;
                originalStates=playRoot.model.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.enabled);
                File.WriteAllText(Dir+"/RuntimeCounts_"+SessionState.GetString(ProfileKey+".Tag","")+".txt","Renderers total="+originalStates.Count+"; enabledActive="+originalStates.Keys.Count(r=>r.enabled&&r.gameObject.activeInHierarchy)+"; instanced sources="+playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true).Where(g=>g.enabled).Sum(g=>g.sources.Length)+"; logical batches="+playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true).Where(g=>g.enabled).Sum(g=>g.data.batches.Length));
                playRoot.trainRunner.ResetToStart();SetCamera(false);
                profileLines.Clear();profileErrors.Clear();
                Application.logMessageReceived+=CaptureError;
                AddCounter("Main Thread",ProfilerCategory.Internal);AddCounter("Render Thread",ProfilerCategory.Internal);AddCounter("GPU Frame Time",ProfilerCategory.Render);
                AddCounter("CPU Main Thread Frame Time",ProfilerCategory.Render);AddCounter("CPU Render Thread Frame Time",ProfilerCategory.Render);
                foreach(string n in new[]{"Draw Calls Count","SetPass Calls Count","Triangles Count","Vertices Count","Batches Count"})AddCounter(n,ProfilerCategory.Render);
                foreach(string n in new[]{"GC Allocated In Frame","Mesh Memory","Texture Memory","Total Used Memory","GC Used Memory"})AddCounter(n,ProfilerCategory.Memory);
                File.WriteAllText(Dir+"/ProfileProgress.txt","Initialized; counters="+string.Join(",",recorders.Keys));
                phase=1;frame=0;waitFrames=35;return;
            }
            if(frame++<waitFrames)return;
            if(phase==1){StartCase("overview_stopped");phase=2;return;}
            if(phase==2){if(Collect())return;FinishCase();if(SessionState.GetString(ProfileKey+".Tag","")=="afterfinal"){FinishProfile();return;}playRoot.trainRunner.Play();StartCase("overview_running_x1");phase=3;return;}
            if(phase==3){if(Collect())return;FinishCase();playRoot.trainRunner.SetSpeed(4);StartCase("overview_running_x4");phase=4;return;}
            if(phase==4){if(Collect())return;FinishCase();playRoot.trainRunner.ResetToStart();SetCamera(true);StartCase("close_stopped");phase=5;return;}
            if(phase==5){if(Collect())return;FinishCase();SetCamera(false);phase=6;frame=0;return;}
            if(phase>=100){PictureTick();return;}
            if(phase>=6)
            {
                var layers=playRoot.GetLayers();int index=(phase-6)/2;
                if(index>=layers.Count){BeginPictures();return;}
                var layer=layers[index];
                if((phase-6)%2==0)
                {
                    foreach(var p in originalStates)if(p.Key!=null)p.Key.enabled=p.Value;
                    foreach(var g in playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true))g.suppressRendering=g.transform==layer.transform;
                    foreach(var r in layer.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                    StartCase("hide_visual_"+layer.key);phase++;return;
                }
                if(Collect())return;FinishCase();phase++;frame=0;
            }
        }
        catch(Exception e){File.WriteAllText(Dir+"/ProfileError.txt",e.ToString());FinishProfile();}
    }
    static void CaptureError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)profileErrors.Add(m);}
    static void AddCounter(string name,ProfilerCategory category){try{var r=ProfilerRecorder.StartNew(category,name,1);if(r.Valid)recorders[name]=r;else r.Dispose();}catch{}}
    static void SetCamera(bool close)
    {
        var center=playRoot.transform.position;
        Vector3 target=center+new Vector3(0,playRoot.tableTopHeight,0);
        // Stay below the warehouse roof: measuring from above it would hide the exhibit.
        Vector3 offset=close?new Vector3(0,1.8f,5):new Vector3(0,3.2f,27);
        profileCamera.transform.SetPositionAndRotation(target+offset,Quaternion.LookRotation(-offset));
    }
    static int sampleIndex;
    static void StartCase(string name)
    {
        label=name;samples.Clear();sampleIndex=0;
        for(int i=0;i<90;i++){var row=new Dictionary<string,double>{{"Frame ms",0}};foreach(var r in recorders)row[r.Key]=0;samples.Add(row);}
        frame=0;waitFrames=15;File.WriteAllText(Dir+"/ProfileProgress.txt",SessionState.GetString(ProfileKey+".Tag","")+" | "+name);
    }
    static bool Collect()
    {
        var row=samples[sampleIndex];row["Frame ms"]=Time.unscaledDeltaTime*1000.0;
        foreach(var r in recorders)row[r.Key]=r.Value.LastValue;
        sampleIndex++;return sampleIndex<90;
    }
    static double Pct(double[] values,double p)=>values[Mathf.Clamp((int)Math.Ceiling(values.Length*p)-1,0,values.Length-1)];
    static void FinishCase()
    {
        foreach(string key in samples[0].Keys){var values=samples.Select(s=>s[key]).OrderBy(n=>n).ToArray();profileLines.Add(label+"\t"+key+"\t"+values.Average().ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+Pct(values,.95).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\t"+Pct(values,.99).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));}
        File.WriteAllLines(Dir+"/Profile_"+SessionState.GetString(ProfileKey+".Tag","")+".tsv",new[]{"Case\tMetric\tMean\tp95\tp99"}.Concat(profileLines));
    }
    static void FinishProfile()
    {
        if(originalStates!=null)foreach(var p in originalStates)if(p.Key!=null)p.Key.enabled=p.Value;
        if(playRoot!=null)foreach(var g in playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true))g.suppressRendering=false;
        foreach(var r in recorders.Values)r.Dispose();recorders.Clear();Application.logMessageReceived-=CaptureError;
        File.WriteAllLines(Dir+"/ProfileErrors_"+SessionState.GetString(ProfileKey+".Tag","")+".txt",profileErrors);
        File.WriteAllText(Dir+"/ProfileDone_"+SessionState.GetString(ProfileKey+".Tag","")+".txt","Completed measured cases; errors="+profileErrors.Count);
        SessionState.SetBool(ProfileKey,false);EditorApplication.isPlaying=false;
    }
}
