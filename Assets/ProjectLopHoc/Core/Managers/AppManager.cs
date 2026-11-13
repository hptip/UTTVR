using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using System;
using System.Threading.Tasks;
using ProjectLopHoc.Features.Authentication.Controllers;

namespace ProjectLopHoc.Core.Managers
{
    [RequireComponent(typeof(NetworkRunner))]
    [RequireComponent(typeof(NetworkSceneManagerDefault))]
    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance { get; private set; }

        [Header("--- Cấu hình Mạng ---")]
        [Tooltip("Tên phòng mặc định khi mới đăng nhập vào Campus")]
        public string defaultCampusSession =
            "Campus_Main_Session";

        [Header("--- Cấu hình Timeout ---")]
        [Tooltip("Thời gian tối đa chờ kết nối")]
        public float connectionTimeout = 25f;

        private NetworkRunner _runner;

        private NetworkSceneManagerDefault _sceneManager;

        // Khóa StartGame khi đang kết nối
        private bool _isConnecting = false;

        private void Awake()
        {
            Debug.Log(
                "[AppManager] Awake()"
            );

            // ==========================================
            // SINGLETON
            // ==========================================

            if (
                Instance != null &&
                Instance != this
            )
            {
                Debug.LogWarning(
                    "[AppManager] Đã có AppManager khác. " +
                    "Destroy object này."
                );

                Destroy(gameObject);

                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            // ==========================================
            // LẤY COMPONENT
            // ==========================================

            _runner =
                GetComponent<NetworkRunner>();

            _sceneManager =
                GetComponent<NetworkSceneManagerDefault>();

            if (_runner == null)
            {
                Debug.LogError(
                    "[AppManager] Không tìm thấy NetworkRunner!"
                );

                return;
            }

            if (_sceneManager == null)
            {
                Debug.LogError(
                    "[AppManager] Không tìm thấy " +
                    "NetworkSceneManagerDefault!"
                );

                return;
            }

            Debug.Log(
                "[AppManager] NetworkRunner sẵn sàng."
            );
        }

        private void OnEnable()
        {
            Debug.Log(
                "[AppManager] Đăng ký OnLoginSuccess."
            );

            // Tránh đăng ký event nhiều lần
            LoginController.OnLoginSuccess -=
                HandleLoginSuccess;

            LoginController.OnLoginSuccess +=
                HandleLoginSuccess;
        }

        private void OnDisable()
        {
            Debug.Log(
                "[AppManager] Hủy đăng ký OnLoginSuccess."
            );

            LoginController.OnLoginSuccess -=
                HandleLoginSuccess;
        }

        // ==============================================
        // LOGIN SUCCESS
        // ==============================================

        private async void HandleLoginSuccess(
            string displayName,
            bool isLecturer)
        {
            Debug.Log(
                $"[AppManager] Nhận LoginSuccess: " +
                $"{displayName}"
            );

            // ==========================================
            // CHỐNG GỌI KẾT NỐI LẦN 2
            // ==========================================

            if (_isConnecting)
            {
                Debug.LogWarning(
                    "[AppManager] Đang kết nối rồi. " +
                    "Bỏ qua yêu cầu thứ hai."
                );

                return;
            }

            // Nếu Runner đã chạy thành công
            // thì không StartGame lại
            if (
                _runner != null &&
                _runner.IsRunning
            )
            {
                Debug.LogWarning(
                    "[AppManager] NetworkRunner đã chạy. " +
                    "Không StartGame lại."
                );

                return;
            }

            await ConnectToSession(
                defaultCampusSession
            );
        }

        // ==============================================
        // CONNECT TO SESSION
        // ==============================================

        public async Task ConnectToSession(
            string sessionName)
        {
            // ==========================================
            // KHÓA KẾT NỐI
            // ==========================================

            if (_isConnecting)
            {
                Debug.LogWarning(
                    "[AppManager] ConnectToSession đang chạy."
                );

                return;
            }

            _isConnecting = true;

            try
            {
                Debug.Log(
                    $"[AppManager] Bắt đầu kết nối: " +
                    $"{sessionName}"
                );

                // ======================================
                // KIỂM TRA RUNNER
                // ======================================

                if (_runner == null)
                {
                    Debug.LogError(
                        "[AppManager] NetworkRunner không tồn tại!"
                    );

                    return;
                }

                if (_sceneManager == null)
                {
                    Debug.LogError(
                        "[AppManager] NetworkSceneManagerDefault " +
                        "không tồn tại!"
                    );

                    return;
                }

                // ======================================
                // KHÔNG REUSE RUNNER
                // ======================================

                if (_runner.IsRunning)
                {
                    Debug.LogWarning(
                        "[AppManager] Runner đang chạy. " +
                        "Không gọi StartGame lần nữa."
                    );

                    return;
                }

                // ======================================
                // KIỂM TRA SESSION
                // ======================================

                if (
                    string.IsNullOrWhiteSpace(
                        sessionName
                    )
                )
                {
                    Debug.LogError(
                        "[AppManager] Session Name đang rỗng!"
                    );

                    return;
                }

                // ======================================
                // SCENE
                // ======================================

                int sceneIndex =
                    SceneManager.GetActiveScene()
                        .buildIndex;

                if (sceneIndex < 0)
                {
                    sceneIndex = 0;
                }

                Debug.Log(
                    $"[AppManager] Scene: " +
                    $"{SceneManager.GetActiveScene().name}"
                );

                Debug.Log(
                    $"[AppManager] Scene Index: " +
                    $"{sceneIndex}"
                );

                // ======================================
                // START GAME ARGS
                // ======================================

                var startArgs =
                    new StartGameArgs
                    {
                        GameMode =
                            GameMode.Shared,

                        SessionName =
                            sessionName,

                        Scene =
                            SceneRef.FromIndex(
                                sceneIndex
                            ),

                        SceneManager =
                            _sceneManager
                    };

                Debug.Log(
                    "[AppManager] Đang gọi StartGame()..."
                );

                // ======================================
                // START GAME
                // ======================================

                Task<StartGameResult> startTask =
                    _runner.StartGame(
                        startArgs
                    );

                // ======================================
                // TIMEOUT
                // ======================================

                Task timeoutTask =
                    Task.Delay(
                        TimeSpan.FromSeconds(
                            connectionTimeout
                        )
                    );

                Task completedTask =
                    await Task.WhenAny(
                        startTask,
                        timeoutTask
                    );

                // ======================================
                // TIMEOUT
                // ======================================

                if (
                    completedTask ==
                    timeoutTask
                )
                {
                    Debug.LogError(
                        $"[AppManager] KẾT NỐI TIMEOUT " +
                        $"sau {connectionTimeout} giây."
                    );

                    return;
                }

                // ======================================
                // RESULT
                // ======================================

                StartGameResult result =
                    await startTask;

                if (result.Ok)
                {
                    Debug.Log(
                        "<color=#00FF00>" +
                        "[AppManager] KẾT NỐI THÀNH CÔNG!" +
                        "</color>"
                    );

                    Debug.Log(
                        $"[AppManager] Session: " +
                        $"{sessionName}"
                    );
                }
                else
                {
                    Debug.LogError(
                        "[AppManager] KẾT NỐI THẤT BẠI: " +
                        $"{result.ShutdownReason}"
                    );
                }
            }
            catch (
                OperationCanceledException
            )
            {
                Debug.LogWarning(
                    "[AppManager] OperationCanceled: " +
                    "kết nối đã bị hủy."
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AppManager] Lỗi kết nối:\n" +
                    ex
                );
            }
            finally
            {
                // Mở khóa
                _isConnecting = false;

                Debug.Log(
                    "[AppManager] ConnectToSession() kết thúc."
                );
            }
        }

