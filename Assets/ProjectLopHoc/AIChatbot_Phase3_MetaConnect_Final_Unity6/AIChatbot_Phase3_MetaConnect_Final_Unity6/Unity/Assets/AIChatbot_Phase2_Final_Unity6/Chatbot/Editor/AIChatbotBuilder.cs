
#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using BIM4D.AI.Generative;

public static class AIChatbotBuilder
{
    // Thư mục gốc của package được suy ra từ vị trí file này (<gốc>/Chatbot/Editor/AIChatbotBuilder.cs),
    // nên đổi tên/di chuyển package vẫn chạy đúng. Prefab nằm ở <gốc>/Prefabs.
    static string _root;
    static string Root => _root ?? (_root = ResolveRoot());

    static string ResolveRoot()
    {
        foreach (var guid in AssetDatabase.FindAssets("AIChatbotBuilder t:Script"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("/AIChatbotBuilder.cs")) continue;
            var editorDir = Path.GetDirectoryName(path).Replace('\\', '/');   // .../Chatbot/Editor
            var chatbotDir = Path.GetDirectoryName(editorDir).Replace('\\', '/'); // .../Chatbot
            return Path.GetDirectoryName(chatbotDir).Replace('\\', '/');          // <gốc package>
        }
        return "Assets/AIChatbot_Phase2_Final_Unity6";
    }

    static readonly Color PanelBg = new Color(0.04f, 0.07f, 0.11f, 0.94f);
    static readonly Color HeaderBg = new Color(0.06f, 0.11f, 0.17f, 1f);
    static readonly Color Cyan = new Color(0.0f, 0.82f, 1f, 1f);
    static readonly Color UserBubble = new Color(0.0f, 0.42f, 0.62f, 1f);
    static readonly Color AIBubble = new Color(0.14f, 0.19f, 0.27f, 1f);
    static readonly Color InputBg = new Color(0.10f, 0.14f, 0.21f, 1f);

    [MenuItem("Tools/AI Chatbot/Build Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureFolder(Root + "/Prefabs");

        var user = BuildRow("UserMessagePrefab", true, UserBubble, "Xin chào");
        var ai = BuildRow("AIMessagePrefab", false, AIBubble, "Xin chào");
        var loading = BuildRow("LoadingMessagePrefab", false, AIBubble, "AI đang suy nghĩ...");

        BuildMain(user, ai, loading);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Chatbot] Đã tạo prefab trong " + Root + "/Prefabs");
    }

