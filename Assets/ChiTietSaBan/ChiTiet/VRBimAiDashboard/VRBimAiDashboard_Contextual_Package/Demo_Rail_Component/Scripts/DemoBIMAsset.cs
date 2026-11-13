using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Click-to-select demo BIM asset for Unity 6.
/// Supports both the old Input Manager and the new Input System.
/// Put this component on the root GameObject of the imported rail model.
/// For VR or a custom BIM selector, call Select() / Deselect() directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoBIMAsset : MonoBehaviour
{
    [Header("Demo asset data")]
    [SerializeField] private string displayName = "Thanh ray UIC60 - Demo";
    [SerializeField] private string assetId = "TRK-001";
    [SerializeField] private string railwaySegment = "KM10+000 – KM10+050";
    [SerializeField] private string assetType = "Kết cấu đường ray";
    [SerializeField] private string operationalStatus = "Đang khai thác - Bình thường";

    [Header("Dashboard connection")]
    [SerializeField] private VRBimAiDashboardController dashboard;
    [SerializeField] private bool autoFindDashboard = true;

    [Header("Desktop click selection")]
    [Tooltip("Optional. If empty, the script uses Camera.main, then the first enabled camera.")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool enableDesktopClick = true;
    [SerializeField] private bool deselectOnEmptyWorldClick = true;
    [SerializeField] private LayerMask raycastLayers = ~0;
    [SerializeField] private float maxRayDistance = 10000f;

    [Header("Selection highlight")]
    [SerializeField] private bool highlightWhenSelected = true;
    [SerializeField] private Color selectedColor = new Color(0.20f, 0.78f, 1.0f, 1.0f);

    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock[] originalBlocks;
    private bool isSelected;
    private int cachedColliderCount;

    // All asset instances share one pointer handler so multiple rails cannot
    // process the same mouse click and cancel each other's selection.
    private static int lastPointerFrame = -1;
    private static DemoBIMAsset currentSelection;
    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(16);

    public string DisplayName => displayName;
    public string AssetId => assetId;
    public string RailwaySegment => railwaySegment;
    public string AssetType => assetType;
    public string OperationalStatus => operationalStatus;
    public bool IsSelected => isSelected;

    private void Reset()
    {
        EnsureColliders();
    }

    private void Awake()
    {
        CacheRenderers();
        EnsureColliders();
        ResolveDashboard();
        ResolveCamera();
    }

    private void Update()
    {
        if (!enableDesktopClick || lastPointerFrame == Time.frameCount)
            return;

        if (!TryGetPointerDown(out Vector2 pointerPosition))
            return;

        lastPointerFrame = Time.frameCount;

        // Do not select 3D objects when the user is clicking an actual UI control.
        // Decorative full-screen Images are intentionally ignored so they do not
        // accidentally block clicks on the central 3D viewport.
        if (IsPointerOverInteractiveUI(pointerPosition))
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("DemoBIMAsset: Không tìm thấy Camera. Hãy gán Main Camera vào trường Target Camera.", this);
            return;
        }

        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastLayers, QueryTriggerInteraction.Ignore))
        {
            DemoBIMAsset hitAsset = hit.collider.GetComponentInParent<DemoBIMAsset>();
            if (hitAsset != null)
            {
                if (currentSelection != null && currentSelection != hitAsset)
                    currentSelection.DeselectInternal();

                hitAsset.Select();
                return;
            }
        }

        if (deselectOnEmptyWorldClick && currentSelection != null)
            currentSelection.Deselect();
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalBlocks = new MaterialPropertyBlock[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            originalBlocks[i] = new MaterialPropertyBlock();
            if (cachedRenderers[i] != null)
                cachedRenderers[i].GetPropertyBlock(originalBlocks[i]);
        }
    }

    private void EnsureColliders()
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        int colliderCount = 0;

        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            // Imported OBJ models can place the MeshFilter on a child object.
            // Put a collider on each mesh child so the root selector can find it
            // with GetComponentInParent<DemoBIMAsset>().
            Collider existing = filter.GetComponent<Collider>();
            if (existing == null)
            {
                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                meshCollider.convex = false;
                meshCollider.isTrigger = false;
                colliderCount++;
            }
            else
            {
                MeshCollider meshCollider = existing as MeshCollider;
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = null;
                    meshCollider.sharedMesh = filter.sharedMesh;
                    meshCollider.convex = false;
                    meshCollider.isTrigger = false;
                }
                colliderCount++;
            }
        }

        // Fallback for models that have renderers but no usable MeshFilter.
        if (colliderCount == 0)
        {
            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                if (cachedRenderers == null || cachedRenderers.Length == 0)
                    cachedRenderers = GetComponentsInChildren<Renderer>(true);

                if (cachedRenderers != null && cachedRenderers.Length > 0)
                {
                    Bounds worldBounds = cachedRenderers[0].bounds;
                    for (int i = 1; i < cachedRenderers.Length; i++)
                        if (cachedRenderers[i] != null) worldBounds.Encapsulate(cachedRenderers[i].bounds);

                    Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
                    Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);
                    box.center = (localMin + localMax) * 0.5f;
                    Vector3 size = localMax - localMin;
                    box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                }
                colliderCount = 1;
            }
        }

        cachedColliderCount = colliderCount;
    }

    private bool TryGetPointerDown(out Vector2 position)
    {
        position = default;
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            pressed = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed && Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pressed = true;
        }