        // ==============================================
        // SWITCH CLASSROOM
        // ==============================================

        public async void SwitchToClassroomSession(
            string classroomSessionName,
            int classroomSceneIndex)
        {
            if (_isConnecting)
            {
                Debug.LogWarning(
                    "[AppManager] Đang kết nối. " +
                    "Không thể chuyển phòng."
                );

                return;
            }

            if (
                _runner == null ||
                _runner.IsRunning == false
            )
            {
                Debug.LogError(
                    "[AppManager] NetworkRunner chưa chạy."
                );

                return;
            }

            _isConnecting = true;

            try
            {
                Debug.Log(
                    $"[AppManager] Chuyển sang phòng học: " +
                    $"{classroomSessionName}"
                );

                // Lưu ý:
                // Không gọi StartGame() lần nữa trên Runner
                // đang chạy.
                //
                // Việc chuyển Scene/Session nên xử lý
                // bằng logic Fusion phù hợp với kiến trúc
                // project của bạn.

                Debug.Log(
                    "[AppManager] SwitchToClassroomSession " +
                    "được gọi."
                );

                // Nếu sau này bạn muốn chuyển sang
                // một Scene khác, hãy xử lý riêng ở đây.
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AppManager] Lỗi chuyển phòng học:\n" +
                    ex
                );
            }
            finally
            {
                _isConnecting = false;
            }
        }

        // ==============================================
        // DESTROY
        // ==============================================

        private async void OnDestroy()
        {
            if (Instance != this)
                return;

            if (
                _runner != null &&
                _runner.IsRunning
            )
            {
                try
                {
                    Debug.Log(
                        "[AppManager] Shutdown Runner..."
                    );

                    await _runner.Shutdown();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AppManager] Lỗi Shutdown: " +
                        ex.Message
                    );
                }
            }
        }
    }
}