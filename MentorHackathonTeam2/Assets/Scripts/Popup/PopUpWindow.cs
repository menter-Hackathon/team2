using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ポップアップ自体にアタッチする基底クラス
/// Startで非表示、開閉関数、開閉時に実行する仮想関数が定義されている
/// </summary>
public class PopUpWindow : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        this.gameObject.SetActive(false);
    }

    private void Open()
    {
        this.gameObject.SetActive(true);
        OnOpen();
    }
    
    private void Close()
    {
        this.gameObject.SetActive(true);
        OnClose();
    }

    public virtual void OnOpen(){}
    public virtual void OnClose(){}
}
