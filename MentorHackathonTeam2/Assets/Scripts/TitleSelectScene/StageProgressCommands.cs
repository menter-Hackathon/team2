using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル画面だけで使うステージ進行デバッグコマンド。
/// このファイルを削除すればコマンドは無効になる。
/// </summary>
public sealed class StageProgressCommands : MonoBehaviour
{
    private const string TitleSceneName = "TitleSelect";
    private const string ProgressKeyPrefix = "SceneSelect.v1.";
    private const float HoldSeconds = 5f;

    private static readonly string[] StageNames = { "Day1", "Day2", "Day3", "Online" };
    private static bool s_waitForRelease;

    private int _activeCommand;
    private float _holdStartedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        s_waitForRelease = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TitleSceneName) return;
        new GameObject(nameof(StageProgressCommands)).AddComponent<StageProgressCommands>();
    }

    private void Update()
    {
        bool unlockHeld = Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Keypad1);
        bool resetHeld = Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Keypad2);

        if (s_waitForRelease)
        {
            if (!unlockHeld && !resetHeld) s_waitForRelease = false;
            return;
        }

        // 両方同時に押した場合は長押しを数え直す。
        int command = unlockHeld == resetHeld ? 0 : unlockHeld ? 1 : 2;
        if (command == 0)
        {
            _activeCommand = 0;
            return;
        }

        if (_activeCommand != command)
        {
            _activeCommand = command;
            _holdStartedAt = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - _holdStartedAt < HoldSeconds) return;

        s_waitForRelease = true;
        if (command == 1)
        {
            UnlockAllStages();
            Debug.Log("[StageProgressCommands] 全ステージを解放しました。");
        }
        else
        {
            ResetStageProgress();
            Debug.Log("[StageProgressCommands] ステージ進行を初期化しました。");
        }

        // 既存のタイトル画面に保存内容を読み直させる。
        SceneManager.LoadScene(TitleSceneName);
    }

    private static void UnlockAllStages()
    {
        // 既存の解放判定は前のDayの記録の有無を見るため、未記録のDayに0%を保存する。
        for (int i = 0; i < 3; i++)
        {
            string stageName = StageNames[i];
            if (!DataManager.Selection.TryGetBestAccuracy(stageName, out _))
                DataManager.Results.RecordResult(stageName, 0f);
        }
    }

    private static void ResetStageProgress()
    {
        foreach (string stageName in StageNames)
        {
            PlayerPrefs.DeleteKey(ProgressKeyPrefix + stageName + ".BestAccuracy");
            PlayerPrefs.DeleteKey(ProgressKeyPrefix + stageName + ".UnlockAnimationPlayed");
        }

        PlayerPrefs.Save();
    }
}
