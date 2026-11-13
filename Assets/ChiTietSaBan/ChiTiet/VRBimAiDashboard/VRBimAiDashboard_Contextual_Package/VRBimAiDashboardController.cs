using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Dashboard controller cho hệ thống BIM.
/// - Tự nhận diện model tổng quan có một Mesh, không cần danh sách overviewModelRoots.
/// - Mở model nhiều Mesh, chuyển CameraFreeMove sang DetailOrbitZoom.
/// - Quay lại model tổng quan và khôi phục CameraFreeMove.
/// - Truy vấn dữ liệu theo ModelID + MeshID.
/// </summary>
[DisallowMultipleComponent]
public sealed class VRBimAiDashboardController : MonoBehaviour
{
    // =====================================================
    // 1. DATABASE UI
    // =====================================================

    [Header("Database UI")]
    [SerializeField] private RailwayElementUI railwayElementUI;


    // =====================================================
    // 2. TWO-LEVEL MODEL VIEW
    // =====================================================

    [Header("Two-Level Model View")]
    [Tooltip("Model nhiều Mesh, ví dụ DuongRay")]
    [SerializeField] private GameObject detailModelRoot;

    [Tooltip("Tâm xoay tùy chọn. Để trống thì dùng Transform của Detail Model Root")]
    [SerializeField] private Transform detailCameraPivot;

    [Header("Camera Controllers")]
    [Tooltip("DetailOrbitZoom trên Main Camera. Nếu để trống sẽ tự tìm trong Scene")]
    [SerializeField] private DetailOrbitZoom detailOrbitZoom;

    [Tooltip("CameraFreeMove trên Main Camera. Nếu để trống sẽ tự tìm trong Scene")]
    [SerializeField] private CameraFreeMove cameraFreeMove;

    [Header("Component List")]
    [Tooltip("UI danh sách Mesh trong model chi tiết")]
    [SerializeField] private BimComponentListUI componentListUI;

    [Header("X-Ray Highlight")]
    [Tooltip("Material URP X-Ray dùng cho cấu kiện đang được chọn")]
    [SerializeField] private Material xrayHighlightMaterial;

    // Lưu Material gốc của từng Renderer để khôi phục chính xác khi bỏ chọn.
    private readonly Dictionary<Renderer, Material[]> originalHighlightMaterials =
        new Dictionary<Renderer, Material[]>();

    private string activeModelID;
    private GameObject activeOverviewRoot;
    private bool isDetailMode;


    // =====================================================
    // 3. EVENTS
    // =====================================================

    [Serializable]
    public class GameObjectEvent : UnityEvent<GameObject> { }

    [Serializable]
    public class StringEvent : UnityEvent<string> { }

    [Header("Integration events")]
    public GameObjectEvent OnViewInBimRequested = new GameObjectEvent();
    public UnityEvent OnInspectionHistoryRequested = new UnityEvent();
    public UnityEvent OnMaintenancePlanRequested = new UnityEvent();
    public StringEvent OnSectionSelected = new StringEvent();
    public StringEvent OnLifecycleStageSelected = new StringEvent();
    public StringEvent OnToolSelected = new StringEvent();
    public StringEvent OnAiMessageSubmitted = new StringEvent();
    public UnityEvent OnVoiceInputRequested = new UnityEvent();
    public UnityEvent OnVrRequested = new UnityEvent();
    public UnityEvent OnSettingsRequested = new UnityEvent();
    public UnityEvent OnCameraRequested = new UnityEvent();


    // =====================================================
    // 4. INITIAL BEHAVIOR
    // =====================================================

    [Header("Initial behavior")]
    [SerializeField] private bool hideContextualPanelsOnStart = true;
    [SerializeField] private bool showAssistantAfterAssetSelection = true;


    // =====================================================
    // 5. BUTTON CACHE
    // =====================================================

    private readonly Dictionary<string, Button> buttons =
        new Dictionary<string, Button>();

    private readonly Dictionary<string, Color> originalButtonColors =
        new Dictionary<string, Color>();

    private readonly List<string> navigationNames = new List<string>
    {
        "BtnNavOverview",
        "BtnNavLifecycle",
        "BtnNavPlanning",
        "BtnNavDesign",
        "BtnNavBuild",
        "BtnNavOps",
        "BtnNavMaint",
        "BtnNavUpgrade",
        "BtnNavAssets",
        "BtnNavMap",
        "BtnNavAI",
        "BtnNavReport"
    };

    private Button viewDetailsButton;
    private Button backToOverviewButton;


    // =====================================================
    // 6. SELECTED ASSET INFORMATION
    // =====================================================

    private GameObject selectedAsset;
    private string assetName = "Chưa chọn cấu kiện";
    private string assetId = "Chưa có mã";
    private string assetSegment = "Chưa gán đoạn tuyến";
    private string assetType = "Cấu kiện BIM";
    private string assetStatus = "Chưa đồng bộ dữ liệu";
    private string activeTab = "General";
    private string activeTool = "";
    private string activeSection = "Tổng quan";


    // =====================================================
    // 7. UI REFERENCES
    // =====================================================

    private Transform assetPanel;
    private Transform aiPanel;
    private Transform lifecyclePanel;
    private Transform mapPanel;
    private Transform toolBar;
    private TMP_InputField chatInput;
    private RectTransform aiPanelRect;
    private RectTransform chatScrollRect;
    private float aiExpandedHeight = 318.75f;
    private float chatExpandedHeight = 210f;
    private bool aiMinimized;


    // =====================================================
    // 8. AWAKE
    // =====================================================

    private void Awake()
    {
        CacheElements();
        ResolveCameraControllers();

        EnsureToolButtons();
        EnsureAssistantControls();

        // Đưa hai nút chuyển chế độ ra ngoài AssetPanel nếu cần,
        // để chúng không bị ẩn khi AssetPanel.SetActive(false).
        KeepModeButtonsOutsideAssetPanel();

        CacheButtons();
        WireButtons();
        BindModeButtons();
        WireChatInput();

        if (assetPanel == null)
        {
            Debug.LogWarning(
                "VRBimAiDashboardController: Không tìm thấy AssetPanel hoặc AssetInfo.",
                this
            );
        }

        if (hideContextualPanelsOnStart)
        {
            SetContextualPanelsVisible(false);
        }

        PopulateGeneralTab();
        SetText("TxtCrumb", "Mô hình BIM  >  Chọn cấu kiện");
    }


    // =====================================================
    // 9. START
    // =====================================================

