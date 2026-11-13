using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class DetailOrbitZoom : MonoBehaviour
{
    [Header("Camera Target")]
    [SerializeField] private Transform pivot;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 0.15f;
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Zoom")]
    [SerializeField] private float distance = 8f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 1000f;
    [SerializeField] private float zoomSpeed = 1f;

    [Header("Initial View")]
    [SerializeField] private float initialYaw = 0f;
    [SerializeField] private float initialPitch = 20f;

    [Header("Camera Framing")]
    [SerializeField] private float framingPadding = 1.25f;

    private float yaw;
    private float pitch;
    private Vector3 orbitCenter;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalFarClipPlane;
    private Camera targetCamera;
    private bool isOrbitEnabled;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        if (targetCamera != null)
            originalFarClipPlane = targetCamera.farClipPlane;

        yaw = initialYaw;
        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
        isOrbitEnabled = false;
    }

    public void SetPivot(Transform target)
    {
        pivot = target;
        if (target != null)
            orbitCenter = target.position;
    }

    // Tương thích với những nơi đang gọi phiên bản một tham số.
    public void EnableForTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("DetailOrbitZoom: Không có Target.", this);
            return;
        }

        EnableForTarget(target, new Bounds(target.position, Vector3.one));
    }

    // Căn camera theo Bounds của toàn bộ mô hình.
    public void EnableForTarget(Transform target, Bounds targetBounds)
    {
        if (target == null)
        {
            Debug.LogWarning("DetailOrbitZoom: Không có Target.", this);
            return;
        }

        pivot = target;
        orbitCenter = targetBounds.center;

        float radius = Mathf.Max(targetBounds.extents.magnitude, 0.5f);
        float verticalFov = targetCamera != null ? targetCamera.fieldOfView : 60f;
        float aspect = targetCamera != null ? targetCamera.aspect : 16f / 9f;

        float halfVertical = verticalFov * 0.5f * Mathf.Deg2Rad;
        float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);
        float halfFov = Mathf.Max(Mathf.Min(halfVertical, halfHorizontal), 0.01f);

        distance = radius / Mathf.Sin(halfFov);
        distance *= Mathf.Max(1f, framingPadding);
        distance = Mathf.Max(minDistance, distance);
        maxDistance = Mathf.Max(maxDistance, distance * 3f);

        yaw = initialYaw;
        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);

        if (targetCamera != null)
        {
            float requiredFarClip = distance + radius * 3f + 10f;
            if (targetCamera.farClipPlane < requiredFarClip)
                targetCamera.farClipPlane = requiredFarClip;
        }

        isOrbitEnabled = true;
        ApplyCameraPosition();

        Debug.Log(
            "DetailOrbitZoom: Đã căn camera. Tâm=" + orbitCenter +
            ", Kích thước=" + targetBounds.size +
            ", Khoảng cách=" + distance,
            this
        );
    }

    public void DisableAndRestore()
    {
        isOrbitEnabled = false;
        pivot = null;
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        if (targetCamera != null)
            targetCamera.farClipPlane = originalFarClipPlane;
    }

    private void Update()
    {
        if (!isOrbitEnabled || pivot == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        bool rotating = false;
        Vector2 mouseDelta = Vector2.zero;
        float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            rotating = Mouse.current.rightButton.isPressed;
            mouseDelta = Mouse.current.delta.ReadValue();
            scroll = Mouse.current.scroll.ReadValue().y / 120f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!rotating && Input.GetMouseButton(1))
        {
            rotating = true;
            mouseDelta = new Vector2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")
            ) * 20f;
        }

        if (Mathf.Approximately(scroll, 0f))
            scroll = Input.GetAxis("Mouse ScrollWheel") * 10f;
#endif

        if (rotating)
        {
            yaw += mouseDelta.x * rotationSpeed;
            pitch -= mouseDelta.y * rotationSpeed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private void LateUpdate()
    {
        if (!isOrbitEnabled || pivot == null)
            return;

        ApplyCameraPosition();
    }

    private void ApplyCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = orbitCenter - rotation * Vector3.forward * distance;
    }
}
