using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs only on explicit file requests; uses the currently open Editor.
[InitializeOnLoad]
public static class UTTQuestDeviceBuild
{
    const string Dir="Temp/QuestDeviceBuild";
    const string Request=Dir+"/request.txt";
    const string Apk="Builds/UTT_SixScenes_Quest.apk";
    static readonly string[] MainScenes={"Login","UTTCampus","Classroom","Showroom","YenTuHaLongXanh","QuanLyVongDoiTaiSan"};
    static UTTQuestDeviceBuild(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try
        {
            if(command=="inspect")Inspect();
            else if(command=="android")
            {
                File.WriteAllText(Dir+"/Stage.txt","Switching target to Android");
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android&&!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("Android target switch failed");
                Inspect();
            }
            else if(command=="build")Build();
        }
        catch(Exception e){File.WriteAllText(Dir+"/Error.txt",e.ToString());File.WriteAllText(Dir+"/Stage.txt","FAILED: "+e.Message);Debug.LogException(e);}
    }
    static void Inspect()
    {
        var rows=new List<string>{"UTC="+DateTime.UtcNow.ToString("O"),"Unity="+Application.unityVersion,"ActiveTarget="+EditorUserBuildSettings.activeBuildTarget,"Package="+PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),"Backend="+PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),"Architectures="+PlayerSettings.Android.targetArchitectures,"AAB="+EditorUserBuildSettings.buildAppBundle,"SplitOBB="+PlayerSettings.Android.splitApplicationBinary};
        var profile=BuildProfile.GetActiveBuildProfile();
        rows.Add("ActiveProfile="+(profile==null?"Default platform profile":AssetDatabase.GetAssetPath(profile)));
        if(profile!=null){rows.Add("ProfileOverrideScenes="+profile.overrideGlobalScenes);rows.AddRange(profile.scenes.Select(s=>"ProfileScene="+s.enabled+" | "+s.path));}
        foreach(string guid in AssetDatabase.FindAssets("t:BuildProfile"))rows.Add("ProfileAsset="+AssetDatabase.GUIDToAssetPath(guid));
        for(int i=0;i<SceneManager.sceneCount;i++){var s=SceneManager.GetSceneAt(i);rows.Add("OpenScene="+s.path+" | dirty="+s.isDirty);}
        foreach(var key in new[]{"AndroidSdkRoot","AndroidNdkRootR16","JdkPath"})rows.Add(key+"="+EditorPrefs.GetString(key,"(bundled/default)"));
        foreach(var key in new[]{"TEMP","TMP","GRADLE_USER_HOME","BEE_CACHE_DIRECTORY"})rows.Add(key+"="+Environment.GetEnvironmentVariable(key));
        rows.AddRange(EditorBuildSettings.scenes.Select(s=>"GlobalScene="+s.enabled+" | "+s.path));
        File.WriteAllLines(Dir+"/Inspection.txt",rows);
    }
    static void Build()
    {
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new Exception("Switch to Android first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);
        foreach(var pair in new[]{("TEMP",@"F:\UnityTemp"),("TMP",@"F:\UnityTemp"),("GRADLE_USER_HOME",@"F:\UnityCache\Gradle"),("BEE_CACHE_DIRECTORY",@"F:\UnityCache\Bee")})
        {Directory.CreateDirectory(pair.Item2);Environment.SetEnvironmentVariable(pair.Item1,pair.Item2);}
        var scenes=MainScenes.Select(n=>"Assets/ProjectLopHoc/Scenes/"+n+".unity").ToList();
        // Include dynamically loaded zone scenes explicitly; follow current data, not historical zone counts.
        foreach(string path in MainScenes.Select(n=>"Assets/ProjectLopHoc/Scenes/"+n+".unity"))
        {
            if(!File.Exists(path))throw new Exception("Missing scene: "+path);
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path),@"scenePath:\s*(Assets/[^\r\n]+\.unity)"))
            {string dep=match.Groups[1].Value.Trim();if(!File.Exists(dep))throw new Exception("Missing dynamic scene: "+dep);if(!scenes.Contains(dep))scenes.Add(dep);}
        }
        // Preserve other enabled scenes used by existing scene-transition code.
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))if(File.Exists(entry.path)&&!scenes.Contains(entry.path))scenes.Add(entry.path);
        var list=scenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        EditorBuildSettings.scenes=list;
        var profile=BuildProfile.GetActiveBuildProfile();
        if(profile!=null&&profile.overrideGlobalScenes){profile.scenes=list;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        EditorUserBuildSettings.buildAppBundle=false;
        Directory.CreateDirectory("Builds");
        File.WriteAllLines(Dir+"/BuildScenes.txt",scenes);
        File.WriteAllText(Dir+"/Stage.txt","BUILDING | "+DateTime.Now.ToString("O")+" | scenes="+scenes.Count+" | output="+Apk);
        BuildReport report;
        if(profile!=null)report=BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions{buildProfile=profile,locationPathName=Apk,options=BuildOptions.None});
        else report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes.ToArray(),target=BuildTarget.Android,targetGroup=BuildTargetGroup.Android,locationPathName=Apk,options=BuildOptions.None});
        var rows=new List<string>{"Result="+report.summary.result,"Errors="+report.summary.totalErrors,"Warnings="+report.summary.totalWarnings,"Time="+report.summary.totalTime,"TotalSize="+report.summary.totalSize,"Output="+Path.GetFullPath(Apk)};
        foreach(var step in report.steps)foreach(var message in step.messages)if(message.type==LogType.Error||message.type==LogType.Exception)rows.Add(step.name+" | "+message.content);
        File.WriteAllLines(Dir+"/BuildReport.txt",rows);
        if(report.summary.result!=BuildResult.Succeeded||!File.Exists(Apk))throw new Exception("Build did not produce successful APK; see BuildReport.txt");
        File.WriteAllText(Dir+"/Stage.txt","SUCCEEDED | APK bytes="+new FileInfo(Apk).Length);
    }
}