    private void Start()
    {
        if (detailModelRoot != null)
        {
            detailModelRoot.SetActive(false);
        }

        if (componentListUI != null)
            componentListUI.Hide();

        ClearSelectionHighlight();
        SetOverviewCameraMode();

        SetModeButton("BtnViewDetails", false);
        SetModeButton("BtnBackToOverview", false);

        isDetailMode = false;
    }


    // =====================================================
    // 10. CACHE UI ELEMENTS
    // =====================================================

    private void CacheElements()
    {
        assetPanel = FindDeep(transform, "AssetPanel");
        if (assetPanel == null)
            assetPanel = FindDeep(transform, "AssetInfo");

        aiPanel = FindDeep(transform, "AiPanel");
        lifecyclePanel = FindDeep(transform, "LifecycleBar");
        mapPanel = FindDeep(transform, "MapPanel");
        toolBar = FindDeep(transform, "ToolBar");

        chatInput = FindDeep(transform, "InpAiChat")?.GetComponent<TMP_InputField>();
        aiPanelRect = aiPanel as RectTransform;
        chatScrollRect = FindDeep(transform, "ChatScroll") as RectTransform;

        if (aiPanelRect != null)
            aiExpandedHeight = aiPanelRect.sizeDelta.y;

        if (chatScrollRect != null)
            chatExpandedHeight = chatScrollRect.sizeDelta.y;
    }


    // =====================================================
    // 11. RESOLVE CAMERA CONTROLLERS
    // =====================================================

    private void ResolveCameraControllers()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            if (cameraFreeMove == null)
                cameraFreeMove = mainCamera.GetComponent<CameraFreeMove>();

