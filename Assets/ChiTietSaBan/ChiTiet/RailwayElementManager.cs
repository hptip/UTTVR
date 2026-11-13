
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RailwayElementUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text idText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Transform content;
    [SerializeField] private ElementInfoRow rowPrefab;
    [SerializeField] private Button closeButton;

    [Header("Display")]
    [SerializeField] private string preferredTitleColumn = "Loại cấu kiện";

    private string selectedModelID;
    private string selectedMeshID;

    private void Awake()
    {
        ValidateReferences();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HidePanel);
            closeButton.onClick.AddListener(HidePanel);
        }

        if (panel != null)
            panel.SetActive(false);
    }

    private void OnEnable()
    {
        RailwayDatabase.DatabaseChanged += RefreshSelectedElement;
    }

    private void OnDisable()
    {
        RailwayDatabase.DatabaseChanged -= RefreshSelectedElement;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    // Gọi khi chọn model 1 mesh.
    // Truy vấn dòng tổng thể: ModelID = MeshID.
    public void ShowModelSummary(string modelID)
    {
        ShowData(modelID, modelID);
    }

    // Gọi khi chọn một mesh con trong model DuongRay.
    public void ShowComponent(string modelID, string meshID)
    {
        ShowData(modelID, meshID);
    }

    // Giữ tương thích với những nơi cũ còn gọi ShowElement.
    public void ShowElement(string id)
    {
        ShowModelSummary(id);
    }

    private void ShowData(string modelID, string meshID)
    {
        if (string.IsNullOrWhiteSpace(modelID) ||
            string.IsNullOrWhiteSpace(meshID))
        {
            Debug.LogWarning("RailwayElementUI: ModelID hoặc MeshID bị rỗng.", this);
            return;
        }

        if (!ValidateReferences())
            return;

        selectedModelID = modelID.Trim();
        selectedMeshID = meshID.Trim();

        panel.SetActive(true);
        SetStatus("Đang tải dữ liệu...");

        RefreshSelectedElement();
    }

    public void RefreshSelectedElement()
    {
        if (string.IsNullOrWhiteSpace(selectedModelID) ||
            string.IsNullOrWhiteSpace(selectedMeshID))
            return;

        if (!ValidateReferences())
            return;

        ClearRows();

        RailwayDatabase database = RailwayDatabase.Instance;

        if (database == null)
        {
            SetHeader("Không tìm thấy Database", selectedMeshID);
            SetStatus("Không tìm thấy RailwayDatabase.Instance.");
            return;
        }

        Dictionary<string, string> data;

        try
        {
            data = database.GetElementDataByModelAndMesh(
                selectedModelID,
                selectedMeshID
            );
        }
        catch (Exception ex)
        {
            SetHeader("Lỗi đọc dữ liệu", selectedMeshID);
            SetStatus("Truy vấn SQLite thất bại: " + ex.Message);
            Debug.LogException(ex, this);
            return;
        }

        if (data == null || data.Count == 0)
        {
            SetHeader("Không tìm thấy dữ liệu", selectedMeshID);

            SetStatus(
                "Không có bản ghi khớp ModelID = " + selectedModelID +
                " và MeshID = " + selectedMeshID
            );

            Debug.LogWarning(
                "Không tìm thấy dữ liệu: ModelID = " + selectedModelID +
                ", MeshID = " + selectedMeshID,
                this
            );

            return;
        }

        string displayTitle = GetValueIgnoreCase(
            data,
            preferredTitleColumn
        );

        if (string.IsNullOrWhiteSpace(displayTitle))
            displayTitle = selectedMeshID;

        SetHeader(displayTitle, selectedMeshID);

        int createdRows = 0;

        foreach (KeyValuePair<string, string> item in data)
        {
            // Không hiển thị hai cột khóa trong danh sách thuộc tính.
            if (string.Equals(item.Key, "ModelID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Key, "MeshID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Key, "ID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ElementInfoRow row = Instantiate(rowPrefab, content, false);

            if (row == null)
            {
                SetStatus("Không thể tạo ElementInfoRow.");
                Debug.LogError("Không Instantiate được rowPrefab.", this);
                return;
            }

            row.Set(item.Key, item.Value);
            createdRows++;
        }

        Canvas.ForceUpdateCanvases();

        RectTransform contentRect = content as RectTransform;

        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        SetStatus("Đã tải " + createdRows + " thuộc tính từ SQLite.");
    }

    public void HidePanel()
    {
        selectedModelID = null;
        selectedMeshID = null;

        ClearRows();

        if (panel != null)
            panel.SetActive(false);
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (panel == null)
        {
            Debug.LogError("RailwayElementUI: Chưa gán Panel.", this);
            valid = false;
        }

        if (content == null)
        {
            Debug.LogError("RailwayElementUI: Chưa gán Content.", this);
            valid = false;
        }

        if (rowPrefab == null)
        {
            Debug.LogError("RailwayElementUI: Chưa gán ElementInfoRow Prefab.", this);
            valid = false;
        }

        return valid;
    }

    private void SetHeader(string title, string id)
    {
        if (titleText != null)
            titleText.text = title ?? string.Empty;

        if (idText != null)
            idText.text = "ModelID: " + selectedModelID +
                          " | MeshID: " + id;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    private void ClearRows()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    private static string GetValueIgnoreCase(
        Dictionary<string, string> data,
        string key)
    {
        if (data == null || string.IsNullOrWhiteSpace(key))
            return null;

        foreach (KeyValuePair<string, string> item in data)
        {
            if (string.Equals(
                item.Key,
                key,
                StringComparison.OrdinalIgnoreCase))
            {
                return item.Value;
            }
        }

        return null;
    }
}
