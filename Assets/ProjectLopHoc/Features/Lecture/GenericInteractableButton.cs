using UnityEngine;
using UnityEngine.Events;
using ProjectLopHoc.Core.Interfaces;
using ProjectLopHoc.Core.UI;

namespace ProjectLopHoc.Features.Lecture
{
    /// <summary>
    /// Script gắn vào các nút 3D, xử lý hiệu ứng đổi màu hover, 
    /// gọi TooltipUI và phát sự kiện click chuột.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GenericInteractableButton : MonoBehaviour, IInteractable
    {
        [Header("--- Cấu hình Tooltip ---")]
        [Tooltip("Nội dung chữ nổi lên khi lia tâm vào nút")]
        public string tooltipText = "Tên nút";

        [Header("--- Sự kiện Click ---")]
        public UnityEvent onInteract;

        [Header("--- Hiệu ứng Highlight Chuột ---")]
        public Renderer buttonRenderer;
        public Color hoverColor = Color.yellow;

        private Color _defaultColor = Color.white;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponent<Renderer>();
            }

            if (buttonRenderer != null && buttonRenderer.sharedMaterial != null)
            {
                _defaultColor = buttonRenderer.sharedMaterial.color;
            }
        }

        public void OnInteract(GameObject interactor)
        {
            onInteract?.Invoke();
        }

        public void OnHoverEnter()
        {
            ApplyColor(hoverColor);

            if (TooltipUI.Instance != null)
            {
                TooltipUI.Instance.Show(tooltipText, transform.position);
            }
        }

        public void OnHoverExit()
        {
            ApplyColor(_defaultColor);

            if (TooltipUI.Instance != null)
            {
                TooltipUI.Instance.Hide();
            }
        }

        private void ApplyColor(Color color)
        {
            if (buttonRenderer == null) return;

            buttonRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorPropertyId, color);
            _propBlock.SetColor(ColorPropertyId, color);
            buttonRenderer.SetPropertyBlock(_propBlock);
        }
    }
}