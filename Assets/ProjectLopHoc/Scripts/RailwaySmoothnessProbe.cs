#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

[InitializeOnLoad]
internal static class RailwaySmoothnessProbeRun
{
    static double deadline;
    static RailwaySmoothnessProbeRun()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists("Temp/RailwaySmoothnessProbe.request")){deadline=0;return;}
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(deadline==0)deadline=EditorApplication.timeSinceStartup+260;
        if(EditorApplication.timeSinceStartup>deadline){File.WriteAllText("Temp/RailwaySmoothness_TIMEOUT.txt","Timed out");File.Delete("Temp/RailwaySmoothnessProbe.request");EditorApplication.isPlaying=false;return;}
        if(!EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=true;return;}
        if(EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<RailwaySmoothnessProbe>()==null)new GameObject("Editor clean smoothness probe").AddComponent<RailwaySmoothnessProbe>();
    }
}
public sealed class RailwaySmoothnessProbe:MonoBehaviour
{
    const int Capacity=120000;
    readonly float[] chainage=new float[Capacity],mainMs=new float[Capacity],loopMs=new float[Capacity],deltaMs=new float[Capacity];readonly long[] allocated=new long[Capacity];
    readonly Dictionary<string,int> loads=new Dictionary<string,int>(),unloads=new Dictionary<string,int>();
    readonly List<ProfilerRecorder> diagnostics=new List<ProfilerRecorder>();readonly List<string> names=new List<string>(),spikes=new List<string>();
    ProfilerRecorder main,loop,gc,draws;string label;TrainSplineMover train;RailwayZoneStreamingManager manager;RailwayMicroChunkStreamer rails;bool started,done;int count,errors,peakZones,peakDraws;long peakRam;float nextSample,nextSpike,start;
    void Start()
    {
        label=File.ReadAllText("Temp/RailwaySmoothnessProbe.request").Trim();
        main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");loop=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"PlayerLoop");gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
        var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
        foreach(var handle in handles){var d=ProfilerRecorderHandle.GetDescription(handle);string n=d.Name;if(names.Count<28&&(n=="GC.Collect"||n.Contains("WaitForTargetFPS")||n.Contains("WaitForPresent")||n.Contains("AsyncUpload")||n=="EditorLoop"||n.Contains("BehaviourUpdate")||n.Contains("IntegrateAll"))){names.Add(n);diagnostics.Add(ProfilerRecorder.StartNew(d.Category,n));}}
        foreach(var scene in AllScenes())if(scene.isLoaded&&scene.name.StartsWith("Zone_"))loads[scene.name]=1;
        SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;Application.logMessageReceived+=Log;
        train=UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>();manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();rails=UnityEngine.Object.FindFirstObjectByType<RailwayMicroChunkStreamer>();
    }
    IEnumerable<Scene> AllScenes(){for(int i=0;i<SceneManager.sceneCount;i++)yield return SceneManager.GetSceneAt(i);}
    void Loaded(Scene scene,LoadSceneMode mode){if(scene.name.StartsWith("Zone_")){loads.TryGetValue(scene.name,out int n);loads[scene.name]=n+1;}}
    void Unloaded(Scene scene){if(scene.name.StartsWith("Zone_")){unloads.TryGetValue(scene.name,out int n);unloads[scene.name]=n+1;}}
    void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
    void Update()
    {
        if(train==null||manager==null||rails==null||train.totalRouteLength<1)return;
        if(!started)
        {
            if(!rails.EssentialReady||manager.LoadedZoneCount<2)return;
            foreach(var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            {
                if(player.Object==null||!player.Object.IsValid||!player.HasStateAuthority)continue;
                var seat=UnityEngine.Object.FindFirstObjectByType<TrainSeatInteractable>();seat.RequestOccupy(player);if(!player.isDriving)return;train.AdjustSpeedLevel(4);started=true;start=Time.realtimeSinceStartup;
                var menu=UnityEngine.Object.FindFirstObjectByType<SceneQuickMenu>();if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(menu,new object[]{false});break;
            }
            if(!started)return;
        }
        train.currentSpeedKmh=train.targetSpeedKmh=200;
        if(train.currentDistanceMeters>=300&&count<Capacity){chainage[count]=train.currentDistanceMeters;mainMs[count]=main.LastValue/1000000f;loopMs[count]=loop.LastValue/1000000f;deltaMs[count]=Time.unscaledDeltaTime*1000;allocated[count]=gc.LastValue;count++;}
        if(Time.unscaledTime>=nextSample){nextSample=Time.unscaledTime+.25f;peakZones=Mathf.Max(peakZones,manager.LoadedZoneCount);peakRam=Math.Max(peakRam,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());}
        if(draws.Valid)peakDraws=Mathf.Max(peakDraws,(int)draws.LastValue);
        if(main.LastValue>30000000&&Time.unscaledTime>nextSpike&&train.currentDistanceMeters>300&&spikes.Count<128)
        {
            nextSpike=Time.unscaledTime+.1f;var text=new StringBuilder($"{train.currentDistanceMeters:F2}m main={main.LastValue/1000000f:F2}ms loop={loop.LastValue/1000000f:F2}ms ");
            for(int i=0;i<names.Count;i++)if(diagnostics[i].Valid&&diagnostics[i].LastValue>2000000)text.Append(names[i]+"="+(diagnostics[i].LastValue/1000000f).ToString("F2")+"ms; ");spikes.Add(text.ToString());
        }
        if(train.currentDistanceMeters<train.totalRouteLength-.1f)return;
        var csv=new StringBuilder("chainage,mainThreadMs,playerLoopMs,gcBytes,deltaMs\n");var culture=System.Globalization.CultureInfo.InvariantCulture;
        for(int i=0;i<count;i++)csv.AppendLine(string.Join(",",chainage[i].ToString(culture),mainMs[i].ToString(culture),loopMs[i].ToString(culture),allocated[i],deltaMs[i].ToString(culture)));
        File.WriteAllText("Temp/RailwaySmoothness_"+label+".csv",csv.ToString());File.WriteAllLines("Temp/RailwaySmoothness_"+label+"_spikes.txt",spikes);
        var report=new StringBuilder($"Full actual movement25..{train.currentDistanceMeters:F3}m at200km/h; samples={count}; no inspection camera/capture; CPU and GC are global Editor counters; MainValid={main.Valid},LoopValid={loop.Valid}; zones={loads.Count}; peakZones={peakZones}; finalZones={manager.LoadedZoneCount}; railMissing={rails.MissingEssentialFrames}; railPeak={rails.PeakActiveChunks}; peakDraws={peakDraws}; peakRam={peakRam}; errors={errors}; elapsed={Time.realtimeSinceStartup-start:F2}s\n");
        foreach(var pair in loads){unloads.TryGetValue(pair.Key,out int n);report.AppendLine($"{pair.Key}: loads={pair.Value},unloads={n}");}
        File.WriteAllText("Temp/RailwaySmoothness_"+label+"_context.txt",report.ToString());done=true;File.Delete("Temp/RailwaySmoothnessProbe.request");EditorApplication.isPlaying=false;
    }
    void OnDestroy(){main.Dispose();loop.Dispose();gc.Dispose();draws.Dispose();foreach(var item in diagnostics)item.Dispose();SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;Application.logMessageReceived-=Log;if(!done&&File.Exists("Temp/RailwaySmoothnessProbe.request")){File.WriteAllText("Temp/RailwaySmoothness_INTERRUPTED.txt","Run interrupted");File.Delete("Temp/RailwaySmoothnessProbe.request");}}
}
#endif
