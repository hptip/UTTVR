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
    private FirstPersonController _localPlayer;
    private float _nextPlayerSearch;
    private int _lastSpeed=-1,_lastLevel=-1,_lastLimit=-1;
    private bool _visible,_visibilityInitialized;

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
        _visibilityInitialized=false;
        _lastSpeed=_lastLevel=_lastLimit=-1;

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

            int speed=Mathf.RoundToInt(trainMover.currentSpeedKmh),level=trainMover.SpeedLevel,limit=Mathf.RoundToInt(trainMover.SpeedLimitForLevel);
            if (speedText != null && speed!=_lastSpeed)
                speedText.text = $"{speed} km/h";

            if (levelText != null && (level!=_lastLevel || limit!=_lastLimit))
                levelText.text = $"NẤC {level}/5  ·  TỐI ĐA {limit} km/h";
            _lastSpeed=speed;_lastLevel=level;_lastLimit=limit;

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
        if(_visibilityInitialized && _visible==visible)return;
        _visibilityInitialized=true;_visible=visible;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private bool HasLocalDriverAuthority()
    {
        if(_localPlayer!=null && _localPlayer.Object!=null && _localPlayer.Object.IsValid && _localPlayer.HasStateAuthority)
            return _localPlayer.isDriving;
        if(Time.unscaledTime<_nextPlayerSearch)return false;
        _nextPlayerSearch=Time.unscaledTime+.25f;
        foreach (FirstPersonController player in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (player != null && player.Object!=null && player.Object.IsValid && player.HasStateAuthority)
            { _localPlayer=player;return player.isDriving; }
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
