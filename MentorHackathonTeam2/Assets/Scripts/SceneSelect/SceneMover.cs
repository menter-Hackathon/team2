using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMover : MonoBehaviour
{
    [SerializeField] private string _sceneName;

    public void MoveScene()
    {
        SceneManager.LoadScene(_sceneName);
    }
}
