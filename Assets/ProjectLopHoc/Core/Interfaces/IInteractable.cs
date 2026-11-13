using UnityEngine;

namespace ProjectLopHoc.Core.Interfaces
{

    /// Giao diện chuẩn cho tất cả các đối tượng có thể tương tác trên nền tảng PC / WebGL.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Kích hoạt khi người dùng click chuột trái hoặc bấm phím tương tác (E).
        /// </summary>
        /// <param name="interactor">GameObject của nhân vật thực hiện hành động</param>
        void OnInteract(GameObject interactor);

        /// <summary>
        /// Kích hoạt khi tâm ngắm hoặc trỏ chuột bắt đầu trỏ vào vật thể.
        /// </summary>
        void OnHoverEnter();

        /// <summary>
        /// Kích hoạt khi tâm ngắm hoặc trỏ chuột rời khỏi vật thể.
        /// </summary>
        void OnHoverExit();
    }
}