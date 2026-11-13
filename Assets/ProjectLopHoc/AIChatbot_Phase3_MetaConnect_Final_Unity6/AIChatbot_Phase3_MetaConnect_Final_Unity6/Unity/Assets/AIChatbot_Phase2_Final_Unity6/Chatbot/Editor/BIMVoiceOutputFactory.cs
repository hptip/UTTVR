#if UNITY_EDITOR
using BIM4D.AI.Speech;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tạo/gán toàn bộ UI Voice Output cho một chatbot:
///  - dải cài đặt (Tự đọc, Ngôn ngữ, Giọng Trước/Sau, Tốc độ 0.7-1.5x, Âm lượng, Dừng, trạng thái) dưới Header;
///  - nút "Đọc" trong prefab tin nhắn AI (ẩn sẵn, chỉ hiện cho câu trả lời AI thật);
///  - BIMTextToSpeechController (+ BIMAudioPlayer tự thêm) và BIMVoiceOutputUI trên root chatbot.
/// IDEMPOTENT: chạy nhiều lần không tạo trùng. Tự nâng cấp dải UI bản Azure cũ (nút Giọng Nam/Nữ) sang bản MetaConnect.
/// Được gọi bởi AIChatbotBuilder và BIMTTSInstaller.
/// </summary>
public static class BIMVoiceOutputFactory
{
    public const string RowName = "VoiceOutputRow";
    public const string SpeakRowName = "SpeakRow";
    const float StripHeight = 96f;        // 3 hàng
    const float LegacyStripHeight = 64f;  // dải 2 hàng của bản Azure
    const float LineHeight = 26f;

    static readonly Color ButtonColor = new Color(0.20f, 0.30f, 0.40f, 1f);
    static readonly Color TextColor = new Color(0.60f, 0.75f, 0.85f, 1f);
    static readonly Color Cyan = new Color(0.0f, 0.82f, 1f, 1f);

    /// <summary>Trả về true nếu có thay đổi.</summary>
    public static bool EnsureVoiceOutput(AIChatbotUIController ui, bool recordUndo)
    {
        if (ui == null || ui.panel == null)
        {
            Debug.LogError("[BIM Voice Output] AIChatbotUIController thiếu panel. Không thể tạo Voice Output UI.");
            return false;
        }

        bool changed = false;
        var panel = ui.panel.GetComponent<RectTransform>();
        var root = ui.gameObject;

        // 1) Controller (RequireComponent tự thêm BIMAudioPlayer) -------------------------
        var controller = root.GetComponent<BIMTextToSpeechController>();
        if (controller == null)
        {
            controller = recordUndo ? Undo.AddComponent<BIMTextToSpeechController>(root) : root.AddComponent<BIMTextToSpeechController>();
            changed = true;
        }

        // 2) Dải cài đặt giọng đọc ---------------------------------------------------------
        var rowTf = panel.Find(RowName);
        if (rowTf != null && rowTf.Find("Row3/SpeedSlider") == null)
        {
            // Dải cũ (Azure, 2 hàng, nút Giọng Nam/Nữ): dựng lại, trả lại chỗ đã thu gọn trước đó.
            if (ui.scrollRect != null)
            {
                var sv0 = ui.scrollRect.GetComponent<RectTransform>();
                sv0.offsetMax = new Vector2(sv0.offsetMax.x, sv0.offsetMax.y + LegacyStripHeight);
            }
            if (recordUndo) Undo.DestroyObjectImmediate(rowTf.gameObject); else Object.DestroyImmediate(rowTf.gameObject);
            rowTf = null;
            changed = true;
        }

        if (rowTf == null)
        {
            float headerH = 60f;
            var header = panel.Find("Header") as RectTransform;
            if (header != null && header.sizeDelta.y > 1f) headerH = header.sizeDelta.y;

            var strip = NewRect(RowName, panel);
            strip.anchorMin = new Vector2(0, 1);
            strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, StripHeight);
            strip.anchoredPosition = new Vector2(0, -headerH);

            var vlg = strip.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 4, 4);
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Hàng 1: Tự đọc | Ngôn ngữ | trạng thái | Dừng
            var row1 = NewRow("Row1", strip);
            NewButton("AutoSpeakButton", row1, "Tự đọc: TẮT", 122);
            NewButton("LanguageButton", row1, "Tiếng Việt", 96);
            var status = NewText("VoiceOutputStatus", row1, "", 13, TextColor, TextAlignmentOptions.MidlineLeft);
            status.raycastTarget = false;
            status.enableAutoSizing = true;
            status.fontSizeMin = 9;
            status.fontSizeMax = 13;
            status.overflowMode = TextOverflowModes.Ellipsis;
            var stLe = status.gameObject.AddComponent<LayoutElement>();
            stLe.flexibleWidth = 1;
            stLe.minWidth = 40;
            stLe.preferredHeight = LineHeight;
            NewButton("StopButton", row1, "Dừng", 60);

