using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>Local seated-only exterior viewpoint; reuses the tracked stereo camera.</summary>
[DefaultExecutionOrder(2000)]
public sealed class TrainExteriorView : MonoBehaviour
{
    [SerializeField] private TrainSplineMover train;
    [SerializeField] private Material restrictedViewMaterial;
    [SerializeField] private Vector3 offset = new Vector3(10, 5, -12);
    [SerializeField] private Vector3 overheadOffset = new Vector3(0, 12, -10);
    [SerializeField, Range(0, 30)] private float yawRange = 15;
    [SerializeField, Range(0, 20)] private float pitchRange = 8;
    private Transform anchor;
    private FirstPersonController driver;
    private Camera viewCamera;
    private GameObject mask;
    private Material maskMaterial;
    private Mesh maskMesh;
    private InputAction toggle, look;
    private float yaw, pitch, nextSearch;
    private bool active;
    private int viewIndex = -1;
    public int ViewIndex => viewIndex; // -1 cabin, 0 overhead, 1 right, 2 left
    public bool IsExterior => active;
    public Transform ViewAnchor => anchor;
    public void Configure(TrainSplineMover mover, Material material) { train = mover; restrictedViewMaterial = material; }

    private void Awake()
    {
        var go = new GameObject("Train Exterior View Anchor");
        go.transform.SetParent(transform, false); anchor = go.transform;
        toggle = new InputAction("Train exterior B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        toggle.AddBinding("<Keyboard>/b");
        look = new InputAction("Train exterior look", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
    }
    private void OnEnable() { toggle?.Enable(); look?.Enable(); Application.onBeforeRender += UpdateMask; }
    private void OnDisable() { SetExterior(false); toggle?.Disable(); look?.Disable(); Application.onBeforeRender -= UpdateMask; }
    private bool LocalDriver => driver != null && driver.Object != null && driver.Object.IsValid && driver.HasStateAuthority && driver.IsDrivingTrain(train);
    private void Update()
    {
        if (train == null || train.frontCar == null) { SetExterior(false); return; }
        if (!LocalDriver && Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + .5f;
            foreach (var player in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                if (player.Object != null && player.Object.IsValid && player.HasStateAuthority && player.IsDrivingTrain(train)) { driver = player; break; }
        }
        if (active && !LocalDriver) SetExterior(false);
        if (toggle.WasPressedThisFrame()) ToggleView();
        if (!active) return;
        Vector2 input = look.ReadValue<Vector2>();
        if (input.magnitude < .2f) input = Vector2.zero;
        yaw = Mathf.Clamp(yaw + input.x * 25 * Time.unscaledDeltaTime, -yawRange, yawRange);
        pitch = Mathf.Clamp(pitch - input.y * 20 * Time.unscaledDeltaTime, -pitchRange, pitchRange);
        if (Application.isEditor && Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw = Mathf.Clamp(yaw + delta.x * .08f, -yawRange, yawRange);
            pitch = Mathf.Clamp(pitch - delta.y * .08f, -pitchRange, pitchRange);
        }
        UpdateAnchor();
    }
    public bool ToggleView()
    {
        if (!active && !LocalDriver) return false;
        if (!active) { viewIndex = 0; SetExterior(true); }
        else if (viewIndex < 2) { viewIndex++; yaw = pitch = 0; UpdateAnchor(); }
        else SetExterior(false);
        return active;
    }
    private void SetExterior(bool enabled)
    {
        if (enabled && (!LocalDriver || restrictedViewMaterial == null)) return;
        if (active == enabled) return;
        active = enabled;
        if (!enabled) viewIndex = -1;
        if (enabled)
        {
            viewCamera = Camera.main;
            if (viewCamera == null) { active = false; return; }
            yaw = pitch = 0; UpdateAnchor(); CreateMask();
            driver.SetSeatedViewOverride(anchor);
        }
        else if (driver != null) driver.SetSeatedViewOverride(null);
        if (mask != null) mask.SetActive(active);
    }
    private void UpdateAnchor()
    {
        var car = train.frontCar;
        Vector3 forward = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 position = viewIndex == 0 ? overheadOffset : new Vector3(viewIndex == 2 ? -offset.x : offset.x, offset.y, offset.z);
        float baseYaw = viewIndex == 0 ? 0 : viewIndex == 2 ? 22 : -22;
        anchor.SetPositionAndRotation(car.position + rotation * position, rotation * Quaternion.Euler((viewIndex == 0 ? 40 : 12) + pitch, baseYaw + yaw, 0));
    }
    private void LateUpdate()
    {
        if (!active || viewCamera == null) return;
        // Desktop fallback only. XR retains its pose driver and headset orientation.
        var origin = viewCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) viewCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        UpdateMask();
    }
    private void UpdateMask()
    {
        if (!active || maskMaterial == null || train == null || train.frontCar == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(train.frontCar.forward, Vector3.up).normalized;
        maskMaterial.SetVector("_TrainForward", forward);
    }
    private void CreateMask()
    {
        if (mask != null) return;
        mask = new GameObject("Exterior View Angular Guard"); mask.layer = viewCamera.gameObject.layer;
        mask.transform.SetParent(viewCamera.transform, false);
        float radius = Mathf.Max(.3f, viewCamera.nearClipPlane * 3);
        mask.transform.localScale = Vector3.one * radius;
        maskMesh = new Mesh { name = "Exterior view guard cube" };
        maskMesh.vertices = new[] {new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
        maskMesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
        maskMesh.RecalculateBounds();
        mask.AddComponent<MeshFilter>().sharedMesh = maskMesh;
        maskMaterial = new Material(restrictedViewMaterial);
        var renderer = mask.AddComponent<MeshRenderer>(); renderer.sharedMaterial = maskMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    private void OnDestroy()
    {
        toggle?.Dispose(); look?.Dispose();
        if (mask != null) Destroy(mask);
        if (maskMaterial != null) Destroy(maskMaterial);
        if (maskMesh != null) Destroy(maskMesh);
    }
}
