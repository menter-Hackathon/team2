using System.Collections;
using DG.Tweening;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>タイトルとA4上のDay1～4を、Cinemachineで行き来する。</summary>
public sealed class TitleSelectController : MonoBehaviour
{
    private const string NicknameKey = "TitleSelect.Nickname";
    private const int MaxNicknameLength = 16;
    private const float ContinueSlideDistance = 1400f;
    private const float TitleSlideDistance = 1200f;
    private const float NavigationSlideDistance = 220f;

    [Header("進行データ")]
    [SerializeField] private StageCatalog _catalog;
    [SerializeField] private DaySectionView[] _daySections;

    [Header("カメラ: 0=タイトル、1～4=Day1～4")]
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private CinemachineCamera[] _cameras;

    [Header("移動演出（秒）")]
    [SerializeField, Min(0.01f)] private float _uiSlideDuration = 0.25f;
    [SerializeField, Min(0f)] private float _cameraStartDelay = 0.1f;
    [SerializeField, Min(0f)] private float _cameraBlendDuration = 0.65f;

    [Header("今日の気分（最高精度の範囲）")]
    [SerializeField] private Sprite _mood90To100;
    [SerializeField] private Sprite _mood70To90;
    [SerializeField] private Sprite _mood40To70;
    [SerializeField] private Sprite _mood0To40;

    [Header("タイトル")]
    [SerializeField] private GameObject _titleHud;
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _continueButton;

    [Header("Day移動")]
    [SerializeField] private GameObject _navigationHud;
    [SerializeField] private Button _upButton;
    [SerializeField] private TMP_Text _upButtonText;
    [SerializeField] private Button _downButton;
    [SerializeField] private TMP_Text _downButtonText;

    private int _currentDay = -1;
    private bool _moving;
    private RectTransform _titleRect;
    private RectTransform _continueRect;
    private RectTransform _upRect;
    private RectTransform _downRect;
    private Vector2 _titleHome;
    private Vector2 _continueHome;
    private Vector2 _upHome;
    private Vector2 _downHome;

    private void Start()
    {
        if (!ReferencesAreValid())
        {
            Debug.LogError("[TitleSelectController] カタログ、Day表示、カメラ、UI参照を確認してください。", this);
            enabled = false;
            return;
        }

        _nicknameInput.characterLimit = MaxNicknameLength;
        _nicknameInput.SetTextWithoutNotify(TrimNickname(PlayerPrefs.GetString(NicknameKey, "")));
        _nicknameInput.readOnly = false;
        _nicknameInput.interactable = true;
        _titleRect = (RectTransform)_titleHud.transform;
        _continueRect = (RectTransform)_continueButton.transform;
        _upRect = (RectTransform)_upButton.transform;
        _downRect = (RectTransform)_downButton.transform;
        _titleHome = _titleRect.anchoredPosition;
        _continueHome = _continueRect.anchoredPosition;
        _upHome = _upRect.anchoredPosition;
        _downHome = _downRect.anchoredPosition;
        _nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
        _nicknameInput.onEndEdit.AddListener(OnNicknameEndEdit);
        _continueButton.onClick.AddListener(ContinueFromTitle);
        _upButton.onClick.AddListener(MoveUp);
        _downButton.onClick.AddListener(MoveDown);

        for (int i = 0; i < _daySections.Length; i++)
        {
            int dayIndex = i;
            _daySections[i].StartButton.onClick.AddListener(() => StartDay(dayIndex));
            RenderDay(i);
            _daySections[i].gameObject.SetActive(true);
        }

        _brain.DefaultBlend.Time = _cameraBlendDuration;
        foreach (Button button in _titleHud.GetComponentsInChildren<Button>(true))
            DayButtonStyle.Apply(button, button.GetComponentInChildren<TMP_Text>(true),
                _catalog.GetStage(0).MainColor);
        SetActiveCamera(0);
        _titleHud.SetActive(true);
        _nicknameInput.gameObject.SetActive(true);
        _continueButton.gameObject.SetActive(true);
        _navigationHud.SetActive(false);
        UpdateContinueButton();
    }

    private void OnDestroy()
    {
        if (_nicknameInput != null)
        {
            _nicknameInput.onValueChanged.RemoveListener(OnNicknameChanged);
            _nicknameInput.onEndEdit.RemoveListener(OnNicknameEndEdit);
        }
        if (_continueButton != null) _continueButton.onClick.RemoveListener(ContinueFromTitle);
        if (_upButton != null) _upButton.onClick.RemoveListener(MoveUp);
        if (_downButton != null) _downButton.onClick.RemoveListener(MoveDown);
        if (_titleRect != null) _titleRect.DOKill();
        if (_continueRect != null) _continueRect.DOKill();
        if (_upRect != null) _upRect.DOKill();
        if (_downRect != null) _downRect.DOKill();
    }

