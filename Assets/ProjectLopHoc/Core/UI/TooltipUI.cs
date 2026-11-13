using UnityEngine;
using TMPro;

namespace ProjectLopHoc.Core.UI
{
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance;

        public TMP_Text label; // Kéo component TextMeshPro 3D vào đây
        public float heightAboveButton = 0.2f; // Khoảng cách nổi phía trên nút

        private bool _isShowing;
        private Camera _mainCamera;

        private void Awake()
        {
            Instance = this;
            Hide();
        }

        private void LateUpdate()
        {
            if (!_isShowing) return;

            // Tự động tìm lại camera nếu camera được tạo sau khi người chơi spawn vào phòng
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Đồng bộ góc quay chữ theo camera của người dùng để chữ luôn đối diện tầm mắt
            transform.rotation = _mainCamera.transform.rotation;
        }

        public void Show(string text, Vector3 worldPosition)
        {
            if (string.IsNullOrEmpty(text)) return;

            _isShowing = true;
            transform.position = worldPosition + Vector3.up * heightAboveButton;

            if (label != null)
            {
                label.text = text;
                label.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _isShowing = false;
            if (label != null) 
            {
                label.gameObject.SetActive(false);
            }
        }
    }
}