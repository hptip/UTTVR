using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ProjectLopHoc.Features.Quiz.Editor
{
    /// <summary>
    /// Thêm nút "Import đề thi từ file .txt" ngay trong Inspector của QuizSO.
    /// Định dạng file .txt:
    ///
    /// Câu 1: Nội dung câu hỏi?
    /// A. Đáp án 1
    /// *B. Đáp án đúng (có dấu * ngay trước chữ cái)
    /// C. Đáp án 3
    /// D. Đáp án 4
    /// [dòng trống]
    /// Câu 2: ...
    ///
    /// Phải nằm trong 1 thư mục tên "Editor" để Unity nhận diện đúng.
    /// </summary>
    [CustomEditor(typeof(QuizSO))]
    public class QuizSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Import đề thi", EditorStyles.boldLabel);

            if (GUILayout.Button("Chọn file .txt và Import", GUILayout.Height(30)))
            {
                string path = EditorUtility.OpenFilePanel("Chọn file đề thi (.txt)", Application.dataPath, "txt");
                if (!string.IsNullOrEmpty(path))
                {
                    ImportFromFile(path);
                }
            }
        }

        private void ImportFromFile(string path)
        {
            var quizSO = (QuizSO)target;
            string content = File.ReadAllText(path);

            // Tách thành từng khối câu hỏi, ngăn cách bởi 1 hoặc nhiều dòng trống
            string[] blocks = content
                .Replace("\r\n", "\n")
                .Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

            var newQuestions = new List<QuizQuestion>();
            int errorCount = 0;

            foreach (var block in blocks)
            {
                var lines = block.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToList();

                if (lines.Count < 5) // 1 dòng câu hỏi + 4 dòng đáp án tối thiểu
                {
                    errorCount++;
                    continue;
                }

                var question = new QuizQuestion();
                question.questionText = lines[0];
                question.options = new string[4];
                question.correctIndex = -1;

                for (int i = 0; i < 4; i++)
                {
                    string line = lines[i + 1];
                    bool isCorrect = line.StartsWith("*");
                    if (isCorrect)
                    {
                        line = line.Substring(1).Trim();
                        question.correctIndex = i;
                    }

                    // Bỏ tiền tố "A." "B." "C." "D." nếu có
                    if (line.Length > 2 && line[1] == '.')
                    {
                        line = line.Substring(2).Trim();
                    }

                    question.options[i] = line;
                }

                if (question.correctIndex == -1)
                {
                    Debug.LogWarning($"[QuizSOEditor] Câu hỏi \"{question.questionText}\" không có đáp án nào đánh dấu * - bỏ qua.");
                    errorCount++;
                    continue;
                }

                newQuestions.Add(question);
            }

            quizSO.questions = newQuestions;
            EditorUtility.SetDirty(quizSO);
            AssetDatabase.SaveAssets();

            string msg = $"Import xong: {newQuestions.Count} câu hỏi hợp lệ.";
            if (errorCount > 0) msg += $" Có {errorCount} câu bị lỗi định dạng, đã bỏ qua.";

            EditorUtility.DisplayDialog("Import đề thi", msg, "OK");
            Debug.Log("[QuizSOEditor] " + msg);
        }
    }
}
