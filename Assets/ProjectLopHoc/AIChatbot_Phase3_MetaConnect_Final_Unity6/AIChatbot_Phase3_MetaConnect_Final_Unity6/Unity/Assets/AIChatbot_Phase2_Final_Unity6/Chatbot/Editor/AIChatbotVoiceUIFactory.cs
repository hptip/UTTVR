#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tạo/gán toàn bộ thành phần Voice UI cho một chatbot: nút Clear, hàng Voice Status
/// (trạng thái + thanh mức âm + đồng hồ), PointerVoiceTrigger, VoiceInputController, VoiceInputView.
///
/// IDEMPOTENT: gọi bao nhiêu lần cũng không tạo trùng (tìm theo tên / GetComponent trước khi tạo).
/// Được dùng bởi AIChatbotBuilder (prefab mới) và VoiceInputInstaller (chatbot đã có trong scene).
/// </summary>
public static class AIChatbotVoiceUIFactory
{
    public const string ClearButtonName = "ClearButton";
    public const string VoiceRowName = "VoiceRow";
    const float VoiceRowHeight = 40f;

    static readonly Color Cyan = new Color(0.0f, 0.82f, 1f, 1f);
    static readonly Color ClearColor = new Color(0.25f, 0.30f, 0.38f, 1f);

    /// <summary>Trả về true nếu có thay đổi nào được thực hiện.</summary>
    public static bool EnsureVoiceUI(AIChatbotUIController ui, bool recordUndo)
    {
        if (ui == null || ui.inputField == null || ui.panel == null)
        {
            Debug.LogError("[BIM AI Voice] AIChatbotUIController thiếu panel hoặc inputField. Không thể tạo Voice UI.");
            return false;
        }

        bool changed = false;
        var panel = ui.panel.GetComponent<RectTransform>();
        var inputArea = ui.inputField.transform.parent as RectTransform;
        if (panel == null || inputArea == null)
        {
            Debug.LogError("[BIM AI Voice] Không tìm thấy InputArea của chatbot.");
            return false;
        }

        // 1) Nút Clear ----------------------------------------------------
        var clearTf = inputArea.Find(ClearButtonName);
        if (clearTf == null)
        {
            var clear = NewButton(ClearButtonName, inputArea, "Xóa", ClearColor, 18);
            clear.gameObject.AddComponent<LayoutElement>().preferredWidth = 60;
            clearTf = clear;
            Register(clear.gameObject, recordUndo);
            changed = true;
        }
        var clearBtn = clearTf.GetComponent<Button>();
        if (ui.clearButton != clearBtn) { ui.clearButton = clearBtn; changed = true; }

        // 2) Nút Mic: chỉ Push-to-Talk, không onClick, không tint ghi đè màu trạng thái -----
        if (ui.voiceButton == null)
        {
            Debug.LogError("[BIM AI Voice] AIChatbotUIController chưa gán Voice Button.");
            return changed;
        }
        var voiceBtn = ui.voiceButton;
        voiceBtn.gameObject.SetActive(true);
        voiceBtn.transition = Selectable.Transition.None;
        for (int i = voiceBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(voiceBtn.onClick, i);
            changed = true;
        }
        var le = voiceBtn.GetComponent<LayoutElement>();
        if (le == null) le = voiceBtn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 64;

        var pointer = voiceBtn.GetComponent<PointerVoiceTrigger>();
        if (pointer == null)
        {
            pointer = voiceBtn.gameObject.AddComponent<PointerVoiceTrigger>();
            changed = true;
        }

        // Thứ tự theo bố cục: [Ô nhập] [Clear] [Mic] [Gửi]
        var inputTf = ui.inputField.transform;
        inputTf.SetSiblingIndex(0);
        clearTf.SetSiblingIndex(1);
        voiceBtn.transform.SetSiblingIndex(2);
        if (ui.sendButton != null) ui.sendButton.transform.SetSiblingIndex(3);

        // 3) Hàng Voice Status: [trạng thái] [thanh mức âm] [00:00 / 00:30] ------------
        TMP_Text statusTx; Slider meter; TMP_Text timerTx;
        var rowTf = panel.Find(VoiceRowName);
        if (rowTf == null)
        {
            var row = NewRect(VoiceRowName, panel);
            row.anchorMin = new Vector2(0, 0);
            row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(0.5f, 0);
            row.sizeDelta = new Vector2(0, VoiceRowHeight);
            row.anchoredPosition = Vector2.zero;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 2, 6);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var st = NewText("VoiceStatus", row, VoiceInputController.HintIdle, 15, new Color(0.6f, 0.75f, 0.85f), TextAlignmentOptions.MidlineLeft);
            st.raycastTarget = false;
            // Thông báo lỗi có thể dài: tự co chữ (10-14) và cho phép 2 dòng thay vì tràn khỏi khung.
            st.enableAutoSizing = true;
            st.fontSizeMin = 10;
            st.fontSizeMax = 14;
            st.overflowMode = TextOverflowModes.Ellipsis;
            var stLe = st.gameObject.AddComponent<LayoutElement>();
            stLe.flexibleWidth = 1;
            stLe.minWidth = 60;
            stLe.preferredHeight = 32;

            var meterRt = BuildLevelMeter("InputLevel", row);
            var meterLe = meterRt.gameObject.AddComponent<LayoutElement>();
            meterLe.preferredWidth = 90;
            meterLe.preferredHeight = 12;

            var tm = NewText("RecordingTimer", row, "00:00 / 00:30", 15, new Color(0.6f, 0.75f, 0.85f), TextAlignmentOptions.MidlineRight);
            tm.raycastTarget = false;
            var tmLe = tm.gameObject.AddComponent<LayoutElement>();
            tmLe.preferredWidth = 108;
            tmLe.preferredHeight = 24;

            // Nhường chỗ cho hàng mới: nâng InputArea và thu gọn danh sách tin nhắn (chỉ làm 1 lần, khi tạo hàng).
            inputArea.anchoredPosition = new Vector2(inputArea.anchoredPosition.x, inputArea.anchoredPosition.y + VoiceRowHeight);
            if (ui.scrollRect != null)
            {
                var sv = ui.scrollRect.GetComponent<RectTransform>();
                sv.offsetMin = new Vector2(sv.offsetMin.x, sv.offsetMin.y + VoiceRowHeight);
            }

            Register(row.gameObject, recordUndo);
            rowTf = row;
            changed = true;
        }
        statusTx = rowTf.Find("VoiceStatus").GetComponent<TMP_Text>();
        meter = rowTf.Find("InputLevel").GetComponent<Slider>();
        timerTx = rowTf.Find("RecordingTimer").GetComponent<TMP_Text>();