    private bool ReferencesAreValid()
    {
        if (_catalog == null || _catalog.Count != 4 || _daySections == null || _daySections.Length != 4
            || _brain == null || _cameras == null || _cameras.Length != 5 || _titleHud == null
            || _nicknameInput == null || _continueButton == null || _navigationHud == null
            || _upButton == null || _upButtonText == null || _downButton == null || _downButtonText == null)
            return false;

        for (int i = 0; i < 5; i++)
            if (_cameras[i] == null) return false;

        for (int i = 0; i < 4; i++)
            if (_daySections[i] == null || !_daySections[i].HasRequiredReferences(i > 0)
                || _catalog.GetStage(i) == null || _catalog.GetStage(i).Name != $"Day{i + 1}")
                return false;

        if (!HasButtonStyle(_continueButton) || !HasButtonStyle(_upButton)
            || !HasButtonStyle(_downButton)) return false;
        foreach (Button button in _titleHud.GetComponentsInChildren<Button>(true))
            if (!HasButtonStyle(button)) return false;

        return true;
    }

    private static bool HasButtonStyle(Button button)
    {
        DayButtonStyle style = button.GetComponent<DayButtonStyle>();
        return style != null && style.HasRequiredReferences;
    }

    private static string TrimNickname(string value)
    {
        string trimmed = (value ?? "").Trim();
        return trimmed.Length <= MaxNicknameLength ? trimmed : trimmed.Substring(0, MaxNicknameLength);
    }

    private void OnNicknameChanged(string value)
    {
        PlayerPrefs.SetString(NicknameKey, TrimNickname(value));
        UpdateContinueButton();
    }

    private void OnNicknameEndEdit(string value)
    {
        string nickname = TrimNickname(value);
        _nicknameInput.SetTextWithoutNotify(nickname);
        PlayerPrefs.SetString(NicknameKey, nickname);
        PlayerPrefs.Save();
        UpdateContinueButton();
    }

    private void UpdateContinueButton()
    {
        _continueButton.interactable = !_moving && !string.IsNullOrWhiteSpace(_nicknameInput.text);
        DayButtonStyle.Apply(_continueButton, _continueButton.GetComponentInChildren<TMP_Text>(true),
            _catalog.GetStage(0).MainColor);
    }

    private void ContinueFromTitle()
    {
        string nickname = TrimNickname(_nicknameInput.text);
        if (nickname.Length == 0 || _moving) return;

        _nicknameInput.SetTextWithoutNotify(nickname);
        PlayerPrefs.SetString(NicknameKey, nickname);
        PlayerPrefs.Save();
        StartCoroutine(MoveToDay(0));
    }

    private void MoveUp()
    {
        if (!_moving && _currentDay >= 0)
            StartCoroutine(MoveToDay(_currentDay - 1));
    }

    private void MoveDown()
    {
        if (!_moving && _currentDay >= 0 && _currentDay < 3)
            StartCoroutine(MoveToDay(_currentDay + 1));
    }

