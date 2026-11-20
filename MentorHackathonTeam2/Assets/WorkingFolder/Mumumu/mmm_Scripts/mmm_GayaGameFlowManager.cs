using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// ガヤゲーム全体の進行（遊び方パネル → 本編 → リザルト）を管理するクラス。
/// </summary>
public class mmm_GayaGameFlowManager : MonoBehaviourPun
{
    /// <summary>ゲームの進行状態。</summary>
    public enum FlowState
    {
        HowToPlay,
        Playing,
        Result,
    }

    [Header("参照")]
    [Tooltip("セリフ進行を担当する mmm_GayaSystem（Play On Start / Loop はオフ推奨）")]
    [SerializeField] private mmm_GayaSystem _gayaSystem;
    [Tooltip("スコア管理（任意）")]
    [SerializeField] private mmm_GayaScoreManager _gayaScoreManager;
    [Tooltip("記録する Day 名。空欄の場合は現在のシーン名を使用（Day1～Day4）")]
    [SerializeField] private string _dayName;

    [Header("遊び方")]
    [Tooltip("遊び方パネルのルートオブジェクト")]
    [SerializeField] private GameObject _howToPlayPanel;
    [Tooltip("遊び方パネル内のゲーム開始ボタン")]
    [SerializeField] private Button _startButton;

    [Header("本編")]
    [Tooltip("ゲーム本編中のみ表示する UI 要素（セリフ枠・ゲージ・レティクルなど）")]
    [SerializeField] private List<GameObject> _gameplayUiElements = new List<GameObject>();

    [Header("リザルト")]
    [Tooltip("リザルトパネルのルートオブジェクト")]
    [SerializeField] private GameObject _resultPanel;

    [Header("オンラインリザルト表示")]
    [SerializeField] private GameObject _multiResultRows;
    [SerializeField] private Image _localRankImage;
    [SerializeField] private Image _opponentRankImage;
    [SerializeField] private TMP_Text _localNameText;
    [SerializeField] private TMP_Text _opponentNameText;
    [SerializeField] private TMP_Text _localScoreText;
    [SerializeField] private TMP_Text _opponentScoreText;
    [SerializeField] private GameObject _nextButtonObject;
    [SerializeField] private Sprite _multiGreat;
    [SerializeField] private Sprite _multiGood;
    [SerializeField] private Sprite _multiMid;
    [SerializeField] private Sprite _multiBad;

    [Header("評価画像")]
    [Tooltip("達成率が Great しきい値以上のときに表示するオブジェクト")]
    [SerializeField] private GameObject _greatResultObject;
    [Tooltip("達成率が Good しきい値以上・Great しきい値未満のときに表示するオブジェクト")]
    [SerializeField] private GameObject _goodResultObject;
    [Tooltip("達成率が Mid しきい値以上・Good しきい値未満のときに表示するオブジェクト")]
    [SerializeField] private GameObject _midResultObject;
    [Tooltip("達成率が Mid しきい値未満のときに表示するオブジェクト")]
    [SerializeField] private GameObject _badResultObject;

