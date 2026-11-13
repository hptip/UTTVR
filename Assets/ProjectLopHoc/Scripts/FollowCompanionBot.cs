using TMPro;
using UnityEngine;

public class FollowCompanionBot : MonoBehaviour
{
    [Header("Cấu hình hiển thị")]
    public string botName = "Bot Hỗ trợ";
    public TMP_Text nameplateText;
    public Transform nameplateTransform;

    [Header("Thông số bám đuổi")]
    public float moveSpeed = 3.2f;
    public float stopDistance = 2.5f;
    public float rotationSpeed = 6.0f;

    [Header("Bù góc xoay Model FBX")]
    [Tooltip("Gõ góc lệch trục của model, ví dụ: (0, -90, 0)")]
    public Vector3 modelRotationOffset = new Vector3(0f, -90f, 0f);

    
    private Transform _playerTransform;
    private Camera _mainCamera;

    private void Start()
    {
        if (nameplateText != null)
        {
            nameplateText.text = $"<color=#00FFFF>[AI]</color>\n{botName}";
        }

        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // Liên tục dò tìm nhân vật khi vừa spawn vào Scene
        if (_playerTransform == null)
        {
            FindLocalPlayer();
            return;
        }

        FollowPlayerTarget();
        UpdateNameplateBillboard();
    }

    private void FindLocalPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
    }

    private void FollowPlayerTarget()
    {
        Vector3 targetPosition = _playerTransform.position;
        Vector3 currentPosition = transform.position;

        // Chỉ tính toán khoảng cách trên mặt phẳng ngang XZ, bỏ qua độ cao Y
        Vector3 flatTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
        float distance = Vector3.Distance(currentPosition, flatTarget);

        // Di chuyển lại gần nếu người chơi bước ra xa
        if (distance > stopDistance)
        {
            Vector3 direction = (flatTarget - currentPosition).normalized;
            transform.position = Vector3.MoveTowards(currentPosition, flatTarget, moveSpeed * Time.deltaTime);

            if (direction != Vector3.zero)
            {
                // Nhân thêm góc bù vào LookRotation
                Quaternion targetRot = Quaternion.LookRotation(direction) * Quaternion.Euler(modelRotationOffset);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // Đứng lại và quay mặt hướng về phía người chơi
            Vector3 lookDir = (flatTarget - currentPosition).normalized;
            if (lookDir != Vector3.zero)
            {
                // Nhân thêm góc bù vào LookRotation
                Quaternion targetRot = Quaternion.LookRotation(lookDir) * Quaternion.Euler(modelRotationOffset);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void UpdateNameplateBillboard()
    {
        if (nameplateTransform == null) return;

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera != null)
        {
            nameplateTransform.rotation = _mainCamera.transform.rotation;
        }
    }
}