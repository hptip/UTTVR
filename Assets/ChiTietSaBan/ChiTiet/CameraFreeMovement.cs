
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFreeMove : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastSpeed = 2f;

    [Header("Camera Rotation")]
    public float mouseSensitivity = 2f;

    [Header("Zoom")]
    public float zoomSpeed = 10f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        float speed = moveSpeed;

        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftShiftKey.isPressed)
            speed *= fastSpeed;

        Vector3 direction = Vector3.zero;

        if (Keyboard.current.wKey.isPressed)
            direction += transform.forward;

        if (Keyboard.current.sKey.isPressed)
            direction -= transform.forward;

        if (Keyboard.current.dKey.isPressed)
            direction += transform.right;

        if (Keyboard.current.aKey.isPressed)
            direction -= transform.right;

        if (Keyboard.current.eKey.isPressed)
            direction += Vector3.up;

        if (Keyboard.current.qKey.isPressed)
            direction -= Vector3.up;

        transform.position += direction.normalized * speed * Time.deltaTime;

        #else
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastSpeed;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 direction =
            transform.right * horizontal +
            transform.forward * vertical;

        if (Input.GetKey(KeyCode.E))
            direction += Vector3.up;

        if (Input.GetKey(KeyCode.Q))
            direction -= Vector3.up;

        transform.position += direction.normalized * speed * Time.deltaTime;
        #endif
    }

    private void HandleRotation()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        #if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            yaw += delta.x * mouseSensitivity * 0.1f;
            pitch -= delta.y * mouseSensitivity * 0.1f;

            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        #else
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity * 10f;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * 10f;

            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        #endif
    }

    private void HandleZoom()
    {
        #if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y / 120f;

        transform.position +=
            transform.forward * scroll * zoomSpeed * Time.deltaTime * 10f;

        #else
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        transform.position +=
            transform.forward * scroll * zoomSpeed * 10f;
        #endif
    }
}