    private IEnumerator MoveToDay(int targetDay)
    {
        _moving = true;
        _continueButton.interactable = false;
        _nicknameInput.readOnly = true;
        _nicknameInput.interactable = false;

        int previousDay = _currentDay;
        Sequence exit = DOTween.Sequence().SetUpdate(true);
        if (previousDay < 0)
        {
            exit.Join(_titleRect.DOAnchorPosY(_titleHome.y + TitleSlideDistance, _uiSlideDuration)
                .SetEase(Ease.InCubic));
            exit.Join(_continueRect.DOAnchorPosX(_continueHome.x + ContinueSlideDistance, _uiSlideDuration)
                .SetEase(Ease.InCubic));
        }
        else
        {
            exit.Join(_upRect.DOAnchorPosY(_upHome.y + NavigationSlideDistance, _uiSlideDuration)
                .SetEase(Ease.InCubic));
            if (_downButton.gameObject.activeSelf)
                exit.Join(_downRect.DOAnchorPosY(_downHome.y - NavigationSlideDistance, _uiSlideDuration)
                    .SetEase(Ease.InCubic));
        }

        if (_cameraStartDelay > 0f)
            yield return new WaitForSecondsRealtime(_cameraStartDelay);
        SetActiveCamera(targetDay + 1);
        yield return exit.WaitForCompletion();

        if (previousDay < 0)
        {
            _titleHud.SetActive(false);
            _continueButton.gameObject.SetActive(false);
        }
        else
        {
            _navigationHud.SetActive(false);
        }

        CinemachineCamera destinationCamera = _cameras[targetDay + 1];
        // Brainがカメラを更新した後のフレームで判定し、ブレンド完了まで次のUIを出さない。
        do
        {
            yield return new WaitForEndOfFrame();
        }
        while (_brain.IsBlending || !object.ReferenceEquals(_brain.ActiveVirtualCamera, destinationCamera));

        _currentDay = targetDay;
        Sequence enter = DOTween.Sequence().SetUpdate(true);
        if (targetDay < 0)
        {
            _titleRect.anchoredPosition = _titleHome + Vector2.up * TitleSlideDistance;
            _continueRect.anchoredPosition = _continueHome + Vector2.right * ContinueSlideDistance;
            _titleHud.SetActive(true);
            _continueButton.gameObject.SetActive(true);
            enter.Join(_titleRect.DOAnchorPos(_titleHome, _uiSlideDuration).SetEase(Ease.OutCubic));
            enter.Join(_continueRect.DOAnchorPos(_continueHome, _uiSlideDuration)
                .SetEase(Ease.OutCubic));
        }
        else
        {
            _upButtonText.text = targetDay == 0 ? "↑ タイトルへ" : $"↑ Day{targetDay}へ";
            DayButtonStyle.Apply(_upButton, _upButtonText,
                _catalog.GetStage(Mathf.Max(0, targetDay - 1)).MainColor);
            _downButton.gameObject.SetActive(targetDay < 3);
            if (targetDay < 3)
            {
                _downButtonText.text = $"↓ Day{targetDay + 2}へ";
                DayButtonStyle.Apply(_downButton, _downButtonText,
                    _catalog.GetStage(targetDay + 1).MainColor);
            }
            _upRect.anchoredPosition = _upHome + Vector2.up * NavigationSlideDistance;
            _downRect.anchoredPosition = _downHome - Vector2.up * NavigationSlideDistance;
            _navigationHud.SetActive(true);
            enter.Join(_upRect.DOAnchorPos(_upHome, _uiSlideDuration).SetEase(Ease.OutCubic));
            if (targetDay < 3)
                enter.Join(_downRect.DOAnchorPos(_downHome, _uiSlideDuration)
                    .SetEase(Ease.OutCubic));
        }

        yield return enter.WaitForCompletion();
        _moving = false;
        if (targetDay < 0)
        {
            _nicknameInput.readOnly = false;
            _nicknameInput.interactable = true;
            UpdateContinueButton();
        }
    }

    private void SetActiveCamera(int activeIndex)
    {
        for (int i = 0; i < _cameras.Length; i++)
            _cameras[i].Priority.Value = i == activeIndex ? 20 : 0;
    }

    private bool IsUnlocked(int index)
    {
        if (index == 0) return true;
        StageCatalog.Stage previous = _catalog.GetStage(index - 1);
        StageCatalog.Stage current = _catalog.GetStage(index);
        return DataManager.Selection.TryGetBestAccuracy(previous.Name, out float best)
            && best >= current.RequiredPreviousAccuracy;
    }

    private void RenderDay(int index)
    {
        StageCatalog.Stage stage = _catalog.GetStage(index);
        bool hasResult = DataManager.Selection.TryGetBestAccuracy(stage.Name, out float best);
        // 未プレイ時は仮の中間アイコンを薄く表示する。
        Sprite moodIcon = hasResult ? GetMoodIcon(best) : _mood70To90;
        Sprite requiredMoodIcon = index == 0 ? null : GetMoodIcon(stage.RequiredPreviousAccuracy);
        _daySections[index].Render(stage, IsUnlocked(index), moodIcon, hasResult, requiredMoodIcon);
    }

    private Sprite GetMoodIcon(float accuracyPercent)
    {
        if (accuracyPercent >= 90f) return _mood90To100;
        if (accuracyPercent >= 70f) return _mood70To90;
        if (accuracyPercent >= 40f) return _mood40To70;
        return _mood0To40;
    }

    private void StartDay(int index)
    {
        if (_moving || index != _currentDay || !IsUnlocked(index)) return;

        string sceneName = _catalog.GetStage(index).SceneName;
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[TitleSelectController] 遷移先シーンを読み込めません: {sceneName}", this);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit() => PlayerPrefs.Save();
}
