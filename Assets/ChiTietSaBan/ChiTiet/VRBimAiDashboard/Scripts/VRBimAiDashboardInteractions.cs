using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Adds runtime click behaviour to the navigation, lifecycle and asset-information
/// controls in the VRBimAiDashboard prefab.
/// Attach this component to the VRBimAiDashboard root object.
/// </summary>
[DisallowMultipleComponent]
public class VRBimAiDashboardInteractions : MonoBehaviour
{
    private static readonly string[] NavigationButtons =
    {
        "BtnNavOverview", "BtnNavLifecycle", "BtnNavPlanning", "BtnNavDesign",
        "BtnNavBuild", "BtnNavOps", "BtnNavMaint", "BtnNavUpgrade",
        "BtnNavAssets", "BtnNavMap", "BtnNavAI", "BtnNavReport"
    };

    private static readonly string[] StageButtons =
    {
        "Stg1", "Stg2", "Stg3", "Stg4", "Stg5", "Stg6"
    };

    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    private readonly Dictionary<Button, ColorBlock> originalColors = new Dictionary<Button, ColorBlock>();

    private readonly TextMeshProUGUI[] specKeys = new TextMeshProUGUI[7];
    private readonly TextMeshProUGUI[] specValues = new TextMeshProUGUI[7];
    private string[] generalKeys;
    private string[] generalValues;

    private GameObject toastObject;
    private TextMeshProUGUI toastText;
    private CanvasGroup toastCanvasGroup;
    private Coroutine toastCoroutine;

    private static readonly Color SelectedNormal = new Color(0.08f, 0.36f, 0.72f, 1f);
    private static readonly Color SelectedHighlight = new Color(0.12f, 0.48f, 0.88f, 1f);
    private static readonly Color SelectedPressed = new Color(0.04f, 0.25f, 0.56f, 1f);

    private void Awake()
    {
        EnsureGraphicRaycaster();
    }

    private void Start()
    {
        CacheButtonsAndColors();
        CacheAssetTable();
        BindNavigationButtons();
        BindStageButtons();
        BindAssetButtons();
        BindHeaderAndToolbarButtons();

        // Keep the same initial selection as the supplied dashboard screenshot.
        SelectButton("BtnNavLifecycle");
        SelectButton("TabGeneral");
    }