            if (detailOrbitZoom == null)
                detailOrbitZoom = mainCamera.GetComponent<DetailOrbitZoom>();
        }

        if (cameraFreeMove == null)
            cameraFreeMove = FindFirstObjectByType<CameraFreeMove>(FindObjectsInactive.Include);

        if (detailOrbitZoom == null)
            detailOrbitZoom = FindFirstObjectByType<DetailOrbitZoom>(FindObjectsInactive.Include);

        if (cameraFreeMove == null)
        {
            Debug.LogError(
                "Dashboard: Không tìm thấy CameraFreeMove trong Scene. Hãy kiểm tra Main Camera.",
                this
            );
        }

        if (detailOrbitZoom == null)
        {
            Debug.LogError(
                "Dashboard: Không tìm thấy DetailOrbitZoom trong Scene. Hãy kiểm tra Main Camera.",
                this
            );
        }
    }


    // =====================================================
    // 12. CAMERA MODE HELPERS
    // =====================================================

    private void SetOverviewCameraMode()
    {
        if (detailOrbitZoom != null)
        {
            // Đảm bảo component được khởi tạo trước khi gọi hàm khôi phục.
            detailOrbitZoom.enabled = true;
            detailOrbitZoom.DisableAndRestore();
            detailOrbitZoom.enabled = false;
        }

        if (cameraFreeMove != null)
            cameraFreeMove.enabled = true;
    }

    private void SetDetailCameraMode()
    {
        // Tắt Free trước để nó không ghi đè Transform của Main Camera.
        if (cameraFreeMove != null)
            cameraFreeMove.enabled = false;

        if (detailOrbitZoom == null)
        {
            Debug.LogError("Dashboard: Không có DetailOrbitZoom để bật chế độ Orbit.", this);
            return;
        }

        Transform target = detailCameraPivot != null
            ? detailCameraPivot
            : detailModelRoot.transform;

        Bounds modelBounds = CalculateModelBounds(detailModelRoot);

        detailOrbitZoom.enabled = true;
        detailOrbitZoom.EnableForTarget(target, modelBounds);

        Debug.Log(
            "Dashboard: CameraFreeMove OFF, DetailOrbitZoom ON. " +
            "Tâm model = " + modelBounds.center +
            ", Kích thước = " + modelBounds.size,
            this
        );
    }

    private void DisableDetailCameraMode()
    {
        if (detailOrbitZoom != null)
        {
            detailOrbitZoom.enabled = true;
            detailOrbitZoom.DisableAndRestore();
            detailOrbitZoom.enabled = false;
        }

        if (cameraFreeMove != null)
            cameraFreeMove.enabled = true;

        Debug.Log("Dashboard: DetailOrbitZoom OFF, CameraFreeMove ON.", this);
    }


    // =====================================================
    // 13. ENSURE TOOL BUTTONS
    // =====================================================

    private void EnsureToolButtons()
    {
        string[] names =
        {
            "ToolMove", "ToolOrbit", "ToolSelect", "ToolMeasure",
            "ToolNote", "ToolClip", "ToolStats", "ToolExitVR"
        };

        foreach (string name in names)
        {
            Transform item = FindDeep(transform, name);
            if (item == null)
                continue;

            Button button = item.GetComponent<Button>();
            if (button == null)
            {
                Image image = item.GetComponent<Image>();
                if (image == null)
                    image = item.gameObject.AddComponent<Image>();

                image.raycastTarget = true;
                button = item.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
            }
        }
    }


    // =====================================================
    // 14. ENSURE ASSISTANT CONTROLS
    // =====================================================

    private void EnsureAssistantControls()
    {
        if (aiPanel == null)
            return;

        Transform header = FindDeep(aiPanel, "AiHeader");
        if (header == null)
            return;

        if (FindDeep(aiPanel, "BtnSend") == null)
        {
            RectTransform inputRect = FindDeep(aiPanel, "InpAiChat") as RectTransform;
            if (inputRect != null)
                inputRect.sizeDelta = new Vector2(400f, inputRect.sizeDelta.y);

            Transform mic = FindDeep(aiPanel, "BtnMic");
            if (mic is RectTransform micRect)
                micRect.anchoredPosition = new Vector2(-52f, micRect.anchoredPosition.y);

            CreateRuntimeButton(
                aiPanel, "BtnSend", "➤",
                new Vector2(1f, 0f), new Vector2(-12f, 10f),
                new Vector2(34f, 34f), new Color(0.12f, 0.43f, 0.88f, 1f)
            );
        }

        if (FindDeep(header, "BtnAiMinimize") == null)
        {
            CreateRuntimeButton(
                header, "BtnAiMinimize", "−",
                new Vector2(1f, 0.5f), new Vector2(-68f, 0f),
                new Vector2(22f, 24f), new Color(0.10f, 0.28f, 0.46f, 0.9f)
            );
        }

        if (FindDeep(header, "BtnAiMaximize") == null)
        {
            CreateRuntimeButton(
                header, "BtnAiMaximize", "□",
                new Vector2(1f, 0.5f), new Vector2(-40f, 0f),
                new Vector2(22f, 24f), new Color(0.10f, 0.28f, 0.46f, 0.9f)
            );
        }
    }


    // =====================================================
    // 15. CREATE RUNTIME BUTTON
    // =====================================================

    private Button CreateRuntimeButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        Color background)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = background;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );

        textObject.transform.SetParent(go.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        TMP_Text referenceText = GetComponentInChildren<TMP_Text>(true);
        if (referenceText != null)
            text.font = referenceText.font;

        text.text = label;
        text.fontSize = 14f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 16f;
        text.raycastTarget = false;

        return button;
    }


    // =====================================================
    // 16. MODE BUTTONS: FIND, MOVE, BIND
    // =====================================================

    private Button FindButtonInScene(string buttonName)
    {
        // Ưu tiên tìm trong cây Dashboard.
        Transform local = FindDeep(transform, buttonName);
        if (local != null)
        {
            Button localButton = local.GetComponent<Button>();
            if (localButton != null)
                return localButton;
        }

        // Nếu Button nằm ngoài cây Dashboard, tìm trong toàn Scene,
        // kể cả khi GameObject hoặc parent đang inactive.
        Button[] allButtons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Button button in allButtons)
        {
            if (button != null && button.gameObject.name == buttonName)
                return button;
        }

        return null;
    }

    private void KeepModeButtonsOutsideAssetPanel()
    {
        MoveModeButtonOutsideAssetPanel("BtnViewDetails");
        MoveModeButtonOutsideAssetPanel("BtnBackToOverview");
    }

    private void MoveModeButtonOutsideAssetPanel(string buttonName)
    {
        if (assetPanel == null)
            return;

        Button button = FindButtonInScene(buttonName);
        if (button == null)
        {
            Debug.LogWarning("Dashboard: Không tìm thấy nút " + buttonName, this);
            return;
        }

        Transform buttonTransform = button.transform;
        if (buttonTransform == assetPanel || buttonTransform.IsChildOf(assetPanel))
        {
            Transform newParent = assetPanel.parent != null
                ? assetPanel.parent
                : transform;

            buttonTransform.SetParent(newParent, true);
            buttonTransform.SetAsLastSibling();

            Debug.Log(buttonName + " đã được đưa ra ngoài AssetPanel.", this);
        }
    }

    private void BindModeButtons()
    {
        viewDetailsButton = FindButtonInScene("BtnViewDetails");
        backToOverviewButton = FindButtonInScene("BtnBackToOverview");

        if (viewDetailsButton != null)
        {
            viewDetailsButton.onClick.RemoveListener(OpenDetailView);
            viewDetailsButton.onClick.AddListener(OpenDetailView);
            viewDetailsButton.interactable = true;
            Debug.Log("Dashboard: Đã gắn sự kiện BtnViewDetails -> OpenDetailView.", this);
        }
        else
        {
            Debug.LogError("Dashboard: Không tìm thấy Button BtnViewDetails.", this);
        }

        if (backToOverviewButton != null)
        {
            backToOverviewButton.onClick.RemoveListener(BackToOverview);
            backToOverviewButton.onClick.AddListener(BackToOverview);
            backToOverviewButton.interactable = true;
            Debug.Log("Dashboard: Đã gắn sự kiện BtnBackToOverview -> BackToOverview.", this);
        }
        else
        {
            Debug.LogError("Dashboard: Không tìm thấy Button BtnBackToOverview.", this);
        }
    }


    // =====================================================
    // 17. CACHE BUTTONS
    // =====================================================

    private void CacheButtons()
    {
        buttons.Clear();

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            Button button = t.GetComponent<Button>();
            if (button == null)
                continue;

            buttons[t.name] = button;

            if (button.targetGraphic is Graphic graphic &&
                !originalButtonColors.ContainsKey(t.name))
            {
                originalButtonColors[t.name] = graphic.color;
            }
        }
    }


    // =====================================================
    // 18. WIRE BUTTONS
    // =====================================================

    private void WireButtons()
    {
        Wire("BtnNavOverview", () => SelectSection("Tổng quan"));
        Wire("BtnNavLifecycle", () =>
        {
            SelectSection("Quản lý vòng đời");
            SetPanelActive(lifecyclePanel, true);
        });
        Wire("BtnNavPlanning", () => SelectSection("Quy hoạch & yêu cầu"));
        Wire("BtnNavDesign", () => SelectSection("Thiết kế (BIM)"));
        Wire("BtnNavBuild", () => SelectSection("Thi công & nghiệm thu"));
        Wire("BtnNavOps", () => SelectSection("Vận hành & giám sát"));
        Wire("BtnNavMaint", () => SelectSection("Bảo trì & sửa chữa"));
        Wire("BtnNavUpgrade", () => SelectSection("Nâng cấp & kết thúc"));
        Wire("BtnNavAssets", OpenAssetPanel);
        Wire("BtnNavMap", () =>
        {
            SelectSection("Bản đồ tuyến");
            SetPanelActive(mapPanel, true);
        });
        Wire("BtnNavAI", () =>
        {
            SelectSection("Trợ lý AI");
            SetPanelActive(aiPanel, true);
        });
        Wire("BtnNavReport", () => SelectSection("Báo cáo & phân tích"));

        Wire("BtnViewBim", RequestViewInBim);
        Wire("BtnInspect", RequestInspectionHistory);
        Wire("BtnPlan", RequestMaintenancePlan);
        Wire("BtnAiClose", () => SetPanelActive(aiPanel, false));
        Wire("BtnAiMinimize", MinimizeAssistant);
        Wire("BtnAiMaximize", MaximizeAssistant);
        Wire("BtnSend", SubmitChat);
        Wire("BtnMic", RequestVoiceInput);

        Wire("BtnCamera", () =>
        {
            OnCameraRequested.Invoke();
            Notify("Camera", "Đã nhận thao tác camera. Hãy kết nối với camera controller của dự án.");
        });

        Wire("BtnVR", () =>
        {
            OnVrRequested.Invoke();
            Notify("VR", "Đã nhận yêu cầu VR. Hãy kết nối với VR controller của dự án.");
        });

        Wire("BtnWifi", () =>
        {
            Notify("Kết nối", "Trạng thái kết nối cần được lấy từ dịch vụ mạng của dự án.");
        });

        Wire("BtnSettings", () =>
        {
            OnSettingsRequested.Invoke();
            Notify("Cài đặt", "Hãy kết nối màn hình cài đặt của dự án tại sự kiện OnSettingsRequested.");
        });

        Wire("TabGeneral", () => SelectAssetTab("General"));
        Wire("TabLifecycle", () => SelectAssetTab("Lifecycle"));
        Wire("TabDocs", () => SelectAssetTab("Docs"));

        Wire("Stg1", () => SelectLifecycleStage("Quy hoạch"));
        Wire("Stg2", () => SelectLifecycleStage("Thiết kế"));
        Wire("Stg3", () => SelectLifecycleStage("Thi công"));
        Wire("Stg4", () => SelectLifecycleStage("Vận hành"));
        Wire("Stg5", () => SelectLifecycleStage("Bảo trì"));
        Wire("Stg6", () => SelectLifecycleStage("Nâng cấp"));

        Wire("ToolMove", () => SelectTool("Di chuyển (WASD)"));
        Wire("ToolOrbit", () => SelectTool("Xoay nhìn"));
        Wire("ToolSelect", () => SelectTool("Chọn đối tượng"));
        Wire("ToolMeasure", () => SelectTool("Đo khoảng cách"));
        Wire("ToolNote", () => SelectTool("Ghi chú / Ảnh"));
        Wire("ToolClip", () => SelectTool("Cắt lớp (BIM)"));
        Wire("ToolStats", () => SelectTool("Hiển thị dữ liệu"));
        Wire("ToolExitVR", () => SelectTool("Thoát VR (Esc)"));
    }

    private void Wire(string name, UnityAction action)
    {
        if (!buttons.TryGetValue(name, out Button button) || button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        button.interactable = true;
    }

    private void WireChatInput()
    {
        if (chatInput != null)
        {
            chatInput.onSubmit.RemoveAllListeners();
            chatInput.onSubmit.AddListener(_ => SubmitChat());
        }
    }


    // =====================================================
    // 19. SHOW FOR ASSET
    // =====================================================

    public void ShowForAsset(GameObject selected)
    {
        if (selected == null)
        {
            HideForAsset();
            return;
        }

        // Kiểm tra model chi tiết trước để không nhận nhầm mesh con
        // thành model tổng quan.
        if (isDetailMode && IsInsideModel(selected, detailModelRoot))
        {
            ElementIdentity identity = selected.GetComponent<ElementIdentity>();
            string meshID = identity != null ? identity.ElementID : selected.name;
            ShowComponent(activeModelID, meshID, selected);
            return;
        }

        GameObject overviewRoot = FindOverviewRoot(selected, selected.name);
        if (overviewRoot != null)
        {
            ShowModelSummary(overviewRoot.name, overviewRoot);
            return;
        }

        selectedAsset = selected;
        assetName = selected.name;
        assetId = selected.name;
        assetSegment = "Chưa gán đoạn tuyến";
        assetType = "Cấu kiện BIM";
        assetStatus = "Chưa đồng bộ dữ liệu";

        SetContextualPanelsVisible(true);
        ApplyAssetHeader();
        SelectAssetTab("General");

        if (!showAssistantAfterAssetSelection)
            SetPanelActive(aiPanel, false);
    }


    // =====================================================
    // 20. SHOW ELEMENT
    // =====================================================

    public void ShowElement(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            HideForAsset();
            return;
        }

        string resolvedId = id.Trim();
        GameObject selected = FindSceneObjectByName(resolvedId);
        HandleMeshClick(selected, resolvedId);
    }


    // =====================================================
    // 21. HANDLE MESH CLICK
    // =====================================================

    public void HandleMeshClick(GameObject meshObject, string meshID)
    {
        if (string.IsNullOrWhiteSpace(meshID))
            return;

        // A. Click cấu kiện bên trong model nhiều Mesh.
        if (IsInsideModel(meshObject, detailModelRoot))
        {
            if (!isDetailMode)
            {
                Debug.LogWarning("Dashboard: Model chi tiết chưa được mở.", this);
                return;
            }

            ShowComponent(activeModelID, meshID, meshObject);
            return;
        }

        // B. Click model tổng quan có một Mesh.
        GameObject overviewRoot = FindOverviewRoot(meshObject, meshID);
        if (overviewRoot != null)
        {
            ShowModelSummary(overviewRoot.name, overviewRoot);
            return;
        }

        Debug.LogWarning(
            "Dashboard: Không xác định được model hoặc mesh: " + meshID,
            this
        );
    }


    // =====================================================
    // 22. FIND OVERVIEW ROOT (NO INSPECTOR LIST)
    // =====================================================

    private GameObject FindOverviewRoot(GameObject clickedObject, string id)
    {
        if (clickedObject == null)
            return null;

        Transform current = clickedObject.transform;
        while (current != null)
        {
            MeshFilter meshFilter = current.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = current.GetComponent<MeshRenderer>();
            SkinnedMeshRenderer skinnedRenderer = current.GetComponent<SkinnedMeshRenderer>();

            bool hasMeshFilter = meshFilter != null && meshFilter.sharedMesh != null;
            bool hasSkinnedMesh = skinnedRenderer != null && skinnedRenderer.sharedMesh != null;

            if ((hasMeshFilter && meshRenderer != null) || hasSkinnedMesh)
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }


    // =====================================================
    // 23. MODEL RELATION + BOUNDS
    // =====================================================

    private bool IsInsideModel(GameObject child, GameObject parent)
    {
        if (child == null || parent == null)
            return false;

        return child.transform == parent.transform ||
               child.transform.IsChildOf(parent.transform);
    }

    private static Bounds CalculateModelBounds(GameObject modelRoot)
    {
        if (modelRoot == null)
            return new Bounds(Vector3.zero, Vector3.one);

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(modelRoot.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = modelRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }
        }

        if (!hasBounds)
            bounds = new Bounds(modelRoot.transform.position, Vector3.one);

        return bounds;
    }


    // =====================================================
    // X-RAY SELECTION HIGHLIGHT
    // =====================================================

    /// <summary>
    /// Thay Material của Renderer được chọn bằng X-Ray Material.
    /// includeChildren = true sẽ áp dụng cho toàn bộ model và các con.
    /// </summary>
    private void HighlightSelection(GameObject selectedObject, bool includeChildren = false)
    {
        // Chỉ duy trì một đối tượng/nhóm được highlight tại một thời điểm.
        ClearSelectionHighlight();

        if (selectedObject == null)
            return;

        if (xrayHighlightMaterial == null)
        {
            Debug.LogError(
                "Dashboard: Chưa gán X-Ray Highlight Material trong Inspector.",
                this
            );
            return;
        }

        Renderer[] renderers;

        if (includeChildren)
        {
            renderers = selectedObject.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            Renderer targetRenderer = selectedObject.GetComponent<Renderer>();

            if (targetRenderer == null)
                targetRenderer = selectedObject.GetComponentInParent<Renderer>();

            if (targetRenderer != null)
            {
                renderers = new Renderer[] { targetRenderer };
            }
            else
            {
                renderers = selectedObject.GetComponentsInChildren<Renderer>(true);
            }
        }

        int highlightedCount = 0;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || originalHighlightMaterials.ContainsKey(renderer))
                continue;

            Material[] originalMaterials = renderer.sharedMaterials;
            if (originalMaterials == null || originalMaterials.Length == 0)
                continue;

            // Lưu đúng các Material gốc, không sửa asset Material trong Project.
            originalHighlightMaterials.Add(renderer, originalMaterials);

            Material[] xrayMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < xrayMaterials.Length; i++)
                xrayMaterials[i] = xrayHighlightMaterial;

            renderer.sharedMaterials = xrayMaterials;
            highlightedCount++;
        }

        Debug.Log(
            "Dashboard: X-Ray Highlight '" + selectedObject.name +
            "' trên " + highlightedCount + " Renderer.",
            selectedObject
        );
    }

    /// <summary>
    /// Khôi phục Material ban đầu cho tất cả Renderer đã được X-Ray.
    /// </summary>
    private void ClearSelectionHighlight()
    {
        foreach (KeyValuePair<Renderer, Material[]> item in originalHighlightMaterials)
        {
            Renderer renderer = item.Key;
            if (renderer != null)
                renderer.sharedMaterials = item.Value;
        }

        originalHighlightMaterials.Clear();
    }

    private void OnDestroy()
    {
        // Tránh để Renderer giữ X-Ray Material nếu Dashboard bị hủy.
        ClearSelectionHighlight();
    }


    // =====================================================
    // 24. SHOW MODEL SUMMARY
    // =====================================================

    public void ShowModelSummary(string modelID, GameObject overviewRoot)
    {
        if (string.IsNullOrWhiteSpace(modelID))
            return;

        activeModelID = modelID.Trim();
        activeOverviewRoot = overviewRoot;
        isDetailMode = false;

        if (detailModelRoot != null)
            detailModelRoot.SetActive(false);

        // Danh sách cấu kiện chỉ xuất hiện ở chế độ chi tiết.
        if (componentListUI != null)
            componentListUI.Hide();

        // Khi chọn model tổng quan: khôi phục X-Ray trước đó,
        // Orbit OFF và Free Camera ON.
        ClearSelectionHighlight();
        SetOverviewCameraMode();

        if (activeOverviewRoot != null)
        {
            activeOverviewRoot.SetActive(true);
            HighlightSelection(activeOverviewRoot, true);
        }

        selectedAsset = activeOverviewRoot;
        assetName = activeModelID;
        assetId = activeModelID;
        assetSegment = "Chưa gán đoạn tuyến";
        assetType = "Mô hình tổng thể";
        assetStatus = "Đã chọn mô hình";

        SetContextualPanelsVisible(true);
        SetPanelActive(assetPanel, true);
        ApplyAssetHeader();
        SelectAssetTab("General");

        if (railwayElementUI != null)
        {
            railwayElementUI.ShowModelSummary(activeModelID);
        }
        else
        {
            Debug.LogError("Dashboard: Chưa gán RailwayElementUI.", this);
        }

        SetModeButton("BtnViewDetails", true);
        SetModeButton("BtnBackToOverview", false);

        if (!showAssistantAfterAssetSelection)
            SetPanelActive(aiPanel, false);

        Debug.Log("Dashboard: Đang xem tổng thể model " + activeModelID, this);
    }


    // =====================================================
    // 25. OPEN DETAIL VIEW
    // =====================================================

    public void OpenDetailView()
    {
        Debug.Log("Dashboard: Nhấn Xem chi tiết.", this);

        if (string.IsNullOrWhiteSpace(activeModelID))
        {
            Debug.LogWarning("Dashboard: Chưa chọn model tổng thể.", this);
            return;
        }

        if (detailModelRoot == null)
        {
            Debug.LogError("Dashboard: Chưa gán Detail Model Root (DuongRay).", this);
            return;
        }

        if (activeOverviewRoot == null)
        {
            Debug.LogWarning("Dashboard: Không có model tổng quan đang chọn.", this);
            return;
        }

        // Hai model phải độc lập, không được lồng model chi tiết bên trong model tổng quan.
        if (IsInsideModel(detailModelRoot, activeOverviewRoot))
        {
            Debug.LogError(
                "DetailModelRoot đang nằm bên trong OverviewRoot. " +
                "Hãy đưa hai model ra thành hai GameObject độc lập trong Hierarchy.",
                this
            );
            return;
        }

        // Khôi phục Material model tổng quan trước khi ẩn nó.
        ClearSelectionHighlight();

        // Hiện model chi tiết trước khi tính Bounds.
        activeOverviewRoot.SetActive(false);
        detailModelRoot.SetActive(true);
        isDetailMode = true;

        // Tự động tạo danh sách cấu kiện từ các Renderer trong model.
        if (componentListUI != null)
            componentListUI.Show(detailModelRoot, this);

        if (railwayElementUI != null)
            railwayElementUI.HidePanel();

        SetPanelActive(assetPanel, false);

        // Tắt Free Camera và bật Orbit Camera, căn theo Bounds model.
        SetDetailCameraMode();

        SetModeButton("BtnViewDetails", false);
        SetModeButton("BtnBackToOverview", true);

        SetText("TxtCrumb", "Mô hình BIM > " + activeModelID + " > Chi tiết");

        Debug.Log("Dashboard: Đã mở chi tiết cho model " + activeModelID, this);
    }


    // =====================================================
    // 26. SHOW COMPONENT
    // =====================================================

    private void ShowComponent(string modelID, string meshID, GameObject meshObject)
    {
        if (string.IsNullOrWhiteSpace(modelID) || string.IsNullOrWhiteSpace(meshID))
            return;

        // X-Ray riêng cấu kiện đang được chọn.
        HighlightSelection(meshObject, false);

        selectedAsset = meshObject;
        assetName = meshID;
        assetId = meshID;
        assetSegment = "Chưa gán đoạn tuyến";
        assetType = "Cấu kiện chi tiết";
        assetStatus = "Đã chọn cấu kiện";

        SetContextualPanelsVisible(true);
        SetPanelActive(assetPanel, true);
        ApplyAssetHeader();
        SelectAssetTab("General");

        if (railwayElementUI != null)
        {
            railwayElementUI.ShowComponent(modelID, meshID);
        }
        else
        {
            Debug.LogError("Dashboard: Chưa gán RailwayElementUI.", this);
        }

        SetModeButton("BtnViewDetails", false);
        SetModeButton("BtnBackToOverview", true);

        if (!showAssistantAfterAssetSelection)
            SetPanelActive(aiPanel, false);

        Debug.Log(
            "Dashboard: Đã chọn cấu kiện.\nModelID = " + modelID + "\nMeshID = " + meshID,
            this
        );
    }


    // =====================================================
    // 27. BACK TO OVERVIEW
    // =====================================================

    public void BackToOverview()
    {
        Debug.Log("Dashboard: Nhấn Quay lại tổng thể.", this);

        if (activeOverviewRoot == null)
        {
            Debug.LogWarning("Dashboard: Không tìm thấy model tổng thể đã chọn.", this);
            return;
        }

        if (componentListUI != null)
            componentListUI.Hide();

        ClearSelectionHighlight();

        if (detailModelRoot != null)
            detailModelRoot.SetActive(false);

        isDetailMode = false;

        // Orbit OFF, khôi phục vị trí camera đã lưu và Free Camera ON.
        DisableDetailCameraMode();

        activeOverviewRoot.SetActive(true);

        // Nạp lại thông tin tổng thể và cập nhật trạng thái hai nút.
        ShowModelSummary(activeModelID, activeOverviewRoot);

        Debug.Log("Dashboard: Đã quay lại tổng thể " + activeModelID, this);
    }


    // =====================================================
    // 28. SET MODE BUTTON
    // =====================================================

    private void SetModeButton(string buttonName, bool visible)
    {
        Button button = null;

        if (buttonName == "BtnViewDetails")
            button = viewDetailsButton;
        else if (buttonName == "BtnBackToOverview")
            button = backToOverviewButton;

        if (button == null)
        {
            button = FindButtonInScene(buttonName);
            if (buttonName == "BtnViewDetails")
                viewDetailsButton = button;
            else if (buttonName == "BtnBackToOverview")
                backToOverviewButton = button;
        }

        if (button == null)
        {
            Debug.LogWarning("Dashboard: Không tìm thấy Button " + buttonName, this);
            return;
        }

        button.gameObject.SetActive(visible);
        button.interactable = true;
    }


    // =====================================================
    // 29. FIND SCENE OBJECT BY NAME
    // =====================================================

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] allTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform item in allTransforms)
        {
            if (item != null && item.name == objectName)
                return item.gameObject;
        }

        return null;
    }


    // =====================================================
    // 30. SET ASSET INFO
    // =====================================================

    public void SetAssetInfo(
        string displayName,
        string id,
        string segment,
        string type,
        string status)
    {
        assetName = Safe(displayName, "Chưa có tên");
        assetId = Safe(id, "Chưa có mã");
        assetSegment = Safe(segment, "Chưa gán đoạn tuyến");
        assetType = Safe(type, "Cấu kiện BIM");
        assetStatus = Safe(status, "Chưa đồng bộ dữ liệu");

        ApplyAssetHeader();
        if (activeTab == "General")
            PopulateGeneralTab();
    }


    // =====================================================
    // 31. HIDE FOR ASSET
    // =====================================================

    public void HideForAsset()
    {
        if (componentListUI != null)
            componentListUI.Hide();

        ClearSelectionHighlight();

        selectedAsset = null;
        activeModelID = null;
        activeOverviewRoot = null;
        isDetailMode = false;

        if (detailModelRoot != null)
            detailModelRoot.SetActive(false);

        SetOverviewCameraMode();

        if (railwayElementUI != null)
            railwayElementUI.HidePanel();

        SetModeButton("BtnViewDetails", false);
        SetModeButton("BtnBackToOverview", false);

        SetContextualPanelsVisible(false);
        SetText("TxtCrumb", "Mô hình BIM > Chọn cấu kiện");
    }


    // =====================================================
    // 32. SAFE STRING
    // =====================================================

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }


    // =====================================================
    // 33. APPLY ASSET HEADER
    // =====================================================

    private void ApplyAssetHeader()
    {
        SetText("TxtAssetTitle", "Thông tin tài sản");
        SetText("TxtSeg", "Đoạn tuyến: " + assetSegment);
        SetText("TxtType", "Loại: " + assetType);
        SetText("TxtCode", "Mã tài sản: " + assetId);
        SetText("TxtStatusVal", assetStatus);
        SetText("TxtCrumb", "Mô hình BIM > " + assetName);
    }


    // =====================================================
    // 34. CONTEXTUAL PANELS
    // =====================================================

    private void SetContextualPanelsVisible(bool visible)
    {
        SetPanelActive(assetPanel, visible);
        SetPanelActive(lifecyclePanel, visible);
        SetPanelActive(mapPanel, visible);
        SetPanelActive(toolBar, visible);
        SetPanelActive(aiPanel, visible && showAssistantAfterAssetSelection);
    }

    private void SetPanelActive(Transform panel, bool active)
    {
        if (panel != null)
            panel.gameObject.SetActive(active);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }


    // =====================================================
    // 35. OPEN ASSET PANEL
    // =====================================================

    private void OpenAssetPanel()
    {
        SetPanelActive(assetPanel, true);

        if (selectedAsset == null)
        {
            assetName = "Chưa chọn cấu kiện";
            assetId = "Chưa có mã";
            assetSegment = "Chưa gán đoạn tuyến";
            assetType = "Cấu kiện BIM";
            assetStatus = "Chưa đồng bộ dữ liệu";

            ApplyAssetHeader();
            PopulateGeneralTab();

            Notify(
                "Thông tin tài sản",
                "Hãy chọn một cấu kiện trong mô hình 3D để nạp thông tin chính xác."
            );
        }
    }


    // =====================================================
    // 36. SELECT SECTION
    // =====================================================

    private void SelectSection(string section)
    {
        activeSection = section;

        foreach (string name in navigationNames)
        {
            if (!buttons.TryGetValue(name, out Button button) || button == null)
                continue;

            bool selected = name == SectionButtonName(section);
            SetButtonColor(
                name,
                selected
                    ? new Color(0.09f, 0.39f, 0.76f, 0.98f)
                    : new Color(0.035f, 0.11f, 0.19f, 0.55f)
            );
        }

        SetText(
            "TxtCrumb",
            section + (selectedAsset != null ? " > " + assetName : "")
        );

        OnSectionSelected.Invoke(section);

        if (section != "Tổng quan" &&
            section != "Quản lý vòng đời" &&
            section != "Bản đồ tuyến" &&
            section != "Trợ lý AI" &&
            section != "Tài sản")
        {
            Notify(
                section,
                "Khu vực đã được chọn. Hãy kết nối module nghiệp vụ tương ứng để mở chức năng đầy đủ."
            );
        }
    }

    private string SectionButtonName(string section)
    {
        switch (section)
        {
            case "Tổng quan": return "BtnNavOverview";
            case "Quản lý vòng đời": return "BtnNavLifecycle";
            case "Quy hoạch & yêu cầu": return "BtnNavPlanning";
            case "Thiết kế (BIM)": return "BtnNavDesign";
            case "Thi công & nghiệm thu": return "BtnNavBuild";
            case "Vận hành & giám sát": return "BtnNavOps";
            case "Bảo trì & sửa chữa": return "BtnNavMaint";
            case "Nâng cấp & kết thúc": return "BtnNavUpgrade";
            case "Tài sản": return "BtnNavAssets";
            case "Bản đồ tuyến": return "BtnNavMap";
            case "Trợ lý AI": return "BtnNavAI";
            default: return "BtnNavReport";
        }
    }


    // =====================================================
    // 37. SELECT ASSET TAB
    // =====================================================

    private void SelectAssetTab(string tab)
    {
        activeTab = tab;
        SetTabColor("TabGeneral", tab == "General");
        SetTabColor("TabLifecycle", tab == "Lifecycle");
        SetTabColor("TabDocs", tab == "Docs");

        if (tab == "General")
            PopulateGeneralTab();
        else if (tab == "Lifecycle")
            PopulateLifecycleTab();
        else
            PopulateDocumentsTab();
    }

    private void PopulateGeneralTab()
    {
        // RailwayElementUI quản lý dữ liệu động.
        if (railwayElementUI != null)
            return;

        string[] keys =
        {
            "Chiều dài đoạn:", "Cấu tạo:", "Loại cấu kiện:",
            "Vị trí lắp đặt:", "Tiêu chuẩn:", "Ngày lắp đặt:",
            "Tình trạng hiện tại:"
        };

        string[] values =
        {
            "Chưa liên kết dữ liệu", "Chưa liên kết dữ liệu", assetType,
            assetSegment, "Chưa liên kết dữ liệu", "Chưa liên kết dữ liệu",
            assetStatus
        };

        SetSpecRows(keys, values);
    }

    private void PopulateLifecycleTab()
    {
        string[] keys =
        {
            "Giai đoạn:", "Mốc thời gian:", "Trạng thái:",
            "Đơn vị phụ trách:", "Hồ sơ:", "Lần cập nhật:", "Ghi chú:"
        };

        string[] values =
        {
            "Chưa liên kết dữ liệu", "Chưa có dữ liệu", "Chưa xác định",
            "Chưa có dữ liệu", "Chưa liên kết hồ sơ", "Chưa có dữ liệu",
            "Dữ liệu vòng đời sẽ lấy từ BIM/CMMS"
        };

        SetSpecRows(keys, values);
    }

    private void PopulateDocumentsTab()
    {
        string[] keys =
        {
            "Bản vẽ thiết kế:", "Mô hình BIM:", "Biên bản nghiệm thu:",
            "Phiếu kiểm tra:", "Hồ sơ bảo trì:", "Tiêu chuẩn áp dụng:",
            "Tệp đính kèm:"
        };

        string[] values =
        {
            "Chưa liên kết tệp", "Đang dùng mô hình trong Scene",
            "Chưa liên kết tệp", "Chưa liên kết tệp", "Chưa liên kết tệp",
            "Chưa liên kết tệp", "Không có tệp đính kèm"
        };

        SetSpecRows(keys, values);
    }

    private void SetSpecRows(string[] keys, string[] values)
    {
        for (int i = 0; i < 7; i++)
        {
            SetText("SpecK" + i, i < keys.Length ? keys[i] : "");
            SetText("SpecV" + i, i < values.Length ? values[i] : "");
        }
    }

    private void SetTabColor(string name, bool active)
    {
        SetButtonColor(
            name,
            active
                ? new Color(0.10f, 0.39f, 0.72f, 1f)
                : new Color(0.07f, 0.16f, 0.25f, 0.95f)
        );
    }


    // =====================================================
    // 38. LIFECYCLE STAGE
    // =====================================================

    private void SelectLifecycleStage(string stage)
    {
        string[] names = { "Stg1", "Stg2", "Stg3", "Stg4", "Stg5", "Stg6" };
        foreach (string name in names)
        {
            Transform t = FindDeep(transform, name);
            if (t != null)
                t.localScale = Vector3.one;
        }

        string selectedName =
            stage == "Quy hoạch" ? "Stg1" :
            stage == "Thiết kế" ? "Stg2" :
            stage == "Thi công" ? "Stg3" :
            stage == "Vận hành" ? "Stg4" :
            stage == "Bảo trì" ? "Stg5" : "Stg6";

        Transform selected = FindDeep(transform, selectedName);
        if (selected != null)
            selected.localScale = Vector3.one * 1.04f;

        SetText("TxtLife", "Vòng đời tài sản • " + stage);
        OnLifecycleStageSelected.Invoke(stage);
    }


    // =====================================================
    // 39. SELECT TOOL
    // =====================================================

    private void SelectTool(string tool)
    {
        activeTool = tool;

        string[] names =
        {
            "ToolMove", "ToolOrbit", "ToolSelect", "ToolMeasure",
            "ToolNote", "ToolClip", "ToolStats", "ToolExitVR"
        };

        foreach (string name in names)
            SetButtonColor(name, new Color(0f, 0f, 0f, 0f));

        string selectedName =
            tool.StartsWith("Di chuyển") ? "ToolMove" :
            tool == "Xoay nhìn" ? "ToolOrbit" :
            tool == "Chọn đối tượng" ? "ToolSelect" :
            tool == "Đo khoảng cách" ? "ToolMeasure" :
            tool == "Ghi chú / Ảnh" ? "ToolNote" :
            tool == "Cắt lớp (BIM)" ? "ToolClip" :
            tool == "Hiển thị dữ liệu" ? "ToolStats" : "ToolExitVR";

        SetButtonColor(selectedName, new Color(0.08f, 0.36f, 0.63f, 0.85f));
        OnToolSelected.Invoke(tool);

        Notify(
            "Công cụ: " + tool,
            "Đã chọn công cụ. Hãy kết nối với BIM/camera controller để thực thi thao tác trên mô hình."
        );
    }


    // =====================================================
    // 40. DASHBOARD ACTIONS
    // =====================================================

    private void RequestViewInBim()
    {
        if (selectedAsset == null)
        {
            Notify(
                "Xem trên BIM",
                "Chưa có cấu kiện được chọn. Hãy chọn một đối tượng trong mô hình 3D trước."
            );
            return;
        }

        OnViewInBimRequested.Invoke(selectedAsset);
        Notify("Xem trên BIM", "Đã gửi đối tượng đang chọn tới sự kiện OnViewInBimRequested.");
    }

    private void RequestInspectionHistory()
    {
        OnInspectionHistoryRequested.Invoke();
        Notify(
            "Lịch sử kiểm tra",
            "Đã gửi yêu cầu mở lịch sử kiểm tra. Cần kết nối dữ liệu kiểm định để hiển thị hồ sơ thực tế."
        );
    }

    private void RequestMaintenancePlan()
    {
        OnMaintenancePlanRequested.Invoke();
        Notify(
            "Kế hoạch bảo trì",
            "Đã gửi yêu cầu lập kế hoạch. Cần kết nối module bảo trì/CMMS để lưu kế hoạch thực tế."
        );
    }

    private void RequestVoiceInput()
    {
        OnVoiceInputRequested.Invoke();
        Notify(
            "Nhập giọng nói",
            "Nút micro đã nhận thao tác. Cần kết nối dịch vụ nhận dạng giọng nói để chuyển âm thanh thành văn bản."
        );
    }


    // =====================================================
    // 41. CHAT
    // =====================================================

    private void SubmitChat()
    {
        if (chatInput == null)
            return;

        string message = chatInput.text.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        SetPanelActive(aiPanel, true);
        SetText("TxtUserMsg", message);
        SetText("TxtAiResultTitle", "Đã nhận câu hỏi");
        SetText("TxtAiLine1", "Câu hỏi đã được ghi nhận trong giao diện.");
        SetText("TxtAiLine2", "Kết nối API AI để nhận phân tích và câu trả lời thực tế.");

        OnAiMessageSubmitted.Invoke(message);
        chatInput.text = "";

        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = FindDeep(transform, "ChatScroll")?.GetComponent<ScrollRect>();
        if (scroll != null)
            scroll.verticalNormalizedPosition = 0f;
    }

    private void Notify(string title, string line)
    {
        SetText("TxtAiResultTitle", title);
        SetText("TxtAiLine1", line);
        SetText(
            "TxtAiLine2",
            "Chức năng nghiệp vụ cần được kết nối với module dữ liệu tương ứng."
        );
        SetPanelActive(aiPanel, true);
    }


    // =====================================================
    // 42. ASSISTANT MINIMIZE / MAXIMIZE
    // =====================================================

    private void MinimizeAssistant()
    {
        if (aiPanelRect == null)
            return;

        aiMinimized = true;
        aiPanelRect.sizeDelta = new Vector2(aiPanelRect.sizeDelta.x, 36f);

        SetPanelActive(FindDeep(aiPanel, "ChatScroll"), false);
        SetPanelActive(FindDeep(aiPanel, "InpAiChat"), false);
        SetPanelActive(FindDeep(aiPanel, "BtnMic"), false);
        SetPanelActive(FindDeep(aiPanel, "BtnSend"), false);
    }

    private void MaximizeAssistant()
    {
        if (aiPanelRect == null)
            return;

        bool restore = aiPanelRect.sizeDelta.y > aiExpandedHeight + 1f;
        aiMinimized = false;

        float targetPanelHeight = restore
            ? aiExpandedHeight
            : Mathf.Min(580f, Mathf.Max(aiExpandedHeight, Screen.height * 0.62f));

        float targetChatHeight = restore
            ? chatExpandedHeight
            : Mathf.Max(chatExpandedHeight, targetPanelHeight - 108f);

        aiPanelRect.sizeDelta = new Vector2(aiPanelRect.sizeDelta.x, targetPanelHeight);

        if (chatScrollRect != null)
            chatScrollRect.sizeDelta = new Vector2(chatScrollRect.sizeDelta.x, targetChatHeight);

        SetPanelActive(FindDeep(aiPanel, "ChatScroll"), true);
        SetPanelActive(FindDeep(aiPanel, "InpAiChat"), true);
        SetPanelActive(FindDeep(aiPanel, "BtnMic"), true);
        SetPanelActive(FindDeep(aiPanel, "BtnSend"), true);
    }


    // =====================================================
    // 43. BUTTON COLOR
    // =====================================================

    private void SetButtonColor(string name, Color color)
    {
        if (!buttons.TryGetValue(name, out Button button) || button == null)
            return;

        if (button.targetGraphic is Graphic graphic)
            graphic.color = color;
    }


    // =====================================================
    // 44. TEXT + FIND DEEP
    // =====================================================

    private void SetText(string name, string value)
    {
        Transform t = FindDeep(transform, name);
        TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
        if (text != null)
            text.text = value;
    }

    private static Transform FindDeep(Transform parent, string objectName)
    {
        if (parent == null)
            return null;

        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }


    // =====================================================
    // SELECT COMPONENT FROM COMPONENT LIST
    // =====================================================

    /// <summary>
    /// Được BimComponentListUI gọi khi người dùng click một dòng cấu kiện.
    /// </summary>
    public void SelectComponentFromList(GameObject meshObject)
    {
        if (!isDetailMode || meshObject == null)
            return;

        if (!IsInsideModel(meshObject, detailModelRoot))
        {
            Debug.LogWarning(
                "Dashboard: Cấu kiện được chọn không thuộc DetailModelRoot.",
                meshObject
            );
            return;
        }

        ElementIdentity identity = meshObject.GetComponent<ElementIdentity>();
        string meshID = identity != null &&
                        !string.IsNullOrWhiteSpace(identity.ElementID)
            ? identity.ElementID.Trim()
            : meshObject.name.Trim();

        ShowComponent(activeModelID, meshID, meshObject);

        // Đưa tâm Orbit đến Bounds của riêng cấu kiện vừa chọn.
        if (detailOrbitZoom != null && detailOrbitZoom.enabled)
        {
            Bounds selectedBounds = CalculateModelBounds(meshObject);
            detailOrbitZoom.EnableForTarget(meshObject.transform, selectedBounds);
        }

        Debug.Log("Dashboard: Đã chọn từ danh sách: " + meshID, meshObject);
    }
}
