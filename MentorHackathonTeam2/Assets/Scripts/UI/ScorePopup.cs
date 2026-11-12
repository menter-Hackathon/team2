using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 加点・減点の数値をポップアップ表示し、フェードアウト後に自身を破棄する演出用コンポーネント。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScorePopup : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("数値表示用テキスト")]
    [SerializeField] private TMP_Text _valueText;
    [Tooltip("フェードアウト用 CanvasGroup")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("色")]
    [Tooltip("加点時の文字色")]
    [SerializeField] private Color _positiveColor = new Color32(0xFF, 0x9F, 0x0A, 0xFF);
    [Tooltip("減点時の文字色")]
    [SerializeField] private Color _negativeColor = new Color32(0x00, 0x7A, 0xFF, 0xFF);

    [Header("動き")]
    [Tooltip("加点時に上方向へ移動する距離（px）")]
    [SerializeField] private float _riseDistance = 80f;
    [Tooltip("加点時の跳ねの高さ（px）")]
    [SerializeField] private float _jumpPower = 30f;
    [Tooltip("減点時に下方向へ移動する距離（px）")]
    [SerializeField] private float _fallDistance = 60f;
    [Tooltip("減点時の震えの強さ（px）")]
    [SerializeField] private float _shakeStrength = 10f;

    [Header("時間")]
    [Tooltip("アニメーション全体の時間（秒）")]
    [SerializeField] private float _duration = 0.9f;
    [Tooltip("フェードアウトを開始するまでの時間（秒）")]
    [SerializeField] private float _fadeDelay = 0.45f;

    private Sequence _sequence;

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    /// <summary>
    /// ポップアップ演出を開始する。完了後に自身を破棄する。
    /// </summary>
    /// <param name="deltaScore">増減値。正なら加点、負なら減点として表示する。</param>
    public void Play(int deltaScore)
    {
        RectTransform rect = (RectTransform)transform;
        bool isPositive = deltaScore >= 0;

        if (_valueText != null)
        {
            _valueText.text = isPositive ? $"+{deltaScore}" : deltaScore.ToString();
            _valueText.color = isPositive ? _positiveColor : _negativeColor;
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        Vector2 start = rect.anchoredPosition;
        _sequence = DOTween.Sequence().SetLink(gameObject);

        if (isPositive)
        {
            rect.localScale = Vector3.one * 0.6f;
            _sequence.Append(rect.DOJumpAnchorPos(start + Vector2.up * _riseDistance, _jumpPower, 1, _duration).SetEase(Ease.OutQuad));
            _sequence.Insert(0f, rect.DOScale(1f, _duration * 0.3f).SetEase(Ease.OutBack));
        }
        else
        {
            _sequence.Append(rect.DOAnchorPos(start + Vector2.down * _fallDistance, _duration).SetEase(Ease.OutQuad));
            if (_valueText != null)
            {
                _sequence.Insert(0f, _valueText.rectTransform
                    .DOShakeAnchorPos(_duration * 0.5f, new Vector2(_shakeStrength, 0f), 25, 0f, false, true));
            }
        }

        if (_canvasGroup != null)
        {
            _sequence.Insert(_fadeDelay, _canvasGroup.DOFade(0f, Mathf.Max(0.01f, _duration - _fadeDelay)));
        }

        _sequence.OnComplete(() => Destroy(gameObject));
    }
}