            // Hàng 2: Giọng  [<] Quang Sơn (1/11) [>]
            var row2 = NewRow("Row2", strip);
            AddLabel(row2, "VoiceCaption", "Giọng", 48);
            NewButton("VoicePrevButton", row2, "<", 34);
            var voiceTxt = NewText("VoiceLabel", row2, "", 14, Color.white, TextAlignmentOptions.Center);
            voiceTxt.raycastTarget = false;
            voiceTxt.enableAutoSizing = true;
            voiceTxt.fontSizeMin = 9;
            voiceTxt.fontSizeMax = 14;
            var vLe = voiceTxt.gameObject.AddComponent<LayoutElement>();
            vLe.flexibleWidth = 1;
            vLe.preferredHeight = LineHeight;
            NewButton("VoiceNextButton", row2, ">", 34);

            // Hàng 3: Âm lượng | Tốc độ (0.7-1.5x)
            var row3 = NewRow("Row3", strip);
            AddLabel(row3, "VolumeLabel", "Âm lượng", 72);
            var vol = NewSlider("VolumeSlider", row3, 0f, 1f, 0.8f, false);
            vol.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            AddLabel(row3, "SpeedLabel", "Tốc độ 1.00x", 92);
            var spd = NewSlider("SpeedSlider", row3, 0.7f, 1.5f, 1.0f, false);
            spd.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            foreach (var le in new[] { vol, spd })
            {
                var l = le.GetComponent<LayoutElement>();
                l.preferredHeight = 18;
                l.minWidth = 40;
            }

            // Nhường chỗ: thu gọn vùng tin nhắn phía trên (chỉ làm 1 lần, khi tạo dải).
            if (ui.scrollRect != null)
            {
                var sv = ui.scrollRect.GetComponent<RectTransform>();
                sv.offsetMax = new Vector2(sv.offsetMax.x, sv.offsetMax.y - StripHeight);
            }