    [MenuItem("Tools/AI Chatbot/Build Demo Scene")]
    public static void BuildDemo()
    {
        BuildPrefabs();

        EnsureFolder(Root + "/Demo");

        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects,
            NewSceneMode.Single
        );

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3, 1, 3);

        var cam = Camera.main;
        cam.transform.position = new Vector3(0, 1.6f, -6);

        var player = cam.gameObject.AddComponent<DemoPlayer>();

        var robot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        robot.name = "Robot";
        robot.transform.position = new Vector3(0, 1, 0);
        robot.AddComponent<RobotInteraction>();

        // Tạo Chat Bot bằng XkiroBrain thật, không dùng MockBrain nữa.
        var chatBot = new GameObject("Chat Bot");
        var xkiroBrain = chatBot.AddComponent<XkiroBrain>();
        chatBot.AddComponent<GroqSpeechToText>();

        var cvGo = new GameObject(
            "CrosshairCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        cvGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var ch = new GameObject(
            "Crosshair",
            typeof(RectTransform),
            typeof(Image)
        );

        ch.transform.SetParent(cvGo.transform, false);
        ch.GetComponent<RectTransform>().sizeDelta = new Vector2(8, 8);
        ch.GetComponent<Image>().color = Cyan;
        ch.GetComponent<Image>().raycastTarget = false;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            Root + "/Prefabs/AIChatbot_Prefab.prefab"
        );

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var ui = inst.GetComponent<AIChatbotUIController>();
        ui.crosshair = ch;
        ui.behavioursToDisable = new Behaviour[] { player };

        // Gán XkiroBrain vào Integration.
        inst.GetComponent<AIChatbotIntegration>().brain = xkiroBrain;

        EditorSceneManager.SaveScene(
            scene,
            Root + "/Demo/AIChatbot_Demo.unity"
        );

        Debug.Log(
            "[Chatbot] Demo scene: " +
            Root +
            "/Demo/AIChatbot_Demo.unity - bấm Play, nhấn C."
        );
    }

    // ----------------------------------------------------------------
    // Message prefab
    // ----------------------------------------------------------------

    static ChatBubble BuildRow(
        string name,
        bool user,
        Color bubbleColor,
        string sample
    )
    {
        var row = NewRect(name, null);

        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = user
            ? TextAnchor.MiddleRight
            : TextAnchor.MiddleLeft;

        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var bubble = NewRect("Bubble", row);

        var img = bubble.gameObject.AddComponent<Image>();
        img.color = bubbleColor;

        var vlg = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var le = bubble.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 200;

        var txt = NewText(
            "Text",
            bubble,
            sample,
            20,
            Color.white,
            TextAlignmentOptions.TopLeft
        );

        var cb = row.gameObject.AddComponent<ChatBubble>();
        cb.text = txt;
        cb.layout = le;

        var path = Root + "/Prefabs/" + name + ".prefab";

        var asset = PrefabUtility.SaveAsPrefabAsset(
            row.gameObject,
            path
        );

        Object.DestroyImmediate(row.gameObject);

        return asset.GetComponent<ChatBubble>();
    }

    // ----------------------------------------------------------------
    // Main prefab
    // ----------------------------------------------------------------

    static void BuildMain(
        ChatBubble userP,
        ChatBubble aiP,
        ChatBubble loadingP
    )
    {
        var root = new GameObject("AIChatbot_Prefab");

        var integ = root.AddComponent<AIChatbotIntegration>();
        var ui = root.AddComponent<AIChatbotUIController>();

        var canvasGo = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        canvasGo.transform.SetParent(root.transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ChatPanel
        var panel = NewRect("ChatPanel", canvasGo.transform);

        panel.gameObject.AddComponent<Image>().color = PanelBg;

        panel.anchorMin = panel.anchorMax = new Vector2(1, 0.5f);
        panel.pivot = new Vector2(1, 0.5f);
        panel.sizeDelta = new Vector2(500, 700);
        panel.anchoredPosition = new Vector2(-24, 0);

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0.82f, 1f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Header
        var header = NewRect("Header", panel);

        header.gameObject.AddComponent<Image>().color = HeaderBg;

        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = new Vector2(1, 1);
        header.pivot = new Vector2(0.5f, 1);
        header.sizeDelta = new Vector2(0, 60);
        header.anchoredPosition = Vector2.zero;

        var title = NewText(
            "Title",
            header,
            "AI Assistant",
            24,
            Cyan,
            TextAlignmentOptions.BottomLeft
        );

        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0, 0.42f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(18, 0);
        title.rectTransform.offsetMax = new Vector2(-70, -4);

        var status = NewText(
            "StatusIndicator",
            header,
            "Sẵn sàng",
            15,
            new Color(0.6f, 0.75f, 0.85f),
            TextAlignmentOptions.TopLeft
        );

        status.rectTransform.anchorMin = Vector2.zero;
        status.rectTransform.anchorMax = new Vector2(1, 0.42f);
        status.rectTransform.offsetMin = new Vector2(18, 4);
        status.rectTransform.offsetMax = new Vector2(-70, 0);

        var close = NewButton(
            "CloseButton",
            header,
            "X",
            new Color(0.55f, 0.15f, 0.2f, 1f),
            22
        );

        close.anchorMin = close.anchorMax = new Vector2(1, 0.5f);
        close.pivot = new Vector2(1, 0.5f);
        close.sizeDelta = new Vector2(40, 40);
        close.anchoredPosition = new Vector2(-12, 0);

        // Scroll view
        var sv = NewRect("MessageScrollView", panel);

        sv.gameObject.AddComponent<Image>().color =
            new Color(0, 0, 0, 0.01f);

        sv.anchorMin = Vector2.zero;
        sv.anchorMax = Vector2.one;
        sv.offsetMin = new Vector2(8, 80);
        sv.offsetMax = new Vector2(-8, -66);

        var scroll = sv.gameObject.AddComponent<ScrollRect>();

        var vp = NewRect("Viewport", sv);
        Stretch(vp);

        vp.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", vp);

        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;

        var cvlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(6, 6, 8, 8);
        cvlg.spacing = 10;
        cvlg.childControlWidth = true;
        cvlg.childControlHeight = true;
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;

        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vp;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        // Input area
        var inputArea = NewRect("InputArea", panel);

        inputArea.anchorMin = new Vector2(0, 0);
        inputArea.anchorMax = new Vector2(1, 0);
        inputArea.pivot = new Vector2(0.5f, 0);
        inputArea.sizeDelta = new Vector2(0, 72);
        inputArea.anchoredPosition = Vector2.zero;

        var ihlg = inputArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        ihlg.padding = new RectOffset(12, 12, 10, 12);
        ihlg.spacing = 8;
        ihlg.childControlWidth = true;
        ihlg.childControlHeight = true;
        ihlg.childForceExpandWidth = false;
        ihlg.childForceExpandHeight = true;

        var inputRt = NewRect("MessageInput", inputArea);

        var inputImg = inputRt.gameObject.AddComponent<Image>();
        inputImg.color = InputBg;

        inputRt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        var field = inputRt.gameObject.AddComponent<TMP_InputField>();

        var area = NewRect("Text Area", inputRt);
        Stretch(area, 12, 6, 12, 6);

        area.gameObject.AddComponent<RectMask2D>();

        var ph = NewText(
            "Placeholder",
            area,
            "Nhập câu hỏi...",
            19,
            new Color(1, 1, 1, 0.4f),
            TextAlignmentOptions.MidlineLeft
        );

        ph.fontStyle = FontStyles.Italic;
        Stretch(ph.rectTransform);

        var tx = NewText(
            "Text",
            area,
            "",
            19,
            Color.white,
            TextAlignmentOptions.MidlineLeft
        );

        Stretch(tx.rectTransform);

        field.textViewport = area;
        field.textComponent = tx;
        field.placeholder = ph;
        field.targetGraphic = inputImg;
        field.lineType = TMP_InputField.LineType.MultiLineSubmit;
        field.caretColor = Cyan;
        field.selectionColor = new Color(0, 0.82f, 1f, 0.35f);

        var send = NewButton(
            "SendButton",
            inputArea,
            "Gửi",
            new Color(0, 0.55f, 0.75f, 1f),
            20
        );

        send.gameObject.AddComponent<LayoutElement>().preferredWidth = 84;

        var voice = NewButton(
            "VoiceButton",
            inputArea,
            "Mic",
            new Color(0.2f, 0.28f, 0.38f, 1f),
            18
        );

        voice.gameObject.AddComponent<LayoutElement>().preferredWidth = 56;

        // Gán tham chiếu UI
        ui.panel = panel.gameObject;
        ui.scrollRect = scroll;
        ui.content = content;
        ui.inputField = field;
        ui.sendButton = send.GetComponent<Button>();
        ui.closeButton = close.GetComponent<Button>();
        ui.voiceButton = voice.GetComponent<Button>();
        ui.statusText = status;

        ui.userMessagePrefab = userP;
        ui.aiMessagePrefab = aiP;
        ui.loadingMessagePrefab = loadingP;

        ui.integration = integ;

        // Voice UI hoàn chỉnh: nút Clear, hàng Voice Status (trạng thái + thanh mức âm + đồng hồ),
        // PointerVoiceTrigger, VoiceInputController, VoiceInputView. Tự gán toàn bộ tham chiếu,
        // idempotent (không tạo trùng component/listener).
        AIChatbotVoiceUIFactory.EnsureVoiceUI(ui, false);

        // Voice Output (TTS): dải cài đặt giọng đọc + nút Đọc trong prefab tin nhắn AI + controller.
        // Idempotent như Voice Input; tự gán toàn bộ tham chiếu.
        BIMVoiceOutputFactory.EnsureVoiceOutput(ui, false);

        PrefabUtility.SaveAsPrefabAsset(
            root,
            Root + "/Prefabs/AIChatbot_Prefab.prefab"
        );

        Object.DestroyImmediate(root);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        if (parent != null)
            go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    static void Stretch(
        RectTransform r,
        float l = 0,
        float b = 0,
        float rt = 0,
        float t = 0
    )
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b);
        r.offsetMax = new Vector2(-rt, -t);
    }

    static TextMeshProUGUI NewText(
        string name,
        Transform parent,
        string text,
        float size,
        Color color,
        TextAlignmentOptions align
    )
    {
        var rt = NewRect(name, parent);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;

        return t;
    }

    static RectTransform NewButton(
        string name,
        Transform parent,
        string label,
        Color color,
        float fontSize
    )
    {
        var rt = NewRect(name, parent);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var cols = btn.colors;
        cols.highlightedColor = new Color(1, 1, 1, 0.85f);
        cols.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cols.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        btn.colors = cols;

        var t = NewText(
            "Label",
            rt,
            label,
            fontSize,
            Color.white,
            TextAlignmentOptions.Center
        );

        Stretch(t.rectTransform);

        return rt;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');

        EnsureFolder(parent);

        AssetDatabase.CreateFolder(
            parent,
            Path.GetFileName(path)
        );
    }
}
#endif
