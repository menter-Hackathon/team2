using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2つの Slider を重ねた汎用2重ゲージ。
/// 増減幅をサブゲージで即座に見せつつ、メイン（またはサブ）を DOTween で滑らかに追従させる。
/// ガヤのスコア表示や開発進捗ゲージなど、任意の値表示に使い回せる。
/// </summary>
public class DualSliderGauge : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("手前に描画されるメインのスライダー")]
    [SerializeField] private Slider _mainSlider;
    [Tooltip("奥（背面）に描画される差分表示用のサブスライダー")]
    [SerializeField] private Slider _subSlider;
    [Tooltip("サブスライダーの Fill 用 Image（増減で色を切り替える）")]
    [SerializeField] private Image _subFillImage;
    [Tooltip("リアクション演出を与える RectTransform（未設定なら自身）")]
    [SerializeField] private RectTransform _reactionTarget;

    [Header("サブゲージ色")]
    [Tooltip("加点時のサブゲージ色")]
    [SerializeField] private Color _increaseSubColor = new Color32(0xFF, 0xD6, 0x0A, 0xFF);
    [Tooltip("減点時のサブゲージ色")]
    [SerializeField] private Color _decreaseSubColor = new Color32(0x00, 0x7A, 0xFF, 0xFF);

    [Header("加点アニメーション")]
    [Tooltip("サブゲージが伸びてからメインゲージが追いつき始めるまでの遅延（秒）")]
    [SerializeField] private float _increaseDelay = 0.15f;
    [Tooltip("メインゲージが追いつくまでの時間（秒）")]
    [SerializeField] private float _increaseDuration = 0.4f;
    [Tooltip("加点時の拡大リアクションの強さ")]
    [SerializeField] private Vector3 _punchScale = new Vector3(0.08f, 0.08f, 0f);
    [Tooltip("拡大リアクションの時間（秒）")]
    [SerializeField] private float _punchDuration = 0.3f;

    [Header("減点アニメーション")]
    [Tooltip("メインゲージが縮んでからサブゲージが縮み始めるまでの遅延（秒）")]
    [SerializeField] private float _decreaseDelay = 0.25f;
    [Tooltip("サブゲージが縮みきるまでの時間（秒）")]
    [SerializeField] private float _decreaseDuration = 0.5f;
    [Tooltip("減点時の横揺れの強さ（px）")]
    [SerializeField] private float _shakeStrength = 12f;
    [Tooltip("横揺れの時間（秒）")]
    [SerializeField] private float _shakeDuration = 0.3f;
    [Tooltip("横揺れの振動数")]
    [SerializeField] private int _shakeVibrato = 20;

    private Tween _valueTween;
    private Tween _reactionTween;
    private Vector3 _baseScale;
    private Vector2 _baseAnchoredPosition;
    private float _targetValue;

    /// <summary>現在の目標値（アニメーション完了後に表示される値）。</summary>
    public float Value => _targetValue;

    /// <summary>最大値。</summary>
    public float MaxValue => _mainSlider != null ? _mainSlider.maxValue : 0f;

    private void Awake()
    {
        if (_reactionTarget == null)
        {
            _reactionTarget = transform as RectTransform;
        }
        if (_reactionTarget != null)
        {
            _baseScale = _reactionTarget.localScale;
            _baseAnchoredPosition = _reactionTarget.anchoredPosition;
        }
        SetupSlider(_mainSlider);
        SetupSlider(_subSlider);
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    /// <summary>
    /// 最大値と現在値を設定し、両スライダーを即座に同期する。
    /// </summary>
    /// <param name="current">現在値。</param>
    /// <param name="max">最大値。</param>
    public void Initialize(float current, float max)
    {
        KillTweens();
        max = Mathf.Max(0.0001f, max);
        _targetValue = Mathf.Clamp(current, 0f, max);

        if (_mainSlider != null)
        {
            _mainSlider.minValue = 0f;
            _mainSlider.maxValue = max;
            _mainSlider.value = _targetValue;
        }
        if (_subSlider != null)
        {
            _subSlider.minValue = 0f;
            _subSlider.maxValue = max;
            _subSlider.value = _targetValue;
        }
    }

    /// <summary>
    /// 新しい値を設定し、増減に応じた2重ゲージアニメーションを再生する。
    /// 実行中のアニメーションは中断され、最新の目標値へ向かう。
    /// </summary>
    /// <param name="newValue">新しい値（0〜最大値でクランプされる）。</param>
    public void SetValue(float newValue)
    {
        if (_mainSlider == null || _subSlider == null)
        {
            Debug.LogWarning("[DualSliderGauge] スライダーが未設定です。", this);
            return;
        }

        newValue = Mathf.Clamp(newValue, 0f, _mainSlider.maxValue);
        if (Mathf.Approximately(newValue, _targetValue))
        {
            return;
        }

        bool isIncrease = newValue > _targetValue;
        _targetValue = newValue;
        KillTweens();

        if (isIncrease)
        {
            PlayIncrease(newValue);
        }
        else
        {
            PlayDecrease(newValue);
        }
    }

    /// <summary>
    /// 加点演出：サブを瞬時に伸ばし、遅れてメインを追従させ、拡大リアクションを与える。
    /// </summary>
    private void PlayIncrease(float target)
    {
        if (_subFillImage != null)
        {
            _subFillImage.color = _increaseSubColor;
        }
        _subSlider.value = target;

        _valueTween = DOTween
            .To(() => _mainSlider.value, x => _mainSlider.value = x, target, _increaseDuration)
            .SetDelay(_increaseDelay)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject);

        if (_reactionTarget != null)
        {
            _reactionTween = _reactionTarget
                .DOPunchScale(_punchScale, _punchDuration, 8, 0.8f)
                .SetLink(gameObject);
        }
    }

    /// <summary>
    /// 減点演出：メインを瞬時に縮め、遅れてサブを縮め、横揺れリアクションを与える。
    /// </summary>
    private void PlayDecrease(float target)
    {
        if (_subFillImage != null)
        {
            _subFillImage.color = _decreaseSubColor;
        }
        _mainSlider.value = target;

        _valueTween = DOTween
            .To(() => _subSlider.value, x => _subSlider.value = x, target, _decreaseDuration)
            .SetDelay(_decreaseDelay)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject);

        if (_reactionTarget != null)
        {
            _reactionTween = _reactionTarget
                .DOShakeAnchorPos(_shakeDuration, new Vector2(_shakeStrength, 0f), _shakeVibrato, 0f, false, true)
                .SetLink(gameObject);
        }
    }

    /// <summary>
    /// 実行中の Tween を停止し、リアクション対象の位置・スケールを初期状態へ戻す。
    /// 中断時はアニメーション途中の値を維持するため、値の Tween は Complete しない。
    /// </summary>
    private void KillTweens()
    {
        _valueTween?.Kill();
        _valueTween = null;

        if (_reactionTween != null)
        {
            _reactionTween.Kill();
            _reactionTween = null;
            if (_reactionTarget != null)
            {
                _reactionTarget.localScale = _baseScale;
                _reactionTarget.anchoredPosition = _baseAnchoredPosition;
            }
        }
    }

    /// <summary>
    /// スライダーを表示専用（操作不可）に設定する。
    /// </summary>
    private static void SetupSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
    }
}
