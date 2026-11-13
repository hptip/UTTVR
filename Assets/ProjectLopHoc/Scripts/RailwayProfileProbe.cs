#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

[InitializeOnLoad]
internal static class RailwayProfileRun
{
    internal const string Request = "Temp/RailwayProfile.request";
    static RailwayProfileRun() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = true;
            return;
        }
        if (EditorApplication.isPlaying && Object.FindFirstObjectByType<RailwayProfileProbe>() == null)
            new GameObject("Editor Railway Profile Probe").AddComponent<RailwayProfileProbe>();
    }
}

internal sealed class RailwayProfileProbe : MonoBehaviour
{
    ProfilerRecorder main, render, gc,playerLoop;
    readonly double[] mainMs = new double[120000], renderMs = new double[120000], gcBytes = new double[120000], deltaMs = new double[120000];
    readonly float[] distance = new float[120000];
    readonly int[] zones = new int[120000];
    int count;
    float started;
    TrainSplineMover train;
    RailwayZoneStreamingManager manager;
    string label;
    RailwayMicroChunkStreamer micro;
    ProfilerRecorder draws;
    long peakRam,peakGraphics;int peakZones,peakDraws;bool cruise;
    readonly UnityEngine.FrameTiming[] timing=new UnityEngine.FrameTiming[1];
    readonly double[] gpuMs=new double[120000];
    readonly double[] playerLoopMs=new double[120000];
    readonly System.Collections.Generic.HashSet<string> visited=new System.Collections.Generic.HashSet<string>();
    int captures;
    bool seatChecked,seatPassed;
    readonly System.Collections.Generic.List<ProfilerRecorder> diagnosticRecorders=new System.Collections.Generic.List<ProfilerRecorder>();
    readonly System.Collections.Generic.List<string> diagnosticNames=new System.Collections.Generic.List<string>();
    readonly System.Collections.Generic.List<string> spikes=new System.Collections.Generic.List<string>();
    double bridgeEntry=-1,bridgeExit=-1;float bridgeStart,bridgeEnd;bool environmentCapture;
    void Start()
    {
        label = File.ReadAllText(RailwayProfileRun.Request).Trim();
        environmentCapture=File.Exists("Temp/QuestEnvironmentCapture.flag");
        main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        playerLoop=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"PlayerLoop");
        render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");
        var handles=new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        foreach(var handle in handles)
        {
            var description=Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);string name=description.Name;
            if(diagnosticNames.Count<24 && (name.Contains("LoadScene")||name.Contains("UnloadScene")||name.Contains("UploadMesh")||name.StartsWith("GC.")||name.Contains("Shader.Create")))
            {diagnosticNames.Add(name);diagnosticRecorders.Add(ProfilerRecorder.StartNew(description.Category,name));}
        }
        started = Time.realtimeSinceStartup;
        var menu=Object.FindFirstObjectByType<SceneQuickMenu>();
        if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(menu,new object[]{false});
    }
    void Update()
    {
        if (train == null) train = Object.FindFirstObjectByType<TrainSplineMover>();
        if (manager == null) manager = Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        if (train == null || train.totalRouteLength <= 0) return;
        if(bridgeEnd==0 && manager!=null)foreach(var facility in manager.fixedInfrastructure)if(facility.kind=="Bridge"){bridgeStart=facility.startChainage;bridgeEnd=facility.endChainage;break;}
        if(bridgeEnd>0 && train.currentDistanceMeters>=bridgeStart && bridgeEntry<0)bridgeEntry=Time.realtimeSinceStartupAsDouble;
        if(bridgeEnd>0 && train.currentDistanceMeters>=bridgeEnd && bridgeExit<0)bridgeExit=Time.realtimeSinceStartupAsDouble;
        if(micro==null)micro=Object.FindFirstObjectByType<RailwayMicroChunkStreamer>();
        if(label=="questFinal" && !cruise)
        {
            if(micro==null||!micro.EssentialReady||manager.LoadedZoneCount<2){started=Time.realtimeSinceStartup;return;}
            if(!seatChecked)
            {
                foreach(var player in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                {
                    if(player.Object==null||!player.Object.IsValid||!player.HasStateAuthority)continue;
                    var seat=Object.FindFirstObjectByType<TrainSeatInteractable>();
                    if(seat!=null){seat.RequestOccupy(player);bool entered=player.isDriving;player.YeuCauRoiGhe();seatPassed=entered&&!player.isDriving;seat.RequestOccupy(player);train.AdjustSpeedLevel(4);}
                    seatChecked=true;break;
                }
                if(!seatChecked){started=Time.realtimeSinceStartup;return;}
            }
            cruise=true;started=Time.realtimeSinceStartup;
        }
        train.currentSpeedKmh = 200;
        train.targetSpeedKmh = 200;
        if (count < distance.Length)
        {
            mainMs[count] = main.Valid ? main.LastValue / 1000000.0 : -1;
            playerLoopMs[count]=playerLoop.Valid?playerLoop.LastValue/1000000.0:-1;
            renderMs[count] = render.Valid ? render.LastValue / 1000000.0 : -1;
            gcBytes[count] = gc.Valid ? gc.LastValue : -1;
            deltaMs[count] = Time.unscaledDeltaTime * 1000;
            distance[count] = train.currentDistanceMeters;
            zones[count] = manager != null ? manager.LoadedZoneCount : 0;
            UnityEngine.FrameTimingManager.CaptureFrameTimings();
            gpuMs[count]=UnityEngine.FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].gpuFrameTime>0?timing[0].gpuFrameTime:-1;
            count++;
        }
        peakRam=System.Math.Max(peakRam,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        peakGraphics=System.Math.Max(peakGraphics,UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver());
        peakZones=Mathf.Max(peakZones,manager.LoadedZoneCount);if(draws.Valid)peakDraws=Mathf.Max(peakDraws,(int)draws.LastValue);
        for(int s=0;s<UnityEngine.SceneManagement.SceneManager.sceneCount;s++){var scene=UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);if(scene.isLoaded && scene.name.StartsWith("Zone_"))visited.Add(scene.name);}
        float captureDistance=environmentCapture?(captures==0?1700:captures==1?4170:5700):(captures==0?600:captures==1?4170:7400);
        if(label=="questFinal" && !File.Exists("Temp/QuestRailwayPureProfile.flag") && captures<3 && train.currentDistanceMeters>captureDistance) {Capture(captures++);}
        if(main.Valid && main.LastValue>40000000 && train.currentDistanceMeters>300)
        {
            var line=new StringBuilder(train.currentDistanceMeters.ToString("F2")+"m main="+(main.LastValue/1000000.0).ToString("F2")+"ms playerLoop="+(playerLoop.Valid?playerLoop.LastValue/1000000.0:-1).ToString("F2")+"ms ");
            for(int k=0;k<diagnosticRecorders.Count;k++)if(diagnosticRecorders[k].Valid&&diagnosticRecorders[k].LastValue>1000000)line.Append(diagnosticNames[k]+"="+(diagnosticRecorders[k].LastValue/1000000.0).ToString("F2")+"ms; ");
            spikes.Add(line.ToString());
        }
        if(label=="questFinal" ? train.currentDistanceMeters<train.totalRouteLength-.1f : Time.realtimeSinceStartup-started<45)return;
        var csv = new StringBuilder("chainage,loadedZones,mainThreadMs,renderThreadMs,gcBytes,deltaMs,gpuMs,playerLoopMs\n");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < count; i++)
            csv.AppendLine(string.Join(",",distance[i].ToString(culture),zones[i],mainMs[i].ToString(culture),renderMs[i].ToString(culture),gcBytes[i].ToString(culture),deltaMs[i].ToString(culture),gpuMs[i].ToString(culture),playerLoopMs[i].ToString(culture)));
        File.WriteAllText("Temp/RailwayProfile_" + label + ".csv", csv.ToString());
        File.WriteAllLines("Temp/RailwayProfile_"+label+"_spikes.txt",spikes);
        File.WriteAllText("Temp/RailwayBridgeCrossingTest.txt",$"Bridge={bridgeStart:F3}..{bridgeEnd:F3}m; speed=200km/h; expectedSeconds={(bridgeEnd-bridgeStart)/(200f/3.6f):F3}; observedRealtimeSeconds={(bridgeEntry>=0&&bridgeExit>=0?bridgeExit-bridgeEntry:-1):F3}; no manual chainage changes.");
        var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        float refresh = 0;
        foreach (var display in displays)
            if (display.running && display.TryGetDisplayRefreshRate(out float rate)) refresh = rate;
        int activeRenderers = 0;
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (renderer.enabled && renderer.gameObject.activeInHierarchy) activeRenderers++;
        File.WriteAllText("Temp/RailwayProfile_" + label + "_context.txt",
            $"Editor={Application.isEditor}; refreshHz={refresh}; target72BudgetMs={1000f/72}; activeGO={Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}; activeRenderers={activeRenderers}; MainThreadRecorder={main.Valid}; RenderThreadRecorder={render.Valid}; peakUnityAllocatedBytes={peakRam}; peakGraphicsDriverBytes={peakGraphics}; peakZones={peakZones}; peakDrawCalls={peakDraws}; visited={string.Join(",",visited)}; railPoolCapacity={micro?.poolCapacity}; peakRailChunks={micro?.PeakActiveChunks}; missingEssentialRailFrames={micro?.MissingEssentialFrames}; seatEnterExit={seatPassed}; visualCaptures={captures}. Cruise=200km/h; chainage advanced by TrainSplineMover only; GPU invalid when csv=-1 or implausibly large. Capture passes, when enabled, introduce intentional spikes at{(environmentCapture ? "1700,4170,5700" : "600,4170,7400")}m.");
        File.Delete(RailwayProfileRun.Request);
        main.Dispose(); render.Dispose(); gc.Dispose();draws.Dispose();
        EditorApplication.isPlaying = false;
    }
    void OnDestroy() { main.Dispose(); render.Dispose(); gc.Dispose();draws.Dispose();playerLoop.Dispose();foreach(var recorder in diagnosticRecorders)recorder.Dispose(); }
    void Capture(int index)
    {
        var go=new GameObject("Editor validation camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.cullingMask=~(1<<5);camera.farClipPlane=1800;camera.nearClipPlane=.1f;
        Vector3 f=train.frontCar.forward;camera.transform.position=train.frontCar.position+Vector3.up*9+train.frontCar.right*12;
        camera.transform.LookAt(train.frontCar.position+f*60+Vector3.up*2);
        if(environmentCapture){camera.transform.position=train.frontCar.position+Vector3.up*2.2f+train.frontCar.right*1.8f;camera.transform.LookAt(train.frontCar.position+f*30+train.frontCar.right*90+Vector3.up*(index==1?-5:1.8f));}
        var rt=new RenderTexture(1280,720,24);rt.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes("Temp/QuestRailway_View_"+index+".png",texture.EncodeToPNG());RenderTexture.active=previous;rt.Release();Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(go);
    }
}
#endif
