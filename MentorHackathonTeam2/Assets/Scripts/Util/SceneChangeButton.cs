using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Buttonオブジェクトに直接アタッチしてScene遷移処理を付与するクラス
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneChangeButton : MonoBehaviour
{
    [SerializeField] private string _sceneName;
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (!string.IsNullOrWhiteSpace(_sceneName))
        {
            _button.onClick.AddListener(ChangeToConfiguredScene);
        }
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(ChangeToConfiguredScene);
    }

    private void ChangeToConfiguredScene() => OnClick(_sceneName);

    public void OnClick(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[SceneChangeButton] 遷移先シーンを読み込めません: {sceneName}", this);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
