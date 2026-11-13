using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Keeps the Login-scene XR camera and tracked controllers alive while Fusion loads UTTCampus.</summary>
public sealed class XROriginSessionPersistence : MonoBehaviour
{
    private static XROriginSessionPersistence _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        EnsureInteractionManager();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.StartsWith("Zone_", System.StringComparison.Ordinal)) return;
        if (mode == LoadSceneMode.Single) EnsureInteractionManager();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera.transform.IsChildOf(transform)) continue;
            if (!camera.CompareTag("MainCamera")) continue;
            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private void EnsureInteractionManager()
    {
        var interactors = GetComponentsInChildren<XRBaseInteractor>(true);
        var manager = GetComponentInChildren<XRInteractionManager>(true);
        if (manager == null)
        {
            foreach (var interactor in interactors)
                if (interactor.interactionManager != null) { manager = interactor.interactionManager; break; }
            if (manager == null) manager = new GameObject("Persistent XR Interaction Manager").AddComponent<XRInteractionManager>();
            manager.transform.SetParent(transform, true);
        }
        foreach (var interactor in interactors)
            if (interactor.interactionManager != manager) interactor.interactionManager = manager;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }
}
