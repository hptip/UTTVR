#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Profiling;

[InitializeOnLoad]
internal static class RailwayExperienceValidationRun
{
    static double deadline;
    static RailwayExperienceValidationRun()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists("Temp/RailwayExperienceValidation.request")||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(deadline==0)deadline=EditorApplication.timeSinceStartup+230;
        if(EditorApplication.timeSinceStartup>deadline){File.WriteAllText("Temp/RailwayExperience_Test.txt","FAIL: wall-clock timeout");File.Delete("Temp/RailwayExperienceValidation.request");EditorApplication.isPlaying=false;deadline=0;return;}
        if(!EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=true;return;}
        if(EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<RailwayExperienceValidation>()==null)new GameObject("Editor Railway Experience Validation").AddComponent<RailwayExperienceValidation>();
    }
}
public sealed class RailwayExperienceValidation:MonoBehaviour
{
    TrainSplineMover train;RailwayZoneStreamingManager manager;RailwayMicroChunkStreamer rails;bool started,done;float startTime;
    int captures,errors,peakZones;long peakMemory;ProfilerRecorder cpu,gc;
    readonly HashSet<string> visited=new HashSet<string>();
    readonly HashSet<string> seenFacilities=new HashSet<string>();
    readonly float[] photoDistance={550,1050,1400,1750,2150,2500,2850,3200,5600,6100};
    readonly string[] photoName={"Subgrade","Tunnel2_Entrance","Tunnel2_Interior","Tunnel2_Exit","Tunnel1_Entrance","Tunnel1_Interior","Tunnel1_Exit","Overhead","VillageRight","VillageLeft"};
    readonly List<double> frameCpu=new List<double>(60000),frameGc=new List<double>(60000);
    string lastException="";
    bool overheadOnly;
    void Start(){overheadOnly=File.ReadAllText("Temp/RailwayExperienceValidation.request").Trim()=="overheadOnly";cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");Application.logMessageReceived+=Log;startTime=Time.realtimeSinceStartup;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){errors++;lastException=message;}}
    void Update()
    {
        if(train==null)train=UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>();if(manager==null)manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();if(rails==null)rails=UnityEngine.Object.FindFirstObjectByType<RailwayMicroChunkStreamer>();
        if(train==null||manager==null||rails==null||train.totalRouteLength<1)return;
        if(!started)
        {
            if(!rails.EssentialReady||manager.LoadedZoneCount<2)return;
            foreach(var player in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            {
                if(player.Object==null||!player.Object.IsValid||!player.HasStateAuthority)continue;
                var seat=UnityEngine.Object.FindFirstObjectByType<TrainSeatInteractable>();seat.RequestOccupy(player);if(!player.isDriving)return;
                train.AdjustSpeedLevel(4);started=true;startTime=Time.realtimeSinceStartup;
                var menu=UnityEngine.Object.FindFirstObjectByType<SceneQuickMenu>();if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(menu,new object[]{false});break;
            }
            if(!started)return;
        }
        train.currentSpeedKmh=train.targetSpeedKmh=200;
        for(int s=0;s<SceneManager.sceneCount;s++){var scene=SceneManager.GetSceneAt(s);if(scene.isLoaded&&scene.name.StartsWith("Zone_"))visited.Add(scene.name);}
        foreach(var facility in manager.fixedInfrastructure.Where(f=>f.kind=="TunnelMountain"))
        {
            if(train.currentDistanceMeters>facility.startChainage+20&&train.currentDistanceMeters<facility.endChainage-20&&GameObject.Find("TunnelMountainGroup_"+facility.id.Replace("TunnelMountain_",""))!=null)seenFacilities.Add(facility.id);
        }
        peakZones=Mathf.Max(peakZones,manager.LoadedZoneCount);peakMemory=Math.Max(peakMemory,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        if(train.currentDistanceMeters>300&&!photoDistance.Any(d=>Mathf.Abs(train.currentDistanceMeters-d)<60)){if(cpu.Valid)frameCpu.Add(cpu.LastValue/1000000.0);if(gc.Valid)frameGc.Add(gc.LastValue);}
        if(captures<photoDistance.Length&&train.currentDistanceMeters>=photoDistance[captures]){Capture(captures);captures++;}
        if(train.currentDistanceMeters<(overheadOnly?3500:train.totalRouteLength-.1f))return;
        bool pass=(overheadOnly?visited.Count>=8:visited.Count==17)&&seenFacilities.Count==2&&rails.MissingEssentialFrames==0&&errors==0;
        frameCpu.Sort();frameGc.Sort();
        File.WriteAllText(overheadOnly?"Temp/RailwayExperience_WireTest.txt":"Temp/RailwayExperience_Test.txt",$"{(pass?"PASS":"FAIL")}: actual TrainSplineMover movement25..{train.currentDistanceMeters:F3}m at200km/h; no manual chainage edits. zones={string.Join(",",visited)}; tunnel groups seen inside both bores={string.Join(",",seenFacilities)}; missingEssentialRailFrames={rails.MissingEssentialFrames}; poolCapacity={rails.poolCapacity}, peakRailChunks={rails.PeakActiveChunks}; peakZones={peakZones}, finalZones={manager.LoadedZoneCount}; captures={captures}; errors={errors}, last={lastException}; elapsed={Time.realtimeSinceStartup-startTime:F2}sec; mainP99={Percentile(frameCpu,.99):F3}ms,max={Percentile(frameCpu,1):F3}ms; GC P99={Percentile(frameGc,.99):F0}B,max={Percentile(frameGc,1):F0}B; editorPeakMemory={peakMemory}B. Metrics exclude +/-60m around all10 inspection captures; Editor only; no Quest/GPU/FPS certification.");
        done=true;File.Delete("Temp/RailwayExperienceValidation.request");EditorApplication.isPlaying=false;
    }
    static double Percentile(List<double> list,double p)=>list.Count>0?list[(int)((list.Count-1)*p)]:-1;
    void Capture(int index)
    {
        var go=new GameObject("Editor sector inspection camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.farClipPlane=1200;camera.cullingMask=~(1<<5);
        float d=train.currentDistanceMeters,t=d/train.totalRouteLength;Vector3 p=train.splineRoute.EvaluatePosition(t),f=train.splineRoute.EvaluateTangent(t);f.y=0;f.Normalize();var right=Vector3.Cross(Vector3.up,f);
        camera.transform.position=p+f*32+Vector3.up*3;camera.transform.LookAt(p+f*100+Vector3.up*4);
        if(index==0){camera.transform.position=p+Vector3.up*7-right*5;camera.transform.LookAt(p+f*55+right*25+Vector3.up*6);}
        if(index==8||index==9){float side=index==8?1:-1;camera.transform.position=p+right*(side*8)+Vector3.up*4;camera.transform.LookAt(p+f*35+right*(side*65)+Vector3.up*3);}
        if(index==3||index==6){camera.transform.position=p+Vector3.up*8+right*18;camera.transform.LookAt(p-f*90+Vector3.up*12);}
        var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;Texture2D texture=null;
        try{RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Temp/Experience_"+photoName[index]+".png",texture.EncodeToPNG());}
        finally{RenderTexture.active=previous;rt.Release();Destroy(rt);if(texture!=null)Destroy(texture);Destroy(go);}
    }
    void OnDestroy(){cpu.Dispose();gc.Dispose();Application.logMessageReceived-=Log;if(!done&&File.Exists("Temp/RailwayExperienceValidation.request")){File.WriteAllText("Temp/RailwayExperience_Test.txt","FAIL: interrupted");File.Delete("Temp/RailwayExperienceValidation.request");}}
}
#endif
