using UnityEngine;

/// <summary>Player mẫu (chỉ dùng trong Demo): chuột xoay camera, WASD di chuyển.</summary>
public class DemoPlayer : MonoBehaviour
{
    public float speed = 5f;
    public float sensitivity = 0.15f;
    float yaw, pitch;

    void Start()
    {
        var e = transform.eulerAngles;
        yaw = e.y; pitch = e.x;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector2 look; Vector2 move;
#if ENABLE_LEGACY_INPUT_MANAGER
        look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (sensitivity * 10f);
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        var k = UnityEngine.InputSystem.Keyboard.current;
        look = m != null ? m.delta.ReadValue() * sensitivity : Vector2.zero;
        move = Vector2.zero;
        if (k != null)
        {
            move.x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            move.y = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
        }
#else
        look = Vector2.zero; move = Vector2.zero;
#endif
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position += (transform.right * move.x + transform.forward * move.y) * (speed * Time.deltaTime);
    }
}
