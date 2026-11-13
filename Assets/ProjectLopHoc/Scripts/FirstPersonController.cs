using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using ProjectLopHoc.Core.Interfaces;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using InputXRController = UnityEngine.InputSystem.XR.XRController;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#endif

public class FirstPersonController : NetworkBehaviour, ITunnelingVignetteProvider
{
    private NetworkCharacterController _ncc;
    private CharacterController _cc;
    private Camera _mainCamera;

    [Header("Cấu hình di chuyển và Chuột")]
    public float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float xrMoveSpeed = 0.8f;
    [SerializeField, Range(1f, 180f)] private float snapTurnAngle = 30f;
    [SerializeField, Min(0f)] private float snapTurnDebounceTime = 0.35f;
    public float mouseSensitivity = 1.5f;
    public float eyeHeight = 1.65f;

    private float _verticalRotation = 0f;
    private float _horizontalRotation = 0f;
    private bool _xrMode;
    private Transform _xrOrigin;
    private UnityEngine.XR.InputDevice _leftController;
    private UnityEngine.XR.InputDevice _rightController;
#if ENABLE_INPUT_SYSTEM
    private XRSimulatedController _simulatedLeftController;
    private XRSimulatedController _simulatedRightController;
    private InputXRController _inputSystemLeftController;
    private InputXRController _inputSystemRightController;
    private Transform _rightRayOriginTransform;
    private bool _simulatedXRMode;
#endif
    private Vector2 _xrMove;
    private Vector2 _xrTurn;
    private bool _xrJump;
    private bool _xrJumpDown;
    private bool _xrInteractDown;
    private bool _xrInteractPressed;
    private bool _snapTurnArmed = true;
    private float _nextSnapTurnTime;
    private float _snapTurnVignetteUntil;
    private TunnelingVignetteController _tunnelingVignette;
    private bool _comfortVignetteActive;
    private Transform _driverViewAnchor;
    private Transform _seatedViewOverride;
    public bool IsDrivingTrain(TrainSplineMover mover) => isDriving && _currentSeat != null && _currentSeat.trainMover == mover;
    public void SetSeatedViewOverride(Transform anchor)
    {
        if (anchor != null && (!isDriving || Object == null || !Object.IsValid || !HasStateAuthority)) return;
        _seatedViewOverride = anchor;
        if (_xrMode && isDriving) FollowDriverViewAnchor();
    }
    private Vector3 _seatedCameraOffsetInOrigin;
    private Quaternion _seatedRigRotationOffset;
    private UnityEngine.InputSystem.XR.TrackedPoseDriver _headTrackedPoseDriver;
    private XRNodePoseDriver _headNodePoseDriver;
    private UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType _previousHeadTrackingType;
    private bool _previousHeadPositionTracking;
    private bool _hasSavedSeatedTracking;
    private readonly VignetteParameters _comfortVignetteParameters = new VignetteParameters
    {
        apertureSize = 0.7f,
        featheringEffect = 0.25f,
        easeInTime = 0.15f,
        easeOutTime = 0.2f,
        easeInTimeLock = true,
        easeOutDelayTime = 0f
    };

    public VignetteParameters vignetteParameters => _comfortVignetteParameters;

    [Header("Cấu hình Tương tác (Raycast)")]
    public float interactionRange = 4f;
    public LayerMask interactableLayer = ~0;

    [Header("Trạng thái Lái Tàu")]
    public bool isDriving = false;
    private TrainSeatInteractable _currentSeat;
    private Transform _trainRoot;

    [Header("Bảng tên trên đầu (Nameplate)")]
    public GameObject nameplateObject;
    public TMP_Text nameplateText;

    [Networked] public NetworkString<_32> NetworkPlayerName { get; set; }
    [Networked] public NetworkString<_32> NetworkPlayerRole { get; set; }

    private bool _jumpRequested = false;
    private InteractableObject _currentHoveredObject;
    private IInteractable _currentXRHoveredObject;

