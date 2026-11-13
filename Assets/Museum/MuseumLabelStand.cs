#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tạo bảng thuyết minh bảo tàng (Museum Exhibit Label Stand):
/// mặt bảng nghiêng + 2 cột inox + 2 đế tròn, có tiêu đề / phụ đề / nội dung nhiều cột / ảnh.
/// Yêu cầu: TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources).
/// Hỗ trợ Built-in RP và URP.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MuseumLabelStand : MonoBehaviour
{
    [System.Serializable]
    public class LabelImage
    {
        public Texture2D texture;
        [Tooltip("Vị trí TÂM ảnh trên mặt bảng, 0..1 (0,0 = góc dưới trái, 1,1 = góc trên phải)")]
        public Vector2 position = new Vector2(0.85f, 0.6f);
        [Tooltip("Kích thước ảnh (mét)")]
        public Vector2 size = new Vector2(0.25f, 0.18f);
    }

    // ---------------------------------------------------------------- Panel
    [Header("Mặt bảng")]
    [Min(0.1f)] public float panelWidth = 1.2f;
    [Min(0.1f)] public float panelHeight = 0.4f;
    [Min(0.005f)] public float panelThickness = 0.02f;
    [Range(0f, 80f)] public float tiltAngle = 20f;
    [Min(0f)] public float frameBorder = 0.01f;
    public Color frameColor = new Color(0.92f, 0.92f, 0.92f);
    public Color faceColor = Color.white;

    // ---------------------------------------------------------------- Stand
    [Header("Chân đứng")]
    [Min(0.1f)] public float poleHeight = 0.9f;
    [Min(0.05f)] public float poleSpacing = 0.8f;
    [Min(0.005f)] public float poleDiameter = 0.03f;
    [Min(0.05f)] public float baseDiameter = 0.3f;
    [Min(0.005f)] public float baseThickness = 0.02f;
    public Color metalColor = new Color(0.78f, 0.78f, 0.8f);
    [Range(0f, 1f)] public float metallic = 0.9f;
    [Range(0f, 1f)] public float smoothness = 0.65f;

    // --------------------------------------------------------------- Layout
    [Header("Bố cục chữ")]
    [Min(0f)] public float margin = 0.03f;
    [Min(0f)] public float gap = 0.012f;
    [Tooltip("Vùng chữ bắt đầu từ đâu (0..1 theo chiều ngang). Tăng lên để chừa chỗ bên trái cho ảnh.")]
    [Range(0f, 0.9f)] public float textRegionStart = 0f;
    [Tooltip("Vùng chữ rộng bao nhiêu (0..1). Giảm xuống để chừa chỗ bên phải cho ảnh.")]
    [Range(0.1f, 1f)] public float textRegionWidth = 0.75f;

    // ---------------------------------------------------------------- Title
    [Header("Tiêu đề")]
    [TextArea(1, 3)] public string titleText = "DISCOVER THE SECRETS OF EGYPT";
    public TMP_FontAsset titleFont;
    [Tooltip("Cỡ chữ tính bằng cm ngoài đời thật")]
    [Min(0.1f)] public float titleSizeCm = 4f;
    public Color titleColor = new Color(0.85f, 0.2f, 0.25f);
    public FontStyles titleStyle = FontStyles.Bold;
    public TextAlignmentOptions titleAlignment = TextAlignmentOptions.TopLeft;

    // ------------------------------------------------------------- Subtitle
    [Header("Phụ đề")]
    [TextArea(1, 3)] public string subtitleText = "A journey along the Nile";
    public TMP_FontAsset subtitleFont;
    [Min(0.1f)] public float subtitleSizeCm = 2.2f;
    public Color subtitleColor = new Color(0.4f, 0.4f, 0.4f);
    public FontStyles subtitleStyle = FontStyles.Italic;
    public TextAlignmentOptions subtitleAlignment = TextAlignmentOptions.TopLeft;

    // ----------------------------------------------------------------- Body
    [Header("Nội dung")]
    [TextArea(6, 20)]
    public string bodyText =
        "Ancient Egypt was one of the world's earliest civilizations, flourishing for over three thousand years " +
        "along the banks of the Nile River. Its monuments, writing system and religious beliefs continue to " +
        "fascinate people across the globe.\n\n" +
        "The objects on display here were found in tombs near Thebes and offer a rare glimpse into daily life, " +
        "craftsmanship and the Egyptian vision of the afterlife.";
    public TMP_FontAsset bodyFont;
    [Min(0.1f)] public float bodySizeCm = 1.8f;
    public Color bodyColor = new Color(0.15f, 0.15f, 0.15f);
    public FontStyles bodyStyle = FontStyles.Normal;
    public TextAlignmentOptions bodyAlignment = TextAlignmentOptions.TopJustified;
    [Range(-50f, 100f)] public float lineSpacing = 0f;
    [Range(0f, 50f)] public float paragraphSpacing = 0f;
    [Range(1, 4)] public int columns = 2;
    [Min(0f)] public float columnGap = 0.02f;

    // --------------------------------------------------------------- Images
    [Header("Hình ảnh")]
    public List<LabelImage> images = new List<LabelImage>();

    [Header("Khác")]
    public bool generateColliders = false;

    // =====================================================================
    const string RootName = "_Generated";
    const string MatName = "MLS_Generated";
    // TMP 3D: fontSize 1 ~ 0.1 m  =>  fontSize = cm * 0.1
    const float CmToTmpSize = 0.1f;

    void Start()
    {
        if (transform.Find(RootName) == null) Rebuild();
    }

    public void Rebuild()
    {
        Clear();

        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        BuildStand(root);

        var pivot = new GameObject("PanelPivot").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(0f, poleHeight, 0f);
        pivot.localRotation = Quaternion.Euler(tiltAngle, 0f, 0f);

        BuildPanel(pivot);
        BuildImages(pivot);
        BuildTexts(pivot);
    }

    // ------------------------------------------------------------------ Stand
    void BuildStand(Transform root)
    {
        var metal = MakeMat(metalColor, metallic, smoothness, null);
        float poleTop = poleHeight - panelThickness;
        float poleLen = Mathf.Max(0.01f, poleTop - baseThickness);

        for (int i = -1; i <= 1; i += 2)
        {
            float x = i * poleSpacing * 0.5f;

            Part(PrimitiveType.Cylinder, "Base", root,
                new Vector3(x, baseThickness * 0.5f, 0f),
                new Vector3(baseDiameter, baseThickness * 0.5f, baseDiameter),
                metal, generateColliders);

            Part(PrimitiveType.Cylinder, "Pole", root,
                new Vector3(x, baseThickness + poleLen * 0.5f, 0f),
                new Vector3(poleDiameter, poleLen * 0.5f, poleDiameter),
                metal, generateColliders);
        }
    }

    // ------------------------------------------------------------------ Panel
    void BuildPanel(Transform pivot)
    {
        Part(PrimitiveType.Cube, "PanelFrame", pivot, Vector3.zero,
            new Vector3(panelWidth, panelHeight, panelThickness),
            MakeMat(frameColor, 0f, 0.4f, null), generateColliders);

        float b = Mathf.Min(frameBorder, Mathf.Min(panelWidth, panelHeight) * 0.4f);
        var face = Part(PrimitiveType.Quad, "PanelFace", pivot,
            new Vector3(0f, 0f, -panelThickness * 0.5f - 0.001f),
            new Vector3(panelWidth - 2f * b, panelHeight - 2f * b, 1f),
            MakeMat(faceColor, 0f, 0.3f, null), false);
    }

    // ----------------------------------------------------------------- Images
    void BuildImages(Transform pivot)
    {
        foreach (var img in images)
        {
            if (img == null || img.texture == null) continue;
            float x = (img.position.x - 0.5f) * panelWidth;
            float y = (img.position.y - 0.5f) * panelHeight;
            Part(PrimitiveType.Quad, "Image_" + img.texture.name, pivot,
                new Vector3(x, y, -panelThickness * 0.5f - 0.002f),
                new Vector3(img.size.x, img.size.y, 1f),
                MakeMat(Color.white, 0f, 0.2f, img.texture), false);
        }
    }

    // ------------------------------------------------------------------ Texts
    void BuildTexts(Transform pivot)
    {
        float zText = -panelThickness * 0.5f - 0.004f;
        float innerW = panelWidth - 2f * margin;
        float regionX = -panelWidth * 0.5f + margin + innerW * textRegionStart;
        float regionW = innerW * textRegionWidth;
        float cursor = panelHeight * 0.5f - margin;
        float bottom = -panelHeight * 0.5f + margin;

        // Title
        if (!string.IsNullOrEmpty(titleText))
        {
            float h = Measure(titleText, titleFont, titleSizeCm, titleStyle, regionW);
            CreateText("Title", pivot, titleText, titleFont, titleSizeCm, titleColor, titleStyle,
                titleAlignment, new Vector2(regionX, cursor), new Vector2(regionW, h), zText);
            cursor -= h + gap;
        }

        // Subtitle
        if (!string.IsNullOrEmpty(subtitleText))
        {
            float h = Measure(subtitleText, subtitleFont, subtitleSizeCm, subtitleStyle, regionW);
            CreateText("Subtitle", pivot, subtitleText, subtitleFont, subtitleSizeCm, subtitleColor,
                subtitleStyle, subtitleAlignment, new Vector2(regionX, cursor), new Vector2(regionW, h), zText);
            cursor -= h + gap;
        }

        // Body (nhiều cột, chữ tự chảy từ cột này sang cột kế tiếp)
        int n = Mathf.Max(1, columns);
        float colW = (regionW - columnGap * (n - 1)) / n;
        float bodyH = Mathf.Max(0.02f, cursor - bottom);
        var boxes = new TextMeshPro[n];

        for (int i = 0; i < n; i++)
        {
            string txt = i == 0 ? bodyText : "";
            boxes[i] = CreateText("Body_" + (i + 1), pivot, txt, bodyFont, bodySizeCm, bodyColor, bodyStyle,
                bodyAlignment, new Vector2(regionX + i * (colW + columnGap), cursor),
                new Vector2(colW, bodyH), zText);
            boxes[i].lineSpacing = lineSpacing;
            boxes[i].paragraphSpacing = paragraphSpacing;
        }
        for (int i = 0; i < n - 1; i++)
        {
            boxes[i].overflowMode = TextOverflowModes.Linked;
            boxes[i].linkedTextComponent = boxes[i + 1];
        }
        boxes[n - 1].overflowMode = TextOverflowModes.Truncate;
    }

    float Measure(string text, TMP_FontAsset font, float sizeCm, FontStyles style, float width)
    {
        var go = new GameObject("measure") { hideFlags = HideFlags.HideAndDontSave };
        var t = go.AddComponent<TextMeshPro>();
        if (font) t.font = font;
        t.fontSize = sizeCm * CmToTmpSize;
        t.fontStyle = style;
        SetWrap(t);
        float h = t.GetPreferredValues(text, width, 0f).y;
        SafeDestroy(go);
        if (h <= 0.0001f) h = sizeCm * 0.013f;
        return h;
    }

    TextMeshPro CreateText(string name, Transform parent, string text, TMP_FontAsset font, float sizeCm,
        Color color, FontStyles style, TextAlignmentOptions align, Vector2 topLeft, Vector2 size, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = sizeCm * CmToTmpSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        SetWrap(t);
        t.overflowMode = TextOverflowModes.Overflow;

        var rt = t.rectTransform;
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.localPosition = new Vector3(topLeft.x, topLeft.y, z);
        return t;
    }

    static void SetWrap(TextMeshPro t)
    {
#if UNITY_2023_2_OR_NEWER
        t.textWrappingMode = TextWrappingModes.Normal;
#else
        t.enableWordWrapping = true;
#endif
    }

    // ---------------------------------------------------------------- Helpers
    GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
        Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null && !collider) SafeDestroy(col);
        return go;
    }

    static Material MakeMat(Color c, float metal, float smooth, Texture tex)
    {
        Shader sh = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Standard");

        var m = new Material(sh) { name = MatName };
        m.color = c;
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    void Clear()
    {
        var old = transform.Find(RootName);
        if (old == null) return;
        foreach (var r in old.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = r.sharedMaterial;
            if (m != null && m.name == MatName) SafeDestroy(m);
        }
        SafeDestroy(old.gameObject);
    }

    static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}


#if UNITY_EDITOR
// ===================== EDITOR (tự động bỏ khi build game) =====================
[CustomEditor(typeof(MuseumLabelStand))]
public class MuseumLabelStandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var stand = (MuseumLabelStand)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Mọi thay đổi ở trên sẽ tự cập nhật bảng.\n" +
            "Đừng chỉnh tay các object con trong '_Generated' vì sẽ bị ghi đè khi rebuild.",
            MessageType.Info);

        if (GUILayout.Button("Rebuild", GUILayout.Height(28)) || changed)
        {
            stand.Rebuild();
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(stand.gameObject.scene);
        }
    }

    [MenuItem("Tools/Museum/Create Exhibit Label Stand")]
    static void CreateFromTools()
    {
        Create(new MenuCommand(null));
    }

    [MenuItem("GameObject/Museum/Exhibit Label Stand", false, 10)]
    static void Create(MenuCommand cmd)
    {
        var go = new GameObject("Museum Label Stand");
        GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
        var stand = go.AddComponent<MuseumLabelStand>();
        stand.Rebuild();
        Undo.RegisterCreatedObjectUndo(go, "Create Museum Label Stand");
        Selection.activeGameObject = go;
    }
}
#endif
