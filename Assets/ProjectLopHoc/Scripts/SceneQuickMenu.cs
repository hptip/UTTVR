using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Fusion;

/// <summary>
/// App-wide scene menu. Offline startup keeps the configured Login XR rig,
/// including its comfort and custom controller setup, while bypassing auth UI.
/// </summary>
public sealed class SceneQuickMenu : MonoBehaviour
{
    private const string StartupScene = "Login";
    private const string DefaultScene = "Classroom";
    private const string MenuObjectName = "UTT Scene Quick Menu";

    private static SceneQuickMenu _instance;
    private static bool _created;

    private static readonly (string scene, string label)[] Destinations =
    {
        ("UTTCampus", "Khuôn viên UTT"),
        ("Classroom", "Lớp học"),
        ("Showroom", "Phòng trưng bày"),
        ("YenTuHaLongXanh", "Yên Tử – Hạ Long"),
        ("QuanLyVongDoiTaiSan", "Quản lý vòng đời tài sản"),
    };

    private Canvas _canvas;
    private Camera _viewCamera;
    private Text _sceneLabel;
    private readonly List<Button> _buttons = new List<Button>();
    private bool _menuOpen;
    public bool IsMenuOpen=>_menuOpen;
    private bool _startupRedirectStarted;
    private bool _leftToggleWasPressed;
    private bool _sceneLoadInProgress;
    private UnityEngine.XR.InputDevice _leftController;
#if ENABLE_INPUT_SYSTEM
    private InputAction _leftMenuAction;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _created = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeFirstScene()
    {
        if (_created)
            return;

        _created = true;
        var menuObject = new GameObject(MenuObjectName);
        DontDestroyOnLoad(menuObject);
        menuObject.AddComponent<SceneQuickMenu>();
    }

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
#if ENABLE_INPUT_SYSTEM
        _leftMenuAction = new InputAction("Scene menu Y", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        _leftMenuAction.Enable();
#endif
        BuildMenu();
    }

    private void Start()
    {
        // In the Editor, show the selector as soon as a playable destination is
        // entered. This makes each scene directly reachable for VR testing.
        if (Application.isEditor && IsDestination(SceneManager.GetActiveScene().name))
            SetMenuOpen(true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
#if ENABLE_INPUT_SYSTEM
        _leftMenuAction?.Dispose();
#endif
        if (_instance == this)
            _instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == StartupScene && !_startupRedirectStarted)
        {
            _startupRedirectStarted = true;
            HideLoginInterface();
            StartCoroutine(OpenDefaultScene());
            return;
        }

        if (IsDestination(scene.name))
        {
            _sceneLoadInProgress = false;
            _viewCamera = null;
            RefreshViewCamera();
            UpdateSceneLabel();
            SetMenuOpen(Application.isEditor);
        }
    }

    private IEnumerator OpenDefaultScene()
    {
        // Preserve the authored comfort, camera and controller configuration.
        // Readiness/rebinding belongs to that rig, not a replacement prefab.
        yield return null;
        AsyncOperation operation = SceneManager.LoadSceneAsync(DefaultScene, LoadSceneMode.Single);
        if (operation == null)
            Debug.LogError($"[SceneQuickMenu] Không tải được Scene mặc định '{DefaultScene}'.");
    }

    private static void HideLoginInterface()
    {
        GameObject loginCanvas = GameObject.Find("LoginCanvas");
        if (loginCanvas != null)
            loginCanvas.SetActive(false);

        GameObject loginPanel = GameObject.Find("LoginPanel");
        if (loginPanel != null)
            loginPanel.SetActive(false);
    }

    private void Update()
    {
        RefreshViewCamera();
        FollowViewCamera();

        bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            togglePressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        // Keep the Editor shortcut available when the active input backend
        // does not publish Keyboard.current to the Game view.
        if (Input.GetKeyDown(KeyCode.F1))
            togglePressed = true;
#endif

        if (!_leftController.isValid)
            _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool controllerPressed = _leftController.isValid &&
            (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondary) && secondary);
#if ENABLE_INPUT_SYSTEM
        controllerPressed |= _leftMenuAction != null && _leftMenuAction.IsPressed();
#endif

