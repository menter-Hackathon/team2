using UnityEngine;
using UnityEngine.UI;

/// <summary>マッチング中の表示と、3つの点の順番に跳ねる演出。</summary>
public sealed class MatchingScreenController : MonoBehaviour
{
    [SerializeField] private RectTransform[] _dots;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private MatchingScripts _matching;
    [SerializeField, Min(0f)] private float _bounceHeight = 18f;
    [SerializeField, Min(0.1f)] private float _cycleSeconds = 1.2f;

    private Vector2[] _dotHomes;

    private void Awake()
    {
        _dotHomes = new Vector2[_dots.Length];
        for (int i = 0; i < _dots.Length; i++)
            if (_dots[i] != null)
                _dotHomes[i] = _dots[i].anchoredPosition;
    }

    private void OnEnable()
    {
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(Cancel);
    }

    private void OnDisable()
    {
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveListener(Cancel);

        if (_dotHomes == null) return;
        for (int i = 0; i < _dots.Length; i++)
            if (_dots[i] != null)
                _dots[i].anchoredPosition = _dotHomes[i];
    }

    private void Update()
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            if (_dots[i] == null) continue;

            float phase = Mathf.Repeat(Time.unscaledTime / _cycleSeconds - i * 0.2f, 1f);
            float bounce = phase < 0.46f ? Mathf.Sin(phase / 0.46f * Mathf.PI) : 0f;
            _dots[i].anchoredPosition = _dotHomes[i] + Vector2.up * (_bounceHeight * bounce);
        }
    }

    private void Cancel()
    {
        if (_cancelButton != null)
            _cancelButton.interactable = false;
        _matching.CancelMatching();
    }
}
