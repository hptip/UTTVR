using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraInfoBoard : MonoBehaviour
{
    [Header("Nội dung (ghi ở đây)")]
    public string title = "Tiêu đề";
    [TextArea(4, 12)]
    public string content = "Dòng thông tin 1\nDòng thông tin 2\nDòng thông tin 3";

    [Header("Vị trí so với camera (mét)")]
    public Camera cam;                                            // để trống = Main Camera
    public Vector3 localPosition = new Vector3(0.6f, 0.3f, 2f);   // X: phải/trái, Y: lên/xuống, Z: xa/gần

    [Header("Kích thước")]
    public float worldWidth = 0.8f;                               // bề rộng bảng (mét)
    public Vector2 pixelSize = new Vector2(400f, 250f);           // tỉ lệ khung

    [Header("Giao diện")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Color textColor = Color.white;
    public int titleFontSize = 32;
    public int bodyFontSize = 22;
    public float padding = 20f;
    public TMP_FontAsset font;                                    // kéo font tiếng Việt vào đây (tuỳ chọn)

    private GameObject _root;
    private RectTransform _canvasRect;
    private Image _bg;
    private TextMeshProUGUI _titleTMP;
    private TextMeshProUGUI _bodyTMP;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[CameraInfoBoard] Không tìm thấy camera. Hãy gán Cam trong Inspector.");
            enabled = false;
            return;
        }
        Build();
        Apply();
    }

    private void Build()
    {
        // Canvas World Space, là con của camera
        _root = new GameObject("InfoBoardCanvas");
        _root.transform.SetParent(cam.transform, false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        _canvasRect = _root.GetComponent<RectTransform>();

        // Khung nền
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        _bg = panel.GetComponent<Image>();
        Stretch(panel.GetComponent<RectTransform>());

        // Tiêu đề
        _titleTMP = CreateText("TitleText", panel.transform);
        _titleTMP.fontStyle = FontStyles.Bold;
        _titleTMP.alignment = TextAlignmentOptions.Center;

        // Nội dung
        _bodyTMP = CreateText("BodyText", panel.transform);
        _bodyTMP.alignment = TextAlignmentOptions.TopLeft;
    }

    private TextMeshProUGUI CreateText(string objName, Transform parent)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Cập nhật toàn bộ giao diện theo các giá trị trong Inspector
    private void Apply()
    {
        if (_root == null) return;

        // Vị trí + kích thước
        _canvasRect.localPosition = localPosition;
        _canvasRect.localRotation = Quaternion.identity;
        _canvasRect.sizeDelta = pixelSize;
        float scale = worldWidth / Mathf.Max(1f, pixelSize.x);
        _canvasRect.localScale = new Vector3(scale, scale, scale);

        // Màu nền
        _bg.color = backgroundColor;

        // Tiêu đề: nằm sát phía trên
        var tr = _titleTMP.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(padding, -(padding + titleFontSize * 1.4f));
        tr.offsetMax = new Vector2(-padding, -padding);

        // Nội dung: phủ phần còn lại bên dưới tiêu đề
        var br = _bodyTMP.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(padding, padding);
        br.offsetMax = new Vector2(-padding, -(padding * 2f + titleFontSize * 1.4f));

        // Chữ
        foreach (var t in new[] { _titleTMP, _bodyTMP })
        {
            t.color = textColor;
            if (font != null) t.font = font;
        }
        _titleTMP.fontSize = titleFontSize;
        _bodyTMP.fontSize = bodyFontSize;
        _titleTMP.text = title;
        _bodyTMP.text = content;
    }

    // Gọi từ script khác khi muốn đổi nội dung lúc đang chạy
    public void SetContent(string newTitle, string newContent)
    {
        title = newTitle;
        content = newContent;
        Apply();
    }

    public void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    // Sửa Inspector khi đang Play thì bảng cập nhật ngay
    private void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }
}