        if (controllerPressed && !_leftToggleWasPressed)
            togglePressed = true;
        _leftToggleWasPressed = controllerPressed;

        if (togglePressed && IsDestination(SceneManager.GetActiveScene().name))
            SetMenuOpen(!_menuOpen);

#if ENABLE_INPUT_SYSTEM
        if (_menuOpen && keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
                SetMenuOpen(false);
            else if (keyboard.digit1Key.wasPressedThisFrame)
                LoadDestination(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                LoadDestination(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                LoadDestination(2);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                LoadDestination(3);
            else if (keyboard.digit5Key.wasPressedThisFrame)
                LoadDestination(4);
        }
#endif
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        // A screen-space Editor fallback keeps the selector discoverable and
        // mouse-operable even when the selected XR camera is unavailable or
        // the world-space panel is outside the Game view.
        if (!Application.isEditor || !_menuOpen || !IsDestination(SceneManager.GetActiveScene().name))
            return;

        float width = Mathf.Max(320f, Mathf.Min(900f, Screen.width - 24f));
        float height = Mathf.Max(72f, Mathf.Min(400f, Screen.height - 16f));
        GUILayout.BeginArea(new Rect(12f, 8f, width, height), GUI.skin.window);

        if (height < 220f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SCENE: " + GetSceneLabel(SceneManager.GetActiveScene().name), GUILayout.Width(220f));
            if (GUILayout.Button("Ẩn", GUILayout.Width(52f)))
                SetMenuOpen(false);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Destinations.Length; i++)
            {
                int destinationIndex = i;
                bool current = string.Equals(SceneManager.GetActiveScene().name,
                    Destinations[i].scene, System.StringComparison.OrdinalIgnoreCase);
                GUI.enabled = !current && !_sceneLoadInProgress;
                if (GUILayout.Button(Destinations[i].label, GUILayout.Height(34f)))
                    LoadDestination(destinationIndex);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("UTT • CHỌN SCENE", GUI.skin.label);
            GUILayout.Label("Đang ở: " + GetSceneLabel(SceneManager.GetActiveScene().name));
            for (int i = 0; i < Destinations.Length; i++)
            {
                int destinationIndex = i;
                bool current = string.Equals(SceneManager.GetActiveScene().name,
                    Destinations[i].scene, System.StringComparison.OrdinalIgnoreCase);
                GUI.enabled = !current && !_sceneLoadInProgress;
                if (GUILayout.Button(Destinations[i].label, GUILayout.Height(46f)))
                    LoadDestination(destinationIndex);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Ẩn menu  (F1 / Y tay trái)", GUILayout.Height(32f)))
                SetMenuOpen(false);
        }
        GUILayout.EndArea();
    }
#endif

    private void BuildMenu()
    {
        var canvasObject = new GameObject(
            "Scene Selector Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(TrackedDeviceGraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.pixelPerfect = false;
        _canvas.sortingOrder = 300;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(820f, 720f);
        canvasRect.localScale = Vector3.one * 0.001f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        Image background = canvasObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.075f, 0.12f, 0.94f);
        background.raycastTarget = true;

        CreateText(canvasRect, "Scene Selector Title", "CHỌN KHÔNG GIAN", 38,
            new Vector2(0f, 300f), new Vector2(720f, 70f), FontStyle.Bold,
            new Color(0.83f, 0.93f, 1f));

        _sceneLabel = CreateText(canvasRect, "Current Scene Label", string.Empty, 24,
            new Vector2(0f, 245f), new Vector2(720f, 50f), FontStyle.Normal,
            new Color(0.72f, 0.83f, 0.91f));

        for (int i = 0; i < Destinations.Length; i++)
        {
            int destinationIndex = i;
            float y = 160f - i * 88f;
            Button button = CreateButton(canvasRect, Destinations[i].label,
                new Vector2(0f, y), new Vector2(650f, 70f),
                () => LoadDestination(destinationIndex));
            _buttons.Add(button);
        }

        CreateText(canvasRect, "Scene Selector Help", "F1 / Y tay trái: mở hoặc đóng  •  Esc: đóng  •  Phím 1–5: chọn nhanh",
            19, new Vector2(0f, -300f), new Vector2(760f, 42f), FontStyle.Normal,
            new Color(0.67f, 0.78f, 0.88f));

        _canvas.gameObject.SetActive(false);
        UpdateSceneLabel();
    }

    private static Font BuiltinFont()
    {
        // Unity 6 no longer exposes Arial.ttf as a built-in font resource.
        // LegacyRuntime.ttf remains the supported runtime font.
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static Text CreateText(Transform parent, string objectName, string value,
        int fontSize, Vector2 position, Vector2 size, FontStyle style, Color color)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = BuiltinFont();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 position,
        Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.10f, 0.30f, 0.46f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.10f, 0.30f, 0.46f, 1f);
        colors.highlightedColor = new Color(0.17f, 0.47f, 0.66f, 1f);
        colors.pressedColor = new Color(0.06f, 0.22f, 0.35f, 1f);
        colors.selectedColor = new Color(0.17f, 0.47f, 0.66f, 1f);
        colors.disabledColor = new Color(0.15f, 0.18f, 0.20f, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateText(rect, "Label", label, 30, Vector2.zero, size - new Vector2(24f, 8f),
            FontStyle.Bold, Color.white);
        return button;
    }

    private void RefreshViewCamera()
    {
        if (_viewCamera != null && _viewCamera.isActiveAndEnabled)
            return;

        _viewCamera = Camera.main;
        if (_viewCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera candidate in cameras)
            {
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    _viewCamera = candidate;
                    break;
                }
            }
        }

        if (_canvas != null && _viewCamera != null)
        {
            _canvas.worldCamera = _viewCamera;
            _canvas.renderMode = RenderMode.WorldSpace;
        }
    }

