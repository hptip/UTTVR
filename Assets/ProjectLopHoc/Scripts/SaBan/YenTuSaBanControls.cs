using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Splines;
using UnityEngine.UI;

public class YenTuSaBanControls : MonoBehaviour
{
    public TrainSplineMover train;
    public Camera viewCamera;
    public Transform modelRoot;
    public Text infoTitle, infoDetails, trainStatus, playPauseLabel, followLabel;
    public Vector3 focusPoint;
    public float orbitDistance = 8f;
    public float orbitHeight = 5f;
    public float zoomStep = 1.2f;
    public bool isFollowing;
    bool playing;
    float speedMultiplier = 1f;
    float baseSpeedKmh = 0.2f;
    Coroutine orbitRoutine;
    Vector3 resetPosition;
    Quaternion resetRotation;

    void Start()
    {
        if (!viewCamera) viewCamera = Camera.main;
        if (train) { train.targetSpeedKmh = 0; train.currentSpeedKmh = 0; train.totalRouteLength=train.splineRoute?train.splineRoute.CalculateLength():train.totalRouteLength; baseSpeedKmh=Mathf.Clamp(train.totalRouteLength/240f*3.6f,0.05f,2f); playing = false; }
        if (viewCamera) { resetPosition = viewCamera.transform.position; resetRotation = viewCamera.transform.rotation; }
        BindButtons(); RefreshStatus();
    }
    void LateUpdate()
    {
        if (isFollowing && train && viewCamera)
        {
            Vector3 target = train.transform.position;
            Vector3 desired = target + new Vector3(-orbitDistance * .7f, orbitHeight * .55f, -orbitDistance * .7f);
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desired, Time.deltaTime * 3f);
            viewCamera.transform.rotation = Quaternion.Slerp(viewCamera.transform.rotation, Quaternion.LookRotation(target - viewCamera.transform.position), Time.deltaTime * 3f);
        }
        if (Input.GetMouseButtonDown(0) && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) SelectByClick();
        RefreshStatus();
    }
    void BindButtons()
    {
        foreach (Button b in FindObjectsOfType<Button>(true))
        {
            switch (b.name)
            {
                case "BtnCamera360": b.onClick.AddListener(Orbit360); break;
                case "BtnZoomIn": b.onClick.AddListener(ZoomIn); break;
                case "BtnZoomOut": b.onClick.AddListener(ZoomOut); break;
                case "BtnFrameAll": b.onClick.AddListener(FrameAll); break;
                case "BtnTopView": b.onClick.AddListener(TopView); break;
                case "BtnResetCamera": b.onClick.AddListener(ResetCamera); break;
                case "BtnPlayPause": b.onClick.AddListener(TogglePlay); break;
                case "BtnResetJourney": b.onClick.AddListener(ResetJourney); break;
                case "BtnSpeed1x": b.onClick.AddListener(() => SetSpeed(1)); break;
                case "BtnSpeed2x": b.onClick.AddListener(() => SetSpeed(2)); break;
                case "BtnSpeed4x": b.onClick.AddListener(() => SetSpeed(4)); break;
                case "BtnFollowTrain": b.onClick.AddListener(ToggleFollow); break;
                case "BtnInfoClose": b.onClick.AddListener(CloseInfo); break;
            }
        }
    }
    public void Orbit360() { if (orbitRoutine != null) StopCoroutine(orbitRoutine); isFollowing = false; orbitRoutine = StartCoroutine(Orbit()); }
    IEnumerator Orbit()
    {
        Vector3 start = viewCamera.transform.position - focusPoint;
        if (start.sqrMagnitude < .01f) start = new Vector3(0, orbitHeight, -orbitDistance);
        for (int i=0;i<=180;i++) { float a=i/180f*360f; Vector3 p=Quaternion.Euler(0,a,0)*start; viewCamera.transform.position=focusPoint+p; viewCamera.transform.rotation=Quaternion.LookRotation(focusPoint-viewCamera.transform.position); yield return null; }
        orbitRoutine=null;
    }
    public void ZoomIn() { isFollowing=false; MoveCamera(1f/zoomStep); }
    public void ZoomOut() { isFollowing=false; MoveCamera(zoomStep); }
    void MoveCamera(float k) { if(!viewCamera)return; Vector3 d=viewCamera.transform.position-focusPoint; if(viewCamera.orthographic)viewCamera.orthographicSize=Mathf.Clamp(viewCamera.orthographicSize*k,.5f,100f); else viewCamera.transform.position=focusPoint+d*k; }
    public void FrameAll() { isFollowing=false; if(!viewCamera)return; viewCamera.transform.position=focusPoint+new Vector3(-orbitDistance*.7f,orbitHeight,-orbitDistance*.7f); viewCamera.transform.rotation=Quaternion.LookRotation(focusPoint-viewCamera.transform.position); }
    public void TopView() { isFollowing=false; if(!viewCamera)return; viewCamera.transform.position=focusPoint+Vector3.up*Mathf.Max(orbitDistance,orbitHeight)*1.4f; viewCamera.transform.rotation=Quaternion.Euler(90,0,0); }
    public void ResetCamera() { isFollowing=false; if(viewCamera){viewCamera.transform.position=resetPosition;viewCamera.transform.rotation=resetRotation;} }
    public void TogglePlay() { if(!train)return; playing=!playing; train.targetSpeedKmh=playing?baseSpeedKmh*speedMultiplier:0; if(playPauseLabel)playPauseLabel.text=playing?"TẠM DỪNG":"CHẠY TÀU"; }
    public void ResetJourney() { if(!train)return; playing=false; train.targetSpeedKmh=0;train.currentSpeedKmh=0;train.currentDistanceMeters=Mathf.Max(train.startDistance,train.carSpacing);train.SendMessage("CapNhatViTriToaXe",SendMessageOptions.DontRequireReceiver); if(playPauseLabel)playPauseLabel.text="CHẠY TÀU"; }
    public void SetSpeed(float m) { speedMultiplier=m; if(train&&playing)train.targetSpeedKmh=baseSpeedKmh*m; }
    public void ToggleFollow() { isFollowing=!isFollowing; if(followLabel)followLabel.text=isFollowing?"TẮT THEO TÀU":"CAMERA THEO TÀU"; }
    public void CloseInfo(){if(infoTitle)infoTitle.text="SA BÀN YÊN TỬ – HẠ LONG XANH";if(infoDetails)infoDetails.text="Bấm vào nhà ga hoặc đoàn tàu để xem thông tin.";}
    void SelectByClick(){if(!viewCamera)return; Ray ray=viewCamera.ScreenPointToRay(Input.mousePosition); RaycastHit[] hits=Physics.RaycastAll(ray,10000); System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance)); foreach(var h in hits){SaBanInfoTarget t=h.collider.GetComponentInParent<SaBanInfoTarget>();if(t){if(infoTitle)infoTitle.text=t.title;if(infoDetails)infoDetails.text=t.title=="ĐOÀN TÀU"&&train?("Trạng thái: "+(playing?"Đang chạy":"Đang dừng")+"\nTốc độ: "+train.currentSpeedKmh.ToString("0.00")+" km/h"):t.details;return;} TrainSplineMover m=h.collider.GetComponentInParent<TrainSplineMover>();if(m){if(infoTitle)infoTitle.text="ĐOÀN TÀU";if(infoDetails)infoDetails.text="Trạng thái: "+(playing?"Đang chạy":"Đang dừng")+"\nTốc độ: "+m.currentSpeedKmh.ToString("0.0")+" km/h";return;}}}
    void RefreshStatus(){if(trainStatus&&train)trainStatus.text=(playing?"ĐANG CHẠY":"ĐANG DỪNG")+" | "+train.currentSpeedKmh.ToString("0.0")+" km/h | "+(speedMultiplier==1?"1x":speedMultiplier==2?"2x":"4x");}
}




