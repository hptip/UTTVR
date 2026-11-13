using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compact world-space speed display and speed-level controls for the train driver.</summary>
public sealed class TrainDriverHUD : MonoBehaviour
{
    [SerializeField] private TrainSplineMover trainMover;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button slowerButton;
    [SerializeField] private Button fasterButton;

    private float _nextRefreshTime;
    private bool _isLocalDriver;

    private void Awake()
    {
        RegisterButtonCallbacks();
        SetVisible(false);
    }

    public void Configure(TrainSplineMover mover, CanvasGroup group, TMP_Text speed, TMP_Text level,
        Button slower, Button faster)
    {
        if (slowerButton != null) slowerButton.onClick.RemoveListener(DecreaseSpeedLevel);
        if (fasterButton != null) fasterButton.onClick.RemoveListener(IncreaseSpeedLevel);

        trainMover = mover;
        canvasGroup = group;
        speedText = speed;
        levelText = level;
        slowerButton = slower;
        fasterButton = faster;

        RegisterButtonCallbacks();
        SetVisible(false);
    }

    private void RegisterButtonCallbacks()
    {
        if (slowerButton != null) slowerButton.onClick.AddListener(DecreaseSpeedLevel);
        if (fasterButton != null) fasterButton.onClick.AddListener(IncreaseSpeedLevel);
    }

    private void OnDestroy()
    {
        if (slowerButton != null) slowerButton.onClick.RemoveListener(DecreaseSpeedLevel);
        if (fasterButton != null) fasterButton.onClick.RemoveListener(IncreaseSpeedLevel);
    }

    private void Update()
    {
        if (trainMover == null || canvasGroup == null) return;

        bool refresh = Time.unscaledTime >= _nextRefreshTime;
        if (refresh)
        {
            _nextRefreshTime = Time.unscaledTime + 0.1f;
            _isLocalDriver = HasLocalDriverAuthority();

            if (speedText != null)
                speedText.text = $"{Mathf.RoundToInt(trainMover.currentSpeedKmh)} km/h";

            if (levelText != null)
                levelText.text = $"NẤC {trainMover.SpeedLevel}/5  ·  TỐI ĐA {Mathf.RoundToInt(trainMover.SpeedLimitForLevel)} km/h";

            if (slowerButton != null)
                slowerButton.interactable = _isLocalDriver;
            if (fasterButton != null)
                fasterButton.interactable = _isLocalDriver;
        }

        bool isSeated = trainMover.isDriverSeated || _isLocalDriver;
        if (!isSeated)
        {
            _isLocalDriver = false;
            if (slowerButton != null) slowerButton.interactable = false;
            if (fasterButton != null) fasterButton.interactable = false;
            SetVisible(false);
            return;
        }

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private bool HasLocalDriverAuthority()
    {
        FirstPersonController[] players = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (FirstPersonController player in players)
        {
            if (player != null && player.isDriving && player.HasStateAuthority)
                return true;
        }

        return false;
    }

    private void DecreaseSpeedLevel() => AdjustSpeedLevel(-1);
    private void IncreaseSpeedLevel() => AdjustSpeedLevel(1);

    private void AdjustSpeedLevel(int delta)
    {
        if (!_isLocalDriver) return;

        FirstPersonController[] players = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (FirstPersonController player in players)
        {
            if (player != null && player.isDriving && player.HasStateAuthority && player.AdjustTrainSpeedLevel(delta))
                return;
        }
    }
}
