using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("--- Cấu hình Nhân vật ---")]
    public NetworkPrefabRef studentPrefab;
    public NetworkPrefabRef lecturerPrefab;

    [Header("--- Vị trí Spawn ---")]
    public Transform studentSpawnPoint;
    public Transform lecturerSpawnPoint;

    private bool _hasSpawned = false;
    private bool _isSpawning = false; // Cờ khóa chặn spawn đồng thời

    private void Start()
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner != null && runner.IsRunning)
        {
            runner.AddCallbacks(this);
            if (runner.LocalPlayer.IsValid || runner.GameMode == GameMode.Single)
            {
                ThucHienSpawnAsync(runner, runner.LocalPlayer);
            }
        }
        else
        {
            KhoiTaoRunnerTestTaiCho();
        }
    }

    private async void KhoiTaoRunnerTestTaiCho()
    {
        if (_hasSpawned || _isSpawning) return;

        GameObject runnerObj = new GameObject("NetworkRunner_TestDirect");
        NetworkRunner runner = runnerObj.AddComponent<NetworkRunner>();
        runnerObj.AddComponent<NetworkSceneManagerDefault>();

        runner.AddCallbacks(this);

        Scene activeScene = SceneManager.GetActiveScene();
        SceneRef activeSceneRef = activeScene.buildIndex >= 0
            ? SceneRef.FromIndex(activeScene.buildIndex)
            : default;

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = PlayerPrefs.GetInt("OfflineDemo", 1) == 1 ? GameMode.Single : GameMode.Shared,
            SessionName = PlayerPrefs.GetInt("OfflineDemo", 1) == 1 ? null : "TestDirect_YenTu",
            Scene = activeSceneRef,
            SceneManager = runnerObj.GetComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("[PlayerSpawner] Đã khởi tạo phòng Test trực tiếp thành công!");
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner != null && runner.IsRunning && (runner.LocalPlayer.IsValid || runner.GameMode == GameMode.Single))
        {
            ThucHienSpawnAsync(runner, runner.LocalPlayer);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            ThucHienSpawnAsync(runner, player);
        }
    }

    private async void ThucHienSpawnAsync(NetworkRunner runner, PlayerRef player)
    {
        // Nếu đã spawn hoặc đang trong tiến trình await spawn thì dừng ngay lập tức
        if (_hasSpawned || _isSpawning || runner == null || !runner.IsRunning) return;

        _isSpawning = true; // Khóa ngay lập tức tại frame này

        bool isLecturer = PlayerPrefs.GetInt("IsLecturer", 0) == 1;
        NetworkPrefabRef prefabToSpawn = isLecturer ? lecturerPrefab : studentPrefab;

        if (!await WaitForTrainReadyAsync())
        {
            _isSpawning = false;
            return;
        }

        Transform targetPoint = ResolveSpawnPoint(isLecturer);

        if (targetPoint == null)
        {
            string expectedName = isLecturer ? "Spawn_Lecturer" : "Spawn_Student";
            Debug.LogError($"[PlayerSpawner] Spawn cancelled: '{expectedName}' was not found in scene '{SceneManager.GetActiveScene().name}'. Refusing to spawn at world origin.", this);
            _isSpawning = false;
            return;
        }

        Vector3 spawnPos = targetPoint.position;
        Quaternion spawnRot = targetPoint.rotation;

        try
        {
            PlayerRef? inputAuthority = runner.GameMode == GameMode.Single
                ? (runner.LocalPlayer.IsValid ? runner.LocalPlayer : (PlayerRef?)null)
                : player;
            var spawnedObj = await runner.SpawnAsync(prefabToSpawn, spawnPos, spawnRot, inputAuthority);
            _hasSpawned = true;
            Debug.Log($"[PlayerSpawner] Đã đưa {(isLecturer ? "Giảng viên" : "Sinh viên")} lên tàu tại {spawnPos}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerSpawner] Lỗi khi Spawn: {ex.Message}");
            _isSpawning = false; // Mở lại nếu thất bại
        }
    }

    private async System.Threading.Tasks.Task<bool> WaitForTrainReadyAsync()
    {
        TrainSplineMover train = FindFirstObjectByType<TrainSplineMover>();
        if (train == null)
            return true;

        // Fusion may report the scene as loaded before the train's Start() has
        // evaluated its spline and moved the root away from world origin. Wait for
        // that initialization before reading a spawn marker parented to the train.
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (train == null)
                break;

            if (train.isActiveAndEnabled && train.splineRoute != null &&
                train.frontCar != null && train.rearCar != null && train.totalRouteLength > 0f)
            {
                // Let the first spline-position update finish before reading the marker.
                await System.Threading.Tasks.Task.Yield();
                return train != null && train.totalRouteLength > 0f;
            }

            await System.Threading.Tasks.Task.Yield();
        }

        Debug.LogError("[PlayerSpawner] Spawn cancelled: YenTu train did not initialize its spline within 10 seconds.", this);
        return false;
    }

    private Transform ResolveSpawnPoint(bool isLecturer)
    {
        Transform configuredPoint = isLecturer ? lecturerSpawnPoint : studentSpawnPoint;
        if (configuredPoint != null)
            return configuredPoint;

        // YenTu's spawn markers are children of a train prefab. Resolve them by name
        // as a runtime fallback because prefab-child references can be null in APK builds.
        GameObject train = GameObject.Find("Train_THSR_700T");
        if (train == null)
            return null;

        string expectedName = isLecturer ? "Spawn_Lecturer" : "Spawn_Student";
        Transform[] descendants = train.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant != null && descendant.name == expectedName)
            {
                Debug.LogWarning($"[PlayerSpawner] Serialized spawn reference was null; resolved '{expectedName}' under Train_THSR_700T.", this);
                return descendant;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }
    }

#pragma warning disable UNT0006
#pragma warning disable CS0618
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
#pragma warning restore CS0618
#pragma warning restore UNT0006
}
