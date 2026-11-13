using UnityEngine;
using ProjectLopHoc.Core.Interfaces;
using Unity.XR.CoreUtils;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectLopHoc.Features.Lecture
{
    /// <summary>
    /// Gắn vào Main Camera của nhân vật. Xử lý quét tia tương tác và click chuột trên WebGL.
    /// </summary>
    public class CrosshairInteractor : MonoBehaviour
    {
        [Header("--- Cấu hình Tương tác ---")]
        [Tooltip("Khoảng cách tối đa có thể tương tác với vật thể")]
        public float interactRange = 5f;
        public LayerMask interactableLayer = ~0;

        private Camera _cam;
        private IInteractable _currentHovered;
        private bool _xrDriven;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            _xrDriven = FindFirstObjectByType<XROrigin>() != null;
        }

        private void Update()
        {
            // XR interactions are processed from the tracked controller ray by FirstPersonController.
            // Do not run a second, detached scene-camera ray in parallel.
            if (_xrDriven || UnityEngine.XR.XRSettings.isDeviceActive) return;
            if (_cam == null) return;

            ProcessHover();

            // Nhận diện click chuột trái hoặc phím E
            if (WasInteractionPressed())
            {
                ProcessInteraction();
            }
        }

        private static bool WasInteractionPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                   (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private void ProcessHover()
        {
            IInteractable detected = PerformRaycast();

            if (detected != _currentHovered)
            {
                _currentHovered?.OnHoverExit();
                _currentHovered = detected;
                _currentHovered?.OnHoverEnter();
            }
        }

        private void ProcessInteraction()
        {
            _currentHovered?.OnInteract(transform.root.gameObject);
        }

        private IInteractable PerformRaycast()
        {
            // Nếu chuột bị khóa tâm ngắm (FPS), bấm thẳng từ giữa màn hình; nếu mở chuột, bấm theo tọa độ trỏ
            Ray ray = (Cursor.lockState == CursorLockMode.Locked) 
                ? new Ray(_cam.transform.position, _cam.transform.forward) 
                : _cam.ScreenPointToRay(GetPointerPosition());

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactableLayer))
            {
                return hit.collider.GetComponentInParent<IInteractable>();
            }

            return null;
        }

        private static Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#endif
        }

        private void OnDisable()
        {
            if (_currentHovered != null)
            {
                _currentHovered.OnHoverExit();
                _currentHovered = null;
            }
        }
    }
}
