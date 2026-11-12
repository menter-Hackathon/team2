using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class GayaSpeechBubble : MonoBehaviour
{
    [Tooltip("ガヤのセリフを表示する TextMeshPro（UI / 3D どちらでも可）")]
    [SerializeField] private TMP_Text _gayaText;
    [SerializeField] private Vector3 _targetScale = Vector3.one;
    [SerializeField, Min(0f)] private float _scaleDuration = 0.3f;
    [SerializeField, Min(0f)] private float _visibleDuration = 2f;
    [SerializeField, Min(0f)] private float _fadeDuration = 1f;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Sequence _animationSequence;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// ガヤのセリフを吹き出しに表示し、拡大→表示維持→フェードアウトのアニメーションを再生する。
    /// </summary>
    /// <param name="gayaPhrase">吹き出しに表示するガヤのセリフ。</param>
    public void PlayAnimation(string gayaPhrase)
    {
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
            .OnComplete(() => gameObject.SetActive(false))
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        if (_animationSequence != null && _animationSequence.IsActive())
        {
            _animationSequence.Kill();
        }
    }
}
