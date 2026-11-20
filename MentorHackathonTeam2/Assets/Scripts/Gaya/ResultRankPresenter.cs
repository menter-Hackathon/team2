using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// ランクごとのリザルト画像、スコア、フィードバックを順に演出する。
/// </summary>
public sealed class ResultRankPresenter : MonoBehaviour
{
    /// <summary>
    /// 画像を表示するまでの待機時間を含む表示ステップ。
    /// </summary>
    [Serializable]
    private sealed class ImageStep
    {
        [Tooltip("この段階で表示する画像オブジェクト")]
        [SerializeField] private GameObject _imageObject;
        [Tooltip("この画像を表示する前の待機時間（秒）")]
        [SerializeField, Min(0f)] private float _delayBeforeShow;

        public GameObject ImageObject => _imageObject;
        public float DelayBeforeShow => _delayBeforeShow;
    }

    [Header("段階表示画像")]
    [Tooltip("順番に表示する画像と、各画像を表示する前の待機時間")]
    [SerializeField] private List<ImageStep> _imageSteps = new List<ImageStep>();

    [Header("スコア表示")]
    [Tooltip("カウントアップしたスコアを表示するテキスト")]
    [SerializeField] private TMP_Text _scoreText;
    [Tooltip("最後の画像を表示してからスコア上昇を始めるまでの待機時間（秒）")]
    [SerializeField, Min(0f)] private float _delayBeforeScore = 0.3f;
    [Tooltip("スコアのカウントアップ所要時間（秒）")]
    [SerializeField, Min(0f)] private float _scoreCountDuration = 1f;
    [Tooltip("スコア表示書式。{0} がスコア値に置換される")]
    [SerializeField] private string _scoreFormat = "{0}";

    [Header("フィードバック")]
    [Tooltip("タイプライター表示するメッセージテキスト")]
    [SerializeField] private TMP_Text _feedbackText;
    [Tooltip("スコア上昇完了後、メッセージ表示を始めるまでの待機時間（秒）")]
    [SerializeField, Min(0f)] private float _delayBeforeFeedback = 0.3f;
    [Tooltip("メッセージの1文字あたりの表示間隔（秒）")]
    [SerializeField, Min(0f)] private float _feedbackCharInterval = 0.05f;

    [Header("次へボタン")]
    [Tooltip("すべてのリザルト演出が終わったあとに表示するボタン")]
    [SerializeField] private GameObject _nextButtonObject;
    [Tooltip("フィードバック表示完了後、ボタンを表示するまでの待機時間（秒）")]
    [SerializeField, Min(0f)] private float _delayBeforeNextButton = 0.3f;

    private CancellationTokenSource _playCancellation;

    /// <summary>
    /// 設定された演出を順番に再生する。
    /// </summary>
    /// <param name="finalScore">最終スコア。</param>
    /// <param name="cancellationToken">呼び出し元のキャンセル通知。</param>
    /// <returns>演出完了またはキャンセル時に完了するタスク。</returns>
    public async UniTask PlayResultAsync(int finalScore, CancellationToken cancellationToken)
    {
        _playCancellation?.Cancel();
        _playCancellation?.Dispose();
        _playCancellation = new CancellationTokenSource();

        CancellationTokenSource playCancellation = _playCancellation;
        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, playCancellation.Token);
        CancellationToken token = linkedCancellation.Token;

        try
        {
            if (_scoreText == null || _feedbackText == null || _nextButtonObject == null)
            {
                Debug.LogError("[ResultRankPresenter] スコア、フィードバック、次へボタンの参照を設定してください。", this);
                return;
            }

            InitializePresentation();

            foreach (ImageStep step in _imageSteps)
            {
                token.ThrowIfCancellationRequested();
                if (step == null || step.ImageObject == null)
                {
                    continue;
                }

                await DelayAsync(step.DelayBeforeShow, token);
                step.ImageObject.SetActive(true);
            }

            await DelayAsync(_delayBeforeScore, token);
            await CountScoreAsync(finalScore, token);

            await DelayAsync(_delayBeforeFeedback, token);
            await RevealFeedbackAsync(token);

            await DelayAsync(_delayBeforeNextButton, token);
            token.ThrowIfCancellationRequested();
            _nextButtonObject.SetActive(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // オブジェクトの非アクティブ化・破棄、または再生し直しによるキャンセル。
        }
        finally
        {
            if (ReferenceEquals(_playCancellation, playCancellation))
            {
                _playCancellation.Dispose();
                _playCancellation = null;
            }
        }
    }

    /// <summary>
    /// 演出対象を初期状態にする。
    /// </summary>
    private void InitializePresentation()
    {
        foreach (ImageStep step in _imageSteps)
        {
            if (step != null && step.ImageObject != null)
            {
                step.ImageObject.SetActive(false);
            }
        }

        _nextButtonObject.SetActive(false);
        _scoreText.text = string.Format(_scoreFormat, 0);
        _feedbackText.ForceMeshUpdate();
        _feedbackText.maxVisibleCharacters = 0;
    }

    /// <summary>
    /// 指定時間だけ待機する。
    /// </summary>
    private static UniTask DelayAsync(float seconds, CancellationToken cancellationToken)
    {
        return UniTask.Delay(
            TimeSpan.FromSeconds(Mathf.Max(0f, seconds)),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// スコアを0から最終値までカウントアップする。
    /// </summary>
    private async UniTask CountScoreAsync(int finalScore, CancellationToken cancellationToken)
    {
        int displayedScore = 0;
        Tween tween = DOTween.To(
                () => displayedScore,
                value =>
                {
                    displayedScore = value;
                    _scoreText.text = string.Format(_scoreFormat, value);
                },
                finalScore,
                Mathf.Max(0f, _scoreCountDuration))
            .SetLink(gameObject);

        await AwaitTweenAsync(tween, cancellationToken);
    }

    /// <summary>
    /// フィードバック文を1文字ずつ表示する。
    /// </summary>
    private async UniTask RevealFeedbackAsync(CancellationToken cancellationToken)
    {
        _feedbackText.ForceMeshUpdate();
        int characterCount = _feedbackText.textInfo.characterCount;
        if (characterCount == 0)
        {
            return;
        }

        int visibleCharacters = 0;
        Tween tween = DOTween.To(
                () => visibleCharacters,
                value =>
                {
                    visibleCharacters = value;
                    _feedbackText.maxVisibleCharacters = value;
                },
                characterCount,
                characterCount * Mathf.Max(0f, _feedbackCharInterval))
            .SetEase(Ease.Linear)
            .SetLink(gameObject);

        await AwaitTweenAsync(tween, cancellationToken);
        _feedbackText.maxVisibleCharacters = characterCount;
    }

    /// <summary>
    /// Tween の完了を待ち、キャンセル時には Tween も停止する。
    /// </summary>
    private static async UniTask AwaitTweenAsync(Tween tween, CancellationToken cancellationToken)
    {
        var completion = new UniTaskCompletionSource();
        tween.OnComplete(() => completion.TrySetResult());
        tween.OnKill(() => completion.TrySetCanceled());

        using (cancellationToken.Register(() => tween.Kill()))
        {
            await completion.Task;
        }
    }

    /// <summary>
    /// オブジェクトが非アクティブになったときに演出をキャンセルする。
    /// </summary>
    private void OnDisable()
    {
        _playCancellation?.Cancel();
    }

    /// <summary>
    /// 実行中の演出を停止し、キャンセルトークンを破棄する。
    /// </summary>
    private void OnDestroy()
    {
        _playCancellation?.Cancel();
        _playCancellation?.Dispose();
        _playCancellation = null;
    }
}
