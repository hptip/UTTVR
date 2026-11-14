using System.IO;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

/// <summary>Play-mode diagnostic: write "probe" to Temp/CameraHeightProbe/request.txt to dump camera heights.</summary>
[InitializeOnLoad]
static class CameraHeightProbe
{
    const string Dir = "Temp/CameraHeightProbe";
    static CameraHeightProbe() => EditorApplication.update += Poll;

    static void Poll()
    {
        string request = Path.Combine(Dir, "request.txt");
        if (!EditorApplication.isPlaying || !File.Exists(request)) return;
        File.Delete(request);

        var sb = new StringBuilder();
        sb.AppendLine("Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        var player = Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (var p in player)
            sb.AppendLine($"Player={p.name} authority={p.HasStateAuthority} feetY={p.transform.position.y:0.000} eyeHeight={p.eyeHeight:0.000} driving={p.isDriving}");
        foreach (var origin in Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
        {
            sb.AppendLine($"Origin={origin.name} active={origin.isActiveAndEnabled} y={origin.transform.position.y:0.000} mode={origin.CurrentTrackingOriginMode} requested={origin.RequestedTrackingOriginMode} yOffset={origin.CameraYOffset:0.000}");
            if (origin.CameraFloorOffsetObject != null)
                sb.AppendLine($"  FloorOffset localY={origin.CameraFloorOffsetObject.transform.localPosition.y:0.000}");
            if (origin.Camera != null)
                sb.AppendLine($"  Camera={origin.Camera.name} enabled={origin.Camera.enabled} localY={origin.Camera.transform.localPosition.y:0.000} worldY={origin.Camera.transform.position.y:0.000}");
        }
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            sb.AppendLine($"Cam={cam.name} enabled={cam.enabled} main={(cam == Camera.main)} worldY={cam.transform.position.y:0.000} parent={(cam.transform.parent != null ? cam.transform.parent.name : "-")}");
        sb.AppendLine("XRDeviceActive=" + UnityEngine.XR.XRSettings.isDeviceActive);
        foreach (string path in new[] { "Train_THSR_700T/dau", "Train_THSR_700T/Spawn_Student", "Train_THSR_700T/dau/Seat/Driver_SeatPoint" })
        {
            var go = GameObject.Find(path);
            if (go != null) sb.AppendLine($"Node={path} worldY={go.transform.position.y:0.000}");
        }
        foreach (var p in player)
        {
            if (!p.HasStateAuthority) continue;
            Vector3 from = p.transform.position + Vector3.up * 0.3f;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(p.transform))
                    sb.AppendLine($"Below={GetPath(hit.transform)} y={hit.point.y:0.000}");
            foreach (var hit in Physics.RaycastAll(from, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(p.transform))
                    sb.AppendLine($"Above={GetPath(hit.transform)} y={hit.point.y:0.000}");
            foreach (var t in p.GetComponentsInChildren<TMPro.TMP_Text>(true))
                sb.AppendLine($"Text={t.name} worldY={t.transform.position.y:0.000} text={t.text.Replace("\n", " | ")}");
        }
        File.WriteAllText(Path.Combine(Dir, "result.txt"), sb.ToString());
    }

    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
}
