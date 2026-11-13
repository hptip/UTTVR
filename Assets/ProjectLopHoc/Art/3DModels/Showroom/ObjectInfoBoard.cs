using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ObjectInfoBoard : MonoBehaviour
{
    [Header("Nội dung (ghi ở đây)")]
    public string title = "Tiêu đề";
    [TextArea(4, 12)]
    public string content = "Dòng thông tin 1\nDòng thông tin 2\nDòng thông tin 3";

    [Header("Vị trí")]
    public Camera cam;                                       // để trống = Main Camera
    public Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);  // bảng nổi cao hơn object bao nhiêu

    [Header("Kích thước")]
    public Vector2 pixelSize = new Vector2(400f, 250f);
    public bool keepConstantSize = true;                     // true = đi gần/xa bảng vẫn to như nhau
    public float sizeFactor = 0.0015f;                       // to/nhỏ khi bật keepConstantSize
    public float fixedScale = 0.005f;                        // dùng khi tắt keepConstantSize

    [Header("Giao diện")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Color textColor = Color.white;
    public int titleFontSize = 32;
    public int bodyFontSize = 22;
    public float padding = 20f;
    public TMP_FontAsset font;                               // font hỗ trợ tiếng Việt (tuỳ chọn)

    private GameObject _root;
    private RectTransform _rect;
    private Image _bg;
    private TextMeshProUGUI _titleTMP;
    private TextMeshProUGUI _bodyTMP;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        Build();
        Apply();
    }

    private void Build()
    {
        // Canvas World Space, KHÔNG làm con của object (tránh bị méo theo scale/xoay của object)
        _root = new GameObject("InfoBoard_" + gameObject.name);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        _rect = _root.GetComponent<RectTransform>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        _bg = panel.GetComponent<Image>();
        _bg.raycastTarget = false;
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;

        _titleTMP = CreateText("TitleText", panel.transform);
        _titleTMP.fontStyle = FontStyles.Bold;
        _titleTMP.alignment = TextAlignmentOptions.Center;

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

    private void Apply()
    {
        if (_root == null) return;

        _rect.sizeDelta = pixelSize;
        _bg.color = backgroundColor;

        var tr = _titleTMP.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(padding, -(padding + titleFontSize * 1.4f));
        tr.offsetMax = new Vector2(-padding, -padding);

        var br = _bodyTMP.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(padding, padding);
        br.offsetMax = new Vector2(-padding, -(padding * 2f + titleFontSize * 1.4f));

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

    private void LateUpdate()
    {
        if (_root == null) return;

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // Nổi phía trên object
        Vector3 pos = transform.position + worldOffset;
        _rect.position = pos;

        // Luôn quay mặt về phía camera
        _rect.rotation = cam.transform.rotation;

        // Giữ kích thước không đổi dù xa hay gần
        float s = fixedScale;
        if (keepConstantSize)
        {
            float dist = Vector3.Dot(pos - cam.transform.position, cam.transform.forward);
            s = Mathf.Max(0.01f, dist) * sizeFactor;
        }
        _rect.localScale = new Vector3(s, s, s);
    }

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

    private void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    private void OnEnable()
    {
        if (_root != null) _root.SetActive(true);
    }

    private void OnDisable()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root);
    }
}