    private void Awake()
    {
        _ncc = GetComponent<NetworkCharacterController>();
        _cc = GetComponent<CharacterController>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UpdateTunnelingVignette(false);
        if (HasStateAuthority && _currentSeat != null && _currentSeat.trainMover != null)
        {
            _currentSeat.trainMover.SetVRControlInput(Vector2.zero);
            _currentSeat.trainMover.isDriverSeated = false;
        }
        if (HasStateAuthority)
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(false);
        RestoreSeatedXRTracking();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_xrMode) ConfigureXRCameras();
    }

    public override void Spawned()
    {
        GameObject originObject = GameObject.Find("XR Origin (VR)");
        _xrOrigin = originObject != null ? originObject.transform : null;
#if ENABLE_INPUT_SYSTEM
        ConfigureXRInputSource();
#endif
        _xrMode = XRSettings.isDeviceActive
#if ENABLE_INPUT_SYSTEM
                  || _simulatedXRMode
#endif
                  ;
        if (!_xrMode && _xrOrigin != null)
        {
            _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _xrMode = _leftController.isValid || _rightController.isValid;
        }
        if (HasStateAuthority)
        {
            FirstPersonController[] existingControllers = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
            foreach (var controller in existingControllers)
            {
                if (controller != this && controller.Object != null && controller.Object.IsValid &&
                    controller.Runner == Runner && controller.HasStateAuthority && controller.isActiveAndEnabled)
                {
                    Debug.LogWarning("[FirstPersonController] Discarding duplicate local player in the same runner; keeping " + controller.name, this);
                    Runner.Despawn(Object);
                    return;
                }
            }

            _mainCamera = ResolvePlayerCamera();
            if (_xrMode)
            {
                ConfigureXRCameras();
                if (_xrOrigin != null)
                {
                    _xrOrigin.SetPositionAndRotation(transform.position, transform.rotation);
#if ENABLE_INPUT_SYSTEM
                    if (_simulatedXRMode)
                    {
                        RefreshSimulatedControllers();
                        CacheRightRayOriginTransform();
                    }
#endif
                    _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                    _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            GameObject trainObj = GameObject.Find("Train_THSR_700T");
            if (trainObj != null)
            {
                _trainRoot = trainObj.transform;
                transform.SetParent(_trainRoot, true);
                _horizontalRotation = transform.localEulerAngles.y;
            }
            else
            {
                _horizontalRotation = transform.eulerAngles.y;
            }

            string myName = PlayerPrefs.GetString("PlayerName", "Sinh viên");
            bool isLecturer = PlayerPrefs.GetInt("IsLecturer", 0) == 1;
            NetworkPlayerName = myName;
            NetworkPlayerRole = isLecturer ? "Giảng viên" : "Lớp: 74DCTT21";
        }
    }

    private void Update()
    {
        if (!HasStateAuthority) return;

        RefreshXRMode();

        if (_xrMode)
        {
#if ENABLE_INPUT_SYSTEM
            RefreshInputSystemControllers();
            if (IsLegacyControllerTracked(XRNode.LeftHand) || IsLegacyControllerTracked(XRNode.RightHand) ||
                IsPhysicalInputController(_inputSystemLeftController) ||
                IsPhysicalInputController(_inputSystemRightController))
                _simulatedXRMode = false;

            if (_simulatedXRMode)
            {
                RefreshSimulatedControllers();
                _xrMove = _simulatedLeftController != null
                    ? _simulatedLeftController.primary2DAxis.ReadValue()
                    : Vector2.zero;
                _xrTurn = _simulatedRightController != null
                    ? _simulatedRightController.primary2DAxis.ReadValue()
                    : Vector2.zero;
                _xrJump = _simulatedLeftController != null && _simulatedLeftController.primaryButton.isPressed;
                _xrInteractDown = _simulatedRightController != null &&
                                  (_simulatedRightController.triggerButton.isPressed ||
                                   _simulatedRightController.trigger.ReadValue() >= 0.55f);
                if (_xrMove == Vector2.zero)
                    TryReadInputSystemAxis(_inputSystemLeftController, out _xrMove);
                if (_xrTurn == Vector2.zero)
                    TryReadInputSystemAxis(_inputSystemRightController, out _xrTurn);
            }
            else
#endif
            {
            if (!_leftController.isValid) _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!_rightController.isValid) _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out _xrMove);
            _rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out _xrTurn);
            if (_xrMove == Vector2.zero) TryReadInputSystemAxis(_inputSystemLeftController, out _xrMove);
            if (_xrTurn == Vector2.zero) TryReadInputSystemAxis(_inputSystemRightController, out _xrTurn);
            _xrJump = _leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool jumpPressed) && jumpPressed;
            _xrInteractDown = _rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed;
            }

            _xrMove = ApplyStickDeadzone(_xrMove);
            _xrTurn = ApplyStickDeadzone(_xrTurn);

            if (isDriving)
            {
                if (_currentSeat != null && _currentSeat.trainMover != null)
                {
                    _currentSeat.trainMover.isDriverSeated = true;
                    _currentSeat.trainMover.SetVRControlInput(_xrMove);
                }
                if (_xrJump && !_xrJumpDown) YeuCauRoiGhe();
                _xrJumpDown = _xrJump;
                UpdateTunnelingVignette(false);
                ClearCurrentHover();
                HandleXRRaycastInteraction(_xrInteractDown && !_xrInteractPressed);
                _xrInteractPressed = _xrInteractDown;
                return;
            }

            ProcessSnapTurn(_xrTurn.x);
            UpdateTunnelingVignette(_xrMove.sqrMagnitude > 0.0225f || Time.unscaledTime < _snapTurnVignetteUntil);
            if (_xrJump && !_xrJumpDown) _jumpRequested = true;
            _xrJumpDown = _xrJump;
            ClearCurrentHover();
            HandleXRRaycastInteraction(_xrInteractDown && !_xrInteractPressed);
            _xrInteractPressed = _xrInteractDown;
            return;
        }

        ClearXRCurrentHover();
        UpdateTunnelingVignette(false);

        // Xoay góc nhìn chuột liên tục cho cả khi đi bộ lẫn khi ngồi ghế lái
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = ReadMouseDelta();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            _horizontalRotation += mouseX;
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -85f, 85f);
        }

        // Đang ngồi lái tàu
        if (isDriving)
        {
            if (WasKeyPressedThisFrame(KeyCode.F))
            {
                YeuCauRoiGhe();
            }
            return;
        }

        // Đang đi bộ tự do
        if (WasKeyPressedThisFrame(KeyCode.Space))
        {
            _jumpRequested = true;
        }

        HandleRaycastInteraction();

        if (WasKeyPressedThisFrame(KeyCode.F))
        {
            KiemTraNgoiVaoGheBangPhimF();
        }
    }

    private void RefreshXRMode()
    {
        if (_xrOrigin == null)
        {
            GameObject originObject = GameObject.Find("XR Origin (VR)");
            _xrOrigin = originObject != null ? originObject.transform : null;
        }
#if ENABLE_INPUT_SYSTEM
        ConfigureXRInputSource();
#endif
        if (_xrMode || _xrOrigin == null) return;

        _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!XRSettings.isDeviceActive && !_leftController.isValid && !_rightController.isValid
#if ENABLE_INPUT_SYSTEM
            && !_simulatedXRMode
#endif
            ) return;

        _xrMode = true;
        _mainCamera = ResolvePlayerCamera();
        ConfigureXRCameras();
        _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
