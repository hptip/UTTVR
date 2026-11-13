using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class RailwayBimRefinementTests
{
    const string TestKey="UTT.Refinement.Test";static string command;static double started;static bool ran;static readonly List<string> results=new List<string>(),errors=new List<string>();
    static RailwayBimRefinementTests()
    {
        EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(EditorApplication.isPlaying&&SessionState.GetBool(TestKey,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors.Add(m);};
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);results.Add("PASS | "+message);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)
        {
            if(!File.Exists("Temp/BimRefinementTest.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            command=File.ReadAllText("Temp/BimRefinementTest.request").Trim();File.Delete("Temp/BimRefinementTest.request");SessionState.SetString(TestKey+".Command",command);
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(active.path!=RailwayBimRefinement.ScenePath){File.WriteAllText("Temp/BimRefinement/Test_BLOCKED.txt","Unexpected active scene: "+active.path);return;}if(active.isDirty)EditorSceneManager.SaveScene(active);
            results.Clear();errors.Clear();ran=false;started=0;ResetInteraction();SessionState.SetBool(TestKey,true);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(TestKey,false)||ran)return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        try
        {
            var c=Object.FindFirstObjectByType<RailwayBimController>();if(c==null||!c.Ready){if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Readiness timeout");return;}
            command=SessionState.GetString(TestKey+".Command","");
            if(command=="topbar"){if(!TopBarTick(c))return;}
            else if(command=="flight"){if(!FlightTick(c))return;}
            else if(command=="bimdata")BimScenario(c);
            else if(command=="selection")Selection(c);
            else if(command=="stages")Stages(c);
            else if(command=="stage-persist")StagePersist(c);
            else if(command=="excel")StageExcel(c);
            else if(command=="ui"){if(!UiTick(c))return;}
            else if(command=="vr"){if(!VrTick(c))return;}
            else if(command=="ui-persist")UiPersist();
            else if(command=="sample")SampleImport(c);
            Check(errors.Count==0,"No new C# runtime errors");results.Add("DONE "+command);Finish();
        }
        catch(Exception e){results.Add("FAIL | "+e);results.AddRange(errors);Finish();}
    }
    static void Finish(){CleanupInteraction();ran=true;Directory.CreateDirectory("Temp/BimRefinement");File.WriteAllLines("Temp/BimRefinement/Test_"+command+".txt",results);SessionState.SetBool(TestKey,false);EditorApplication.isPlaying=false;}
    static void Selection(RailwayBimController c)
    {
        c.SetOverview();string segment=c.catalog.segments[0].id;var block=c.catalog.blocks.First(b=>b.segmentID==segment);var bolt=block.id+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;
        c.Select(segment);Check(c.Level==0&&c.SelectedID==segment,"Segment selection focuses without descending");float old=Mathf.Max(60,c.Visible(segment).GetComponent<Renderer>().bounds.extents.magnitude/Mathf.Sin(c.cameraController.GetComponent<Camera>().fieldOfView*.5f*Mathf.Deg2Rad)*1.2f);Check(c.cameraController.current.distance<old,"Closer segment fit: "+c.cameraController.current.distance+"m vs old "+old+"m");
        c.EnterDetails();c.Select(block.id);Check(c.Level==1&&c.SelectedID==block.id,"Block selection keeps level");c.EnterDetails();c.Select(bolt);Check(c.Level==2&&c.cameraController.current.distance<1.1f,"Bolt automatic focus closer than former 1.1m minimum");
        var keep=c.Visible(bolt).GetComponent<Renderer>();var sibling=c.CurrentItems().First(a=>a.AssetID!=bolt);var other=c.Visible(sibling.AssetID).GetComponent<Renderer>();other.enabled=false;
        c.ToggleIsolation();Check(c.IsIsolated&&keep.enabled&&c.outline.HighlightedRenderers==1,"Isolation keeps selected renderer and outline");Check(c.overviewRoot.GetComponentsInChildren<BimAssetProxy>(true).All(p=>!p.GetComponent<Renderer>().enabled),"Isolation hides overview layers, not terrain/environment");
        c.ToggleIsolation();Check(!c.IsIsolated&&!other.enabled,"Restore snapshot preserves pre-hidden sibling");other.enabled=true;c.ToggleIsolation();c.Select(sibling.AssetID);Check(!c.IsIsolated,"Changing selection restores isolation");
        var hiddenSelected=c.Visible(sibling.AssetID).GetComponent<Renderer>();hiddenSelected.enabled=false;c.ToggleIsolation();Check(hiddenSelected.enabled,"Isolate temporarily reveals an initially hidden selection");c.ToggleIsolation();Check(!hiddenSelected.enabled&&c.outline.HighlightedRenderers==0,"Restore returns initially hidden selection to hidden without a ghost outline");hiddenSelected.enabled=true;
        c.Back();Check(c.Level==1&&c.SelectedID==block.id,"Back restores selected parent block");c.Back();Check(c.Level==0&&c.SelectedID==segment,"Back restores selected segment");c.Back();Check(c.Level==0,"Back at top level safe");c.Locate(bolt);Check(c.Level==2&&c.SelectedID==bolt,"Search locate resolves context and final ID");
        Check(c.Store.Assets.Count==33434,"Stable 34+334+33066 asset IDs");
    }
}
