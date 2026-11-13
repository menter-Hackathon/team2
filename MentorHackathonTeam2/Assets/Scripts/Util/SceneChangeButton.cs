using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangeButton : MonoBehaviour
{
    public void OnClick(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[SceneSelectController] 遷移先シーンを読み込めません: {sceneName}", this);
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
