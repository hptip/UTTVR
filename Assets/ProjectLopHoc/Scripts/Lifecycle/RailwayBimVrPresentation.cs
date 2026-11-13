using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using TMPro;

/// <summary>Scene-scoped presentation. Tracked HMD pose is never written by this adapter.</summary>
public sealed class RailwayBimVrPresentation:MonoBehaviour
{
    public RailwayBimController controller;public RailwayBimDashboard dashboard;public Canvas dashboardCanvas;public GameObject localRigPrefab;
    [Range(.2f,2.5f)] public float locomotionSpeed=1.8f;[Range(15,60)] public float snapTurnDegrees=30;[Range(.1f,.6f)] public float deadzone=.22f;
    public bool locomotionEnabled=true;public float panelDistance=1.25f;
    public bool IsVr{get;private set;}public XROrigin Origin{get;private set;}public bool PanelVisible=>visible;
    public bool LeftCollapsed{get;private set;}public bool AssetCollapsed{get;private set;}
    bool visible=true,pendingView,manualEditorMode,turnArmed=true;double nextDeviceCheck;float nextTurn;
    GameObject ownedRig;Camera desktop;XRRayInteractor[] rays=Array.Empty<XRRayInteractor>();RailwayBimVrKeyboard keyboard;SceneQuickMenu sceneMenu;
    InputAction togglePanel,moveInput,turnInput;
    sealed class RaySettings{public XRRayInteractor ray;public XRInteractionManager manager;public float distance;public bool ui;public int inputMode;public XRInputValueReader<Vector2> scroll;public InputAction scrollAction;}
    readonly List<RaySettings> savedRays=new List<RaySettings>();
    Vector3 previousOriginPosition;Quaternion previousOriginRotation;XROrigin preservedOrigin;bool reusedRig,originSnapshotTaken,previousCameraEnabled,previousListenerEnabled;StereoTargetEyeMask previousStereo;
    sealed class RectState
    {
        public RectTransform rect;public Transform parent;public Vector2 min,max,offsetMin,offsetMax,pivot,size;public Vector3 scale,position;public Quaternion rotation;
        public RectState(RectTransform r){rect=r;parent=r.parent;min=r.anchorMin;max=r.anchorMax;offsetMin=r.offsetMin;offsetMax=r.offsetMax;pivot=r.pivot;size=r.sizeDelta;scale=r.localScale;position=r.localPosition;rotation=r.localRotation;}
        public void Restore(){if(rect==null)return;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;rect.localScale=scale;rect.localPosition=position;rect.localRotation=rotation;}
    }
    sealed class Pane{public RectState original;public Canvas canvas;public Vector2 size;public Vector3 offset;public float scale;}
    readonly List<Pane> panes=new List<Pane>();RectState desktopLeft,desktopAsset,statusOriginal;Pane leftPane,assetPane;
    public IReadOnlyList<Canvas> WorldPanels=>panes.Select(p=>p.canvas).ToArray();
    void Awake()
    {
        desktop=controller.cameraController.GetComponent<Camera>();sceneMenu=FindFirstObjectByType<SceneQuickMenu>();
        togglePanel=new InputAction("Bảng tài sản",InputActionType.Button,"<XRController>{LeftHand}/primaryButton");togglePanel.Enable();
        moveInput=new InputAction("Di chuyển riêng khi ẩn UI",InputActionType.Value,"<XRController>{LeftHand}/primary2DAxis");moveInput.Enable();
        turnInput=new InputAction("Quay nấc riêng khi ẩn UI",InputActionType.Value,"<XRController>{RightHand}/primary2DAxis");turnInput.Enable();
        controller.cameraController.ViewRequested+=RequestView;keyboard=gameObject.AddComponent<RailwayBimVrKeyboard>();keyboard.presentation=this;keyboard.dashboard=dashboard;
        if(dashboard.leftNav!=null)desktopLeft=new RectState((RectTransform)dashboard.leftNav.transform);desktopAsset=new RectState((RectTransform)dashboard.assetPanel.transform);
    }
    IEnumerator Start(){while(!controller.Ready&&controller.enabled)yield return null;if(WantsHeadset())SetVrMode(true);}
    bool WantsHeadset()=>Application.platform==RuntimePlatform.Android||XRSettings.isDeviceActive||FindFirstObjectByType<XROrigin>()!=null;
    void RequestView(){if(IsVr)pendingView=true;}
    static bool Typing()=>EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused==true;
    void Update()
    {
        if(Application.isEditor&&Keyboard.current?.f8Key.wasPressedThisFrame==true){manualEditorMode=true;SetVrMode(!IsVr);}
        if(Keyboard.current?.f2Key.wasPressedThisFrame==true&&!Typing())SetPanelVisible(!visible);
        if(!manualEditorMode&&Time.realtimeSinceStartupAsDouble>nextDeviceCheck){nextDeviceCheck=Time.realtimeSinceStartupAsDouble+1;if(!IsVr&&WantsHeadset())SetVrMode(true);}
        if(!IsVr||!controller.Ready)return;
        if(togglePanel.WasPressedThisFrame())SetPanelVisible(!visible);
        if(!visible&&locomotionEnabled&&!(sceneMenu!=null&&sceneMenu.IsMenuOpen))StepLocomotion(moveInput.ReadValue<Vector2>(),turnInput.ReadValue<Vector2>(),Time.unscaledDeltaTime);
    }
    public void StepLocomotion(Vector2 move,Vector2 turn,float deltaTime)
    {
        if(!IsVr||visible||!locomotionEnabled||Origin?.Camera==null)return;
        var head=Origin.Camera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
        if(move.magnitude>deadzone)
        {
            float magnitude=Mathf.Clamp01((move.magnitude-deadzone)/(1-deadzone));var direction=(forward*move.y+right*move.x).normalized;
            var delta=direction*Mathf.Clamp(locomotionSpeed,.2f,2.5f)*magnitude*Mathf.Min(deltaTime,.05f);
            Origin.transform.position+=delta;
        }
        // Inspection flight moves the Origin, never the tracked HMD local pose.
        if(Mathf.Abs(turn.y)>deadzone&&Mathf.Abs(turn.y)>Mathf.Abs(turn.x))
            Origin.transform.position+=Vector3.up*(Mathf.Sign(turn.y)*(Mathf.Abs(turn.y)-deadzone)/(1-deadzone)*Mathf.Clamp(locomotionSpeed,.2f,2.5f)*Mathf.Min(deltaTime,.05f));
        if(Mathf.Abs(turn.x)<.3f)turnArmed=true;
        if(turnArmed&&Mathf.Abs(turn.x)>.7f&&Time.unscaledTime>=nextTurn){Origin.transform.RotateAround(head.position,Vector3.up,Mathf.Sign(turn.x)*snapTurnDegrees);turnArmed=false;nextTurn=Time.unscaledTime+.25f;}
    }
    void LateUpdate()
    {
        if(!IsVr||!controller.Ready)return;if(pendingView){pendingView=false;MoveToCurrentView();}
        foreach(var ray in rays)
        {
            if(ray==null||!ray.isActiveAndEnabled||!ray.selectInput.ReadWasPerformedThisFrame())continue;
            if(ray.TryGetCurrentUIRaycastResult(out var ui)&&(!ray.TryGetCurrent3DRaycastHit(out var solid)||ui.distance<=solid.distance+.02f))continue;
            var origin=ray.rayOriginTransform!=null?ray.rayOriginTransform:ray.transform;controller.UsePointer(new Ray(origin.position,origin.forward));
        }
    }
    static void Fill(RectTransform rect){rect.pivot=new Vector2(.5f,.5f);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;rect.localPosition=Vector3.zero;}
    Pane AddPane(string name,RectTransform content,Vector2 size,Vector3 offset,float scale=.0011f)
    {
        if(content==null)return null;var g=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(TrackedDeviceGraphicRaycaster));g.transform.SetParent(transform,false);
        var canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Origin.Camera;canvas.sortingOrder=20;
        var pane=new Pane{original=new RectState(content),canvas=canvas,size=size,offset=offset,scale=scale};((RectTransform)canvas.transform).sizeDelta=size;canvas.transform.localScale=Vector3.one*scale;content.SetParent(canvas.transform,false);Fill(content);panes.Add(pane);return pane;
    }
    public void SetVrMode(bool enabled)
    {
        if(Application.isEditor)manualEditorMode=true;if(IsVr==enabled)return;
        if(enabled)
        {
            Origin=FindFirstObjectByType<XROrigin>();if(Origin==null&&localRigPrefab!=null){ownedRig=Instantiate(localRigPrefab);ownedRig.name="XR Origin (VR)";Origin=ownedRig.GetComponent<XROrigin>();}
            if(Origin==null||Origin.Camera==null){controller.Message("Không tìm thấy XR Origin có camera");return;}
            reusedRig=ownedRig==null;if(reusedRig&&!originSnapshotTaken){originSnapshotTaken=true;preservedOrigin=Origin;previousOriginPosition=Origin.transform.position;previousOriginRotation=Origin.transform.rotation;previousCameraEnabled=Origin.Camera.enabled;previousStereo=Origin.Camera.stereoTargetEye;previousListenerEnabled=Origin.Camera.GetComponent<AudioListener>()?.enabled??false;}
            IsVr=true;controller.cameraController.xrPresentation=true;desktop.enabled=false;var listener=desktop.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;
            Origin.Camera.enabled=true;Origin.Camera.stereoTargetEye=StereoTargetEyeMask.Both;var xrListener=Origin.Camera.GetComponent<AudioListener>();if(xrListener!=null)xrListener.enabled=true;
            var manager=Origin.GetComponentInChildren<XRInteractionManager>();if(manager==null)manager=Origin.gameObject.AddComponent<XRInteractionManager>();
            rays=Origin.GetComponentsInChildren<XRRayInteractor>(true);savedRays.Clear();foreach(var ray in rays)
            {
#pragma warning disable CS0618
                string hand=ray.name.IndexOf("Left",StringComparison.OrdinalIgnoreCase)>=0?"LeftHand":"RightHand";var scroll=new InputAction("Cuộn bảng "+hand,InputActionType.Value,"<XRController>{"+hand+"}/primary2DAxis");scroll.Enable();
                savedRays.Add(new RaySettings{ray=ray,manager=ray.interactionManager,distance=ray.maxRaycastDistance,ui=ray.enableUIInteraction,inputMode=(int)ray.inputCompatibilityMode,scroll=ray.uiScrollInput,scrollAction=scroll});ray.forceDeprecatedInput=false;
#pragma warning restore CS0618
                ray.uiScrollInput=new XRInputValueReader<Vector2>("Cuộn bảng",XRInputValueReader.InputSourceMode.InputAction){inputAction=scroll};ray.interactionManager=manager;ray.enableUIInteraction=true;ray.maxRaycastDistance=1500;
            }
            foreach(var target in controller.overviewRoot.GetComponentsInChildren<XRSimpleInteractable>(true))target.interactionManager=manager;foreach(var target in controller.GetComponentsInChildren<XRSimpleInteractable>(true))target.interactionManager=manager;
            leftPane=AddPane("BIM VR · Danh sách",dashboard.leftNav!=null?(RectTransform)dashboard.leftNav.transform:null,new Vector2(520,920),new Vector3(-.88f,-.12f,panelDistance));
            assetPane=AddPane("BIM VR · Hồ sơ",(RectTransform)dashboard.assetPanel.transform,new Vector2(760,1080),new Vector3(1.02f,-.12f,panelDistance));
            var commandPane=AddPane("BIM VR · Thao tác",dashboard.commandPanel,new Vector2(1040,320),new Vector3(0,.52f,panelDistance+.06f));
            if(commandPane!=null)
            {
                dashboard.commandPanel.anchorMax=new Vector2(1,.68f);
                var statusPanel=(RectTransform)dashboard.status.transform.parent;
                statusOriginal=new RectState(statusPanel);statusPanel.SetParent(commandPane.canvas.transform,false);Fill(statusPanel);statusPanel.anchorMin=new Vector2(0,.72f);
            }
            dashboardCanvas.gameObject.SetActive(false);pendingView=true;SetPanelVisible(visible);ApplySidebar(false);ApplySidebar(true);
            controller.Message("VR · Cò: chọn · X: giao diện · Y: đổi không gian · Ẩn UI để di chuyển và quay nấc. A/B/nắm phải để trống.");
        }
        else
        {
            keyboard.Close();RestoreRaySettings();statusOriginal?.Restore();statusOriginal=null;foreach(var pane in panes){pane.original.Restore();if(pane.canvas!=null)Destroy(pane.canvas.gameObject);}panes.Clear();leftPane=assetPane=null;
            IsVr=false;controller.cameraController.xrPresentation=false;desktop.enabled=true;var listener=desktop.GetComponent<AudioListener>();if(listener!=null)listener.enabled=true;
            if(reusedRig&&Origin?.Camera!=null){Origin.Camera.enabled=false;var xrListener=Origin.Camera.GetComponent<AudioListener>();if(xrListener!=null)xrListener.enabled=false;}
            if(ownedRig!=null)Destroy(ownedRig);ownedRig=null;Origin=null;dashboardCanvas.gameObject.SetActive(visible);ApplySidebar(false);ApplySidebar(true);controller.cameraController.SetFreeMode(!visible);
        }
    }
    void RestoreRaySettings()
    {
        foreach(var saved in savedRays)
        {
            if(saved.ray!=null){
#pragma warning disable CS0618
                saved.ray.inputCompatibilityMode=(XRBaseInputInteractor.InputCompatibilityMode)saved.inputMode;
#pragma warning restore CS0618
                saved.ray.maxRaycastDistance=saved.distance;saved.ray.enableUIInteraction=saved.ui;if(saved.manager!=null)saved.ray.interactionManager=saved.manager;saved.ray.uiScrollInput=saved.scroll;}
            saved.scrollAction?.Dispose();
        }
        savedRays.Clear();
    }
    public void SetPanelVisible(bool value)
    {
        visible=value;if(!value){keyboard.Close();pendingView=false;EventSystem.current?.SetSelectedGameObject(null);}
        dashboardCanvas.gameObject.SetActive(value&&!IsVr);foreach(var pane in panes)if(pane.canvas!=null)pane.canvas.gameObject.SetActive(value);
        if(value&&IsVr)PlacePanel();if(!IsVr)controller.cameraController.SetFreeMode(!value);
    }
    public void ToggleSidebar(bool asset){if(asset)AssetCollapsed=!AssetCollapsed;else LeftCollapsed=!LeftCollapsed;ApplySidebar(asset);}
    void ApplySidebar(bool asset)
    {
        bool collapsed=asset?AssetCollapsed:LeftCollapsed;var body=asset?dashboard.assetBody:dashboard.leftBody;var button=asset?dashboard.collapseAssetButton:dashboard.collapseNavButton;
        if(body!=null)body.SetActive(!collapsed);if(button!=null){button.GetComponentInChildren<TMP_Text>().text=collapsed?(asset?"Mở hồ sơ":"Mở danh sách"):"Thu gọn";var r=(RectTransform)button.transform;r.anchorMin=collapsed?new Vector2(.035f,.06f):new Vector2(.80f,.936f);r.anchorMax=collapsed?new Vector2(.965f,.94f):new Vector2(.98f,.995f);r.offsetMin=r.offsetMax=Vector2.zero;}
        var pane=asset?assetPane:leftPane;if(IsVr&&pane!=null){((RectTransform)pane.canvas.transform).sizeDelta=new Vector2(pane.size.x,collapsed?64:pane.size.y);PlacePanel();}
        else if(!IsVr){var original=asset?desktopAsset:desktopLeft;if(original!=null){original.Restore();if(collapsed)original.rect.anchorMin=new Vector2(original.min.x,original.max.y-.07f);}}
    }
    public void PlacePanel()
    {
        if(Origin?.Camera==null)return;keyboard.Close();var head=Origin.Camera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)forward=Origin.transform.forward;var right=Vector3.Cross(Vector3.up,forward);
        foreach(var pane in panes)
        {
            Vector3 point=head.position+right*pane.offset.x+Vector3.up*pane.offset.y+forward*pane.offset.z;var rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(point-head.position,Vector3.up).normalized,Vector3.up);
            bool collapsed=pane==leftPane?LeftCollapsed:pane==assetPane&&AssetCollapsed;if(collapsed)point+=Vector3.up*(pane.size.y-64)*pane.scale*.5f;
            pane.canvas.transform.SetPositionAndRotation(point,rotation);
        }
    }
    void MoveToCurrentView()
    {
        if(Origin?.Camera==null)return;var head=Origin.Camera.transform;var bounds=controller.cameraController.FocusBounds;var direction=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;if(direction.sqrMagnitude<.1f)direction=Origin.transform.forward;
        var inspectionRotation=Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Euler(24,0,0);
        float distance=Mathf.Clamp(RailwayBimCamera.FitDistance(bounds,inspectionRotation,Origin.Camera.fieldOfView,Origin.Camera.aspect,.86f,.86f),.4f,850);
        Vector3 position=bounds.center-direction*distance+Vector3.up*Mathf.Max(bounds.extents.y+.22f,distance*.44f);
        // A non-convex collider may not report a sphere wholly inside its volume. Stay above the selected bounds
        // and raise an inspection point above broad ground surfaces without mistaking thin overhead wires for ground.
        foreach(var hit in Physics.RaycastAll(position+Vector3.up*250,Vector3.down,500,~0,QueryTriggerInteraction.Ignore))
        {var renderer=hit.collider.GetComponent<Renderer>();if(hit.collider is TerrainCollider||hit.collider.GetComponent<BimAssetProxy>()==null&&renderer!=null&&renderer.bounds.size.x>10&&renderer.bounds.size.z>10)position.y=Mathf.Max(position.y,hit.point.y+.25f);}
        for(int i=0;i<5&&Physics.CheckSphere(position,.08f,~0,QueryTriggerInteraction.Ignore);i++)position+=-direction*.18f+Vector3.up*.12f;
        Origin.transform.position+=position-head.position; // no local HMD position/rotation or forced headset yaw
        if(visible)PlacePanel();
    }
    void OnDestroy()
    {
        RestoreRaySettings();
        if(originSnapshotTaken&&preservedOrigin!=null){preservedOrigin.transform.SetPositionAndRotation(previousOriginPosition,previousOriginRotation);if(preservedOrigin.Camera!=null){preservedOrigin.Camera.enabled=previousCameraEnabled;preservedOrigin.Camera.stereoTargetEye=previousStereo;var listener=preservedOrigin.Camera.GetComponent<AudioListener>();if(listener!=null)listener.enabled=previousListenerEnabled;}}
        if(controller?.cameraController!=null){controller.cameraController.ViewRequested-=RequestView;controller.cameraController.xrPresentation=false;}
        togglePanel?.Dispose();moveInput?.Dispose();turnInput?.Dispose();if(ownedRig!=null)Destroy(ownedRig);
    }
}