    private void FollowViewCamera()
    {
        if (!_menuOpen || _canvas == null || _viewCamera == null)
            return;

        Transform canvasTransform = _canvas.transform;
        canvasTransform.position = _viewCamera.transform.position + _viewCamera.transform.forward * 1.4f;
        canvasTransform.rotation = _viewCamera.transform.rotation;
    }

    private void SetMenuOpen(bool open)
    {
        _menuOpen = open;
        if (_canvas != null)
            _canvas.gameObject.SetActive(open);

        if (open)
        {
            UpdateSceneLabel();
            RefreshViewCamera();
            FollowViewCamera();
            if (EventSystem.current != null && _buttons.Count > 0)
                EventSystem.current.SetSelectedGameObject(_buttons[0].gameObject);
        }
    }

    private void UpdateSceneLabel()
    {
        if (_sceneLabel == null)
            return;

        string activeScene = SceneManager.GetActiveScene().name;
        _sceneLabel.text = $"Đang ở: {GetSceneLabel(activeScene)}";
        foreach (Button button in _buttons)
        {
            if (button != null)
                button.interactable = !string.Equals(button.name, GetSceneLabel(activeScene), System.StringComparison.Ordinal);
        }
    }

    private static string GetSceneLabel(string sceneName)
    {
        foreach (var destination in Destinations)
        {
            if (string.Equals(destination.scene, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return destination.label;
        }
        return sceneName;
    }

    private static bool IsDestination(string sceneName)
    {
        foreach (var destination in Destinations)
        {
            if (string.Equals(destination.scene, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void LoadDestination(int destinationIndex)
    {
        if (_sceneLoadInProgress || destinationIndex < 0 || destinationIndex >= Destinations.Length)
            return;

        string targetScene = Destinations[destinationIndex].scene;
        if (string.Equals(SceneManager.GetActiveScene().name, targetScene, System.StringComparison.OrdinalIgnoreCase))
        {
            SetMenuOpen(false);
            return;
        }

        int buildIndex = FindBuildIndex(targetScene);
        if (buildIndex < 0)
        {
            Debug.LogError($"[SceneQuickMenu] Scene '{targetScene}' không có trong Build Scene List.");
            return;
        }

        _sceneLoadInProgress = true;
        SetMenuOpen(false);

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning && runner.GameMode != GameMode.Single)
        {
            runner.LoadScene(SceneRef.FromIndex(buildIndex));
            return;
        }

        SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
    }

    private static int FindBuildIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName,
                    System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