#if ENABLE_INPUT_SYSTEM
        if (_simulatedXRMode)
        {
            RefreshSimulatedControllers();
            CacheRightRayOriginTransform();
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ConfigureXRInputSource()
    {
        if (!Application.isEditor || _xrOrigin == null) return;

        XRDeviceSimulator simulator = UnityEngine.Object.FindAnyObjectByType<XRDeviceSimulator>();
        if (simulator == null)
        {
            _simulatedXRMode = false;
            return;
        }

        bool hasPhysicalController =
            IsLegacyControllerTracked(XRNode.LeftHand) ||
            IsLegacyControllerTracked(XRNode.RightHand);

        foreach (var device in InputSystem.devices)
        {
            if (device is InputXRController controller && IsPhysicalInputController(controller))
            {
                hasPhysicalController = true;
                break;
            }
        }

        simulator.enabled = !hasPhysicalController;
        _simulatedXRMode = simulator.isActiveAndEnabled && !hasPhysicalController;
    }
#endif

    private Camera ResolvePlayerCamera()
    {
        if (_xrMode && _xrOrigin != null)
        {
            Camera[] cameras = _xrOrigin.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
                if (camera != null && camera.enabled) return camera;
            if (cameras.Length > 0) return cameras[0];
        }
        return Camera.main;
    }

    private void ConfigureXRCameras()
    {
        if (_xrOrigin == null) return;
        _mainCamera = ResolvePlayerCamera();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == _mainCamera || camera.gameObject.name != "Main Camera" ||
                camera.transform.IsChildOf(_xrOrigin)) continue;
            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private static Vector2 ApplyStickDeadzone(Vector2 value)
    {
        if (value.sqrMagnitude < 0.0225f) return Vector2.zero;
        return Vector2.ClampMagnitude(value, 1f);
    }

    private void ProcessSnapTurn(float horizontalAxis)
    {
        if (Mathf.Abs(horizontalAxis) < 0.35f)
        {
            _snapTurnArmed = true;
            return;
        }

        if (!_snapTurnArmed || Time.unscaledTime < _nextSnapTurnTime) return;

        _horizontalRotation += Mathf.Sign(horizontalAxis) * snapTurnAngle;
        _snapTurnArmed = false;
        _nextSnapTurnTime = Time.unscaledTime + snapTurnDebounceTime;
        _snapTurnVignetteUntil = Time.unscaledTime + 0.35f;
    }

    private void UpdateTunnelingVignette(bool shouldBeActive)
    {
        if (_tunnelingVignette == null && _xrOrigin != null)
            _tunnelingVignette = _xrOrigin.GetComponentInChildren<TunnelingVignetteController>(true);
        if (_tunnelingVignette == null || shouldBeActive == _comfortVignetteActive) return;

        if (shouldBeActive)
            _tunnelingVignette.BeginTunnelingVignette(this);
        else
            _tunnelingVignette.EndTunnelingVignette(this);

        _comfortVignetteActive = shouldBeActive;
    }

    private void KiemTraNgoiVaoGheBangPhimF()
    {
        if (_mainCamera == null)
        {
            _mainCamera = ResolvePlayerCamera();
            if (_mainCamera == null) return;
        }

        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer))
        {
            TrainSeatInteractable seat = hit.collider.GetComponentInParent<TrainSeatInteractable>();
            if (seat != null)
            {
                seat.RequestOccupy(this);
            }
        }
    }

    public void ThietLapVaoGhe(TrainSeatInteractable seat, Transform mountPoint)
    {
        isDriving = true;
        _currentSeat = seat;
        if (HasStateAuthority && _currentSeat != null && _currentSeat.trainMover != null)
            _currentSeat.trainMover.isDriverSeated = true;

        if (_ncc != null) _ncc.enabled = false;
        if (_cc != null) _cc.enabled = false;

        Transform target = mountPoint != null ? mountPoint : seat.transform;
        transform.position = target.position;
        transform.rotation = target.rotation;

        _horizontalRotation = target.eulerAngles.y;
        _verticalRotation = 0f;

        if (_xrMode && HasStateAuthority)
        {
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(true);
            EnterSeatedXRMode(seat, target);
        }
    }

    private void EnterSeatedXRMode(TrainSeatInteractable seat, Transform mountPoint)
    {
        if (_xrOrigin == null)
        {
            GameObject originObject = GameObject.Find("XR Origin (VR)");
            _xrOrigin = originObject != null ? originObject.transform : null;
        }

        _mainCamera = ResolvePlayerCamera();
        if (_xrOrigin == null || _mainCamera == null) return;

        _driverViewAnchor = seat != null ? seat.transform.Find("Driver_SeatPoint/DriverViewAnchor") : null;
        if (_driverViewAnchor == null && mountPoint != null)
            _driverViewAnchor = mountPoint.Find("DriverViewAnchor");
        if (_driverViewAnchor == null)
            _driverViewAnchor = mountPoint != null ? mountPoint : seat != null ? seat.transform : null;

        _headTrackedPoseDriver = _mainCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        _headNodePoseDriver = _mainCamera.GetComponent<XRNodePoseDriver>();
        if (!_hasSavedSeatedTracking)
        {
            if (_headTrackedPoseDriver != null)
                _previousHeadTrackingType = _headTrackedPoseDriver.trackingType;
            if (_headNodePoseDriver != null)
                _previousHeadPositionTracking = _headNodePoseDriver.trackPosition;
            _hasSavedSeatedTracking = true;
        }

        if (_headTrackedPoseDriver != null)
            _headTrackedPoseDriver.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationOnly;
        if (_headNodePoseDriver != null)
            _headNodePoseDriver.trackPosition = false;

        Vector3 currentForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
        Vector3 anchorForward = Vector3.ProjectOnPlane(_driverViewAnchor.forward, Vector3.up).normalized;
        if (currentForward.sqrMagnitude > 0.001f && anchorForward.sqrMagnitude > 0.001f)
        {
            float yawCorrection = Vector3.SignedAngle(currentForward, anchorForward, Vector3.up);
            _xrOrigin.rotation = Quaternion.AngleAxis(yawCorrection, Vector3.up) * _xrOrigin.rotation;
        }

        _seatedCameraOffsetInOrigin = _xrOrigin.InverseTransformPoint(_mainCamera.transform.position);
        _seatedRigRotationOffset = Quaternion.Inverse(_driverViewAnchor.rotation) * _xrOrigin.rotation;
        FollowDriverViewAnchor();
        UpdateTunnelingVignette(false);
    }

    private void FollowDriverViewAnchor()
    {
        if (_driverViewAnchor == null || _xrOrigin == null || _mainCamera == null) return;

        Transform view = _seatedViewOverride != null ? _seatedViewOverride : _driverViewAnchor;
        _xrOrigin.rotation = view.rotation * _seatedRigRotationOffset;
        _xrOrigin.position = view.position - _xrOrigin.rotation * _seatedCameraOffsetInOrigin;
    }

    private void RestoreSeatedXRTracking()
    {
        _seatedViewOverride = null;
        if (!_hasSavedSeatedTracking) return;

        if (_headTrackedPoseDriver != null)
            _headTrackedPoseDriver.trackingType = _previousHeadTrackingType;
        if (_headNodePoseDriver != null)
            _headNodePoseDriver.trackPosition = _previousHeadPositionTracking;

        _headTrackedPoseDriver = null;
        _headNodePoseDriver = null;
        _driverViewAnchor = null;
        _hasSavedSeatedTracking = false;
    }

    public void YeuCauRoiGhe()
    {
        if (!isDriving) return;

        TrainSeatInteractable seat = _currentSeat;
        if (seat != null && seat.trainMover != null && !seat.trainMover.IsStopped) return;

        ThietLapRoiGhe();

        if (seat != null)
        {
            seat.RequestVacate(this);
        }
    }

    public bool AdjustTrainSpeedLevel(int delta)
    {
        if (!HasStateAuthority || !isDriving || _currentSeat == null || _currentSeat.trainMover == null)
            return false;

        _currentSeat.trainMover.isDriverSeated = true;
        _currentSeat.trainMover.AdjustSpeedLevel(delta);
        return true;
    }

    public void ThietLapRoiGhe()
    {
        if (!isDriving) return;

        TrainSeatInteractable seat = _currentSeat;
        if (seat != null && seat.trainMover != null && !seat.trainMover.IsStopped) return;
        if (seat != null && HasStateAuthority)
            seat.trainMover?.SetVRControlInput(Vector2.zero);

        isDriving = false;
        _currentSeat = null;

        // Clear the driver's control gate immediately. RequestVacate also arrives as an RPC,
        // but by then this local controller is already out of the seat and the RPC callback
        // can legitimately return early from this method.
        if (HasStateAuthority && seat != null && seat.trainMover != null)
        {
            seat.trainMover.SetVRControlInput(Vector2.zero);
            seat.trainMover.isDriverSeated = false;
        }

        // Lùi lại phía sau ghế lái 0.8m trên sàn buồng lái
        transform.position -= transform.forward * 0.8f;

        if (HasStateAuthority && _xrMode && _xrOrigin != null)
        {
            _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(false);
            RestoreSeatedXRTracking();
        }

        if (_ncc != null) _ncc.enabled = true;
        if (_cc != null) _cc.enabled = true;
    }

    private void HandleRaycastInteraction()
    {
        if (_mainCamera == null)
        {
            _mainCamera = ResolvePlayerCamera();
            if (_mainCamera == null) return;
        }

        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer))
        {
            InteractableObject target = hit.collider.GetComponentInParent<InteractableObject>();

            if (target != null)
            {
                if (_currentHoveredObject != target)
                {
                    ClearCurrentHover();
                    _currentHoveredObject = target;
                    _currentHoveredObject.OnHoverEnter();
                }

                if (WasMousePressedThisFrame() || WasKeyPressedThisFrame(KeyCode.E))
                {
                    target.OnInteract();
                }

                return;
            }
        }

        ClearCurrentHover();
    }

    private void HandleXRRaycastInteraction(bool interactPressed)
    {
        if (_xrOrigin == null || !TryGetRightControllerPose(out Vector3 position, out Quaternion rotation) ||
            !Physics.Raycast(new Ray(_xrOrigin.TransformPoint(position), _xrOrigin.rotation * rotation * Vector3.forward), out RaycastHit hit, interactionRange, interactableLayer))
        {
            ClearXRCurrentHover();
            return;
        }

        IInteractable target = FindXRInteractable(hit.collider);
        if (_currentXRHoveredObject != target)
        {
            ClearXRCurrentHover();
            _currentXRHoveredObject = target;
            _currentXRHoveredObject?.OnHoverEnter();
        }

        if (!interactPressed) return;
        if (target != null) target.OnInteract(gameObject);
        TrainSeatInteractable seat = hit.collider.GetComponentInParent<TrainSeatInteractable>();
        if (seat != null) seat.RequestOccupy(this);
    }

    private static IInteractable FindXRInteractable(Collider hitCollider)
    {
        if (hitCollider == null) return null;

        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable interactable)
                return interactable;
        }

        InteractableObject legacyInteractable = hitCollider.GetComponentInParent<InteractableObject>();
        return legacyInteractable;
    }

    private bool TryGetRightControllerPose(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
#if ENABLE_INPUT_SYSTEM
        CacheRightRayOriginTransform();
        if (_rightRayOriginTransform != null)
        {
            position = _xrOrigin.InverseTransformPoint(_rightRayOriginTransform.position);
            rotation = Quaternion.Inverse(_xrOrigin.rotation) * _rightRayOriginTransform.rotation;
            return true;
        }

        if (_simulatedXRMode)
        {
            RefreshSimulatedControllers();
            if (_simulatedRightController != null)
            {
                var positionControl = _simulatedRightController.TryGetChildControl<Vector3Control>("devicePosition");
                var rotationControl = _simulatedRightController.TryGetChildControl<QuaternionControl>("deviceRotation");
                if (positionControl != null && rotationControl != null)
                {
                    position = positionControl.ReadValue();
                    rotation = rotationControl.ReadValue();
                    return true;
                }
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }
#endif
        if (!_rightController.isValid) _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!_rightController.isValid ||
            !_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out position) ||
            !_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation))
            return false;
        return true;
    }

