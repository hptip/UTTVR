using UnityEngine;
using UnityEngine.SceneManagement;

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
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }
}
