
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BimComponentListUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform content;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button itemButtonPrefab;
    [SerializeField] private TMP_Text countText;

    [Header("Appearance")]
    [SerializeField] private Color selectedColor =
        new Color(1f, 0.78f, 0.2f, 1f);

    private VRBimAiDashboardController dashboard;

    private readonly List<ComponentEntry> entries =
        new List<ComponentEntry>();

    private string currentSearch = "";

    private sealed class ComponentEntry
    {
        public GameObject target;
        public string meshID;
        public string displayName;
        public Button button;
        public Graphic graphic;
        public Color originalColor;
    }

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(
                ApplySearch
            );
        }
    }

    private void OnDestroy()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(
                ApplySearch
            );
        }
    }

    // =========================================
    // SHOW LIST
    // =========================================

    public void Show(
        GameObject modelRoot,
        VRBimAiDashboardController controller)
    {
        if (modelRoot == null)
        {
            Debug.LogWarning(
                "ComponentListUI: Model Root is null."
            );
            return;
        }

        dashboard = controller;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        BuildList(modelRoot);
    }

    // =========================================
    // HIDE LIST
    // =========================================

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    // =========================================
    // BUILD COMPONENT LIST
    // =========================================

    private void BuildList(GameObject modelRoot)
    {
        entries.Clear();

        if (content == null)
        {
            Debug.LogError(
                "ComponentListUI: Chưa gán Content."
            );
            return;
        }

        if (itemButtonPrefab == null)
        {
            Debug.LogError(
                "ComponentListUI: Chưa gán Button Prefab."
            );
            return;
        }

        // Xóa danh sách được tạo lần trước.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }

        // Lấy Renderer trong model.
        Renderer[] renderers =
            modelRoot.GetComponentsInChildren<Renderer>(false);

        HashSet<GameObject> addedObjects =
            new HashSet<GameObject>();

        List<GameObject> meshObjects =
            new List<GameObject>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            GameObject meshObject = renderer.gameObject;

            if (!meshObject.activeInHierarchy)
                continue;

            // Chỉ lấy MeshRenderer và SkinnedMeshRenderer.
            bool isValidMesh = false;

            if (renderer is MeshRenderer)
            {
                MeshFilter meshFilter =
                    meshObject.GetComponent<MeshFilter>();

                isValidMesh =
                    meshFilter != null &&
                    meshFilter.sharedMesh != null;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;

                isValidMesh =
                    skinned.sharedMesh != null;
            }

            if (!isValidMesh)
                continue;

            if (addedObjects.Add(meshObject))
            {
                meshObjects.Add(meshObject);
            }
        }

        // Sắp xếp theo tên.
        meshObjects.Sort(
            (a, b) => string.Compare(
                a.name,
                b.name,
                StringComparison.OrdinalIgnoreCase
            )
        );

        // Tạo từng dòng trong danh sách.
        foreach (GameObject meshObject in meshObjects)
        {
            CreateItem(meshObject);
        }

        ApplySearch(currentSearch);

        Debug.Log(
            "ComponentListUI: Đã tạo danh sách " +
            entries.Count + " cấu kiện."
        );
    }

    // =========================================
    // CREATE ITEM BUTTON
    // =========================================

    private void CreateItem(GameObject meshObject)
    {
        ElementIdentity identity =
            meshObject.GetComponent<ElementIdentity>();

        string meshID =
            identity != null &&
            !string.IsNullOrWhiteSpace(identity.ElementID)
                ? identity.ElementID.Trim()
                : meshObject.name.Trim();

        Button button = Instantiate(
            itemButtonPrefab,
            content,
            false
        );

        button.gameObject.name =
            "Item_" + meshObject.name;

        button.interactable = true;

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.text = meshObject.name;
            label.raycastTarget = false;
        }
        else
        {
            Debug.LogWarning(
                "ComponentItemButton chưa có TMP_Text."
            );
        }

        Graphic graphic = button.targetGraphic;

        if (graphic == null)
            graphic = button.GetComponent<Image>();

        ComponentEntry entry = new ComponentEntry
        {
            target = meshObject,
            meshID = meshID,
            displayName = meshObject.name,
            button = button,
            graphic = graphic,
            originalColor = graphic != null
                ? graphic.color
                : Color.white
        };

        entries.Add(entry);

        // Gắn sự kiện click.
        button.onClick.RemoveAllListeners();

        button.onClick.AddListener(
            () => SelectComponent(entry)
        );
    }

    // =========================================
    // SELECT COMPONENT
    // =========================================

    private void SelectComponent(ComponentEntry entry)
    {
        if (
            entry == null ||
            entry.target == null ||
            dashboard == null
        )
        {
            return;
        }

        // Đổi màu dòng đang được chọn.
        foreach (ComponentEntry item in entries)
        {
            if (item.graphic == null)
                continue;

            item.graphic.color =
                item == entry
                    ? selectedColor
                    : item.originalColor;
        }

        // Gửi đối tượng sang Dashboard.
        dashboard.SelectComponentFromList(
            entry.target
        );
    }

    // =========================================
    // SEARCH
    // =========================================

    public void ApplySearch(string value)
    {
        currentSearch = value ?? "";

        int visibleCount = 0;

        foreach (ComponentEntry entry in entries)
        {
            if (entry.button == null)
                continue;

            bool matches =
                string.IsNullOrWhiteSpace(currentSearch) ||
                entry.displayName.IndexOf(
                    currentSearch.Trim(),
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                entry.meshID.IndexOf(
                    currentSearch.Trim(),
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            entry.button.gameObject.SetActive(matches);

            if (matches)
                visibleCount++;
        }

        if (countText != null)
        {
            countText.text =
                "Hiển thị " + visibleCount +
                " / " + entries.Count + " cấu kiện";
        }
    }

    // =========================================
    // CLEAR
    // =========================================

    public void ClearList()
    {
        entries.Clear();

        if (content != null)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        if (countText != null)
            countText.text = "0 cấu kiện";
    }
}
