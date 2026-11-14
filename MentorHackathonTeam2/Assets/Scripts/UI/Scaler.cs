using DG.Tweening;
using UnityEngine;

public class Scaler : MonoBehaviour
{
    [Header("Scale Animation")]
    [Tooltip("Scale reached before returning to the original scale of 1")]
    [SerializeField] private Vector3 _targetScale = new Vector3(1.2f, 1.2f, 1f);
    [Tooltip("Duration of one scale transition in seconds")]
    [SerializeField, Min(0f)] private float _duration = 0.5f;
    [Tooltip("Easing applied to each scale transition")]
    [SerializeField] private Ease _ease = Ease.InOutSine;

    private Tween _scaleTween;

    private void OnEnable()
    {
        transform.localScale = Vector3.one;
        _scaleTween = transform
            .DOScale(_targetScale, _duration)
            .SetEase(_ease)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        if (_scaleTween != null && _scaleTween.IsActive())
        {
            _scaleTween.Kill();
        }
        _scaleTween = null;
    }
}
