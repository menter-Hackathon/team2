using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>ゲーム終了時のリザルト画面を制御する。</summary>
public sealed class ResultController : MonoBehaviour
{
    [Header("定数・保存キー")]
    [Tooltip("タイトル画面で保存されたニックネームのPlayerPrefsキー")]
    private const string NicknameKey = "TitleSelect.Nickname";

    [Tooltip("集計対象とする各日程の識別名")]
    private static readonly string[] DayNames = { "Day1", "Day2", "Day3" };

    [Header("遷移先")]
    [Tooltip("タイトルボタンを押した際に遷移するシーン名")]
    [SerializeField] private string _titleSceneName = "TitleSelect";

    [Header("評価テキスト（平均スコアに応じたコメント）")]
    [Tooltip("平均精度90%〜100%のときに表示する評価テキスト")]
    [SerializeField, TextArea(2, 4)] private string _eval90To100 = "完璧な立ち回り！すべての課題を高い精度でこなしました。";

    [Tooltip("平均精度70%〜90%未満のときに表示する評価テキスト")]
    [SerializeField, TextArea(2, 4)] private string _eval70To90 = "素晴らしい成績です！安定したパフォーマンスを発揮できました。";

    [Tooltip("平均精度40%〜70%未満のときに表示する評価テキスト")]
    [SerializeField, TextArea(2, 4)] private string _eval40To70 = "まずまずの結果です。次はさらに高い精度を目指してみましょう。";

    [Tooltip("平均精度0%〜40%未満のときに表示する評価テキスト")]
    [SerializeField, TextArea(2, 4)] private string _eval0To40 = "お疲れ様でした！まずは各日程の感覚を掴むところから始めましょう。";

    [Header("UI要素")]
    [Tooltip("プレイヤーのニックネームを表示するTextMeshPro")]
    [SerializeField] private TMP_Text _nameText;

    [Tooltip("平均スコアに応じた評価コメントを表示するTextMeshPro")]
    [SerializeField] private TMP_Text _feedbackText;

    [Tooltip("タイトル画面へ戻るためのボタン")]
    [SerializeField] private Button _titleButton;

    [Header("文字送り演出")]
    [Tooltip("1文字を表示する間隔（秒）。値が小さいほど速く表示される")]
    [SerializeField, Min(0.001f)] private float _charInterval = 0.05f;

    [Header("内部状態")]
    [Tooltip("ニックネーム文字送り演出用のTweenインスタンス")]
    private Tween _nameTween;

    [Tooltip("評価コメント文字送り演出用のTweenインスタンス")]
    private Tween _feedbackTween;

    [Header("BGM")] [SerializeField] private AudioClip bgm;

    /// <summary>初期化処理を行い、UIの参照検証とリザルト表示を開始する。</summary>
    private void Start()
    {
        if (_nameText == null || _feedbackText == null || _titleButton == null)
        {
            Debug.LogError("[ResultController] NameText, FeedbackText, TitleButton のいずれかが未設定です。", this);
            enabled = false;
            return;
        }

        _titleButton.onClick.AddListener(BackToTitle);
        AudioManager.Instance.PlayBgm(bgm);

        float averageAccuracy = CalculateAverageAccuracy();
        PlayTextAnimations(averageAccuracy);
    }

    /// <summary>破棄時にイベントリスナーの解除と実行中のTweenの停止を行う。</summary>
    private void OnDestroy()
    {
        if (_titleButton != null)
            _titleButton.onClick.RemoveListener(BackToTitle);

        _nameTween?.Kill();
        _feedbackTween?.Kill();
    }

    /// <summary>Day1〜Day3の最高精度を取得し、全日程の平均精度を算出する。</summary>
    private float CalculateAverageAccuracy()
    {
        float totalAccuracy = 0f;

        for (int i = 0; i < DayNames.Length; i++)
        {
            bool hasResult = DataManager.Selection.TryGetBestAccuracy(DayNames[i], out float best);
            totalAccuracy += hasResult ? best : 0f;
        }

        return totalAccuracy / DayNames.Length;
    }

    /// <summary>ニックネームと平均精度に応じた評価テキストを1文字ずつ表示する。</summary>
    private void PlayTextAnimations(float averageAccuracy)
    {
        string nickname = PlayerPrefs.GetString(NicknameKey, "").Trim();
        string feedback = GetEvaluationMessage(averageAccuracy);

        _nameText.text = nickname;
        _nameText.maxVisibleCharacters = 0;

        _feedbackText.text = feedback;
        _feedbackText.maxVisibleCharacters = 0;

        float nameDuration = nickname.Length * _charInterval;
        _nameTween = DOTween.To(
            () => _nameText.maxVisibleCharacters,
            x => _nameText.maxVisibleCharacters = x,
            nickname.Length,
            nameDuration
        ).SetEase(Ease.Linear).SetUpdate(true);

        float feedbackDuration = feedback.Length * _charInterval;
        _feedbackTween = DOTween.To(
            () => _feedbackText.maxVisibleCharacters,
            x => _feedbackText.maxVisibleCharacters = x,
            feedback.Length,
            feedbackDuration
        ).SetEase(Ease.Linear).SetUpdate(true);
    }

    /// <summary>指定された精度（%）に対応する評価テキストを返す。</summary>
    private string GetEvaluationMessage(float accuracyPercent)
    {
        if (accuracyPercent >= 90f) return _eval90To100;
        if (accuracyPercent >= 70f) return _eval70To90;
        if (accuracyPercent >= 40f) return _eval40To70;
        return _eval0To40;
    }

    /// <summary>設定されたタイトルシーンへ遷移する。</summary>
    private void BackToTitle()
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName) || !Application.CanStreamedLevelBeLoaded(_titleSceneName))
        {
            Debug.LogWarning($"[ResultController] 遷移先シーンを読み込めません: {_titleSceneName}", this);
            return;
        }
        SceneManager.LoadScene(_titleSceneName);
    }
}