    [Header("評価しきい値")]
    [Tooltip("Great 評価となる達成率の下限（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _greatThreshold = 0.9f;
    [Tooltip("Good 評価となる達成率の下限（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _goodThreshold = 0.7f;
    [Tooltip("Mid 評価となる達成率の下限（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float _midThreshold = 0.4f;

    private FlowState _state = FlowState.HowToPlay;

    private bool _isMyReady = false;
    private bool _isOpponentReady = false;

    private bool _isGameStarted = false;
    private TMP_Text _startButtonLabel;
    private string _startButtonOriginalText;
    private int _opponentFinalScore;
    private int _opponentMaxScore;
    private bool _hasOpponentScore;
    private bool _resultRevealFinished;
    private bool _opponentWasShown;

    /// <summary>現在の進行状態。</summary>
    public FlowState State => _state;

    /// <summary>進行状態が変化した際に発火する。</summary>
    public event Action<FlowState> OnStateChanged;

    private void Awake()
    {
        if (_startButton != null)
        {
            _startButtonLabel = _startButton.GetComponentInChildren<TMP_Text>(true);
            if (_startButtonLabel != null)
                _startButtonOriginalText = _startButtonLabel.text;
        }

        // mmm_GayaSystem.Start より前に実行されるため、自動再生・ループをここで確実に止める
        if (_gayaSystem != null)
        {
            _gayaSystem.PlayOnStart = false;
            _gayaSystem.Loop = false;
        }
        EnterHowToPlay();
    }

    private void OnEnable()
    {
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(OnStartButtonClicked);
        }
    }

    /// <summary>
    /// スタートボタン押下時の処理。遊び方表示中のみゲームを開始する。
    /// </summary>
    private void OnStartButtonClicked()
    {
        if (_state != FlowState.HowToPlay)
        {
            return;
        }
        if (TryGetComponent(out mmm_CloudflareGeminiClient generator) && !generator.IsContentReady)
        {
            return;
        }
        if (_isMyReady)
        {
            return;
        }
        
        Debug.Log("自分がボタンを押しました");
        _isMyReady = true;
        if (_startButtonLabel != null)
            _startButtonLabel.text = "準備完了";
        _startButton.interactable = false;
        CheckReady();

        // 相手にもログを送る
        photonView.RPC(nameof(OnOpponentButtonClicked), RpcTarget.Others);
    }

    [PunRPC]
    private void OnOpponentButtonClicked()
    {
        Debug.Log("相手がボタンを押しました");
        _isOpponentReady = true;
        CheckReady();
    }

    private void CheckReady()
    {
        // ゲーム開始の決定はMasterClientだけが行う
        if(!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if(_isMyReady && _isOpponentReady && !_isGameStarted)
        {
            _isGameStarted = true;

            Debug.Log("2人とも準備完了！ゲーム開始！");
            
            // 1.5秒後をゲーム開始時刻にする
            double startTime = PhotonNetwork.Time + 1.5;

            // 2人いｎ同じ開始時刻を通知
            photonView.RPC(nameof(StartGameAt), RpcTarget.All, startTime);
        }
        
    }

    [PunRPC]
    private void StartGameAt(double startTime)
    {
        StartGameAtAsync(startTime, destroyCancellationToken).Forget();
    }

    private async UniTaskVoid StartGameAtAsync(double startTime, CancellationToken ct)
    {
        // Photonの共通時刻が開始時刻になるまで待つ
        await UniTask.WaitUntil(() => PhotonNetwork.Time >= startTime, cancellationToken: ct);
        
        RunGameAsync(ct).Forget();
    }

    /// <summary>
    /// 本編を開始し、全セリフ再生完了後にリザルトへ遷移する。
    /// </summary>
    /// <param name="ct">中断用トークン。</param>
    private async UniTaskVoid RunGameAsync(CancellationToken ct)
    {
        if (_gayaSystem == null)
        {
            Debug.LogError("[mmm_GayaGameFlowManager] mmm_GayaSystem が未設定です。", this);
            return;
        }

        EnterPlaying();

        try
        {
            await _gayaSystem.PlaySequenceAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // PlaySequenceAsync はキャンセル時も例外を投げずに戻るため、ここで判定する
        if (ct.IsCancellationRequested)
        {
            return;
        }

        EnterResult();
    }

    /// <summary>
    /// 遊び方パネルのみを表示した初期状態にする。
    /// </summary>
    private void EnterHowToPlay()
    {
        SetActiveSafe(_howToPlayPanel, true);
        SetGameplayUiActive(false);
        SetActiveSafe(_resultPanel, false);
        HideAllRankObjects();
        SetActiveSafe(_multiResultRows, false);
        SetActiveSafe(_nextButtonObject, false);
        if (_startButton != null)
        {
            _startButton.interactable = true;
        }
        if (_startButtonLabel != null)
            _startButtonLabel.text = _startButtonOriginalText;
        ChangeState(FlowState.HowToPlay);
    }

    /// <summary>
    /// 本編用 UI を表示し、スコアを初期化する。
    /// </summary>
    private void EnterPlaying()
    {
        if (_startButton != null)
        {
            _startButton.interactable = false;
        }
        SetActiveSafe(_howToPlayPanel, false);
        SetActiveSafe(_resultPanel, false);
        HideAllRankObjects();
        _hasOpponentScore = false;
        _resultRevealFinished = false;
        _opponentWasShown = false;
        SetGameplayUiActive(true);

        if (_gayaScoreManager != null)
        {
            _gayaScoreManager.ResetScore();
        }
        ChangeState(FlowState.Playing);
    }

    /// <summary>
    /// 本編用 UI と残存演出を片付け、最終スコア付きでリザルトパネルを表示する。
    /// </summary>
    private void EnterResult()
    {
        _gayaSystem.ClearSpeechBubbles();
        if (_gayaScoreManager != null)
        {
            _gayaScoreManager.ClearPopups();
        }
        SetGameplayUiActive(false);

        int score = _gayaScoreManager != null ? _gayaScoreManager.Score : 0;
        int maxScore = _gayaScoreManager != null ? _gayaScoreManager.MaxScore : 0;
        float ratio = maxScore > 0 ? (float)score / maxScore : 0f;
        string dayName = string.IsNullOrWhiteSpace(_dayName)
            ? SceneManager.GetActiveScene().name
            : _dayName;
        DataManager.Results.RecordResult(dayName, ratio * 100f);
        SetActiveSafe(_resultPanel, true);
        HideAllRankObjects();
        // このスクリプトを使う既存の mmm_Day1 シーンは従来の表示を続ける。
        if (_multiResultRows == null)
        {
            ShowRankObject(Mathf.Clamp01(ratio), score);
            ChangeState(FlowState.Result);
            return;
        }

        SetActiveSafe(_multiResultRows, false);
        SetActiveSafe(_nextButtonObject, false);
        if (_localNameText != null)
            _localNameText.text = GetPlayerName(PhotonNetwork.LocalPlayer, "あなた");
        if (_opponentNameText != null)
        {
            var others = PhotonNetwork.PlayerListOthers;
            _opponentNameText.text = others.Length > 0
                ? GetPlayerName(others[0], "相手") : "相手";
        }
        if (_localScoreText != null) _localScoreText.text = "0";
        if (_opponentScoreText != null) _opponentScoreText.text = "—";

        // 先に終了した側の通知も受け取れるよう、受信状態は EnterPlaying で初期化する。
        if (PhotonNetwork.InRoom && PhotonNetwork.PlayerListOthers.Length > 0)
            photonView.RPC(nameof(ReceiveFinalScore), RpcTarget.Others, score, maxScore);

        ChangeState(FlowState.Result);
        PlayMultiResultAsync(score, maxScore, destroyCancellationToken).Forget();
    }

    [PunRPC]
    private void ReceiveFinalScore(int score, int maxScore, PhotonMessageInfo info)
    {
        if (info.Sender == null || PhotonNetwork.LocalPlayer == null ||
            info.Sender.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            return;

        _opponentFinalScore = Mathf.Max(0, score);
        _opponentMaxScore = Mathf.Max(0, maxScore);
        _hasOpponentScore = true;

        // 表示待ちの上限を超えてから届いた場合も、相手の行を更新する。
        if (_state == FlowState.Result && _resultRevealFinished && !_opponentWasShown)
            RevealLateOpponentAsync(destroyCancellationToken).Forget();
    }

    private async UniTaskVoid PlayMultiResultAsync(int localScore, int localMaxScore, CancellationToken ct)
    {
        try
        {
            // 背景を先に見せる時間を通信にも使う。
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: ct);
            float timeoutAt = Time.realtimeSinceStartup + 8f;
            while (!_hasOpponentScore && IsOpponentInRoom() && Time.realtimeSinceStartup < timeoutAt)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

            SetActiveSafe(_multiResultRows, true);
            SetRankImage(_localRankImage, localScore, localMaxScore);
            _opponentWasShown = _hasOpponentScore;
            if (_opponentWasShown)
                SetRankImage(_opponentRankImage, _opponentFinalScore, _opponentMaxScore);
            else if (_opponentRankImage != null)
                _opponentRankImage.enabled = false;

            // 評価画像を見せてから数字を動かす、従来の間隔を維持する。
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f), cancellationToken: ct);
            int opponentScore = _opponentWasShown ? _opponentFinalScore : 0;
            await CountScoresAsync(localScore, opponentScore, _opponentWasShown, ct);

            _resultRevealFinished = true;
            if (_hasOpponentScore && !_opponentWasShown)
                await RevealLateOpponentAsync(ct);

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), cancellationToken: ct);
            SetActiveSafe(_nextButtonObject, true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async UniTask RevealLateOpponentAsync(CancellationToken ct)
    {
        if (_opponentWasShown) return;
        _opponentWasShown = true;
        SetRankImage(_opponentRankImage, _opponentFinalScore, _opponentMaxScore);
        try
        {
            await CountScoresAsync(0, _opponentFinalScore, true, ct, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async UniTask CountScoresAsync(int localScore, int opponentScore,
        bool showOpponent, CancellationToken ct, bool updateLocal = true)
    {
        float elapsed = 0f;
        const float duration = 1f;
        while (elapsed < duration)
        {
            ct.ThrowIfCancellationRequested();
            float t = Mathf.Clamp01(elapsed / duration);
            if (updateLocal && _localScoreText != null)
                _localScoreText.text = Mathf.RoundToInt(localScore * t).ToString();
            if (showOpponent && _opponentScoreText != null)
                _opponentScoreText.text = Mathf.RoundToInt(opponentScore * t).ToString();
            elapsed += Time.unscaledDeltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        if (updateLocal && _localScoreText != null) _localScoreText.text = localScore.ToString();
        if (showOpponent && _opponentScoreText != null) _opponentScoreText.text = opponentScore.ToString();
    }

    private void SetRankImage(Image image, int score, int maxScore)
    {
        if (image == null) return;
        float ratio = maxScore > 0 ? (float)score / maxScore : 0f;
        image.sprite = ratio >= _greatThreshold ? _multiGreat
            : ratio >= _goodThreshold ? _multiGood
            : ratio >= _midThreshold ? _multiMid : _multiBad;
        image.enabled = image.sprite != null;
    }

    private static bool IsOpponentInRoom() => PhotonNetwork.InRoom &&
        PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 1;

    private static string GetPlayerName(Photon.Realtime.Player player, string fallback) =>
        player != null && !string.IsNullOrWhiteSpace(player.NickName) ? player.NickName : fallback;

    /// <summary>
    /// 達成率に応じた評価オブジェクトだけを表示し、結果演出を開始する。
    /// </summary>
    /// <param name="ratio">0〜1 の達成率。</param>
    /// <param name="finalScore">最終スコア。</param>
    private void ShowRankObject(float ratio, int finalScore)
    {
        GameObject selected;
        if (ratio >= _greatThreshold)
        {
            selected = _greatResultObject;
        }
        else if (ratio >= _goodThreshold)
        {
            selected = _goodResultObject;
        }
        else if (ratio >= _midThreshold)
        {
            selected = _midResultObject;
        }
        else
        {
            selected = _badResultObject;
        }

        SetActiveSafe(_greatResultObject, selected == _greatResultObject);
        SetActiveSafe(_goodResultObject, selected == _goodResultObject);
        SetActiveSafe(_midResultObject, selected == _midResultObject);
        SetActiveSafe(_badResultObject, selected == _badResultObject);

        if (selected == null)
        {
            Debug.LogError("[mmm_GayaGameFlowManager] 達成率に対応する評価オブジェクトが設定されていません。", this);
            return;
        }

        ResultRankPresenter presenter = selected.GetComponent<ResultRankPresenter>();
        if (presenter == null)
        {
            Debug.LogError($"[mmm_GayaGameFlowManager] {selected.name} に ResultRankPresenter が設定されていません。", selected);
            return;
        }

        presenter.PlayResultAsync(finalScore, destroyCancellationToken).Forget();
    }

    /// <summary>
    /// 4つの評価オブジェクトをすべて非表示にする。
    /// </summary>
    private void HideAllRankObjects()
    {
        SetActiveSafe(_greatResultObject, false);
        SetActiveSafe(_goodResultObject, false);
        SetActiveSafe(_midResultObject, false);
        SetActiveSafe(_badResultObject, false);
    }

    /// <summary>
    /// 本編用 UI 要素をまとめて表示／非表示にする。
    /// </summary>
    private void SetGameplayUiActive(bool isActive)
    {
        foreach (GameObject element in _gameplayUiElements)
        {
            SetActiveSafe(element, isActive);
        }
    }

    /// <summary>
    /// 進行状態を更新し、イベントを発火する。
    /// </summary>
    private void ChangeState(FlowState next)
    {
        _state = next;
        OnStateChanged?.Invoke(next);
    }

    /// <summary>
    /// null を許容して GameObject の表示状態を切り替える。
    /// </summary>
    private static void SetActiveSafe(GameObject target, bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
