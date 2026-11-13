/// <summary>各 Day の終了処理に渡す、計算済み精度の保存窓口。</summary>
public interface IStageResultRecorder
{
    void RecordResult(string dayName, float accuracyPercent);
}
