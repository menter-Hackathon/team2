using UnityEngine;
using UnityEngine.UI;

/// <summary>ボタン押下時にAudioManager経由でクリック効果音を再生する。</summary>
[RequireComponent(typeof(Button))]
public sealed class ButtonSoundPlayer : MonoBehaviour
{
    [Header("クリック音")]
    [Tooltip("ボタン押下時に再生する効果音")]
    [SerializeField] private AudioClip _clickSe;
    [Tooltip("クリック効果音の音量倍率")]
    [SerializeField] private float _volumeScale = 1f;

    private Button _button;

    /// <summary>ボタンのクリック通知を登録する。</summary>
    private void OnEnable()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClickSe);
    }

    /// <summary>ボタンのクリック通知を解除する。</summary>
    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlayClickSe);
        }
    }

    /// <summary>クリック効果音を再生する。</summary>
    private void PlayClickSe()
    {
        AudioManager.Instance?.PlaySe(_clickSe, _volumeScale);
    }
}
