using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UTT.SaBan
{
    /// <summary>
    /// Dựng bảng điều khiển nổi (Canvas World Space) gắn liền với Sa Bàn.
    /// Bảng là GameObject thật trong scene nên bạn có thể kéo đi chỗ khác, xoay, đổi cỡ bằng công cụ Move/Scale của Unity.
    /// Bố cục: [CHẠY TÀU] / [x1 x2 x3 x4] / [VỀ GA ĐẦU] / các nút ẨN-HIỆN từng lớp.
    /// Không có xem 360 độ hay phóng to/thu nhỏ.
    /// </summary>
    public static class SaBanPanelFactory
    {
        public const string PanelName = "SaBan_ControlPanel";
        public const float PixelToMeter = 0.001f;   // 1000 pixel UI = 1 mét thật

        private const float Width = 560f;
        private const float Pad = 20f;
        private const float Gap = 8f;

        private static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.14f, 0.94f);
        private static readonly Color TitleColor = new Color(0.55f, 0.90f, 0.95f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color PlayColor = new Color(0.12f, 0.55f, 0.28f, 1f);
        private static readonly Color SpeedColor = new Color(0.20f, 0.28f, 0.45f, 1f);
        private static readonly Color ResetColor = new Color(0.45f, 0.20f, 0.20f, 1f);
        private static readonly Color LayerOnColor = new Color(0.10f, 0.52f, 0.48f, 1f);

        /// <summary>
        /// Tạo mới hoặc dựng lại bảng. Nếu <paramref name="existing"/> khác null thì giữ nguyên vị trí/góc xoay/cỡ
        /// của bảng cũ và chỉ dựng lại nội dung (dùng khi bạn thêm lớp mới).
        /// </summary>
        public static Transform Build(SaBanRoot root, Transform existing, Vector3 localPosition, bool immediate)
        {
            GameObject panelGo;
            if (existing != null)
            {
                panelGo = existing.gameObject;
                for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = panelGo.transform.GetChild(i).gameObject;
                    if (immediate) Object.DestroyImmediate(child); else Object.Destroy(child);
                }
            }
            else
            {
                panelGo = new GameObject(PanelName, typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                panelGo.transform.localPosition = localPosition;
                panelGo.transform.localRotation = Quaternion.identity;
                panelGo.transform.localScale = Vector3.one * PixelToMeter;
            }

            Canvas canvas = panelGo.GetComponent<Canvas>();
            if (canvas == null) canvas = panelGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (panelGo.GetComponent<GraphicRaycaster>() == null) panelGo.AddComponent<GraphicRaycaster>();

            SaBanControlPanel controller = panelGo.GetComponent<SaBanControlPanel>();
            if (controller == null) controller = panelGo.AddComponent<SaBanControlPanel>();
            controller.root = root;

            // Danh sách lớp sẽ có nút: chỉ lớp có nội dung và được bật "Show On Panel"
            var layers = new List<SaBanLayer>();
            foreach (SaBanLayer layer in root.GetLayers())
            {
                if (layer.showOnPanel && layer.transform.childCount > 0) layers.Add(layer);
            }

            const float titleH = 46f, statusH = 34f, playH = 60f, speedH = 50f, resetH = 44f, headerH = 26f, layerH = 48f;
            int rows = (layers.Count + 1) / 2;
            float height = Pad + titleH + Gap + statusH + Gap + playH + Gap + speedH + Gap + resetH;
            if (rows > 0) height += 14f + headerH + Gap + rows * (layerH + Gap) - Gap;
            height += Pad;

            RectTransform rect = panelGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Width, height);

            // Nền
            RectTransform bg = NewRect("Background", rect, new Vector2(Width, height), Vector2.zero);
            Image bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.color = PanelColor;
            bgImage.raycastTarget = true;

            float top = height * 0.5f;       // toạ độ y của mép trên (gốc ở giữa)
            float y = Pad;                    // con trỏ tính từ mép trên xuống

            // Tiêu đề
            AddText(rect, "TitleText", "ĐIỀU KHIỂN SA BÀN", 30, TitleColor, TextAnchor.MiddleCenter,
                new Vector2(Width - 2 * Pad, titleH), new Vector2(0f, top - y - titleH * 0.5f));
            y += titleH + Gap;

            // Trạng thái
            AddText(rect, "StatusText", "ĐANG DỪNG  |  x1  |  0%", 22, TextColor, TextAnchor.MiddleCenter,
                new Vector2(Width - 2 * Pad, statusH), new Vector2(0f, top - y - statusH * 0.5f));
            y += statusH + Gap;

            // Nút chạy tàu
            AddButton(rect, "Btn_Play", "CHẠY TÀU", new Vector2(Width - 2 * Pad, playH),
                new Vector2(0f, top - y - playH * 0.5f), PlayColor, 28);
            y += playH + Gap;

            // x1 x2 x3 x4
            float speedW = (Width - 2 * Pad - 3 * Gap) / 4f;
            for (int i = 1; i <= 4; i++)
            {
                float x = -(Width - 2 * Pad) * 0.5f + speedW * 0.5f + (i - 1) * (speedW + Gap);
                AddButton(rect, "Btn_Speed_" + i, "x" + i, new Vector2(speedW, speedH),
                    new Vector2(x, top - y - speedH * 0.5f), SpeedColor, 26);
            }
            y += speedH + Gap;

            // Về ga đầu
            AddButton(rect, "Btn_Reset", "VỀ GA ĐẦU", new Vector2(Width - 2 * Pad, resetH),
                new Vector2(0f, top - y - resetH * 0.5f), ResetColor, 22);
            y += resetH;

            // Các lớp
            if (rows > 0)
            {
                y += 14f;
                AddText(rect, "LayerHeader", "ẨN / HIỆN CÁC LỚP", 20, TitleColor, TextAnchor.MiddleCenter,
                    new Vector2(Width - 2 * Pad, headerH), new Vector2(0f, top - y - headerH * 0.5f));
                y += headerH + Gap;

                float colW = (Width - 2 * Pad - Gap) / 2f;
                for (int i = 0; i < layers.Count; i++)
                {
                    int row = i / 2, col = i % 2;
                    float x = -(Width - 2 * Pad) * 0.5f + colW * 0.5f + col * (colW + Gap);
                    float cy = top - (y + row * (layerH + Gap)) - layerH * 0.5f;
                    SaBanLayer layer = layers[i];
                    AddButton(rect, "Btn_Layer_" + layer.key, LayerLabel(layer, layer.IsVisible),
                        new Vector2(colW, layerH), new Vector2(x, cy), LayerOnColor, 22);
                }
            }

            return panelGo.transform;
        }

        /// <summary>Nhãn nút luôn là HÀNH ĐỘNG sẽ xảy ra khi bấm: đang hiện thì ghi "ẨN ...", đang ẩn thì ghi "HIỆN ...".</summary>
        public static string LayerLabel(SaBanLayer layer, bool visible)
        {
            return (visible ? "ẨN " : "HIỆN ") + layer.label;
        }

        // ============================ Các hàm dựng UI nhỏ ============================

        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            return rt;
        }

        private static Text AddText(RectTransform parent, string name, string value, int fontSize, Color color,
            TextAnchor anchor, Vector2 size, Vector2 position)
        {
            RectTransform rt = NewRect(name, parent, size, position);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void AddButton(RectTransform parent, string name, string label, Vector2 size, Vector2 position,
            Color color, int fontSize)
        {
            RectTransform rt = NewRect(name, parent, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            // Collider để người chơi góc nhìn thứ nhất ngắm tâm vào rồi bấm E
            BoxCollider collider = rt.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 30f);

            SaBanPanelButton interact = rt.gameObject.AddComponent<SaBanPanelButton>();
            interact.objectName = label + "\n[Nhấn E]";
            interact.targetSceneName = "";

            AddText(rt, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter,
                new Vector2(size.x - 10f, size.y - 6f), Vector2.zero);
        }
    }
}
