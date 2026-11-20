using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>音量と使用素材のクレジットを表示する設定ポップアップ。</summary>
public sealed class SettingsPopUpWindow : PopUpWindow
{
    private const string VolumeKey = "Settings.Volume";
    private const float DefaultValue = 1f;
    private const string Credits = "会議室・教卓：CGTrader掲載素材\n"
        + "木目02：建築パース.com\n"
        + "Low Poly Office Props - LITE：RRFreelance\n"
        + "めっちゃカメレオン アバター：BR's Shop\n"
        + "けいふぉんと！：Do-Font / Apache License 2.0\n"
        + "Dela Gothic One：The Dela Gothic Project Authors / OFL 1.1\n"
        + "BGM「学び舎の風景」：稿屋 隆 / OpenTracks\n"
        + "効果音・環境音：OtoLogic（CC BY 4.0）";

    private static float _volume = DefaultValue;

    public static float Volume => _volume;

    [Header("音量とクレジット")]
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private TMP_Text _volumeValueText;
    [SerializeField] private TMP_Text _creditsText;
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _masterVolumeParameter = "MasterVolume";

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

        base.Start();

        if (_volumeSlider == null)
        {
            Debug.LogError("[SettingsPopUpWindow] 音量スライダーを設定してください。", this);
            enabled = false;
            return;
        }

        _volumeSlider.minValue = 0f;
        _volumeSlider.maxValue = 1f;
        if (_creditsText != null) _creditsText.text = Credits;
        ApplyVolume();
        UpdateSliderValues();

        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    protected override void OnDestroy()
    {
        KillTweens();
        if (_volumeSlider != null) _volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
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
        ApplyVolume();
        PlayerPrefs.SetFloat(VolumeKey, _volume);
        UpdateValueTexts();
    }

    private void ApplyVolume()
    {
        // Mixer が使える場合は Master のみを調節し、Listener との二重減衰を避ける。
        float decibels = _volume <= 0.0001f ? -80f : Mathf.Log10(_volume) * 20f;
        if (_audioMixer != null && !string.IsNullOrWhiteSpace(_masterVolumeParameter)
            && _audioMixer.SetFloat(_masterVolumeParameter, decibels))
            AudioListener.volume = 1f;
        else
            AudioListener.volume = _volume;
    }

    private void UpdateSliderValues()
    {
        if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(_volume);
        UpdateValueTexts();
    }

    private void UpdateValueTexts()
    {
        if (_volumeValueText != null) _volumeValueText.text = $"{_volume * 100f:0}%";
    }
}
