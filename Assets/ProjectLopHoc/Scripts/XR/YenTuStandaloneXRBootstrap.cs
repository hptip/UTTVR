using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Supplies an XR Origin when YenTuHaLongXanh is launched as the first scene,
/// while reusing the persistent origin when entered through Login.
/// </summary>
[DisallowMultipleComponent]
public sealed class YenTuStandaloneXRBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject fallbackRigPrefab;
    private Transform _fallbackRig;

    private void Awake()
    {
        // The scene's persistent Login rig is preferred. Only provide a local rig
        // when this scene is the app's entry point (Quest) or is played directly in Editor.
        if (GameObject.Find("XR Origin (VR)") != null)
            return;

        if (!Application.isMobilePlatform && !Application.isEditor)
            return;

        if (fallbackRigPrefab == null)
        {
            Debug.LogError("[YenTuStandaloneXRBootstrap] XR fallback prefab is not assigned.", this);
            return;
        }

        GameObject rig = Instantiate(fallbackRigPrefab);
        rig.name = "XR Origin (VR)";
        _fallbackRig = rig.transform;
        AlignFallbackRigToSpawnPoint();
        DisableSceneMainCamera(rig.transform);

        // Starter Assets leaves this manager external to its rig prefab.
        if (FindFirstObjectByType<XRInteractionManager>() == null)
            rig.AddComponent<XRInteractionManager>();
    }

    private void LateUpdate()
    {
        if (_fallbackRig == null)
            return;

        // Keep the headset at the intended train spawn while Fusion starts its
        // local runner. FirstPersonController takes over once the local player exists.
        FirstPersonController[] players = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (FirstPersonController player in players)
        {
            if (player != null && player.HasStateAuthority)
            {
                _fallbackRig = null;
                return;
            }
        }

        AlignFallbackRigToSpawnPoint();
    }

    private void AlignFallbackRigToSpawnPoint()
    {
        if (_fallbackRig == null)
            return;

        PlayerSpawner spawner = GetComponent<PlayerSpawner>();
        if (spawner == null)
            return;

        Transform spawnPoint = PlayerPrefs.GetInt("IsLecturer", 0) == 1
            ? spawner.lecturerSpawnPoint
            : spawner.studentSpawnPoint;
        if (spawnPoint != null)
            _fallbackRig.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
    }

    private static void DisableSceneMainCamera(Transform xrOrigin)
    {
        // Direct scene startup has no XROriginSessionPersistence yet. Disable its
        // default Main Camera immediately so it cannot render from the old origin
        // while Fusion is still creating the local player.
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera.gameObject.name != "Main Camera" ||
                camera.transform.IsChildOf(xrOrigin))
                continue;

            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }
    }
}
