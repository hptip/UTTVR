
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Raycast model 3D và gửi ID đến Dashboard hoặc RailwayElementUI.
/// Hỗ trợ model một mesh và model nhiều mesh.
/// </summary>
[DisallowMultipleComponent]
public sealed class MeshClickHandler : MonoBehaviour
{
    // =========================================
    // 1. DASHBOARD CONNECTION
    // =========================================

    [Header("Dashboard Connection")]
    [SerializeField]
    private VRBimAiDashboardController dashboard;

    [SerializeField]
    private bool autoFindDashboard = true;

    // =========================================
    // 2. UI CONNECTION
    // =========================================

    [Header("UI Connection")]
    [SerializeField]
    private RailwayElementUI elementUI;

    [SerializeField]
    private bool autoFindElementUI = true;

    // =========================================
    // 3. CAMERA
    // =========================================

    [Header("Camera")]
    [SerializeField]
    private Camera targetCamera;

    // =========================================
    // 4. RAYCAST
    // =========================================

    [Header("Raycast")]
    [SerializeField]
    private LayerMask raycastLayers = ~0;

    [SerializeField]
    private float maxRayDistance = 10000f;

    // =========================================
    // 5. UI INPUT
    // =========================================

    [Header("UI Input")]
    [SerializeField]
    private bool ignoreClicksOverInteractiveUI = true;

    // =========================================
    // 6. STATIC VARIABLES
    // =========================================

    private static int lastHandledFrame = -1;

    private static readonly List<RaycastResult> UiResults =
        new List<RaycastResult>(16);

    // =========================================
    // 7. AWAKE
    // =========================================

    private void Awake()
    {
        ResolveDashboard();
        ResolveElementUI();
        ResolveCamera();
    }

    // =========================================
    // 8. UPDATE
    // =========================================

    private void Update()
    {
        // Tránh xử lý click nhiều lần trong cùng một frame
        if (lastHandledFrame == Time.frameCount)
            return;

        // Kiểm tra người dùng có click không
        if (!TryGetPointerDown(out Vector2 pointerPosition))
            return;

        lastHandledFrame = Time.frameCount;

        // Bỏ qua nếu click vào UI
        if (ignoreClicksOverInteractiveUI &&
            IsPointerOverInteractiveUI(pointerPosition))
        {
            return;
        }

        // Tìm Camera
        Camera cam = ResolveCamera();

        if (cam == null)
        {
            Debug.LogError(
                "MeshClickHandler: Không tìm thấy Camera!",
                this
            );

            return;
        }

        // Tạo Raycast
        Ray ray = cam.ScreenPointToRay(pointerPosition);

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            raycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            Debug.Log("MeshClickHandler: Không trúng Collider nào.", this);
            return;
        }

        // =====================================
        // 9. XÁC ĐỊNH GAMEOBJECT ĐƯỢC CLICK
        // =====================================

        GameObject meshObject = GetMeshObject(hit.collider);

        if (meshObject == null)
        {
            Debug.LogWarning(
                "MeshClickHandler: Không xác định được Mesh Object.",
                hit.collider
            );

            return;
        }

        // Tìm ElementIdentity
        ElementIdentity identity =
            hit.collider.GetComponent<ElementIdentity>();

        if (identity == null)
            identity = meshObject.GetComponent<ElementIdentity>();

        if (identity == null)
            identity = hit.collider.GetComponentInParent<ElementIdentity>();

        // Nếu có ElementIdentity thì ưu tiên ElementID.
        // Nếu không có thì sử dụng tên GameObject chứa Mesh.
        string id = identity != null
            ? identity.ElementID
            : meshObject.name.Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning(
                "MeshClickHandler: Không lấy được ID từ Mesh.",
                hit.collider
            );

