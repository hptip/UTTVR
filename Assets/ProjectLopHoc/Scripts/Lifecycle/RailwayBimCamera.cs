using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class RailwayBimCamera:MonoBehaviour
{
    public event System.Action ViewRequested;
    public bool xrPresentation;public bool FreeMode{get;private set;}public float freeSpeed=12;
    [System.Serializable] public struct View{public Vector3 center;public float yaw,pitch,distance,min,max;public Bounds bounds;}
    public View current;public int mode;public Bounds zoneBounds;public Bounds FocusBounds{get;private set;}
    readonly View[] previous=new View[3];readonly bool[] hasPrevious=new bool[3];
    Vector3 smoothCenter;float smoothDistance;bool initialized;
    public static float FitDistance(Bounds bounds,Quaternion rotation,float verticalFov,float aspect,float widthFraction=1,float heightFraction=1)
    {
        var local=Quaternion.Inverse(rotation);Vector3 max=Vector3.zero;
        for(int i=0;i<8;i++){var e=bounds.extents;var v=local*new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z);max=Vector3.Max(max,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));}
        float tan=Mathf.Tan(Mathf.Clamp(verticalFov,20,120)*Mathf.Deg2Rad*.5f);
        return Mathf.Max(max.y/(tan*heightFraction),max.x/(tan*Mathf.Max(aspect,.5f)*widthFraction))*1.07f+max.z;
    }
    public void Focus(Bounds bounds,int newMode,bool preserve=true)
    {
        if(preserve&&initialized&&mode!=newMode){previous[mode]=current;hasPrevious[mode]=true;}
        mode=newMode;FocusBounds=bounds;current.bounds=bounds;current.center=bounds.center;FreeMode=false;
        if(!initialized){current.yaw=130;current.pitch=42;}
        current.min=newMode==0?.6f:newMode==1?.4f:.12f;current.max=newMode==0?1600:newMode==1?180:30;
        var camera=GetComponent<Camera>();camera.nearClipPlane=.01f;
        current.distance=Mathf.Clamp(FitDistance(bounds,Quaternion.Euler(current.pitch,current.yaw,0),camera.fieldOfView,camera.aspect,xrPresentation?1:.40f,xrPresentation?1:.72f),current.min,current.max);
        if(!initialized){smoothCenter=current.center;smoothDistance=current.distance;initialized=true;}ViewRequested?.Invoke();
    }
    public void Restore(int toMode,Bounds fallback){FreeMode=false;mode=toMode;if(hasPrevious[toMode]){current=previous[toMode];FocusBounds=current.bounds;ViewRequested?.Invoke();}else Focus(fallback,toMode,false);}
    public void ResetView(){Focus(zoneBounds,0,false);}
    public void SetFreeMode(bool value)
    {
        if(value&&!FreeMode){current.center=transform.position+Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward*current.distance;smoothCenter=current.center;smoothDistance=current.distance;}
        FreeMode=value;
    }
    public void ReturnToFocus(){FreeMode=false;ViewRequested?.Invoke();}
    public void Orbit(Vector2 delta)
    {
        var previousForward=Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward;
        current.yaw+=delta.x*.17f;current.pitch=Mathf.Clamp(current.pitch-delta.y*.17f,FreeMode?-89:5,FreeMode?89:85);
        if(FreeMode)current.center+=(Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward-previousForward)*current.distance;
    }
    public void Zoom(float scroll)
    {
        if(FreeMode){current.center+=Quaternion.Euler(current.pitch,current.yaw,0)*Vector3.forward*(scroll*freeSpeed*.5f);return;}
        current.distance=Mathf.Clamp(current.distance*Mathf.Exp(-scroll*.12f),current.min,current.max);
    }
    static bool Typing()=>EventSystem.current?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>()?.isFocused==true;
    void Update()
    {
        if(xrPresentation||Typing())return;var mouse=Mouse.current;
        if(mouse!=null&&!(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject()))
        {
            if(mouse.rightButton.isPressed)Orbit(mouse.delta.ReadValue());Zoom(mouse.scroll.ReadValue().y/120);
            if(mouse.middleButton.isPressed){var d=mouse.delta.ReadValue();var shift=(-transform.right*d.x-transform.up*d.y)*current.distance*.001f;current.center+=shift;}
        }
        if(FreeMode&&Keyboard.current!=null)
        {
            var k=Keyboard.current;Vector3 move=transform.right*((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0))+transform.forward*((k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0))+Vector3.up*((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0));
            Vector3 delta=Vector3.ClampMagnitude(move,1)*freeSpeed*(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed?3:1)*Time.unscaledDeltaTime;
            current.center+=delta;
        }
    }
    void LateUpdate()
    {
        if(xrPresentation)return;float t=1-Mathf.Exp(-Time.unscaledDeltaTime*14);
        smoothCenter=FreeMode?current.center:Vector3.Lerp(smoothCenter,current.center,t);smoothDistance=FreeMode?current.distance:Mathf.Lerp(smoothDistance,current.distance,t);
        var r=Quaternion.Euler(current.pitch,current.yaw,0);var desired=smoothCenter-r*Vector3.forward*smoothDistance;
        transform.SetPositionAndRotation(desired,r);
    }
}
