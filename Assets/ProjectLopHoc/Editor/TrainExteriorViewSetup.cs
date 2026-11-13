using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class TrainExteriorViewSetup
{
    const string Request="Temp/TrainExteriorView.request";
    static TrainExteriorViewSetup()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string mode=File.ReadAllText(Request).Trim();
            if(mode=="install"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Request);
                var scene=SceneManager.GetActiveScene();
                if(scene.name!="YenTuHaLongXanh")throw new InvalidOperationException("Open YenTu scene required; refusing other scene modification.");
                var mover=UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>();
                if(mover==null)throw new InvalidOperationException("Train missing");
                var shader=Shader.Find("UTT/Train Exterior Angular Guard");
                if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Guard shader error");
                const string path="Assets/ProjectLopHoc/Generated/QuestRailway/TrainExteriorGuard.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                var view=UnityEngine.Object.FindFirstObjectByType<TrainExteriorView>();
                if(view==null)view=new GameObject("Train Exterior Camera Mode").AddComponent<TrainExteriorView>();
                view.Configure(mover,material);EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.WriteAllText("Temp/TrainExteriorView_Installed.txt","Saved seated-only exterior view in YenTu; one camera/XR Origin reused, no duplicate audio/camera. B/right secondaryButton toggle; right stick limited yaw/pitch, Editor B + RMB. Shader directional guard excludes backward world rays.");
            }
            else if(mode=="test")
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=true;return;}
                if(EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<TrainExteriorViewValidation>()==null)
                    new GameObject("Editor exterior view validation").AddComponent<TrainExteriorViewValidation>();
            }
        }
        catch(Exception e){File.WriteAllText("Temp/TrainExteriorView_ERROR.txt",e.ToString());File.Delete(Request);Debug.LogException(e);}
    }
}

[DefaultExecutionOrder(3000)]
internal sealed class TrainExteriorViewValidation:MonoBehaviour
{
    string result="";bool finished;
    TrainSeatInteractable observedSeat;TrainExteriorView observedView;float cabinSampleError=-1;
    void LateUpdate()
    {
        if(observedSeat==null||observedView==null||observedView.IsExterior||Camera.main==null)return;
        var anchor=observedSeat.transform.Find("Driver_SeatPoint/DriverViewAnchor");
        if(anchor!=null)cabinSampleError=Vector3.Distance(Camera.main.transform.position,anchor.position);
    }
    IEnumerator Start()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<SceneQuickMenu>();
        if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.Invoke(menu,new object[]{false});
        float deadline=Time.realtimeSinceStartup+45;FirstPersonController player=null;
        var view=UnityEngine.Object.FindFirstObjectByType<TrainExteriorView>();var seat=UnityEngine.Object.FindFirstObjectByType<TrainSeatInteractable>();
        observedSeat=seat;observedView=view;
        while(player==null&&Time.realtimeSinceStartup<deadline)
        {
            foreach(var p in UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))if(p.Object!=null&&p.Object.IsValid&&p.HasStateAuthority){player=p;break;}
            yield return null;
        }
        if(player==null||view==null||seat==null){Finish("FAIL: missing local player/seat/view");yield break;}
        bool standingRejected=!view.ToggleView();
        seat.RequestOccupy(player);yield return new WaitForSeconds(.7f);
        if(!player.isDriving){Finish("FAIL: cannot occupy seat");yield break;}
        if(Keyboard.current==null){Finish("FAIL: test keyboard unavailable");yield break;}
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.B));yield return null;yield return null;
        bool bEnabled=view.IsExterior&&view.ViewIndex==0;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
        float cameraError=Vector3.Distance(Camera.main.transform.position,view.ViewAnchor.position);
        var train=seat.trainMover;train.AdjustSpeedLevel(4);float start=train.currentDistanceMeters;
        deadline=Time.realtimeSinceStartup+120;
        bool tunnelCaptured=false,tunnelLoaded=false;
        while(train.currentDistanceMeters<4170&&Time.realtimeSinceStartup<deadline)
        {
            if(!tunnelCaptured&&train.currentDistanceMeters>=2490){tunnelLoaded=GameObject.Find("TunnelMountainGroup_Forest_01")!=null;Capture(Camera.main,"Temp/TrainTunnel_Inside.png");tunnelCaptured=true;}
            train.currentSpeedKmh=200;train.targetSpeedKmh=200;yield return null;
        }
        bool reached=train.currentDistanceMeters>=4170;
        bool backwardHidden=false;
        if(reached)
        {
            var camera=Camera.main;Capture(camera,"Temp/TrainExterior_Overhead.png");
            view.ToggleView();yield return null;yield return null;bool right=view.ViewIndex==1;Capture(camera,"Temp/TrainExterior_Right.png");
            view.ToggleView();yield return null;yield return null;bEnabled&=right&&view.ViewIndex==2;Capture(camera,"Temp/TrainExterior_Left.png");
            var rotation=camera.transform.rotation;
            try{camera.transform.rotation=Quaternion.LookRotation(-train.frontCar.forward,Vector3.up);backwardHidden=Capture(camera,"Temp/TrainExterior_BackwardGuard.png").maxColorComponent<.01f;}
            finally{camera.transform.rotation=rotation;}
        }
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.B));yield return null;yield return null;
        bool bDisabled=!view.IsExterior;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
        var driverAnchor=seat.transform.Find("Driver_SeatPoint/DriverViewAnchor");
        float restoreError=cabinSampleError; // Sample after camera and train LateUpdate, not across two different frames.
        view.ToggleView();train.currentSpeedKmh=train.targetSpeedKmh=0;train.SetVRControlInput(Vector2.zero);player.YeuCauRoiGhe();
        yield return null;yield return null;
        bool exitRestored=!view.IsExterior&&!player.isDriving;
        result=$"Standing rejected={standingRejected}; B/cycle top-right-left={bEnabled}; B cabin={bDisabled}; cameraAnchorError={cameraError:F3}m; cabinRestoreError={restoreError:F3}m; seat exit restored={exitRestored}; backward hidden={backwardHidden}; tunnel loaded while passing={tunnelLoaded}; actual train chainage {start:F2}->{train.currentDistanceMeters:F2}; bridge reached={reached}; rig count={UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length}. Physical B/headset not tested. No saved runtime train changes.";
        Finish((standingRejected&&bEnabled&&bDisabled&&exitRestored&&backwardHidden&&tunnelLoaded&&reached&&cameraError<.5f&&restoreError>=0&&restoreError<.5f?"PASS: ":"FAIL: ")+result);
    }
    static Color Capture(Camera camera,string path)
    {
        var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;var stereo=camera.stereoTargetEye;Texture2D texture=null;
        try{camera.stereoTargetEye=StereoTargetEyeMask.None;RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());return texture.GetPixel(640,360);}
        finally{camera.stereoTargetEye=stereo;RenderTexture.active=previous;rt.Release();Destroy(rt);if(texture!=null)Destroy(texture);}
    }
    void Finish(string text){finished=true;File.WriteAllText("Temp/TrainExteriorView_Test.txt",text);File.Delete("Temp/TrainExteriorView.request");EditorApplication.isPlaying=false;}
    void OnDestroy(){if(!finished&&File.Exists("Temp/TrainExteriorView.request")){File.WriteAllText("Temp/TrainExteriorView_Test.txt","FAIL: validation interrupted "+result);File.Delete("Temp/TrainExteriorView.request");}}
}
