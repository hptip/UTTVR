using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RailwaySmoothnessJob
{
    static RailwaySmoothnessJob()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        const string request="Temp/RailwaySmoothnessJob.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try
        {
            string mode=File.ReadAllText(request).Trim();if(mode.Length==0)return;File.Delete(request);
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)throw new InvalidOperationException("Unsaved scene; refusing to discard changes");
            if(scene.name!="YenTuHaLongXanh")EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity");
            if(mode=="apply") { RailwayEnvironmentDensity.ApplyBilateralAndMeshOptimization(); return; }
            if(mode=="visual") { RailwayEnvironmentDensity.InspectBilateralSubgrade(); return; }
            if(mode=="settings")
            {
                var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();manager.maximumAdaptiveLoadAheadMeters=1200;EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return;
            }
            var game=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(game!=null)EditorWindow.GetWindow(game).Focus();
            File.WriteAllText("Temp/RailwaySmoothnessProbe.request",mode);
        }
        catch(Exception e){File.WriteAllText("Temp/RailwaySmoothness_ERROR.txt",e.ToString());}
    }
}
