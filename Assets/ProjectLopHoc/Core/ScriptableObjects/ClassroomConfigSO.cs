using UnityEngine;
using System.Collections.Generic;

namespace ProjectLopHoc.Core.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewClassroomConfig", menuName = "ProjectLopHoc/Configs/Classroom Config")]
    public class ClassroomConfigSO : ScriptableObject
    {
        [Header("Thông tin Lớp Học")]
        public string classroomId = "ROOM_101";
        public string classroomName = "Phòng học Lý thuyết & Thực hành Đường sắt";
        public int maxStudents = 30;

        [Header("Cấu hình Bài giảng (PPT Slides)")]
        [Tooltip("Tên các thư mục con trong Assets/Resources/Lectures/ - VD: BaiGiang_01, BaiGiang_02. Thứ tự trong danh sách này quyết định thứ tự Next/Prev Lecture.")]
        public List<string> availableLectures = new List<string> { "BaiGiang_01", "BaiGiang_02" };

        [Header("Mật khẩu Giảng viên (Tạm thời lưu tại đây)")]
        public string lecturerSecretPin = "123456";
    }
}
