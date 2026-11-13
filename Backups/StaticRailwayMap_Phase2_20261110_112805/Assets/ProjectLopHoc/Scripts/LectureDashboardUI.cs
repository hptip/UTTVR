using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLopHoc.UI
{
    public class LectureDashboardUI : MonoBehaviour
    {
        [Header("Khung hiển thị chi tiết bên trái")]
        public TMP_Text textName;
        public TMP_Text textMsv;
        public TMP_Text textScore;

        [Header("Khung cuộn danh sách bên phải")]
        public Transform contentContainer;
        public GameObject studentNameButtonPrefab;

        private List<DataBridge.StudentScoreRecord> _studentList = new List<DataBridge.StudentScoreRecord>();

        private void OnEnable()
        {
            LoadStudentData();
        }

        public async void LoadStudentData()
        {
            if (contentContainer == null || studentNameButtonPrefab == null)
            {
                Debug.LogWarning("[LectureDashboardUI] Chưa gán Content Container hoặc StudentNameButtonPrefab.");
                return;
            }

            // Dọn sạch danh sách cũ trong ScrollView
            for (int i = contentContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(contentContainer.GetChild(i).gameObject);
            }

            // Lấy danh sách từ DataBridge
            if (DataBridge.Instance != null)
            {
                _studentList = await DataBridge.Instance.GetStudentRecordsAsync();
            }

            // Tạo các nút tên sinh viên vào danh sách cuộn
            foreach (var student in _studentList)
            {
                GameObject btnObj = Instantiate(studentNameButtonPrefab, contentContainer);

                TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = student.full_name;
                }

                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    DataBridge.StudentScoreRecord current = student;
                    btn.onClick.AddListener(() => DisplayDetail(current));
                }
            }

            // Mặc định chọn dòng đầu tiên
            if (_studentList.Count > 0)
            {
                DisplayDetail(_studentList[0]);
            }
            else
            {
                ClearDetails();
            }
        }

        public void DisplayDetail(DataBridge.StudentScoreRecord student)
        {
            if (textName != null) textName.text = $"Họ tên: {student.full_name}";
            if (textMsv != null) textMsv.text = $"Mã SV: {student.student_id}";
            if (textScore != null) textScore.text = $"Điểm: {student.score}";
        }

        private void ClearDetails()
        {
            if (textName != null) textName.text = "Họ tên: Chờ cập nhật";
            if (textMsv != null) textMsv.text = "Mã SV: Chờ cập nhật";
            if (textScore != null) textScore.text = "Điểm: Chờ cập nhật";
        }
    }
}