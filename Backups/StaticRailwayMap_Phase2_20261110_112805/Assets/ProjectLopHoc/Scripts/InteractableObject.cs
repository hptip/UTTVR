using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion; // Cần dùng thư viện Fusion để chuyển Scene đồng bộ mạng
using ProjectLopHoc.Core.Interfaces;

public class InteractableObject : MonoBehaviour, IInteractable
{
    [Header("Thông tin Tương tác")]
    public string objectName = "Cửa Lớp Học";
    
    [Header("Hiển thị Highlight")]
    public Renderer highlightRenderer; 

    [Header("Chuyển Scene")]
    [Tooltip("Để trống nếu không phải là cửa. Nhập chính xác tên Scene cần đến, ví dụ: Classroom")]
    public string targetSceneName = "Classroom";

    private void Awake()
    {
        if (highlightRenderer == null)
        {
            highlightRenderer = GetComponent<Renderer>();
        }
        
        if (highlightRenderer != null)
        {
            highlightRenderer.enabled = false;
        }
    }

    public void OnHoverEnter()
    {
        if (highlightRenderer != null)
        {
            highlightRenderer.enabled = true;
        }
    }

    public void OnHoverExit()
    {
        if (highlightRenderer != null)
        {
            highlightRenderer.enabled = false;
        }
    }

    public virtual void OnInteract()
    {
        Debug.Log($"[Đã tương tác]: {objectName} ({gameObject.name})");

        // Nếu có khai báo Scene đích, thực hiện chuyển Scene
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            ChuyenScene(targetSceneName);
        }
    }

    public void OnInteract(GameObject interactor) => OnInteract();

    private void ChuyenScene(string sceneName)
    {
        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        
        // Tìm số thứ tự index của scene trong Build Settings theo tên file
        int sceneIndex = TimIndexCuaScene(sceneName);

        if (sceneIndex < 0)
        {
            Debug.LogError($"[InteractableObject] Không tìm thấy Scene '{sceneName}' trong Build Settings! Hãy kiểm tra File > Build Settings.");
            return;
        }

        // Nếu đang chạy mạng Photon Fusion
        if (runner != null && runner.IsRunning)
        {
            runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        }
        else
        {
            // Dự phòng khi chạy đơn máy test cục bộ
            SceneManager.LoadScene(sceneIndex);
        }
    }

    // Hàm hỗ trợ tìm index bằng tên ngắn (ví dụ chỉ cần truyền "Showroom")
    private int TimIndexCuaScene(string targetName)
    {
        int totalScenes = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < totalScenes; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (string.Equals(fileName, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}
