using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs only when the explicit request marker exists. No scene assets are saved.
[InitializeOnLoad]
internal static class StandaloneSeatValidation
{
    private const string Request = "Temp/StandaloneSeatValidation.request";
    private const string Report = "Temp/StandaloneSeatValidation.txt";
    private static double deadline;
    static StandaloneSeatValidation() => EditorApplication.update += Tick;

    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (SceneManager.GetActiveScene().name != "YenTuHaLongXanh")
            return;
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.isPlaying = true;
            return;
        }
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 45;
        foreach (var player in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (player.Object == null || !player.Object.IsValid || !player.HasStateAuthority) continue;
            var seat = Object.FindFirstObjectByType<TrainSeatInteractable>();
            if (seat == null || seat.trainMover == null || !seat.trainMover.IsStopped) continue;
            try
            {
                seat.RequestOccupy(player);
                bool entered = player.isDriving && seat.IsOccupied && seat.trainMover.isDriverSeated;
                player.YeuCauRoiGhe();
                bool exited = !player.isDriving && !seat.IsOccupied && !seat.trainMover.isDriverSeated;
                Finish($"Direct YenTu Play Mode: player={player.name}; runner={player.Runner.GameMode}; enter={entered}; exit={exited}; XR rigs={Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length}. Ray/controller interaction and Quest frame timing not tested.");
            }
            catch (System.Exception e) { Finish("FAIL: " + e); }
            return;
        }
        if (EditorApplication.timeSinceStartup > deadline) Finish("FAIL: timed out waiting for local player and stopped train seat.");
    }

    private static void Finish(string result)
    {
        File.WriteAllText(Report, result);
        File.Delete(Request);
        Debug.Log("[StandaloneSeatValidation] " + result);
        EditorApplication.isPlaying = false;
    }
}
