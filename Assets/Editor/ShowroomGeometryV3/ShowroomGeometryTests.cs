using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ShowroomGeometryTests
{
    const string Key = "UTT.ShowroomGeometryV3.Test";
    static double began;
    static bool configured;
    static ShowroomGeometryBenchmark benchmark;
    static ShowroomGeometryTests() { EditorApplication.update += Tick; }
    public static string Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode first");
        if (SceneManager.GetActiveScene().isDirty) throw new Exception("Save current scene first");
        Directory.CreateDirectory("Temp/ShowroomMerge"); Directory.CreateDirectory("Temp/ShowroomPerformance"); Directory.CreateDirectory("Temp/ShowroomPerformance/Images_baseline"); Directory.CreateDirectory("Temp/ShowroomPerformance/Images_after");
        SessionState.SetString(Key,"regression"); SessionState.SetBool("UTT.ShowroomPackage.Test.Performance",true);
        typeof(ShowroomPackageMergeTools).GetMethod("BeginPlayTests",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        return "Regression started in Quality10; four-mode Editor measurement follows automatically";
    }
    public static string RetryRegressionOnly()
    {
        Begin(); SessionState.SetBool(Key+".Only",true); return "Retrying only regression after creating its missing output folder";
    }
    public static string BeginMeasurementOnly()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new Exception("Saved Edit Mode scene required");
        SessionState.SetString(Key,"measurement");began=0;configured=false;EditorApplication.isPlaying=true;return "Measuring final accepted geometry";
    }
    static void Tick()
    {
        string phase=SessionState.GetString(Key,"");if(phase=="")return;
        try
        {
            if(phase=="regression")
            {
                if(EditorApplication.isPlaying)
                {
                    var c=Object.FindFirstObjectByType<ShowroomGeometryController>();if(c!=null&&c.mode!=ShowroomGeometryMode.Quality10)c.SetMode(ShowroomGeometryMode.Quality10);
                    return;
                }
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(SessionState.GetBool("UTT.ShowroomPackage.Test",false))return;
                string result=File.ReadAllText("Temp/ShowroomPerformance/Regression.txt");
                File.WriteAllText(ShowroomGeometryV3.Output+"/Regression.txt",result);
                if(SessionState.GetBool(Key+".Only",false)){SessionState.SetBool(Key+".Only",false);SessionState.SetString(Key,"");return;}
                SessionState.SetString(Key,"measurement");began=0;configured=false;EditorApplication.isPlaying=true;return;
            }
            if(phase=="measurement")
            {
                if(!EditorApplication.isPlaying)return;
                if(began==0)began=EditorApplication.timeSinceStartup;
                if(EditorApplication.timeSinceStartup-began<8)return;
                if(!configured)
                {
                    var controller=Object.FindFirstObjectByType<ShowroomGeometryController>();if(controller==null)throw new Exception("Controller missing");
                    foreach(var g in Object.FindObjectsByType<ShowroomInstancedVisualGroup>(FindObjectsSortMode.None))if(g.sources.Any(r=>r!=null&&r.enabled))throw new Exception("Duplicate instanced source rendering");
                    foreach(var mode in new[]{ShowroomGeometryMode.Original,ShowroomGeometryMode.Quality50,ShowroomGeometryMode.Quality25,ShowroomGeometryMode.Quality10})
                    {
                        controller.SetMode(mode);
                        foreach(var f in controller.targets)if(f!=null&&f.sharedMesh==null)throw new Exception("Missing mesh after mode switch");
                        foreach(var g in Object.FindObjectsByType<ShowroomInstancedVisualGroup>(FindObjectsSortMode.None))foreach(var b in g.data.batches)
                            if(ShowroomGeometryController.Resolve(b.mesh,g)!=controller.profile.Resolve(b.mesh,mode))throw new Exception("Instancing resolver did not select profile mesh");
                    }
                    controller.SetMode(ShowroomGeometryMode.DeviceDefault);
                    if(controller.EffectiveMode!=ShowroomGeometryMode.Original)throw new Exception("Desktop default must remain original");
                    foreach(var c in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))c.enabled=false;
                    foreach(var s in Object.FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None))s.enabled=false;
                    var origin=Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                    if(origin!=null)foreach(var b in origin.GetComponentsInChildren<Behaviour>(true))if(b.GetType().Name.Contains("PoseDriver")||b.GetType().Name=="QuestRayControllerInput")b.enabled=false;
                    foreach(var b in Resources.FindObjectsOfTypeAll<Behaviour>())if(b!=null&&b.gameObject.scene.IsValid()&&(b.GetType().Name=="XRDeviceSimulator"||b.GetType().Name=="XRInteractionSimulator"))b.enabled=false;
                    var root=controller.GetComponent<UTT.SaBan.SaBanRoot>();var camera=origin!=null?origin.Camera:Camera.main;
                    Vector3 target=root.transform.position+Vector3.up*root.tableTopHeight;var offset=new Vector3(0,3.2f,27);camera.transform.SetPositionAndRotation(target+offset,Quaternion.LookRotation(-offset));
                    var menu=Object.FindFirstObjectByType<SceneQuickMenu>();if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{false});
                    var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));SessionState.SetBool(Key+".Max",window.maximized);window.maximized=true;window.Focus();
                    benchmark=controller.GetComponent<ShowroomGeometryBenchmark>();benchmark.warmupFrames=90;benchmark.sampleFrames=180;benchmark.Begin();configured=true;return;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                if(benchmark.Running)return;
                File.WriteAllText(ShowroomGeometryV3.Output+"/EditorBenchmarkPath.txt",benchmark.OutputDirectory+"\n"+(benchmark.LastFailure??"Completed"));
                if(benchmark.OutputDirectory!=null)
                {
                    string to=ShowroomGeometryV3.Output+"/EditorBenchmark";Directory.CreateDirectory(to);
                    foreach(var f in Directory.GetFiles(benchmark.OutputDirectory))File.Copy(f,to+"/"+Path.GetFileName(f),true);
                }
                EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized=SessionState.GetBool(Key+".Max",false);SessionState.SetString(Key,"");EditorApplication.isPlaying=false;
            }
        }
        catch(Exception e)
        {
            File.WriteAllText(ShowroomGeometryV3.Output+"/TestError.txt",e.ToString());SessionState.SetString(Key,"");if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
        }
    }
}
