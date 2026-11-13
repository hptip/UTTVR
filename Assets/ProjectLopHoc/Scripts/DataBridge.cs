using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using ProjectLopHoc.Features.Authentication.Services;

public class DataBridge : MonoBehaviour
{
    public static DataBridge Instance;

    [Header("Offline VR Demo")]
    [SerializeField] private bool offlineDemo = true;

    #if UNITY_EDITOR
    private string baseUrl = "http://127.0.0.1:5000/api";
    #else
    private string baseUrl = "/api";
    #endif

    public int CurrentUserId { get; private set; } = -1;
    public string CurrentUsername { get; private set; }
    public string CurrentFullName { get; private set; }
    public string CurrentRole { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class UserData
    {
        public int user_id;
        public string username;
        public string full_name;
        public string role;
    }

    [Serializable]
    private class LoginResponse
    {
        public string status;
        public string message;
        public UserData data;
    }

    [Serializable]
    private class QuizResultPayload
    {
        public int user_id;
        public string quiz_title;
        public int score;
        public int total_questions;
    }

    // Đăng nhập trả về kết quả async
    public async Task<(bool success, string message)> LoginAsync(string username, string password)
    {
        if (offlineDemo)
        {
            // This path never constructs or sends a UnityWebRequest.
            await Task.Yield();
            var user = OfflineAuthenticationService.FindUser(username);
            if (user == null) return (false, "Không tìm thấy tài khoản.");
            if (!OfflineAuthenticationService.VerifyPassword(user, password))
                return (false, "Mật khẩu không chính xác.");

            CurrentUserId = user.id;
            CurrentUsername = user.username;
            CurrentFullName = user.full_name;
            CurrentRole = user.role;
            return (true, $"Xin chào {CurrentFullName}!");
        }

        LoginRequest reqData = new LoginRequest { username = username, password = password };
        string jsonBody = JsonUtility.ToJson(reqData);

        using (UnityWebRequest req = new UnityWebRequest($"{baseUrl}/auth/login", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    LoginResponse res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    CurrentUserId = res.data.user_id;
                    CurrentUsername = res.data.username;
                    CurrentFullName = res.data.full_name;
                    CurrentRole = res.data.role;

                    return (true, $"Xin chào {CurrentFullName}!");
                }
                catch (Exception e)
                {
                    return (false, "Lỗi phân tích dữ liệu server: " + e.Message);
                }
            }
            else
            {
                return (false, "Tài khoản hoặc mật khẩu không chính xác!");
            }
        }
    }

    public void SaveQuizResult(string quizTitle, int score, int totalQuestions)
    {
        if (offlineDemo) return;
        if (CurrentUserId <= 0)
        {
            Debug.LogError("[DataBridge] Chưa đăng nhập. Không thể lưu điểm!");
            return;
        }

        QuizResultPayload payload = new QuizResultPayload
        {
            user_id = CurrentUserId,
            quiz_title = quizTitle,
            score = score,
            total_questions = totalQuestions
        };

        StartCoroutine(PostSimpleRoutine($"{baseUrl}/quiz/submit", JsonUtility.ToJson(payload)));
    }

    private System.Collections.IEnumerator PostSimpleRoutine(string url, string jsonBody)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
        }
    }

    [Serializable]
    public class StudentScoreRecord
    {
        public string full_name;
        public string student_id;
        public string score;
    }

    [Serializable]
    private class StudentListResponse
    {
        public string status;
        public StudentScoreRecord[] data;
    }

    // Hàm lấy danh sách sinh viên từ máy chủ API
    public async Task<System.Collections.Generic.List<StudentScoreRecord>> GetStudentRecordsAsync()
    {
        var list = new System.Collections.Generic.List<StudentScoreRecord>();

        if (offlineDemo)
        {
            AddFallbackStudentRecords(list);
            return list;
        }

        using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/students/scores"))
        {
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    StudentListResponse res = JsonUtility.FromJson<StudentListResponse>(req.downloadHandler.text);
                    if (res != null && res.data != null)
                    {
                        list.AddRange(res.data);
                        return list;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[DataBridge] Lỗi đọc JSON: " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("[DataBridge] Chưa kết nối được endpoint /students/scores. Dùng dữ liệu mẫu.");
            }
        }

        // Dữ liệu dự phòng tự động khi máy chủ backend chưa bật endpoint
        AddFallbackStudentRecords(list);
        return list;
    }

    private static void AddFallbackStudentRecords(System.Collections.Generic.List<StudentScoreRecord> list)
    {
        list.Add(new StudentScoreRecord { full_name = "Nguyễn Văn An", student_id = "74DCTT21001", score = "8.5" });
        list.Add(new StudentScoreRecord { full_name = "Trần Thị Bình", student_id = "74DCTT21002", score = "9.0" });
        list.Add(new StudentScoreRecord { full_name = "Lê Hoàng Cường", student_id = "74DCTT21003", score = "7.0" });
        list.Add(new StudentScoreRecord { full_name = "Phạm Minh Đức", student_id = "74DCTT21004", score = "10.0" });
    }
}