        // 4) Component điều khiển + hiển thị (trên root chatbot) -------------------------
        var root = ui.gameObject;
        var controller = root.GetComponent<VoiceInputController>();
        if (controller == null) { controller = root.AddComponent<VoiceInputController>(); changed = true; }

        var view = root.GetComponent<VoiceInputView>();
        if (view == null) { view = root.AddComponent<VoiceInputView>(); changed = true; }
        view.controller = controller;
        view.pointerTrigger = pointer;
        view.micGraphic = voiceBtn.targetGraphic != null ? voiceBtn.targetGraphic : voiceBtn.GetComponent<Graphic>();
        view.micLabel = voiceBtn.GetComponentInChildren<TMP_Text>(true);
        view.statusText = statusTx;
        view.levelSlider = meter;
        view.timerText = timerTx;

        ui.showVoiceButton = true;

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(voiceBtn);
        return changed;
    }

    // ----------------------------------------------------------------- helpers

    static void Register(GameObject go, bool recordUndo)
    {
        if (recordUndo && go.scene.IsValid()) Undo.RegisterCreatedObjectUndo(go, "Add Voice UI");
    }

    static RectTransform BuildLevelMeter(string name, Transform parent)
    {
        var rt = NewRect(name, parent);

        var bg = NewRect("Background", rt);
        Stretch(bg);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(1, 1, 1, 0.14f);
        bgImg.raycastTarget = false;

        var fillArea = NewRect("Fill Area", rt);
        Stretch(fillArea);

        var fill = NewRect("Fill", fillArea);
        fill.anchorMin = new Vector2(0, 0);
        fill.anchorMax = new Vector2(0, 1);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImg = fill.gameObject.AddComponent<Image>();
        fillImg.color = Cyan;
        fillImg.raycastTarget = false;

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 0f;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.interactable = false; // chỉ là đồng hồ đo, người dùng không kéo
        return rt;
    }

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

    static RectTransform NewButton(string name, Transform parent, string label, Color color, float fontSize)
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

        var t = NewText("Label", rt, label, fontSize, Color.white, TextAlignmentOptions.Center);
        t.raycastTarget = false;
        Stretch(t.rectTransform);
        return rt;
    }
}
#endif
