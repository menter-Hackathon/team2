using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class BubbleSizeAdjustment : MonoBehaviour
{
    [SerializeField] private TMP_Text _gayaText;

    private RectTransform _rectTransform;
    private string _lastText;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        UpdateHeight();
    }

    private void onCreate()
    {
        UpdateHeight();
    }

    private void LateUpdate()
    {
        if (_gayaText != null && _gayaText.text != _lastText)
        {
            UpdateHeight();
        }
    }

    private void UpdateHeight()
    {
        if (_gayaText == null)
        {
            Debug.LogWarning("[BubbleSizeAdjustment] _gayaText is not assigned.", this);
            return;
        }

        _lastText = _gayaText.text;

        Vector2 size = _rectTransform.sizeDelta;
        size.y = _lastText.Length > 7 ? 140f : 80f;
        size.x = _lastText.Length > 5 ? 320f : 290f - (6 - _lastText.Length) * 30f;
        _rectTransform.sizeDelta = size;
    }
}