#if ENABLE_INPUT_SYSTEM
    private void RefreshInputSystemControllers()
    {
        foreach (var device in InputSystem.devices)
        {
            if (!(device is InputXRController controller)) continue;
            for (int i = 0; i < controller.usages.Count; i++)
            {
                string usage = controller.usages[i].ToString();
                if (usage == "LeftHand") _inputSystemLeftController = controller;
                else if (usage == "RightHand") _inputSystemRightController = controller;
            }
        }
    }

    private static bool IsPhysicalInputController(InputXRController controller)
    {
        if (controller == null || controller is XRSimulatedController || !controller.added)
            return false;

        var trackingControl = controller.TryGetChildControl<IntegerControl>("trackingState");
        if (trackingControl != null)
            return (trackingControl.ReadValue() &
                    (int)(UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation)) != 0;

        var trackedControl = controller.TryGetChildControl<ButtonControl>("isTracked");
        if (trackedControl != null)
            return trackedControl.isPressed;

        var position = controller.TryGetChildControl<Vector3Control>("devicePosition");
        var rotation = controller.TryGetChildControl<QuaternionControl>("deviceRotation");
        return (position != null && position.ReadValue().sqrMagnitude > 0.000001f) ||
               (rotation != null && Quaternion.Angle(rotation.ReadValue(), Quaternion.identity) > 0.001f);
    }

    private static bool IsLegacyControllerTracked(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,
                out UnityEngine.XR.InputTrackingState trackingState))
            return (trackingState &
                    (UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation)) != 0;

        bool hasPosition = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position) &&
                           position.sqrMagnitude > 0.000001f;
        bool hasRotation = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation) &&
                           Quaternion.Angle(rotation, Quaternion.identity) > 0.001f;
        return hasPosition || hasRotation;
    }

    private static bool TryReadInputSystemAxis(InputXRController controller, out Vector2 axis)
    {
        axis = Vector2.zero;
        if (controller == null || !controller.added) return false;

        Vector2Control control = controller.TryGetChildControl<Vector2Control>("primary2DAxis");
        if (control == null) control = controller.TryGetChildControl<Vector2Control>("thumbstick");
        if (control == null) control = controller.TryGetChildControl<Vector2Control>("joystick");
        if (control == null) return false;

        axis = control.ReadValue();
        return true;
    }

    private void RefreshSimulatedControllers()
    {
        foreach (var device in InputSystem.devices)
        {
            if (!(device is XRSimulatedController controller)) continue;
            for (var i = 0; i < controller.usages.Count; i++)
            {
                var usage = controller.usages[i].ToString();
                if (usage == "LeftHand") _simulatedLeftController = controller;
                else if (usage == "RightHand") _simulatedRightController = controller;
            }
        }
    }

    private void CacheRightRayOriginTransform()
    {
        if (_rightRayOriginTransform != null || _xrOrigin == null) return;
        var rays = _xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true);
        foreach (var ray in rays)
        {
            if (ray != null && ray.name.Contains("Right"))
            {
                _rightRayOriginTransform = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                return;
            }
        }
    }
