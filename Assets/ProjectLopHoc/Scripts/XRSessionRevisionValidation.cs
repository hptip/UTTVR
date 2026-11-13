#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

public sealed class XRSessionRevisionValidation:MonoBehaviour
{
    XRSimulatedController testController;bool finished;
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+60;
        while(SceneManager.GetActiveScene().name!="Classroom"&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(2);
        if(SceneManager.GetActiveScene().name!="Classroom"){Finish("FAIL: Login did not redirect to Classroom");yield break;}
        var origin=Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        var rays=origin.GetComponentsInChildren<XRRayInteractor>(true);
        bool initialRegistered=rays.Length==2&&rays.All(r=>r.isActiveAndEnabled&&r.interactionManager!=null&&r.interactionManager.IsRegistered((IXRInteractor)r));
        var actions=origin.GetComponentsInChildren<InputActionManager>(true);
        bool actionsReady=actions.Any(a=>a.isActiveAndEnabled&&a.actionAssets.Any(asset=>asset!=null&&asset.enabled));
        yield return new WaitForEndOfFrame();
        int visibleLines=origin.GetComponentsInChildren<LineRenderer>(true).Count(v=>v.enabled&&v.gameObject.activeInHierarchy&&v.positionCount>=2);
        var comfort=origin.GetComponentInChildren<TunnelingVignetteController>(true);
        bool canonicalRig=origin.GetComponent<XROriginSessionPersistence>()!=null&&comfort!=null;
        var snapshot=new System.Text.StringBuilder();
        foreach(var item in origin.GetComponentsInChildren<Behaviour>(true))
        {
            string name=item.GetType().Name;if(!(name.Contains("Interactor")||name.Contains("Visual")||name.Contains("Controller")||name.Contains("PoseDriver")||name.Contains("Modality")))continue;
            snapshot.AppendLine(item.GetType().Name+" | "+item.name+" | enabled="+item.enabled+",active="+item.gameObject.activeInHierarchy);
            if(item is XRBaseInteractor interactor)snapshot.AppendLine("manager="+interactor.interactionManager?.name+" registered="+(interactor.interactionManager!=null&&interactor.interactionManager.IsRegistered((IXRInteractor)interactor)));
        }
        foreach(var line in origin.GetComponentsInChildren<LineRenderer>(true))snapshot.AppendLine("LINE "+line.name+" enabled="+line.enabled+" active="+line.gameObject.activeInHierarchy+" count="+line.positionCount);
        foreach(var device in InputSystem.devices)if(device is UnityEngine.InputSystem.XR.XRController)snapshot.AppendLine("DEVICE "+device.name+" usage="+string.Join(",",device.usages)+" tracked="+device.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("isTracked")?.isPressed);
        File.WriteAllText("Temp/XRStartup_RigSnapshot.txt",snapshot.ToString());
        var menu=Object.FindFirstObjectByType<SceneQuickMenu>();var field=typeof(SceneQuickMenu).GetField("_menuOpen",BindingFlags.Instance|BindingFlags.NonPublic);
        bool before=(bool)field.GetValue(menu);
        // Reuse the tracked simulator hand. Adding/removing a second untracked
        // LeftHand device changes XRInputModalityManager and invalidates the test.
        bool xIgnored=false,yToggled=false;
        var simulator=Object.FindFirstObjectByType<XRDeviceSimulator>();
        var left=InputSystem.devices.OfType<XRSimulatedController>().FirstOrDefault(d=>d.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand));
        var stateField=typeof(XRDeviceSimulator).GetField("m_LeftControllerState",BindingFlags.Instance|BindingFlags.NonPublic);
        var updateMenu=typeof(SceneQuickMenu).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        if(simulator!=null&&left!=null&&stateField!=null)
        {
            var original=(XRSimulatedControllerState)stateField.GetValue(simulator);
            try
            {
                var primary=original;InputState.Change(left,primary.WithButton(ControllerButton.PrimaryButton));updateMenu.Invoke(menu,null);xIgnored=(bool)field.GetValue(menu)==before;
                InputState.Change(left,original);updateMenu.Invoke(menu,null);
                var secondary=original;InputState.Change(left,secondary.WithButton(ControllerButton.SecondaryButton));updateMenu.Invoke(menu,null);yToggled=(bool)field.GetValue(menu)!=before;
            }
            finally{InputState.Change(left,original);updateMenu.Invoke(menu,null);}
        }
        yield return null;
        bool comfortInput=false,snapInput=false,aimInput=false,pressInput=false;
        var player=Resources.FindObjectsOfTypeAll<FirstPersonController>().FirstOrDefault(p=>p.gameObject.scene.IsValid()&&p.Object!=null&&p.Object.IsValid&&p.HasStateAuthority);
        float playerDeadline=Time.realtimeSinceStartup+20;
        while(player==null&&Time.realtimeSinceStartup<playerDeadline)
        {yield return null;player=Resources.FindObjectsOfTypeAll<FirstPersonController>().FirstOrDefault(p=>p.gameObject.scene.IsValid()&&p.Object!=null&&p.Object.IsValid&&p.HasStateAuthority);}
        if(player==null)
        {
            var missing=new System.Text.StringBuilder("No local player ready in first Classroom after20s\n");
            foreach(var p in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include,FindObjectsSortMode.None))missing.AppendLine($"{p.name}:active={p.gameObject.activeInHierarchy},valid={p.Object!=null&&p.Object.IsValid},stateAuth={p.HasStateAuthority}");
            foreach(var spawner in Object.FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None))missing.AppendLine($"Spawner={spawner.name},spawned={typeof(PlayerSpawner).GetField("_hasSpawned",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(spawner)},spawning={typeof(PlayerSpawner).GetField("_isSpawning",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(spawner)}");
            foreach(var runner in Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsSortMode.None))missing.AppendLine($"Runner={runner.name},running={runner.IsRunning},mode={runner.GameMode}");
            foreach(var obj in Resources.FindObjectsOfTypeAll<Fusion.NetworkObject>())if(obj.gameObject.scene.IsValid())missing.AppendLine($"NETOBJECT {obj.name},valid={obj.IsValid},scene={obj.gameObject.scene.name},flags={obj.hideFlags}/{obj.gameObject.hideFlags},scripts={string.Join(",",obj.GetComponents<MonoBehaviour>().Select(c=>c!=null?c.GetType().Name:"MISSING"))}");
            File.WriteAllText("Temp/XRStartup_PlayerReadiness.txt",missing.ToString());
        }
        var right=InputSystem.devices.OfType<XRSimulatedController>().FirstOrDefault(d=>d.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand));
        var rightStateField=typeof(XRDeviceSimulator).GetField("m_RightControllerState",BindingFlags.Instance|BindingFlags.NonPublic);
        float moveSpeed=-1,snapAngle=-1;
        if(player!=null&&simulator!=null&&left!=null&&right!=null)
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var update=typeof(FirstPersonController).GetMethod("Update",flags);
            var heading=typeof(FirstPersonController).GetField("_horizontalRotation",flags);var armed=typeof(FirstPersonController).GetField("_snapTurnArmed",flags);var until=typeof(FirstPersonController).GetField("_snapTurnVignetteUntil",flags);
            moveSpeed=(float)typeof(FirstPersonController).GetField("xrMoveSpeed",flags).GetValue(player);snapAngle=(float)typeof(FirstPersonController).GetField("snapTurnAngle",flags).GetValue(player);
            var oldLeft=(XRSimulatedControllerState)stateField.GetValue(simulator);var oldRight=(XRSimulatedControllerState)rightStateField.GetValue(simulator);var oldHeading=heading.GetValue(player);var oldArmed=armed.GetValue(player);var oldUntil=until.GetValue(player);
            var legacy=origin.GetComponentsInChildren<XRController>(true).FirstOrDefault(c=>c.controllerNode==UnityEngine.XR.XRNode.RightHand);
            var ray=rays.FirstOrDefault(r=>r.GetComponent<XRController>()?.controllerNode==UnityEngine.XR.XRNode.RightHand);
            var pose=ray!=null&&ray.rayOriginTransform!=null?ray.rayOriginTransform.GetComponent<XRNodePoseDriver>():null;
            var reader=legacy!=null?legacy.GetComponent<QuestRayControllerInput>():null;pressInput=reader!=null&&reader.isActiveAndEnabled;
            try
            {
                var moving=oldLeft;moving.primary2DAxis=Vector2.up;InputState.Change(left,moving);update.Invoke(player,null);comfortInput=(bool)typeof(FirstPersonController).GetField("_comfortVignetteActive",flags).GetValue(player);
                InputState.Change(left,oldLeft);armed.SetValue(player,true);var turning=oldRight;turning.primary2DAxis=Vector2.right;InputState.Change(right,turning);update.Invoke(player,null);snapInput=Mathf.Abs(Mathf.DeltaAngle((float)oldHeading,(float)heading.GetValue(player))-snapAngle)<.1f;
                if(pose!=null)
                {
                    var beforePose=pose.transform.localRotation;var aim=oldRight;aim.deviceRotation=Quaternion.Euler(0,25,0)*oldRight.deviceRotation;InputState.Change(right,aim);typeof(XRNodePoseDriver).GetMethod("ApplyPose",flags).Invoke(pose,null);aimInput=Quaternion.Angle(beforePose,pose.transform.localRotation)>5;
                }
            }
            finally
            {
                InputState.Change(left,oldLeft);InputState.Change(right,oldRight);heading.SetValue(player,oldHeading);armed.SetValue(player,oldArmed);until.SetValue(player,oldUntil);update.Invoke(player,null);
                if(pose!=null)typeof(XRNodePoseDriver).GetMethod("ApplyPose",flags).Invoke(pose,null);
            }
        }
        var dashboard=Resources.FindObjectsOfTypeAll<Canvas>().FirstOrDefault(c=>c.name=="DashboardCanvas"&&c.gameObject.scene.IsValid());
        bool dashboardOwned=dashboard!=null&&dashboard.gameObject.scene.name=="Classroom"&&!dashboard.transform.IsChildOf(origin.transform);
        typeof(SceneQuickMenu).GetMethod("LoadDestination",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{2});
        deadline=Time.realtimeSinceStartup+60;while(SceneManager.GetActiveScene().name!="Showroom"&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return new WaitForSeconds(2);
        bool unloaded=dashboard==null;
        var newOrigin=Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        var newRays=newOrigin.GetComponentsInChildren<XRRayInteractor>(true);
        bool managerRetained=newRays.Length==2&&newRays.All(r=>r.isActiveAndEnabled&&r.interactionManager!=null&&r.interactionManager.IsRegistered((IXRInteractor)r));
        bool module=UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.currentInputModule is UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule;
        bool retained=newOrigin==origin;
        Finish((initialRegistered&&canonicalRig&&visibleLines==2&&comfortInput&&snapInput&&aimInput&&pressInput&&xIgnored&&yToggled&&dashboardOwned&&unloaded&&managerRetained&&module&&retained?"PASS: ":"FAIL: ")+$"Original Login rig and vignette preserved={canonicalRig}; registered XR rays={initialRegistered},handCount={rays.Length},visible lines={visibleLines}; comfort activation via movement={comfortInput}; snap turn={snapInput},angle={snapAngle},moveSpeed={moveSpeed}; aim follows simulated rotation={aimInput}; legacy UI trigger fallback={pressInput}; Y toggles={yToggled},X ignored={xIgnored}; Dashboard owned/destroyed={dashboardOwned}/{unloaded}; Showroom registered rays={managerRetained}; same original rig retained={retained}; XRUI module={module}; rigs={Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length}. Physical headset/buttons not tested; no scenery test or APK build.");
    }
    void Finish(string report){finished=true;File.WriteAllText("Temp/XRSessionRevision_Test.txt",report);File.Delete("Temp/XRSessionRevision.request");EditorApplication.isPlaying=false;}
    void OnDestroy(){if(testController!=null&&testController.added)InputSystem.RemoveDevice(testController);if(!finished&&File.Exists("Temp/XRSessionRevision.request")){File.WriteAllText("Temp/XRSessionRevision_Test.txt","FAIL: interrupted");File.Delete("Temp/XRSessionRevision.request");}}
}

#endif