            if (recordUndo && strip.gameObject.scene.IsValid()) Undo.RegisterCreatedObjectUndo(strip.gameObject, "Add Voice Output UI");
            rowTf = strip;
            changed = true;
        }

        // 3) BIMVoiceOutputUI (gán toàn bộ tham chiếu) --------------------------------------
        var vo = root.GetComponent<BIMVoiceOutputUI>();
        if (vo == null) { vo = root.AddComponent<BIMVoiceOutputUI>(); changed = true; }
        vo.controller = controller;

        var auto = rowTf.Find("Row1/AutoSpeakButton");
        var lang = rowTf.Find("Row1/LanguageButton");
        var stop = rowTf.Find("Row1/StopButton");
        var prev = rowTf.Find("Row2/VoicePrevButton");
        var next = rowTf.Find("Row2/VoiceNextButton");
        vo.autoSpeakButton = auto.GetComponent<Button>();
        vo.autoSpeakLabel = auto.GetComponentInChildren<TMP_Text>(true);
        vo.languageButton = lang.GetComponent<Button>();
        vo.languageLabel = lang.GetComponentInChildren<TMP_Text>(true);
        vo.stopButton = stop.GetComponent<Button>();
        vo.statusText = rowTf.Find("Row1/VoiceOutputStatus").GetComponent<TMP_Text>();
        vo.voicePrevButton = prev.GetComponent<Button>();
        vo.voiceNextButton = next.GetComponent<Button>();
        vo.voiceLabel = rowTf.Find("Row2/VoiceLabel").GetComponent<TMP_Text>();
        vo.volumeSlider = rowTf.Find("Row3/VolumeSlider").GetComponent<Slider>();
        vo.speedSlider = rowTf.Find("Row3/SpeedSlider").GetComponent<Slider>();
        vo.speedLabel = rowTf.Find("Row3/SpeedLabel").GetComponent<TMP_Text>();

        // 4) Nút "Đọc" trong prefab tin nhắn AI --------------------------------------------
        if (EnsureSpeakRow(ui.aiMessagePrefab)) changed = true;

        EditorUtility.SetDirty(vo);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(ui);
        return changed;
    }

    // ------------------------------------------------------------------ prefab tin nhắn AI

    static bool EnsureSpeakRow(ChatBubble aiPrefab)
    {
        if (aiPrefab == null)
        {
            Debug.LogWarning("[BIM Voice Output] AIChatbotUIController chưa gán aiMessagePrefab: bỏ qua nút Đọc trên bubble.");
            return false;
        }

        string path = AssetDatabase.GetAssetPath(aiPrefab.gameObject);
        bool isAsset = !string.IsNullOrEmpty(path);
        GameObject contents = isAsset ? PrefabUtility.LoadPrefabContents(path) : aiPrefab.gameObject;
        bool changed = false;
        try
        {
            var bubble = contents.transform.Find("Bubble");
            if (bubble == null)
            {
                Debug.LogWarning("[BIM Voice Output] Prefab tin nhắn AI không có object 'Bubble': bỏ qua nút Đọc.");
            }
            else if (bubble.Find(SpeakRowName) == null)
            {
                var row = NewRect(SpeakRowName, bubble);
                var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.UpperRight;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 0;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

                var btn = NewButton("SpeakButton", row, "Đọc", 76);
                var sb = row.gameObject.AddComponent<BIMSpeakButton>();
                sb.button = btn.GetComponent<Button>();
                sb.label = btn.GetComponentInChildren<TMP_Text>(true);
                sb.background = btn.GetComponent<Image>();

                row.gameObject.SetActive(false); // chỉ hiện cho câu trả lời AI thật (BIMSpeakButton.Bind)
                if (isAsset) PrefabUtility.SaveAsPrefabAsset(contents, path);
                changed = true;
            }
        }
        finally
        {
            if (isAsset) PrefabUtility.UnloadPrefabContents(contents);
        }
        return changed;
    }

    // ------------------------------------------------------------------ helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        return t;
    }

    static RectTransform NewRow(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = LineHeight;
        return rt;
    }

    static RectTransform NewButton(string name, Transform parent, string label, float preferredWidth)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = ButtonColor;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(1, 1, 1, 0.85f);
        cols.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cols.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        btn.colors = cols;

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = LineHeight - 2;

        var t = NewText("Label", rt, label, 14, Color.white, TextAlignmentOptions.Center);
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = 9;
        t.fontSizeMax = 14;
        Stretch(t.rectTransform);
        return rt;
    }

    static void AddLabel(Transform parent, string name, string text, float preferredWidth)
    {
        var t = NewText(name, parent, text, 13, TextColor, TextAlignmentOptions.MidlineLeft);
        t.raycastTarget = false;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = LineHeight;
    }

    static RectTransform NewSlider(string name, Transform parent, float min, float max, float value, bool whole)
    {
        var rt = NewRect(name, parent);

        var bg = NewRect("Background", rt);
        bg.anchorMin = new Vector2(0, 0.25f);
        bg.anchorMax = new Vector2(1, 0.75f);
        bg.offsetMin = Vector2.zero;
        bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.16f);

        var fillArea = NewRect("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0, 0.25f);
        fillArea.anchorMax = new Vector2(1, 0.75f);
        fillArea.offsetMin = new Vector2(5, 0);
        fillArea.offsetMax = new Vector2(-15, 0);
        var fill = NewRect("Fill", fillArea);
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(0, 1);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        fill.sizeDelta = new Vector2(10, 0);
        fill.gameObject.AddComponent<Image>().color = Cyan;

        var handleArea = NewRect("Handle Slide Area", rt);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10, 0);
        handleArea.offsetMax = new Vector2(-10, 0);
        var handle = NewRect("Handle", handleArea);
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        handle.sizeDelta = new Vector2(16, 0);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.value = value;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return rt;
    }
}
#endif
