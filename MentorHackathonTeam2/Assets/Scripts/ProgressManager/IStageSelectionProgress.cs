/// <summary>ステージ選択画面が参照・更新できる進行記録。</summary>
public interface IStageSelectionProgress
{
    bool TryGetBestAccuracy(string dayName, out float accuracyPercent);
    bool WasUnlockAnimationPlayed(string dayName);
    void MarkUnlockAnimationPlayed(string dayName);
}
