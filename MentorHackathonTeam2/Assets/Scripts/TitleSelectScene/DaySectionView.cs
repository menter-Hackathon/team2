using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>A4上の各Day枠に重ねる情報と開始ボタン。</summary>
public sealed class DaySectionView : MonoBehaviour
{
    [SerializeField] private TMP_Text _descriptionText;
    [FormerlySerializedAs("_accuracyText")]
    [SerializeField] private TMP_Text _moodLabel;
    [SerializeField] private Image _moodIcon;
    [SerializeField] private TMP_Text _unlockStateText;
    [SerializeField] private Image _requiredMoodIcon;
    [SerializeField] private TMP_Text _unlockSuffixText;
    [SerializeField] private Button _startButton;
    [SerializeField] private TMP_Text _startButtonText;

    public Button StartButton => _startButton;

    public bool HasRequiredReferences(bool needsUnlockRequirement)
    {
        DayButtonStyle style = _startButton == null ? null : _startButton.GetComponent<DayButtonStyle>();
        return _descriptionText != null && _moodLabel != null && _moodIcon != null
            && _unlockStateText != null && _startButton != null && _startButtonText != null
            && style != null && style.HasRequiredReferences
            && (!needsUnlockRequirement || (_requiredMoodIcon != null && _unlockSuffixText != null));
    }

    public void Render(StageCatalog.Stage stage, bool unlocked, Sprite moodIcon, bool hasResult,
        Sprite requiredMoodIcon)
    {
        _descriptionText.text = stage.Description;
        _moodLabel.text = "今日の気分は？";
        _moodIcon.sprite = moodIcon;
        _moodIcon.enabled = moodIcon != null;
        _moodIcon.color = hasResult ? Color.white : new Color(1f, 1f, 1f, 0.8f);

        _unlockStateText.color = unlocked ? stage.MainColor : new Color(0.43f, 0.46f, 0.50f);
        if (unlocked)
        {
            _unlockStateText.text = "プレイ可能";
            if (_requiredMoodIcon != null) _requiredMoodIcon.gameObject.SetActive(false);
            if (_unlockSuffixText != null) _unlockSuffixText.gameObject.SetActive(false);
        }
        else
        {
            _unlockStateText.text = "前日の気分が";
            _requiredMoodIcon.gameObject.SetActive(true);
            _requiredMoodIcon.sprite = requiredMoodIcon;
            _requiredMoodIcon.enabled = requiredMoodIcon != null;
            _unlockSuffixText.gameObject.SetActive(true);
            _unlockSuffixText.text = "以上で解放";
            _unlockSuffixText.color = _unlockStateText.color;
        }

        _startButton.interactable = unlocked;
        _startButtonText.text = unlocked ? "スタート" : "未解放";
        DayButtonStyle.Apply(_startButton, _startButtonText, stage.MainColor);
    }
}
