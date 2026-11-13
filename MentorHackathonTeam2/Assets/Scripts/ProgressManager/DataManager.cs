using UnityEngine;

/// <summary>
/// プレイヤーの進行記録への入口。内部の保存処理は一つだけ保持し、
/// 利用側には用途別のインターフェースのみ渡す。
/// </summary>
public static class DataManager
{
    private static readonly ProgressStore Instance = new ProgressStore();

    public static IStageSelectionProgress Selection => Instance;
    public static IStageResultRecorder Results => Instance;

    private sealed class ProgressStore : IStageSelectionProgress, IStageResultRecorder
    {
        private const string KeyPrefix = "SceneSelect.v1.";

        public bool TryGetBestAccuracy(string dayName, out float accuracyPercent)
        {
            accuracyPercent = 0f;
            if (!IsKnownDay(dayName))
            {
                return false;
            }

            string key = BestAccuracyKey(dayName);
            if (!PlayerPrefs.HasKey(key))
            {
                return false;
            }

            accuracyPercent = Mathf.Clamp(PlayerPrefs.GetFloat(key), 0f, 100f);
            return true;
        }

        public void RecordResult(string dayName, float accuracyPercent)
        {
            if (!IsKnownDay(dayName) || float.IsNaN(accuracyPercent) || float.IsInfinity(accuracyPercent))
            {
                Debug.LogWarning($"[DataManager] Invalid Day or accuracy: {dayName}, {accuracyPercent}");
                return;
            }

            float clamped = Mathf.Clamp(accuracyPercent, 0f, 100f);
            if (TryGetBestAccuracy(dayName, out float currentBest) && currentBest >= clamped)
            {
                return;
            }

            PlayerPrefs.SetFloat(BestAccuracyKey(dayName), clamped);
            PlayerPrefs.Save();
        }

        public bool WasUnlockAnimationPlayed(string dayName)
        {
            return IsKnownDay(dayName) && PlayerPrefs.GetInt(UnlockAnimationKey(dayName), 0) != 0;
        }

        public void MarkUnlockAnimationPlayed(string dayName)
        {
            if (!IsKnownDay(dayName) || WasUnlockAnimationPlayed(dayName))
            {
                return;
            }

            PlayerPrefs.SetInt(UnlockAnimationKey(dayName), 1);
            PlayerPrefs.Save();
        }

        private static bool IsKnownDay(string dayName)
        {
            return dayName == "Day1" || dayName == "Day2" || dayName == "Day3" || dayName == "Day4";
        }

        private static string BestAccuracyKey(string dayName) => KeyPrefix + dayName + ".BestAccuracy";
        private static string UnlockAnimationKey(string dayName) => KeyPrefix + dayName + ".UnlockAnimationPlayed";
    }
}