            return;
        }

        Debug.Log(
            "MeshClickHandler: Click Mesh = '" +
            meshObject.name +
            "', ID = '" + id + "'.",
            hit.collider
        );

        // =====================================
        // 10. GỬI ID ĐẾN DASHBOARD HOẶC UI
        // =====================================

        ResolveDashboard();

        if (dashboard != null)
{
    dashboard.HandleMeshClick(meshObject, id);

    Debug.Log(
        "Dashboard nhận Mesh = " + meshObject.name +
        " | ID = " + id
    );
}
        else
        {
            // Chỉ dùng cách này khi không có Dashboard.
            ResolveElementUI();

            if (elementUI != null)
            {
                elementUI.ShowElement(id);

                Debug.Log(
                    "RailwayElementUI đã nhận ID: " + id
                );
            }
            else
            {
                Debug.LogError(
                    "Không tìm thấy Dashboard hoặc RailwayElementUI!",
                    this
                );
            }
        }
    }

    // =========================================
    // 11. RESOLVE DASHBOARD
    // =========================================

    private void ResolveDashboard()
    {
        if (dashboard == null && autoFindDashboard)
        {
            dashboard =
                FindFirstObjectByType<VRBimAiDashboardController>();
        }
    }

    // =========================================
    // 12. RESOLVE ELEMENT UI
    // =========================================

    private void ResolveElementUI()
    {
        if (elementUI == null && autoFindElementUI)
        {
            elementUI =
                FindFirstObjectByType<RailwayElementUI>();
        }
    }

    // =========================================
    // 13. RESOLVE CAMERA
    // =========================================

    private Camera ResolveCamera()
    {
        if (targetCamera != null &&
            targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;

        if (targetCamera != null)
            return targetCamera;

        Camera[] cameras =
            FindObjectsByType<Camera>(FindObjectsSortMode.None);

        foreach (Camera cam in cameras)
        {
            if (cam != null && cam.isActiveAndEnabled)
            {
                targetCamera = cam;
                break;
            }
        }

        return targetCamera;
    }

    // =========================================
    // 14. GET POINTER INPUT
    // =========================================

    private bool TryGetPointerDown(out Vector2 position)
    {
        position = default;

        bool pressed = false;

        // Input System mới
#if ENABLE_INPUT_SYSTEM

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            pressed = true;
        }
        else if (
            Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position =
                Touchscreen.current.primaryTouch.position.ReadValue();

            pressed = true;
        }

#endif

        // Input Manager cũ
#if ENABLE_LEGACY_INPUT_MANAGER

        if (!pressed && Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pressed = true;
        }

#endif

        return pressed;
    }

    // =========================================
    // 15. CHECK POINTER OVER UI
    // =========================================

    private bool IsPointerOverInteractiveUI(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return false;

        PointerEventData pointerData =
            new PointerEventData(eventSystem)
            {
                position = position
            };

        UiResults.Clear();

        eventSystem.RaycastAll(pointerData, UiResults);

        foreach (RaycastResult result in UiResults)
        {
            if (result.gameObject == null)
                continue;

            if (
                result.gameObject.GetComponentInParent<Selectable>() != null ||
                result.gameObject.GetComponentInParent<ScrollRect>() != null
            )
            {
                return true;
            }
        }

        return false;
    }

    // =========================================
    // 16. GET MESH OBJECT
    // =========================================

    

private static GameObject GetMeshObject(Collider hitCollider)
{
    if (hitCollider == null)
        return null;

    GameObject obj = hitCollider.gameObject;

    // Kiểm tra MeshFilter
    MeshFilter mf = obj.GetComponent<MeshFilter>();

    // Kiểm tra MeshCollider
    MeshCollider mc = obj.GetComponent<MeshCollider>();

    // Kiểm tra SkinnedMeshRenderer
    SkinnedMeshRenderer skinned =
        obj.GetComponent<SkinnedMeshRenderer>();

    // Model có MeshFilter và Mesh
    if (mf != null && mf.sharedMesh != null)
        return obj;

    // Model có MeshCollider và Mesh
    if (mc != null && mc.sharedMesh != null)
        return obj;

    // Model sử dụng SkinnedMeshRenderer
    if (skinned != null && skinned.sharedMesh != null)
        return obj;

    Debug.LogError(
        "Object được click không chứa Mesh hợp lệ: " +
        obj.name +
        "\nMeshFilter: " + (mf != null) +
        "\nMeshCollider: " + (mc != null) +
        "\nSkinnedMeshRenderer: " + (skinned != null),
        obj
    );

    return null;
}
}
