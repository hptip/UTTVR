using UnityEngine;
using System.Collections.Generic;

namespace ProjectLopHoc.Features.Quiz
{
    [CreateAssetMenu(fileName = "NewQuiz", menuName = "ProjectLopHoc/Configs/Quiz Config")]
    public class QuizSO : ScriptableObject
    {
        [Header("Thông tin đề thi")]
        public string quizName = "Đề thi 1";

        [Header("Danh sách câu hỏi (dùng nút Import ở dưới, không cần gõ tay)")]
        public List<QuizQuestion> questions = new List<QuizQuestion>();
    }
}
