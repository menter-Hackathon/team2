using UnityEngine;
using UnityEngine.UI;

/// <summary>ボタンで開閉するポップアップ。ボタン参照は必要なものだけ設定する。</summary>
public class PopUpWindow : PopupBase
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    protected override void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        base.Start();
    }

    protected virtual void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }
}
