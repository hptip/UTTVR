using System;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Small Phase 1 selection harness, with no database writes or streaming dependencies.</summary>
[DisallowMultipleComponent]
public sealed class RailwayLifecycleInspector : MonoBehaviour
{
    public Camera inspectionCamera;
    public DetailOrbitZoom orbit;
    public Transform detailedModule;
    public RailwayLifecycleElement Selected {get;private set;}
    public event Action<RailwayLifecycleElement> SelectionChanged;
    MaterialPropertyBlock block;
    Renderer highlighted;
    MaterialPropertyBlock previous;
    public int SelectionCount {get;private set;}
    void Start(){FocusModule();}
    public void FocusModule()
    {
        if(detailedModule==null||orbit==null)return;
        var rs=detailedModule.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;
        var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
        orbit.EnableForTarget(detailedModule,bounds);
    }
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if(Keyboard.current!=null&&Keyboard.current.fKey.wasPressedThisFrame)FocusModule();
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&inspectionCamera!=null)
        {
            Vector2 p=Mouse.current.position.ReadValue();
            // Reserve the compact information overlay for its own controls.
            if(p.x<480&&p.y>Screen.height-126)return;
            bool cycle=Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);
            SelectRay(inspectionCamera.ScreenPointToRay(p),cycle);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if(Input.GetKeyDown(KeyCode.F))FocusModule();
        if(Input.GetMouseButtonDown(0)&&inspectionCamera!=null)SelectRay(inspectionCamera.ScreenPointToRay(Input.mousePosition));
#endif
    }
    public bool SelectRay(Ray ray,bool cycleOccluded=false)
    {
        if(cycleOccluded)
        {
            var elements=Physics.RaycastAll(ray,2000,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)
                .Select(h=>h.collider.GetComponent<RailwayLifecycleElement>()).Where(e=>e!=null).Distinct().ToArray();
            if(elements.Length==0)return false;
            int index=Array.IndexOf(elements,Selected);Select(elements[(index+1)%elements.Length]);return true;
        }
        if(!Physics.Raycast(ray,out var hit,2000,~0,QueryTriggerInteraction.Ignore))return false;
        var element=hit.collider.GetComponent<RailwayLifecycleElement>();
        if(element==null)return false;
        Select(element);return true;
    }
    public void Select(RailwayLifecycleElement element)
    {
        if(highlighted!=null)highlighted.SetPropertyBlock(previous);
        Selected=element;highlighted=element!=null?element.GetComponent<Renderer>():null;
        if(highlighted!=null)
        {
            previous=new MaterialPropertyBlock();highlighted.GetPropertyBlock(previous);
            block=new MaterialPropertyBlock();highlighted.GetPropertyBlock(block);
            block.SetColor("_BaseColor",new Color(1f,.66f,.08f));block.SetColor("_Color",new Color(1f,.66f,.08f));highlighted.SetPropertyBlock(block);
        }
        SelectionCount++;SelectionChanged?.Invoke(element);
    }
    void OnDisable(){if(highlighted!=null)highlighted.SetPropertyBlock(previous);}
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16,16,455,104),GUI.skin.box);
        GUILayout.Label("RAILWAY ASSET INSPECTION - PHASE 1");
        GUILayout.Label("RMB: orbit | Wheel: zoom | F: focus | Click: select | Alt+click: layers");
        GUILayout.Label(Selected!=null?Selected.componentName+"  |  "+Selected.ID:"Select a component on the detailed Track_Right module.");
        GUILayout.EndArea();
    }
}