#endif

        return pressed;
    }

    private bool IsPointerOverInteractiveUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, UiRaycastResults);

        foreach (RaycastResult result in UiRaycastResults)
        {
            if (result.gameObject == null)
                continue;

            // Only controls that genuinely receive input block 3D selection.
            // Plain Images/background decorations do not block the central model.
            if (result.gameObject.GetComponentInParent<Selectable>() != null ||
                result.gameObject.GetComponentInParent<ScrollRect>() != null)
                return true;
        }

        return false;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.isActiveAndEnabled)
                {
                    targetCamera = cam;
                    break;
                }
            }
        }
        return targetCamera;
    }

    private void ResolveDashboard()
    {
        if (dashboard == null && autoFindDashboard)
            dashboard = FindFirstObjectByType<VRBimAiDashboardController>();
    }

    /// <summary>Public entry point for VR controllers or an existing BIM selector.</summary>
    public void Select()
    {
        if (currentSelection != null && currentSelection != this)
            currentSelection.DeselectInternal();

        currentSelection = this;
        isSelected = true;
        ResolveDashboard();

        if (cachedRenderers == null || originalBlocks == null)
            CacheRenderers();

        if (highlightWhenSelected)
            ApplyHighlight();

        if (dashboard != null)
        {
            dashboard.ShowForAsset(gameObject);
            dashboard.SetAssetInfo(displayName, assetId, railwaySegment, assetType, operationalStatus);
        }
        else
        {
            Debug.LogWarning("DemoBIMAsset: Không tìm thấy VRBimAiDashboardController trong Scene. Hãy gán Dashboard hoặc đặt prefab dashboard vào Scene.", this);
        }
    }

    /// <summary>Public entry point for an existing BIM selector or an Unselect button.</summary>
    public void Deselect()
    {
        DeselectInternal();
        if (currentSelection == this)
            currentSelection = null;
    }

    private void DeselectInternal()
    {
        isSelected = false;
        RestoreOriginalAppearance();
        ResolveDashboard();
        if (dashboard != null)
            dashboard.HideForAsset();
    }

    public void SetDashboard(VRBimAiDashboardController target)
    {
        dashboard = target;
    }

    private void ApplyHighlight()
    {
        if (cachedRenderers == null)
            CacheRenderers();

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer target in cachedRenderers)
        {
            if (target == null) continue;
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", selectedColor);
            block.SetColor("_Color", selectedColor);
            target.SetPropertyBlock(block);
        }
    }

    private void RestoreOriginalAppearance()
    {
        if (cachedRenderers == null || originalBlocks == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null && i < originalBlocks.Length)
                cachedRenderers[i].SetPropertyBlock(originalBlocks[i]);
        }
    }

    [ContextMenu("Check Demo BIM Click Setup")]
    private void CheckClickSetup()
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Camera cam = ResolveCamera();
        ResolveDashboard();

        Debug.Log(
            "[DemoBIMAsset Check] " + gameObject.name +
            " | MeshFilters: " + filters.Length +
            " | Colliders: " + colliders.Length +
            " | Cached colliders: " + cachedColliderCount +
            " | Camera: " + (cam != null ? cam.name : "MISSING") +
            " | Dashboard: " + (dashboard != null ? dashboard.name : "MISSING") +
            " | Input System: " + InputBackendName(), this);

        if (filters.Length == 0)
            Debug.LogWarning("DemoBIMAsset: Không tìm thấy MeshFilter trong object hoặc các object con.", this);
        if (colliders.Length == 0)
            Debug.LogWarning("DemoBIMAsset: Chưa có Collider. Hãy chạy Play hoặc Reset component để tạo Collider.", this);
        if (cam == null)
            Debug.LogWarning("DemoBIMAsset: Chưa tìm thấy Camera hoạt động hoặc Camera Main.", this);
        if (dashboard == null)
            Debug.LogWarning("DemoBIMAsset: Chưa tìm thấy Dashboard Controller.", this);
    }

    private static string InputBackendName()
    {
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
        return "Both";
#elif ENABLE_INPUT_SYSTEM
        return "New Input System";
#elif ENABLE_LEGACY_INPUT_MANAGER
        return "Legacy Input Manager";
#else
        return "No supported input backend enabled";
#endif
    }
}
