using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;

// Gắn script này vào RawImage hiển thị minimap.
// Bấm vào điểm nào trên minimap thì nhân vật CỦA MÌNH (không phải của người khác) dịch chuyển tới đó.
// Không cần gắn tag Player: dự án dùng Photon Fusion (chế độ Shared) nên nhân vật của mình
// chính là FirstPersonController có HasStateAuthority = true.
public class MinimapTeleport : MonoBehaviour, IPointerClickHandler
{
    [Header("Camera nhìn từ trên xuống dùng cho minimap")]
    public Camera minimapCamera;

    [Header("Bật/tắt bảng minimap bằng phím (tuỳ chọn)")]
    public GameObject mapPanel;          // kéo Panel chứa minimap vào đây (để trống nếu không cần)
    public KeyCode toggleKey = KeyCode.M;

    [Header("Dịch chuyển")]
    public float rayStartHeight = 500f;  // độ cao bắn tia xuống để tìm mặt đất
    public float heightOffset = 0.3f;    // nâng nhân vật lên chút để không kẹt xuống sàn
    public LayerMask groundMask = ~0;

    private FirstPersonController _localPlayer;

    private void Update()
    {
        if (mapPanel != null && Input.GetKeyDown(toggleKey))
        {
            bool open = !mapPanel.activeSelf;
            mapPanel.SetActive(open);

            // FirstPersonController chỉ cho di chuyển khi chuột đang bị khoá,
            // nên mở bản đồ thì mở khoá chuột để bấm, đóng bản đồ thì khoá lại.
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (minimapCamera == null || !minimapCamera.orthographic)
        {
            Debug.LogWarning("[MinimapTeleport] Chưa gán minimapCamera hoặc camera chưa để Orthographic.");
            return;
        }

        FirstPersonController player = GetLocalPlayer();
        if (player == null)
        {
            Debug.LogWarning("[MinimapTeleport] Chưa tìm thấy nhân vật của mình (có thể chưa spawn xong).");
            return;
        }

        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        // Đổi điểm bấm thành toạ độ 0..1 trong ảnh minimap
        Vector2 uv = Rect.PointToNormalized(rect.rect, localPoint);

        // Camera nhìn xuống (Rotation X = 90): trục phải = +X thế giới, trục lên = +Z thế giới
        float size = minimapCamera.orthographicSize;
        Vector3 camPos = minimapCamera.transform.position;
        float worldX = camPos.x + (uv.x - 0.5f) * 2f * size * minimapCamera.aspect;
        float worldZ = camPos.z + (uv.y - 0.5f) * 2f * size;

        // Tìm độ cao mặt đất tại điểm đó
        float worldY = player.transform.position.y;
        Vector3 rayOrigin = new Vector3(worldX, rayStartHeight, worldZ);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayStartHeight * 2f, groundMask))
        {
            worldY = hit.point.y;
        }

        Vector3 target = new Vector3(worldX, worldY + heightOffset, worldZ);

        // Dịch chuyển bằng NetworkCharacterController để Fusion đồng bộ cho mọi người
        var ncc = player.GetComponent<NetworkCharacterController>();
        if (ncc != null)
        {
            ncc.Teleport(target);
        }
        else
        {
            player.transform.position = target;
        }
    }

    // Nhân vật của mình = FirstPersonController có quyền điều khiển (HasStateAuthority)
    private FirstPersonController GetLocalPlayer()
    {
        if (_localPlayer != null && _localPlayer.Object != null && _localPlayer.HasStateAuthority)
            return _localPlayer;

        var all = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p.Object != null && p.HasStateAuthority)
            {
                _localPlayer = p;
                return p;
            }
        }
        return null;
    }
}
