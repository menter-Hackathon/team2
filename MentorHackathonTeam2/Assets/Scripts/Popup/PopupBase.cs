using UnityEngine;

/// <summary>
/// ボタンの有無に関係なく、ポップアップの表示と開閉時の処理を管理する。
/// 初期非表示にするため、シーン上ではポップアップを有効にしておく。
/// </summary>
public class PopupBase : MonoBehaviour
{
    private bool _isOpen;

    protected virtual void Start()
    {
        // Start より前に Open された場合は、その表示を維持する。
        if (!_isOpen) gameObject.SetActive(false);
    }

    public void Open()
    {
        if (_isOpen) return;

        _isOpen = true;
        gameObject.SetActive(true);
        OnOpened();
    }

    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        OnClosed();
        gameObject.SetActive(false);
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }
}