#endif

    private void ClearCurrentHover()
    {
        if (_currentHoveredObject != null)
        {
            _currentHoveredObject.OnHoverExit();
            _currentHoveredObject = null;
        }
    }

    private void ClearXRCurrentHover()
    {
        _currentXRHoveredObject?.OnHoverExit();
        _currentXRHoveredObject = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || isDriving) return;

        if (_xrMode)
        {
            transform.rotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
            Vector3 forward = _mainCamera != null ? Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 xrMoveDirection = forward * _xrMove.y + right * _xrMove.x;
            _ncc.Move(xrMoveDirection * xrMoveSpeed * Runner.DeltaTime);
            if (_jumpRequested)
            {
                _ncc.Jump();
                _jumpRequested = false;
            }
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (_trainRoot != null)
        {
            transform.localRotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
        }

        Vector2 keyboardMove = ReadKeyboardMove();
        float moveHorizontal = keyboardMove.x;
        float moveVertical = keyboardMove.y;

        Vector3 moveDirection = (transform.forward * moveVertical + transform.right * moveHorizontal).normalized;
        _ncc.Move(moveDirection * moveSpeed * Runner.DeltaTime);

        if (_jumpRequested)
        {
            _ncc.Jump();
            _jumpRequested = false;
        }
    }

    private static Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private static bool WasKeyPressedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return key switch
        {
            KeyCode.F => Keyboard.current.fKey.wasPressedThisFrame,
            KeyCode.E => Keyboard.current.eKey.wasPressedThisFrame,
            KeyCode.Space => Keyboard.current.spaceKey.wasPressedThisFrame,
            _ => false
        };
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    private static bool WasMousePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static Vector2 ReadKeyboardMove()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return Vector2.zero;
        float horizontal = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) -
                           (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
        float vertical = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f) -
                         (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(horizontal, vertical);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    private void LateUpdate()
    {
        if (Object == null || !Object.IsValid) return;

        if (HasStateAuthority)
        {
            if (_xrMode)
            {
                if (_xrOrigin == null)
                {
                    GameObject originObject = GameObject.Find("XR Origin (VR)");
                    _xrOrigin = originObject != null ? originObject.transform : null;
                }
                if (_xrOrigin != null)
                {
                    if (isDriving && _driverViewAnchor != null && HasStateAuthority)
                    {
                        Transform mount = _currentSeat != null && _currentSeat.driverSeatPoint != null
                            ? _currentSeat.driverSeatPoint
                            : _driverViewAnchor.parent;
                        if (mount != null)
                            transform.SetPositionAndRotation(mount.position, mount.rotation);
                        FollowDriverViewAnchor();
                    }
                    else
                    {
                        _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
                    }
                    if (_mainCamera == null || !_mainCamera.enabled) _mainCamera = ResolvePlayerCamera();
                }
            }

            if (_mainCamera == null) _mainCamera = ResolvePlayerCamera();

            if (_mainCamera != null && !_xrMode)
            {
                if (isDriving && _currentSeat != null)
                {
                    Transform mount = _currentSeat.driverSeatPoint != null ? _currentSeat.driverSeatPoint : _currentSeat.transform;
                    transform.position = mount.position;

                    // Đặt mắt người ngồi cao hơn sàn buồng lái 1.15m
                    _mainCamera.transform.position = mount.position + new Vector3(0f, 1.15f, 0f);

                    // Cho phép lia chuột tự do 360 độ quanh cabin
                    Quaternion cabinBaseRot = mount.rotation;
                    _mainCamera.transform.rotation = cabinBaseRot * Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
                }
                else
                {
                    _mainCamera.transform.position = transform.position + new Vector3(0f, eyeHeight, 0f);

                    if (_trainRoot != null)
                    {
                        _mainCamera.transform.rotation = transform.rotation * Quaternion.Euler(_verticalRotation, 0f, 0f);
                    }
                    else
                    {
                        _mainCamera.transform.rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
                    }
                }
            }
        }

        if (nameplateObject != null)
        {
            if (nameplateText != null)
            {
                nameplateText.text = $"{NetworkPlayerName}\n<size=70%><color=#FFD700>{NetworkPlayerRole}</color></size>";
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                nameplateObject.transform.rotation = cam.transform.rotation;
            }
        }
    }
}
