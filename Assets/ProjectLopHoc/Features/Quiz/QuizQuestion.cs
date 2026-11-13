using System;

namespace ProjectLopHoc.Features.Quiz
{
    [Serializable]
    public class QuizQuestion
    {
        public string questionText;
        public string[] options = new string[4]; // A, B, C, D
        public int correctIndex; // 0=A, 1=B, 2=C, 3=D
    }
}
