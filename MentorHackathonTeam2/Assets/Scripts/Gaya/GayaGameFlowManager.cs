using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ガヤゲーム全体の進行（遊び方パネル → 本編 → リザルト）を管理するクラス。
/// </summary>
public class GayaGameFlowManager : MonoBehaviour
{
    /// <summary>ゲームの進行状態。</summary>
    public enum FlowState
    {
        HowToPlay,
        Playing,
        Result,
    }

    [Header("参照")]
    [Tooltip("セリフ進行を担当する GayaSystem（Play On Start / Loop はオフ推奨）")]
    [SerializeField] private GayaSystem _gayaSystem;
    [Tooltip("スコア管理（任意）")]
    [SerializeField] private GayaScoreManager _gayaScoreManager;
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
    [Tooltip("リザルト画面の最終スコア表示テキスト（任意）")]
    [SerializeField] private TMP_Text _resultScoreText;
    [Tooltip("最終スコアの表示フォーマット（{0} にスコアが入る）")]
    [SerializeField] private string _resultScoreFormat = "SCORE : {0}";

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

    /// <summary>現在の進行状態。</summary>
    public FlowState State => _state;

    /// <summary>進行状態が変化した際に発火する。</summary>
    public event Action<FlowState> OnStateChanged;

    private void Awake()
    {
        // GayaSystem.Start より前に実行されるため、自動再生・ループをここで確実に止める
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
        RunGameAsync(destroyCancellationToken).Forget();
    }

    /// <summary>
    /// 本編を開始し、全セリフ再生完了後にリザルトへ遷移する。
    /// </summary>
    /// <param name="ct">中断用トークン。</param>
    private async UniTaskVoid RunGameAsync(CancellationToken ct)
    {
        if (_gayaSystem == null)
        {
            Debug.LogError("[GayaGameFlowManager] GayaSystem が未設定です。", this);
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
        if (_startButton != null)
        {
            _startButton.interactable = true;
        }
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
        ShowRankObject(Mathf.Clamp01(ratio));

        if (_resultScoreText != null)
        {
            _resultScoreText.text = string.IsNullOrEmpty(_resultScoreFormat)
                ? score.ToString()
                : string.Format(_resultScoreFormat, score);
        }

        SetActiveSafe(_resultPanel, true);
        ChangeState(FlowState.Result);
    }

    /// <summary>
    /// 達成率に応じた評価オブジェクトを1つだけ表示し、残りを非表示にする。
    /// </summary>
    /// <param name="ratio">0〜1 の達成率。</param>
    private void ShowRankObject(float ratio)
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