    private void EnsureGraphicRaycaster()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning(
                "[VRBimAiDashboard] Chua tim thay EventSystem. Hay them EventSystem vao Scene " +
                "de nhan click UI.", this);
        }
    }

    private void CacheButtonsAndColors()
    {
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        foreach (Button button in allButtons)
        {
            if (button == null) continue;
            buttons[button.gameObject.name] = button;
            originalColors[button] = button.colors;
        }
    }

    private void CacheAssetTable()
    {
        for (int i = 0; i < 7; i++)
        {
            Transform key = FindDescendant(transform, "SpecK" + i);
            Transform value = FindDescendant(transform, "SpecV" + i);
            if (key != null) specKeys[i] = key.GetComponent<TMP_Text>() as TextMeshProUGUI;
            if (value != null) specValues[i] = value.GetComponent<TMP_Text>() as TextMeshProUGUI;
        }

        generalKeys = new string[7];
        generalValues = new string[7];
        for (int i = 0; i < 7; i++)
        {
            generalKeys[i] = specKeys[i] != null ? specKeys[i].text : string.Empty;
            generalValues[i] = specValues[i] != null ? specValues[i].text : string.Empty;
        }
    }

    private void BindNavigationButtons()
    {
        Bind("BtnNavOverview", () =>
        {
            SelectButton("BtnNavOverview");
            SetPanelActive("AssetPanel", true);
            SetPanelActive("MapPanel", true);
            SetPanelActive("LifecycleBar", true);
            ShowToast("Đã chuyển về Tổng quan.");
        });

        Bind("BtnNavLifecycle", () =>
        {
            SelectButton("BtnNavLifecycle");
            SetPanelActive("LifecycleBar", true);
            ShowToast("Đã chọn Quản lý vòng đời.");
        });

        Bind("BtnNavAssets", () =>
        {
            SelectButton("BtnNavAssets");
            SetPanelActive("AssetPanel", true);
            ShowToast("Đã mở Thông tin tài sản.");
        });

        Bind("BtnNavMap", () =>
        {
            SelectButton("BtnNavMap");
            SetPanelActive("MapPanel", true);
            ShowToast("Đã mở Bản đồ tuyến.");
        });

        Bind("BtnNavAI", () =>
        {
            SelectButton("BtnNavAI");
            Transform panel = FindDescendant(transform, "AiPanel");
            if (panel != null) panel.gameObject.SetActive(!panel.gameObject.activeSelf);
            ShowToast("Đã chuyển trạng thái Trợ lý AI.");
        });

        BindModuleNotice("BtnNavPlanning", "Quy hoạch & yêu cầu");
        BindModuleNotice("BtnNavDesign", "Thiết kế BIM");
        BindModuleNotice("BtnNavBuild", "Thi công & nghiệm thu");
        BindModuleNotice("BtnNavOps", "Vận hành & giám sát");
        BindModuleNotice("BtnNavMaint", "Bảo trì & sửa chữa");
        BindModuleNotice("BtnNavUpgrade", "Nâng cấp & kết thúc");
        BindModuleNotice("BtnNavReport", "Báo cáo & phân tích");

        Bind("BtnAiClose", () =>
        {
            SetPanelActive("AiPanel", false);
            ShowToast("Đã đóng Trợ lý AI.");
        });
    }

    private void BindModuleNotice(string buttonName, string moduleName)
    {
        Bind(buttonName, () =>
        {
            SelectButton(buttonName);
            ShowToast("Đã chọn " + moduleName + ". Màn hình chi tiết của module này chưa có trong prefab.");
        });
    }

    private void BindStageButtons()
    {
        string[] stageNames =
        {
            "Quy hoạch (2024)", "Thiết kế (2025)", "Thi công (2026–2027)",
            "Vận hành (2028–)", "Bảo trì định kỳ", "Nâng cấp (2038+)"
        };

        for (int i = 0; i < StageButtons.Length; i++)
        {
            int stageIndex = i;
            string buttonName = StageButtons[i];
            Bind(buttonName, () =>
            {
                SelectStage(buttonName);
                ShowToast("Đã chọn giai đoạn: " + stageNames[stageIndex] + ".");
            });
        }
    }

    private void BindHeaderAndToolbarButtons()
    {
        Bind("IcHome", () =>
        {
            SelectButton("BtnNavOverview");
            SetPanelActive("AssetPanel", true);
            SetPanelActive("MapPanel", true);
            SetPanelActive("LifecycleBar", true);
            ShowToast("Đã trở về Tổng quan.");
        });

        Bind("BtnSettings", () => ShowToast("Đã chọn Cài đặt. Cần nối với màn hình cấu hình ứng dụng."));
        Bind("BtnWifi", () => ShowToast("Trạng thái kết nối cần được lấy từ hệ thống mạng của ứng dụng."));
        Bind("BtnCamera", () => ShowToast("Đã chọn Camera. Cần nối với bộ điều khiển camera trong Scene."));
        Bind("BtnVR", () => ShowToast("Đã chọn chế độ VR. Cần kết nối với XR Origin và XR Interaction Manager."));
        Bind("BtnMic", () => ShowToast("Đã chọn Micro. Cần tích hợp quyền microphone và dịch vụ nhận dạng giọng nói."));

        BindTool("ToolMove", "Di chuyển (WASD)");
        BindTool("ToolOrbit", "Xoay nhìn");
        BindTool("ToolSelect", "Chọn đối tượng");
        BindTool("ToolMeasure", "Đo khoảng cách");
        BindTool("ToolNote", "Ghi chú / Ảnh");
        BindTool("ToolClip", "Cắt lớp BIM");
        BindTool("ToolStats", "Hiển thị dữ liệu");
        BindTool("ToolExitVR", "Thoát VR");
    }

    private void BindTool(string buttonName, string toolLabel)
    {
        Bind(buttonName, () =>
        {
            SelectTool(buttonName);
            ShowToast("Đã chọn công cụ " + toolLabel + ". Cần nối với bộ điều khiển 3D để thao tác mô hình thực tế.");
        });
    }

    private void BindAssetButtons()
    {
        Bind("TabGeneral", () => ShowAssetTab("general"));
        Bind("TabLifecycle", () => ShowAssetTab("lifecycle"));
        Bind("TabDocs", () => ShowAssetTab("docs"));

        Bind("BtnViewBim", () =>
        {
            ShowToast("Đã chọn Xem trên BIM. Cần liên kết nút này với đối tượng BIM trong Scene để điều khiển mô hình 3D.");
        });

        Bind("BtnInspect", () =>
        {
            ShowToast("Đã chọn Kiểm tra. Cần kết nối với module kiểm tra/ghi nhận hiện trạng để lưu kết quả.");
        });

        Bind("BtnPlan", () =>
        {
            ShowToast("Đã chọn Lập kế hoạch bảo trì. Cần kết nối với module lập lịch để tạo kế hoạch thực tế.");
        });
    }

    private void Bind(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Button button;
        if (buttons.TryGetValue(buttonName, out button) && button != null)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            Debug.LogWarning("[VRBimAiDashboard] Khong tim thay Button: " + buttonName, this);
        }
    }

    private void ShowAssetTab(string tab)
    {
        SelectButton(tab == "general" ? "TabGeneral" : tab == "lifecycle" ? "TabLifecycle" : "TabDocs");

        if (tab == "general")
        {
            SetTable(generalKeys, generalValues);
            ShowToast("Đang hiển thị Thông tin chung.");
            return;
        }

        if (tab == "lifecycle")
        {
            SetTable(
                new string[]
                {
                    "Ngày đưa vào khai thác:", "Lần kiểm tra gần nhất:", "Kết quả kiểm tra:",
                    "Khuyến nghị:", "Kỳ kiểm tra tiếp theo:", "Trạng thái tài sản:", "Mã tài sản:"
                },
                new string[]
                {
                    "2028", "06/2026", "Bình thường", "Kiểm tra định kỳ",
                    "06/2027", "Đang khai thác", "TRK-001"
                });
            ShowToast("Đang hiển thị Hồ sơ vòng đời.");
            return;
        }

        SetTable(
            new string[]
            {
                "Bản vẽ thiết kế:", "Bản vẽ cấu tạo:", "Mô hình BIM:",
                "Hồ sơ nghiệm thu:", "Biên bản kiểm tra:", "Quy trình bảo trì:", "Tiêu chuẩn kỹ thuật:"
            },
            new string[]
            {
                "Chưa gắn tệp", "Chưa gắn tệp", "Chưa gắn tệp",
                "Chưa gắn tệp", "Chưa gắn tệp", "Chưa gắn tệp", "Chưa gắn tệp"
            });
        ShowToast("Đang hiển thị Tài liệu. Các tệp cần được liên kết trong dự án.");
    }

    private void SetTable(string[] keys, string[] values)
    {
        for (int i = 0; i < 7; i++)
        {
            if (specKeys[i] != null && keys != null && i < keys.Length) specKeys[i].text = keys[i];
            if (specValues[i] != null && values != null && i < values.Length) specValues[i].text = values[i];
        }
    }

    private void SelectButton(string selectedName)
    {
        if (selectedName.StartsWith("BtnNav"))
        {
            for (int i = 0; i < NavigationButtons.Length; i++)
                ApplyButtonSelection(NavigationButtons[i], NavigationButtons[i] == selectedName);
            return;
        }

        if (selectedName.StartsWith("Tab"))
        {
            ApplyButtonSelection("TabGeneral", selectedName == "TabGeneral");
            ApplyButtonSelection("TabLifecycle", selectedName == "TabLifecycle");
            ApplyButtonSelection("TabDocs", selectedName == "TabDocs");
        }
    }

    private void SelectStage(string selectedName)
    {
        for (int i = 0; i < StageButtons.Length; i++)
            ApplyButtonSelection(StageButtons[i], StageButtons[i] == selectedName);
    }

    private void SelectTool(string selectedName)
    {
        string[] toolNames = { "ToolMove", "ToolOrbit", "ToolSelect", "ToolMeasure", "ToolNote", "ToolClip", "ToolStats", "ToolExitVR" };
        for (int i = 0; i < toolNames.Length; i++)
            ApplyButtonSelection(toolNames[i], toolNames[i] == selectedName);
    }

    private void ApplyButtonSelection(string buttonName, bool selected)
    {
        Button button;
        if (!buttons.TryGetValue(buttonName, out button) || button == null) return;

        ColorBlock colors;
        if (!originalColors.TryGetValue(button, out colors)) colors = button.colors;

        if (selected)
        {
            colors.normalColor = SelectedNormal;
            colors.highlightedColor = SelectedHighlight;
            colors.pressedColor = SelectedPressed;
            colors.selectedColor = SelectedHighlight;
        }
        button.colors = colors;
    }

    private void SetPanelActive(string panelName, bool active)
    {
        Transform panel = FindDescendant(transform, panelName);
        if (panel != null) panel.gameObject.SetActive(active);
    }

    private Transform FindDescendant(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDescendant(parent.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }

    private void ShowToast(string message)
    {
        EnsureToast();
        if (toastText == null) return;

        toastText.text = message;
        toastObject.SetActive(true);
        toastCanvasGroup.alpha = 1f;

        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(HideToastAfterDelay());
    }

    private void EnsureToast()
    {
        if (toastObject != null) return;

        Canvas canvas = GetComponentInParent<Canvas>(true);
        Transform toastParent = canvas != null ? canvas.transform : transform;

        toastObject = new GameObject("DashboardInteractionToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        toastObject.transform.SetParent(toastParent, false);
        toastObject.transform.SetAsLastSibling();

        RectTransform rect = toastObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(720f, 64f);

        Image background = toastObject.GetComponent<Image>();
        background.color = new Color(0.025f, 0.09f, 0.17f, 0.96f);
        background.raycastTarget = false;

        toastCanvasGroup = toastObject.GetComponent<CanvasGroup>();
        toastCanvasGroup.blocksRaycasts = false;
        toastCanvasGroup.interactable = false;

        GameObject labelObject = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(toastObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 5f);
        labelRect.offsetMax = new Vector2(-18f, -5f);

        toastText = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_Text sourceText = GetComponentInChildren<TMP_Text>(true);
        if (sourceText != null) toastText.font = sourceText.font;
        toastText.fontSize = 20f;
        toastText.fontStyle = FontStyles.Bold;
        toastText.color = Color.white;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.enableWordWrapping = true;
        toastText.raycastTarget = false;

        toastObject.SetActive(false);
    }

    private IEnumerator HideToastAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.8f);
        if (toastCanvasGroup != null) toastCanvasGroup.alpha = 0f;
        if (toastObject != null) toastObject.SetActive(false);
        toastCoroutine = null;
    }
}
