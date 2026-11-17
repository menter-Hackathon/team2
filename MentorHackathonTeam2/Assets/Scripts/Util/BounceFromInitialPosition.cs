using DG.Tweening;
using UnityEngine;

/// <summary>
/// 有効化時の位置からワールドの上方向に弾み、待機を挟んで繰り返す。
/// </summary>
public class BounceFromInitialPosition : MonoBehaviour
{
    [Header("弾む動き")]
    [Tooltip("弾む高さの中央値。自身と親すべての Scale を反映した距離で弾む")]
    [SerializeField, Min(0f)] private float _heightMedian = 0.2f;
    [Tooltip("弾む高さのランダム幅（中央値からの増減幅）")]
    [SerializeField, Min(0f)] private float _heightRandomWidth = 0.05f;
    [Tooltip("上昇と下降にかかる合計時間（秒）")]
    [SerializeField, Min(0.01f)] private float _bounceDuration = 0.4f;

    [Header("待機")]
    [Tooltip("弾む前の待機時間の中央値（秒）")]
    [SerializeField, Min(0f)] private float _waitMedian = 1f;
    [Tooltip("待機時間のランダム幅（中央値からの増減幅、秒）")]
    [SerializeField, Min(0f)] private float _waitRandomWidth = 0.2f;

    [Header("ガヤ連動")]
    [Tooltip("クリック結果を受け取る GayaSystem。未設定ならシーン内から探す")]
    [SerializeField] private GayaSystem _gayaSystem;
    [Tooltip("正解時に弾む高さを2倍にする時間（秒）")]
    [SerializeField, Min(0f)] private float _correctBoostDuration = 2f;
    [Tooltip("おてつき時に完全停止する時間（秒）")]
    [SerializeField, Min(0f)] private float _missStopDuration = 1f;

    private Vector3 _initialPosition;
    private Sequence _sequence;
    private Tween _feedbackTimer;
    private float _heightMultiplier = 1f;
    private bool _isStopped;

    private void OnEnable()
    {
        _initialPosition = transform.position;
        if (_gayaSystem == null)
        {
            _gayaSystem = FindFirstObjectByType<GayaSystem>();
        }
        if (_gayaSystem != null)
        {
            _gayaSystem.OnWordClaimed += HandleWordClaimed;
        }
        StartNextBounce();
    }

    private void OnDisable()
    {
        if (_gayaSystem != null)
        {
            _gayaSystem.OnWordClaimed -= HandleWordClaimed;
        }
        _feedbackTimer?.Kill();
        _feedbackTimer = null;
        _sequence?.Kill();
        _sequence = null;
        _heightMultiplier = 1f;
        _isStopped = false;
        transform.position = _initialPosition;
    }

    private void HandleWordClaimed(string targetId, string playerId, string phrase, bool isGayaWord)
    {
        float duration = Mathf.Max(0f, isGayaWord ? _correctBoostDuration : _missStopDuration);
        _feedbackTimer?.Kill();
        _feedbackTimer = null;
        if (duration <= 0f)
        {
            EndFeedback();
            return;
        }

        _sequence?.Kill();
        _sequence = null;

        if (isGayaWord)
        {
            _isStopped = false;
            _heightMultiplier = 3f;
            StartNextBounce(true);
            _feedbackTimer = DOVirtual.DelayedCall(duration, EndFeedback, false).SetLink(gameObject);
        }
        else
        {
            _isStopped = true;
            _heightMultiplier = 1f;
            // 現在位置のまま止め、停止時間が終わってから初期位置へ戻す。
            _feedbackTimer = DOVirtual.DelayedCall(duration, EndFeedback, false).SetLink(gameObject);
        }
    }

    private void EndFeedback()
    {
        _feedbackTimer = null;
        _sequence?.Kill();
        _sequence = null;
        _heightMultiplier = 1f;
        _isStopped = false;

        if ((transform.position - _initialPosition).sqrMagnitude <= 0.00000001f)
        {
            transform.position = _initialPosition;
            StartNextBounce();
            return;
        }

        float returnDuration = Mathf.Max(0.01f, _bounceDuration) * 0.5f;
        _sequence = DOTween.Sequence()
            .Append(transform.DOMove(_initialPosition, returnDuration).SetEase(Ease.InQuad))
            .OnComplete(StartNextBounce)
            .SetLink(gameObject);
    }

    private void StartNextBounce()
    {
        StartNextBounce(false);
    }

    private void StartNextBounce(bool skipWait)
    {
        if (_isStopped)
        {
            return;
        }

        float wait = skipWait ? 0f : RandomValue(_waitMedian, _waitRandomWidth);
        float height = RandomValue(_heightMedian, _heightRandomWidth);
        float halfDuration = Mathf.Max(0.01f, _bounceDuration) * 0.5f;
        // lossyScale には自身と親階層の Scale が含まれる。
        float worldHeight = height * _heightMultiplier * Mathf.Abs(transform.lossyScale.y);
        Vector3 top = _initialPosition + Vector3.up * worldHeight;
        if (skipWait)
        {
            top.y = Mathf.Max(top.y, transform.position.y);
        }

        _sequence = DOTween.Sequence()
            .AppendInterval(wait)
            .Append(transform.DOMove(top, halfDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOMove(_initialPosition, halfDuration).SetEase(Ease.InQuad))
            .OnComplete(StartNextBounce)
            .SetLink(gameObject);
    }

    private static float RandomValue(float median, float width)
    {
        float safeMedian = Mathf.Max(0f, median);
        float safeWidth = Mathf.Max(0f, width);
        return Random.Range(Mathf.Max(0f, safeMedian - safeWidth), safeMedian + safeWidth);
    }
}
