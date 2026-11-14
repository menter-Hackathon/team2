using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>音量とマウス感度を PlayerPrefs に保存する設定ポップアップ。</summary>
public sealed class SettingsPopUpWindow : PopUpWindow
{
    private const string VolumeKey = "Settings.Volume";
    private const string MouseSensitivityKey = "Settings.MouseSensitivity";
    private const float DefaultValue = 1f;
    private const float MinMouseSensitivity = 0.1f;
    private const float MaxMouseSensitivity = 2f;

    private static float _volume = DefaultValue;
    private static float _mouseSensitivity = DefaultValue;

    public static float Volume => _volume;
    public static float MouseSensitivity => _mouseSensitivity;

    [Header("スライダー")]
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private Slider _mouseSensitivitySlider;
    [SerializeField] private TMP_Text _volumeValueText;
    [SerializeField] private TMP_Text _mouseSensitivityValueText;

    [Header("開閉演出")]
    [SerializeField] private RectTransform _popupPanel;
    [SerializeField] private Image _backdrop;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.22f;

    private Vector3 _expandedScale = Vector3.one;
    private float _backdropAlpha;
    private Tween _scaleTween;
    private Tween _backdropTween;
    private bool _closing;

    private void Awake()
    {
        if (_popupPanel == null)
        {
            Debug.LogError("[SettingsPopUpWindow] Popup Panelを設定してください。", this);
            return;
        }

        _expandedScale = _popupPanel.localScale;
        _popupPanel.localScale = Vector3.zero;
        if (_backdrop != null)
        {
            _backdropAlpha = _backdrop.color.a;
            SetBackdropAlpha(0f);
        }
    }

    protected override void Start()
    {
        _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultValue));
        _mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultValue), MinMouseSensitivity, MaxMouseSensitivity);
        AudioListener.volume = _volume;

        base.Start();

        if (_volumeSlider == null || _mouseSensitivitySlider == null)
        {
            Debug.LogError("[SettingsPopUpWindow] 2本のスライダーを設定してください。", this);
            enabled = false;
            return;
        }

        _volumeSlider.minValue = 0f;
        _volumeSlider.maxValue = 1f;
        _mouseSensitivitySlider.minValue = 0f;
        _mouseSensitivitySlider.maxValue = 1f;
        UpdateSliderValues();

        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
    }

    protected override void OnDestroy()
    {
        KillTweens();
        if (_volumeSlider != null) _volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (_mouseSensitivitySlider != null) _mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
        base.OnDestroy();
    }

    protected override void OnOpened()
    {
        UpdateSliderValues();
    }

    protected override void OnClosed()
    {
        PlayerPrefs.Save();
    }

    public override void Open()
    {
        if (_popupPanel == null)
        {
            base.Open();
            return;
        }
        if (IsOpen && !_closing) return;

        bool wasOpen = IsOpen;
        KillTweens();
        if (!wasOpen)
        {
            _popupPanel.localScale = Vector3.zero;
            if (_backdrop != null) SetBackdropAlpha(0f);
            base.Open();
        }

        _closing = false;
        _scaleTween = _popupPanel.DOScale(_expandedScale, _openDuration)
            .SetEase(Ease.OutCubic).SetUpdate(true);
        if (_backdrop != null)
            _backdropTween = _backdrop.DOFade(_backdropAlpha, _openDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public override void Close()
    {
        if (!IsOpen || _closing) return;
        if (_popupPanel == null)
        {
            base.Close();
            return;
        }

        _closing = true;
        KillTweens();
        _scaleTween = _popupPanel.DOScale(Vector3.zero, _closeDuration)
            .SetEase(Ease.InCubic).SetUpdate(true).OnComplete(FinishClose);
        if (_backdrop != null)
            _backdropTween = _backdrop.DOFade(0f, _closeDuration)
                .SetEase(Ease.InCubic).SetUpdate(true);
    }

    private void FinishClose()
    {
        _closing = false;
        _scaleTween = null;
        base.Close();
    }

    private void KillTweens()
    {
        _scaleTween?.Kill();
        _backdropTween?.Kill();
        _scaleTween = null;
        _backdropTween = null;
    }

    private void SetBackdropAlpha(float alpha)
    {
        Color color = _backdrop.color;
        color.a = alpha;
        _backdrop.color = color;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    private void OnVolumeChanged(float sliderValue)
    {
        _volume = Mathf.Clamp01(sliderValue);
        AudioListener.volume = _volume;
        PlayerPrefs.SetFloat(VolumeKey, _volume);
        UpdateValueTexts();
    }

    private void OnMouseSensitivityChanged(float sliderValue)
    {
        _mouseSensitivity = MouseValueFromSlider(sliderValue);
        PlayerPrefs.SetFloat(MouseSensitivityKey, _mouseSensitivity);
        UpdateValueTexts();
    }

    private void UpdateSliderValues()
    {
        if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(_volume);
        if (_mouseSensitivitySlider != null) _mouseSensitivitySlider.SetValueWithoutNotify(SliderFromMouseValue(_mouseSensitivity));
        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        if (_volumeValueText != null) _volumeValueText.text = $"{_volume * 100f:0}%";
        if (_mouseSensitivityValueText != null) _mouseSensitivityValueText.text = $"{_mouseSensitivity:0.00}x";
    }

    // 0.1～1 と 1～2 をスライダーの左右半分に割り当て、初期値1を中央にする。
    private static float MouseValueFromSlider(float sliderValue)
    {
        float value = Mathf.Clamp01(sliderValue);
        return value <= 0.5f
            ? Mathf.Lerp(MinMouseSensitivity, DefaultValue, value * 2f)
            : Mathf.Lerp(DefaultValue, MaxMouseSensitivity, (value - 0.5f) * 2f);
    }

    private static float SliderFromMouseValue(float mouseValue)
    {
        float value = Mathf.Clamp(mouseValue, MinMouseSensitivity, MaxMouseSensitivity);
        return value <= DefaultValue
            ? Mathf.InverseLerp(MinMouseSensitivity, DefaultValue, value) * 0.5f
            : 0.5f + Mathf.InverseLerp(DefaultValue, MaxMouseSensitivity, value) * 0.5f;
    }
}
