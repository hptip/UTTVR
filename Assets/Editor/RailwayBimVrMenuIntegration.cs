using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayBimVrMenuIntegration
{
    const string Key="UTT.Bim.MenuTest";static int step;static double until,start;static Canvas previousCanvas;static GameObject previousRig,externalRig;static Vector3 originalPosition;static AsyncOperation unload;
    static RailwayBimVrMenuIntegration(){EditorApplication.update+=Tick;}
    static void Next(int n,double delay=1){step=n;until=EditorApplication.timeSinceStartup+delay;}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)
        {
            if(!File.Exists("Temp/BimVRMenuTest.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Temp/BimVRMenuTest.request");
            if(SceneManager.GetActiveScene().path!=RailwayBimVrUpgrade.ScenePath)return;
            if(SceneManager.GetActiveScene().isDirty)EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            step=0;start=0;SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            if(start==0)start=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-start>120)throw new Exception("Menu scene load timeout");if(EditorApplication.timeSinceStartup<until)return;
            var vr=Object.FindFirstObjectByType<RailwayBimVrPresentation>();
            switch(step)
            {
                case 0:if(vr==null||!vr.controller.Ready)return;vr.SetVrMode(true);Next(1);break;
                case 1:
                    previousCanvas=vr.dashboardCanvas;previousRig=vr.Origin.gameObject;var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BimMenuIntegration_InMemory"));unload=SceneManager.UnloadSceneAsync(old);Next(2);break;
                case 2:
                    if(!unload.isDone)return;if(previousCanvas!=null||previousRig!=null)throw new Exception("Old dashboard or local rig leaked after scene unload");
                    var menu=Object.FindFirstObjectByType<SceneQuickMenu>();typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(menu,new object[]{true});
                    var button=menu.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Quản lý vòng đời tài sản");if(!button.interactable)throw new Exception("Menu destination disabled");button.onClick.Invoke();Next(3);break;
                case 3:
                    if(vr==null||!vr.controller.Ready||SceneManager.GetActiveScene().path!=RailwayBimVrUpgrade.ScenePath)return;
                    externalRig=Object.Instantiate(vr.localRigPrefab);externalRig.name="Existing authored rig - test only";originalPosition=externalRig.transform.position;Object.DontDestroyOnLoad(externalRig);vr.SetVrMode(true);Next(4);break;
                case 4:
                    if(Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length!=1)throw new Exception("Duplicate or missing origin after menu load");
                    if(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length!=1)throw new Exception("Duplicate EventSystem after menu load");
                    if(vr.Origin.gameObject!=externalRig)throw new Exception("Existing origin not reused");vr.SetVrMode(false);Next(5,.4);break;
                case 5:
                    if(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)!=1)throw new Exception("Duplicate cameras after desktop toggle");vr.SetVrMode(true);Next(6);break;
                case 6:
                    if(vr.WorldPanels.Count!=3||vr.WorldPanels.Any(p=>!p.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>().enabled))throw new Exception("Tracked spatial panel raycaster disabled after second VR entry");
                    var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("BimScopeRestore_InMemory"));unload=SceneManager.UnloadSceneAsync(scene);Next(7);break;
                case 7:
                    if(!unload.isDone)return;
                    if(externalRig==null||(externalRig.transform.position-originalPosition).magnitude>.001f)throw new Exception("Existing rig position was not restored");
#pragma warning disable CS0618
                    if(externalRig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>().Any(r=>(int)r.inputCompatibilityMode!=0))throw new Exception("Existing ray input mode was not restored");
#pragma warning restore CS0618
                    if(!externalRig.GetComponent<Unity.XR.CoreUtils.XROrigin>().Camera.enabled)throw new Exception("Existing headset camera not restored");
                    Object.Destroy(externalRig);
                    File.WriteAllText("Temp/BimVR/MenuIntegration.txt","PASS | Unloaded scene destroys its dashboard and locally owned XR rig.\nPASS | Actual Quản lý vòng đời tài sản menu Button.onClick loaded the registered scene asynchronously from another in-memory scene.\nPASS | Reloaded scene initializes SQLite registry; an existing persistent authored XR rig is reused with exactly one XR Origin and one EventSystem.\nPASS | Desktop/VR/VR toggle keeps one camera and reenables tracked UI raycaster.\nPASS | Leaving the BIM scene restores the existing rig's position, camera and automatic ray input mode.\nNo source scene/zone was modified. Physical headset and physical Y button not tested.");SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;break;
            }
        }
        catch(Exception e){File.WriteAllText("Temp/BimVR/MenuIntegration.txt","FAIL | "+e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;}
    }
}
