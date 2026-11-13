using System;
using UnityEngine;

namespace UTT.SaBan
{
    /// <summary>
    /// Nút trên bảng điều khiển có thể bấm bằng phím E / chuột trái khi người chơi ở góc nhìn thứ nhất
    /// (dùng đúng cơ chế InteractableObject sẵn có của FirstPersonController: ngắm tâm vào nút rồi bấm E).
    /// Khi con trỏ chuột đang mở khóa, bấm trực tiếp bằng chuột qua UI Button như thường.
    /// </summary>
    public sealed class SaBanPanelButton : InteractableObject
    {
        [NonSerialized] public Action onActivate;

        private void Awake()
        {
            // Nút này KHÔNG phải cửa chuyển cảnh
            targetSceneName = "";
        }

        public override void OnInteract()
        {
            onActivate?.Invoke();
        }
    }
}
