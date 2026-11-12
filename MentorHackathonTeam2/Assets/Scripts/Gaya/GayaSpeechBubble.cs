using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// ガヤ成功時に表示される吹き出し。
/// セリフを表示し、拡大 → 表示維持 → フェードアウトの後に自身を破棄する。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class GayaSpeechBubble : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("ガヤのセリフを表示する TextMeshPro（UI / 3D どちらでも可）")]
    [SerializeField] private TMP_Text _gayaText;

    [Header("アニメーション")]
    [Tooltip("拡大アニメーション後の最終スケール")]
    [SerializeField] private Vector3 _targetScale = Vector3.one;
    [Tooltip("拡大アニメーションの時間（秒）")]
    [SerializeField, Min(0f)] private float _scaleDuration = 0.3f;
    [Tooltip("拡大後に表示を維持する時間（秒）")]
    [SerializeField, Min(0f)] private float _visibleDuration = 2f;
    [Tooltip("フェードアウトの時間（秒）。完了後に自身を破棄")]
    [SerializeField, Min(0f)] private float _fadeDuration = 1f;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Sequence _animationSequence;
    private Action<GayaSpeechBubble> _onReleased;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// ガヤのセリフを吹き出しに表示し、拡大→表示維持→フェードアウトのアニメーションを再生する。
    /// アニメーション完了時に GameObject は破棄される。
    /// </summary>
    /// <param name="gayaPhrase">吹き出しに表示するガヤのセリフ。</param>
    /// <param name="onReleased">吹き出しが破棄される際に一度だけ呼ばれるコールバック（出現地点の解放用）。</param>
    public void PlayAnimation(string gayaPhrase, Action<GayaSpeechBubble> onReleased = null)
    {
        _onReleased = onReleased;

        if (_gayaText != null)
        {
            _gayaText.text = gayaPhrase;
        }
        else
        {
            Debug.LogWarning("[GayaSpeechBubble] _gayaText is not assigned.", this);
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_animationSequence != null && _animationSequence.IsActive())
        {
            _animationSequence.Kill();
        }

        gameObject.SetActive(true);
        _rectTransform.localScale = Vector3.zero;
        _canvasGroup.alpha = 1f;

        _animationSequence = DOTween.Sequence()
            .Append(_rectTransform.DOScale(_targetScale, _scaleDuration))
            .AppendInterval(_visibleDuration)
            .Append(_canvasGroup.DOFade(0f, _fadeDuration))
            .OnComplete(() => Destroy(gameObject))
            .SetLink(gameObject);
    }

    private void OnDestroy()
    {
        // アニメーション完了による破棄だけでなく、シーン遷移などでの破棄時にも確実に解放を通知する
        Action<GayaSpeechBubble> callback = _onReleased;
        _onReleased = null;
        callback?.Invoke(this);
    }

    private void OnDisable()
    {
        if (_animationSequence != null && _animationSequence.IsActive())
        {
            _animationSequence.Kill();
        }
    }
}
