using UnityEngine;
using TMPro;

namespace ProjectLopHoc.Features.Info
{
    public enum InfoBoardMode
    {
        FollowObject,   // Bảng bám theo vị trí object trên màn hình
        FixedOnScreen   // Bảng cố định 1 góc màn hình
    }

    public class FloatingInfoBoard : MonoBehaviour
    {
        [Header("Gắn vào object")]
        public Transform target;
        public Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
        public Camera cam;

        [Header("Chế độ hiển thị")]
        public InfoBoardMode mode = InfoBoardMode.FollowObject;
        public Vector2 screenOffset = Vector2.zero;        // lệch thêm (pixel)
        [Range(0f, 1f)] public float fixedAnchorX = 0.85f; // dùng cho FixedOnScreen
        [Range(0f, 1f)] public float fixedAnchorY = 0.85f;
        public bool clampToScreen = true;
        public bool hideWhenBehindCamera = true;

        [Header("UI (Canvas = Screen Space - Overlay)")]
        public RectTransform panel;
        public TMP_Text titleText;
        public TMP_Text bodyText;

        [Header("Nội dung tự ghi")]
        public string title = "Tiêu đề";
        [TextArea(4, 12)]
        public string content = "Dòng thông tin 1\nDòng thông tin 2\nDòng thông tin 3";

        private Canvas _canvas;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (panel != null) _canvas = panel.GetComponentInParent<Canvas>();
            Refresh();
        }

        // Gọi hàm này khi muốn đổi nội dung lúc đang chạy
        public void SetContent(string newTitle, string newContent)
        {
            title = newTitle;
            content = newContent;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.gameObject.SetActive(visible);
        }

        private void Refresh()
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = content;
        }

        private void LateUpdate()
        {
            if (panel == null || cam == null) return;

            Vector3 screenPos;

            if (mode == InfoBoardMode.FixedOnScreen)
            {
                screenPos = new Vector3(Screen.width * fixedAnchorX,
                                        Screen.height * fixedAnchorY, 0f);
            }
            else
            {
                if (target == null) return;

                screenPos = cam.WorldToScreenPoint(target.position + worldOffset);

                if (hideWhenBehindCamera)
                {
                    bool behind = screenPos.z < 0f;
                    if (panel.gameObject.activeSelf == behind)
                        panel.gameObject.SetActive(!behind);
                    if (behind) return;
                }
            }

            screenPos += (Vector3)screenOffset;

            if (clampToScreen)
            {
                float scale = _canvas != null ? _canvas.scaleFactor : 1f;
                Vector2 half = panel.rect.size * scale * 0.5f;
                screenPos.x = Mathf.Clamp(screenPos.x, half.x, Screen.width - half.x);
                screenPos.y = Mathf.Clamp(screenPos.y, half.y, Screen.height - half.y);
            }

            panel.position = screenPos;
        }

#if UNITY_EDITOR
        private void OnValidate() => Refresh();
#endif
    }
}