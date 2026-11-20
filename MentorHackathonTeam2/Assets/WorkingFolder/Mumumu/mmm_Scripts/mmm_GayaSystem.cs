using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// ガヤゲームのテキストシステム本体。
/// TMP の文字送り、UniTask によるセリフ進行、回答済み単語の頂点カラー変更、
/// および先着・後着のクリック判定を担当する。
/// </summary>
public class mmm_GayaSystem : MonoBehaviourPun
{
    [Header("参照")]
    [Tooltip("セリフを表示する TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI _text;

    [Header("シーン別スライド遷移（Day2用）")]
    [Tooltip("スライド遷移を有効にするシーン名")]
    [SerializeField] private string _slideSceneName = "Day2";
    [Tooltip("複製・スライド移動させる対象UIのルート RectTransform")]
    [SerializeField] private RectTransform _gameplayUiRoot;
    [Tooltip("複製UIの初期Xオフセットおよびスライド移動量")]
    [SerializeField] private float _slideOffsetX = 900f;
    [Tooltip("スライド移動にかかる時間（秒）")]
    [SerializeField] private float _slideDuration = 0.5f;
    [Tooltip("スライド移動のイージング")]
    [SerializeField] private Ease _slideEase = Ease.OutCubic;

    [Header("セリフ")]
    [Tooltip("再生するセリフデータ一覧（上から順に再生）")]
    [SerializeField] private List<DialogueLineData> _lines = new List<DialogueLineData>();
    [Tooltip("全セリフ再生後に先頭へ戻ってループするか")]
    [SerializeField] private bool _loop = true;
    [Tooltip("Start 時に自動でシーケンスを開始するか")]
    [SerializeField] private bool _playOnStart = true;

    [Header("スキップ")]
    [Tooltip("スキップ可能なセリフの再生中だけ表示するボタン（任意）")]
    [SerializeField] private Button _skipButton;

    [Header("ガヤ吹き出し")]
    [Tooltip("ガヤ成功時に生成する吹き出しのプレハブ（GayaSpeechBubble 付き）")]
    [SerializeField] private GayaSpeechBubble _gayaSpeechBubblePrefab;
    [Tooltip("吹き出しの出現候補地点（Canvas 配下の空 RectTransform）。使用中でない地点からランダムに選ばれる")]
    [SerializeField] private List<RectTransform> _gayaBubbleSpawnPoints = new List<RectTransform>();
    [Tooltip("全地点が使用中の場合に、全地点からランダムに選んで重ねて表示するか（false なら生成をスキップ）")]
    [SerializeField] private bool _allowOverlapWhenFull = false;

    [Header("表示色")]
    [Tooltip("回答済み（ガヤ成功）単語の表示色")]
    [SerializeField] private Color _claimedColor = new Color32(0xFF, 0x3B, 0x30, 0xFF);
    [Tooltip("おてつき（非ガヤワード）をクリックした単語の表示色")]
    [SerializeField] private Color _opponentClaimColor = Color.gray;

    [Header("おてつき")]
    [Tooltip("非ガヤワードをクリックした際の吹き出しテキストのフォーマット（{0} にクリックした単語が入る）")]
    [SerializeField] private string _missBubbleFormat = "{0}！？";





    /// <summary>
    /// 回答済み単語1つ分の記録（回答したプレイヤーと、正解ガヤだったか）。
    /// </summary>
    private sealed class ClaimRecord
    {
        public readonly int FirstActorNumber;
        public int SecondActorNumber;

        public ClaimRecord(int firstActorNumber)
        {
            FirstActorNumber = firstActorNumber;
        }
    }

    private readonly Dictionary<int, Dictionary<string, ClaimRecord>> _claimsByLine =
        new Dictionary<int, Dictionary<string, ClaimRecord>>();
    private Dictionary<string, ClaimRecord> _claimedBy = new Dictionary<string, ClaimRecord>();
    private int _currentLineIndex = -1;
    private DialogueLineData _currentLine;
    private DialogueLineData _linePreparedBySlide;
    private CancellationTokenSource _skipLineCancellation;
    private bool _skipRequested;
    private bool _isTransitioning;
    private Tween _slideTween;
    private readonly Dictionary<RectTransform, GayaSpeechBubble> _occupiedSpawnPoints = new Dictionary<RectTransform, GayaSpeechBubble>();
    private readonly List<RectTransform> _spawnCandidates = new List<RectTransform>();

    /// <summary>
    /// 単語の確定に成功した際に発火する（targetId, playerId, 吹き出しテキスト, isGayaWord）。
    /// isGayaWord が true なら正解ガヤ（加点）、false なら非ガヤ（おてつき・減点）。
    /// </summary>
    public event Action<string, string, string, bool> OnWordClaimed;

    /// <summary>
    /// 単語の確定に成功した際に、吹き出しの出現地点付きで発火する
    /// （targetId, playerId, 吹き出しテキスト, isGayaWord, 出現地点, クリック順）。
    /// 吹き出しを生成できなかった場合、出現地点は null になる。
    /// </summary>
    public event Action<string, string, string, bool, RectTransform, int> OnWordClaimedAt;

    /// <summary>Start 時に自動でシーケンスを開始するか（Start より前に設定すること）。</summary>
    public bool PlayOnStart
    {
        get => _playOnStart;
        set => _playOnStart = value;
    }

    /// <summary>全セリフ再生後に先頭へ戻ってループするか。</summary>
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    /// <summary>現在のセリフ表示に使っている TextMeshProUGUI。</summary>
    public TextMeshProUGUI CurrentText => _text;

    public int CurrentLineIndex => _currentLineIndex;

    /// <summary>セリフUIがスライド遷移中か。</summary>
    public bool IsTransitioning => _isTransitioning;

    /// <summary>
    /// 表示中のガヤ吹き出しをすべて破棄し、出現地点の占有状態をリセットする。
    /// </summary>
    public void ClearSpeechBubbles()
    {
        var bubbles = new List<GayaSpeechBubble>(_occupiedSpawnPoints.Values);
        _occupiedSpawnPoints.Clear();
        foreach (GayaSpeechBubble bubble in bubbles)
        {
            if (bubble != null)
            {
                Destroy(bubble.gameObject);
            }
        }
    }

    /// <summary>メッシュ変更監視とスキップボタンのクリック通知を登録する。</summary>
    private void OnEnable()
    {
        // レイアウト変更などで TMP がメッシュを再生成した場合にも色を維持するための保険
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        if (_skipButton != null)
        {
            _skipButton.onClick.AddListener(OnSkipButtonClicked);
            SetSkipButtonVisible(_currentLine != null && _currentLine.isSkippable && !_skipRequested);
        }
    }

    /// <summary>登録済み通知を解除し、実行中のスライドTweenを停止する。</summary>
    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (_slideTween != null && _slideTween.IsActive())
        {
            _slideTween.Kill();
        }
        _slideTween = null;
        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(OnSkipButtonClicked);
            SetSkipButtonVisible(false);
        }
    }

    /// <summary>スキップ可能なセリフを中断する。スライド遷移中は処理しない。</summary>
    private void OnSkipButtonClicked()
    {
        if (_isTransitioning || _currentLine == null || !_currentLine.isSkippable || _skipLineCancellation == null || _skipRequested)
        {
            return;
        }

        _skipRequested = true;
        SetSkipButtonVisible(false);
        _skipLineCancellation.Cancel();
    }

    /// <summary>スキップボタンの表示を切り替える。遷移中は常に非表示にする。</summary>
    private void SetSkipButtonVisible(bool visible)
    {
        visible = visible && !_isTransitioning;
        if (_skipButton != null && _skipButton.gameObject.activeSelf != visible)
        {
            _skipButton.gameObject.SetActive(visible);
        }
    }

    /// <summary>設定に応じてセリフシーケンスを開始する。</summary>
    private void Start()
    {
    
        PhotonView pv = GetComponent<PhotonView>();

        if (pv != null)
        {
            Debug.Log($"mmm_GayaSystem ViewID: {pv.ViewID}");
        }
        if (_playOnStart)
        {
            PlaySequenceAsync(destroyCancellationToken).Forget();
        }
    }

    /// <summary>
    /// 登録された全セリフを順に再生する。<c>_loop</c> が true の場合は繰り返す。
    /// </summary>
    /// <param name="ct">中断用トークン（通常は destroyCancellationToken）。</param>
    /// <returns>シーケンス完了（またはキャンセル）時に完了する UniTask。</returns>
    public async UniTask PlaySequenceAsync(CancellationToken ct)
    {
        if (_text == null || _lines.Count == 0)
        {
            Debug.LogWarning("[mmm_GayaSystem] Text またはセリフが未設定です。");
            return;
        }

        try
        {
            do
            {
                for (int i = 0; i < _lines.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    DialogueLineData line = _lines[i];
                    if (line != null)
                    {
                        DialogueLineData nextLine = FindNextLine(i + 1);
                        await PlayLineAsync(line, nextLine, i, ct);
                    }
                }
            }
            while (_loop);
        }
        catch (OperationCanceledException)
        {
            // 破棄・中断時は静かに終了
        }
    }

    /// <summary>
    /// MasterClient が表示中の単語とクリック順を判定する。
    /// </summary>
    [PunRPC]
    private void RequestClaimWord(string targetId, int lineIndex, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || info.Sender == null ||
            lineIndex != _currentLineIndex || !IsTargetInteractable(targetId))
        {
            return;
        }

        int actorNumber = info.Sender.ActorNumber;
        _claimedBy.TryGetValue(targetId, out ClaimRecord record);
        if (record != null && (record.FirstActorNumber == actorNumber || record.SecondActorNumber != 0))
        {
            return;
        }
        bool isGayaWord = TryFindGayaTarget(targetId, out GayaTargetData target);
        string phrase = isGayaWord
            ? target.gayaPhrase
            : BuildMissPhrase(TryGetLinkInfo(targetId, out TMP_LinkInfo link) ? link.GetLinkText() : string.Empty);
        int claimOrder = record == null ? 1 : 2;
        int firstActorNumber = record == null ? actorNumber : record.FirstActorNumber;

        // Master へは先に適用し、連続するリクエストの順番を確定させる。
        ApplyClaimWordCore(lineIndex, targetId, actorNumber, firstActorNumber, isGayaWord, phrase, claimOrder);
        photonView.RPC(nameof(ApplyClaimWord), RpcTarget.Others,
            lineIndex, targetId, actorNumber, firstActorNumber, isGayaWord, phrase, claimOrder);
    }

    [PunRPC]
    private void ApplyClaimWord(int lineIndex, string targetId, int actorNumber,
        int firstActorNumber, bool isGayaWord, string phrase, int claimOrder, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient) return;
        ApplyClaimWordCore(lineIndex, targetId, actorNumber, firstActorNumber,
            isGayaWord, phrase, claimOrder);
    }

    private void ApplyClaimWordCore(int lineIndex, string targetId, int actorNumber,
        int firstActorNumber, bool isGayaWord, string phrase, int claimOrder)
    {
        Dictionary<string, ClaimRecord> claims = GetLineClaims(lineIndex);
        claims.TryGetValue(targetId, out ClaimRecord record);
        if (claimOrder == 1)
        {
            if (record != null) return;
            record = new ClaimRecord(firstActorNumber);
            claims[targetId] = record;
        }
        else if (claimOrder == 2)
        {
            if (record == null)
            {
                record = new ClaimRecord(firstActorNumber);
                claims[targetId] = record;
            }
            if (record.SecondActorNumber != 0) return;
            record.SecondActorNumber = actorNumber;
        }
        else
        {
            return;
        }

        string playerId = actorNumber.ToString();
        RectTransform spawnPoint = null;
        if (lineIndex == _currentLineIndex)
        {
            RefreshMeshAndColors();
            spawnPoint = SpawnGayaSpeechBubble(phrase);
        }
        OnWordClaimed?.Invoke(targetId, playerId, phrase, isGayaWord);
        OnWordClaimedAt?.Invoke(targetId, playerId, phrase, isGayaWord, spawnPoint, claimOrder);
    }

    private Dictionary<string, ClaimRecord> GetLineClaims(int lineIndex)
    {
        if (!_claimsByLine.TryGetValue(lineIndex, out Dictionary<string, ClaimRecord> claims))
        {
            claims = new Dictionary<string, ClaimRecord>();
            _claimsByLine[lineIndex] = claims;
        }
        return claims;
    }

    /// <summary>
    /// 指定 ID の単語が現在クリック可能か（表示済みかつ二人に未取得か）を返す。
    /// </summary>
    /// <param name="targetId">リンク ID。</param>
    /// <returns>操作可能なら true。</returns>
    public bool IsTargetInteractable(string targetId)
    {
        if (_isTransitioning || string.IsNullOrEmpty(targetId) ||
            (_claimedBy.TryGetValue(targetId, out ClaimRecord record) && record.SecondActorNumber != 0))
        {
            return false;
        }
        return TryGetLinkInfo(targetId, out TMP_LinkInfo link) && IsLinkFullyVisible(link);
    }

    /// <summary>
    /// ガヤの吹き出しを生成し、セリフを渡してアニメーションを再生する。
    /// </summary>
    /// <param name="gayaPhrase">吹き出しに表示するガヤのセリフ。</param>
    /// <returns>吹き出しを生成した出現地点。生成できなかった場合は null。</returns>
    private RectTransform SpawnGayaSpeechBubble(string gayaPhrase)
    {
        if (_gayaSpeechBubblePrefab == null)
        {
            Debug.LogWarning("[mmm_GayaSystem] Gaya speech bubble prefab is not assigned.", this);
            return null;
        }

        if (!TrySelectSpawnPoint(out RectTransform spawnPoint))
        {
            Debug.LogWarning("[mmm_GayaSystem] 空いている吹き出しの出現地点がないため、生成をスキップしました。", this);
            return null;
        }

        GayaSpeechBubble bubble = Instantiate(_gayaSpeechBubblePrefab, spawnPoint, false);
        _occupiedSpawnPoints[spawnPoint] = bubble;
        bubble.PlayAnimation(gayaPhrase, releasedBubble => ReleaseSpawnPoint(spawnPoint, releasedBubble));
        return spawnPoint;
    }

    /// <summary>
    /// 使用中でない出現地点からランダムに1つ選ぶ。
    /// 全地点が使用中の場合は <c>_allowOverlapWhenFull</c> に従い、全地点から選ぶか失敗を返す。
    /// </summary>
    /// <param name="spawnPoint">選ばれた出現地点。</param>
    /// <returns>地点を選べた場合 true。</returns>
    private bool TrySelectSpawnPoint(out RectTransform spawnPoint)
    {
        _spawnCandidates.Clear();
        foreach (RectTransform point in _gayaBubbleSpawnPoints)
        {
            if (point != null && !_occupiedSpawnPoints.ContainsKey(point))
            {
                _spawnCandidates.Add(point);
            }
        }

        if (_spawnCandidates.Count == 0 && _allowOverlapWhenFull)
        {
            foreach (RectTransform point in _gayaBubbleSpawnPoints)
            {
                if (point != null)
                {
                    _spawnCandidates.Add(point);
                }
            }
        }

        if (_spawnCandidates.Count == 0)
        {
            spawnPoint = null;
            return false;
        }

        spawnPoint = _spawnCandidates[UnityEngine.Random.Range(0, _spawnCandidates.Count)];
        return true;
    }

    /// <summary>
    /// 吹き出しの破棄時に呼ばれ、その吹き出しが占有していた出現地点を解放する。
    /// 重ねて表示された別の吹き出しが同じ地点を占有している場合は解放しない。
    /// </summary>
    /// <param name="spawnPoint">解放する出現地点。</param>
    /// <param name="bubble">破棄された吹き出し。</param>
    private void ReleaseSpawnPoint(RectTransform spawnPoint, GayaSpeechBubble bubble)
    {
        if (spawnPoint != null
            && _occupiedSpawnPoints.TryGetValue(spawnPoint, out GayaSpeechBubble owner)
            && owner == bubble)
        {
            _occupiedSpawnPoints.Remove(spawnPoint);
        }
    }

    /// <summary>
    /// セリフ1行分のライフサイクル（文字送り → 維持 → 消去 → 待機）を実行する。
    /// </summary>
    private async UniTask PlayLineAsync(DialogueLineData line, DialogueLineData nextLine, int lineIndex, CancellationToken ct)
    {
        bool lineWasPreparedBySlide = _linePreparedBySlide == line;
        if (lineWasPreparedBySlide)
        {
            _linePreparedBySlide = null;
        }

        using (var skipLineCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            _skipLineCancellation = skipLineCancellation;
            _skipRequested = false;
            _currentLineIndex = lineIndex;
            _claimedBy = GetLineClaims(lineIndex);
            _currentLine = line;

            if (!lineWasPreparedBySlide)
            {
                _text.text = line.markedUpText;
                _text.ForceMeshUpdate();
                _text.maxVisibleCharacters = line.charInterval <= 0f ? _text.textInfo.characterCount : 0;
            }
            SetSkipButtonVisible(line.isSkippable);

            bool transitionedToNextLine = false;
            try
            {
                int totalChars = _text.textInfo.characterCount;
                if (!lineWasPreparedBySlide)
                {
                    await TypeCharactersAsync(totalChars, line.charInterval, skipLineCancellation.Token);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(line.postDisplayWait), cancellationToken: skipLineCancellation.Token);

                SetSkipButtonVisible(false);
                if (nextLine != null && IsSlideSceneActive())
                {
                    transitionedToNextLine = await SlideToNextLineAsync(nextLine, skipLineCancellation.Token);
                }

                if (!transitionedToNextLine)
                {
                    _text.maxVisibleCharacters = 0;
                    _text.text = string.Empty;
                    _currentLine = null;
                    await UniTask.Delay(TimeSpan.FromSeconds(line.nextLineWait), cancellationToken: skipLineCancellation.Token);
                }
            }
            catch (OperationCanceledException) when (_skipRequested && !ct.IsCancellationRequested)
            {
                SetSkipButtonVisible(false);
                if (nextLine != null && IsSlideSceneActive())
                {
                    transitionedToNextLine = await SlideToNextLineAsync(nextLine, ct);
                }
            }
            finally
            {
                SetSkipButtonVisible(false);
                if (!transitionedToNextLine && _text != null)
                {
                    _text.maxVisibleCharacters = 0;
                    _text.text = string.Empty;
                }
                if (!transitionedToNextLine)
                {
                    _currentLine = null;
                }
                _skipLineCancellation = null;
            }
        }
    }

    /// <summary>
    /// 指定位置以降で最初に見つかるセリフを返す。ループ先頭への遷移は最終行として扱う。
    /// </summary>
    /// <param name="startIndex">検索開始位置。</param>
    /// <returns>次のセリフ。後続がない場合は null。</returns>
    private DialogueLineData FindNextLine(int startIndex)
    {
        for (int i = startIndex; i < _lines.Count; i++)
        {
            if (_lines[i] != null)
            {
                return _lines[i];
            }
        }
        return null;
    }

    /// <summary>アクティブシーンがスライド遷移対象かを返す。</summary>
    private bool IsSlideSceneActive()
    {
        return SceneManager.GetActiveScene().name == _slideSceneName;
    }

    /// <summary>
    /// 次のセリフを複製UIに準備し、現在のUIと同時に左へスライドする。
    /// </summary>
    /// <param name="nextLine">スライド後に表示するセリフ。</param>
    /// <param name="ct">中断用トークン。</param>
    /// <returns>スライドと参照の引き継ぎが完了した場合 true。</returns>
    private async UniTask<bool> SlideToNextLineAsync(DialogueLineData nextLine, CancellationToken ct)
    {
        if (_gameplayUiRoot == null)
        {
            Debug.LogWarning("[mmm_GayaSystem] Day2 スライド遷移には _gameplayUiRoot の設定が必要です。", this);
            return false;
        }

        RectTransform oldRoot = _gameplayUiRoot;
        Vector2 oldAnchoredPosition = oldRoot.anchoredPosition;
        Transform oldSkipButtonParent = _skipButton != null && _skipButton.transform.IsChildOf(oldRoot)
            ? _skipButton.transform
            : null;
        int[] skipButtonPath = oldSkipButtonParent != null
            ? GetChildIndexPath(oldRoot, oldSkipButtonParent)
            : null;

        RectTransform nextRoot = Instantiate(oldRoot, oldRoot.parent, false);
        nextRoot.transform.SetSiblingIndex(0);
        nextRoot.anchoredPosition = oldRoot.anchoredPosition + new Vector2(_slideOffsetX, 0f);

        TextMeshProUGUI nextText = nextRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nextText == null)
        {
            Debug.LogError("[mmm_GayaSystem] 複製した _gameplayUiRoot 内に TextMeshProUGUI がありません。", nextRoot);
            Destroy(nextRoot.gameObject);
            return false;
        }

        Button nextSkipButton = null;
        if (skipButtonPath != null)
        {
            Transform clonedButtonTransform = GetChildAtPath(nextRoot, skipButtonPath);
            if (clonedButtonTransform == null || !clonedButtonTransform.TryGetComponent(out nextSkipButton))
            {
                Debug.LogError("[mmm_GayaSystem] 複製したUI内のスキップボタンを特定できません。", nextRoot);
                Destroy(nextRoot.gameObject);
                return false;
            }
        }

        nextText.text = nextLine.markedUpText;
        nextText.ForceMeshUpdate();
        nextText.maxVisibleCharacters = nextText.textInfo.characterCount;

        _isTransitioning = true;
        SetSkipButtonVisible(false);
        if (nextSkipButton != null)
        {
            nextSkipButton.gameObject.SetActive(false);
        }

        UniTaskCompletionSource completion = new UniTaskCompletionSource();
        bool transferred = false;
        try
        {
            Sequence sequence = DOTween.Sequence()
                .Join(oldRoot.DOAnchorPosX(oldRoot.anchoredPosition.x - _slideOffsetX, _slideDuration))
                .Join(nextRoot.DOAnchorPosX(nextRoot.anchoredPosition.x - _slideOffsetX, _slideDuration))
                .SetEase(_slideEase)
                .SetLink(gameObject);
            _slideTween = sequence
                .OnComplete(() => completion.TrySetResult())
                .OnKill(() => completion.TrySetCanceled());

            using (ct.Register(() =>
            {
                if (_slideTween != null && _slideTween.IsActive())
                {
                    _slideTween.Kill();
                }
            }))
            {
                await completion.Task;
            }

            ct.ThrowIfCancellationRequested();

            _currentLineIndex++;
            _claimedBy = GetLineClaims(_currentLineIndex);
            _text = nextText;
            _gameplayUiRoot = nextRoot;
            _currentLine = nextLine;
            _linePreparedBySlide = nextLine;

            if (nextSkipButton != null)
            {
                _skipButton.onClick.RemoveListener(OnSkipButtonClicked);
                _skipButton = nextSkipButton;
                _skipButton.onClick.AddListener(OnSkipButtonClicked);
            }

            Destroy(oldRoot.gameObject);
            transferred = true;
            return true;
        }
        finally
        {
            if (_slideTween != null && _slideTween.IsActive())
            {
                _slideTween.Kill();
            }
            _slideTween = null;
            if (!transferred && nextRoot != null)
            {
                Destroy(nextRoot.gameObject);
            }
            if (!transferred && oldRoot != null)
            {
                oldRoot.anchoredPosition = oldAnchoredPosition;
            }
            _isTransitioning = false;
        }
    }

    /// <summary>子孫Transformへの各階層のSiblingIndexを取得する。</summary>
    private int[] GetChildIndexPath(Transform root, Transform child)
    {
        var indices = new List<int>();
        Transform current = child;
        while (current != root && current != null)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }
        if (current != root)
        {
            return null;
        }
        indices.Reverse();
        return indices.ToArray();
    }

    /// <summary>SiblingIndexの配列で指定した子孫Transformを取得する。</summary>
    private Transform GetChildAtPath(Transform root, int[] path)
    {
        Transform current = root;
        foreach (int childIndex in path)
        {
            if (childIndex < 0 || childIndex >= current.childCount)
            {
                return null;
            }
            current = current.GetChild(childIndex);
        }
        return current;
    }

    /// <summary>
    /// DOTween で maxVisibleCharacters を1文字ずつ進め、完了まで待機する。
    /// 文字数が変わるたびにメッシュを再構築し、回答済み単語の色を再適用する。
    /// </summary>
    private async UniTask TypeCharactersAsync(int totalChars, float interval, CancellationToken ct)
    {
        if (totalChars <= 0)
        {
            return;
        }

        if (interval <= 0f)
        {
            print("a");
            ct.ThrowIfCancellationRequested();
            _text.maxVisibleCharacters = totalChars;
            RefreshMeshAndColors();
            return;
        }

        var tcs = new UniTaskCompletionSource();
        int visible = 0;
        Tween tween = DOTween
            .To(() => visible, x =>
            {
                if (x == visible)
                {
                    return;
                }
                visible = x;
                _text.maxVisibleCharacters = x;
                RefreshMeshAndColors();
            }, totalChars, totalChars * Mathf.Max(0f, interval))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() => tcs.TrySetResult())
            .OnKill(() => tcs.TrySetCanceled());

        using (ct.Register(() => tween.Kill()))
        {
            await tcs.Task;
        }

        _text.maxVisibleCharacters = totalChars;
        RefreshMeshAndColors();
    }

    /// <summary>
    /// メッシュを即時再構築した上で回答済み単語の頂点カラーを適用する。
    /// 先にメッシュを確定させることで、後続の Canvas 再構築で色がリセットされるのを防ぐ。
    /// </summary>
    private void RefreshMeshAndColors()
    {
        if (_text == null)
        {
            return;
        }
        _text.ForceMeshUpdate();
        ApplyClaimedColors();
    }

    /// <summary>
    /// TMP がメッシュを再生成した際に呼ばれ、回答済み単語の色を再適用する。
    /// </summary>
    private void OnTextChanged(UnityEngine.Object obj)
    {
        if (obj == _text)
        {
            ApplyClaimedColors();
        }
    }

    /// <summary>
    /// 自分が取得済みなら赤、相手だけが取得済みなら灰色でリンクを描画する。
    /// </summary>
    private void ApplyClaimedColors()
    {
        if (_text == null || _currentLine == null || _claimedBy.Count == 0)
        {
            return;
        }

        TMP_TextInfo textInfo = _text.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
        {
            return;
        }

        int localActorNumber = PhotonNetwork.LocalPlayer != null
            ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        Color32 localColor = _claimedColor;
        Color32 opponentColor = _opponentClaimColor;
        bool isModified = false;

        for (int i = 0; i < textInfo.linkCount; i++)
        {
            TMP_LinkInfo link = textInfo.linkInfo[i];
            if (!_claimedBy.TryGetValue(link.GetLinkID(), out ClaimRecord record))
            {
                continue;
            }
            Color32 color = record.FirstActorNumber == localActorNumber ||
                            record.SecondActorNumber == localActorNumber
                ? localColor : opponentColor;

            int start = link.linkTextfirstCharacterIndex;
            int end = Mathf.Min(start + link.linkTextLength, textInfo.characterCount);
            for (int c = start; c < end; c++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[c];
                if (!charInfo.isVisible)
                {
                    continue;
                }

                Color32[] colors = textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;
                int v = charInfo.vertexIndex;
                if (colors == null || v + 3 >= colors.Length)
                {
                    continue;
                }

                colors[v] = color;
                colors[v + 1] = color;
                colors[v + 2] = color;
                colors[v + 3] = color;
                isModified = true;
            }
        }

        if (isModified)
        {
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    /// <summary>
    /// 現在のテキストから指定 ID のリンク情報を検索する。
    /// </summary>
    /// <returns>見つかった場合 true。</returns>
    private bool TryGetLinkInfo(string targetId, out TMP_LinkInfo result)
    {
        TMP_TextInfo textInfo = _text != null ? _text.textInfo : null;
        if (textInfo != null && _currentLine != null)
        {
            for (int i = 0; i < textInfo.linkCount; i++)
            {
                if (textInfo.linkInfo[i].GetLinkID() == targetId)
                {
                    result = textInfo.linkInfo[i];
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    /// <summary>
    /// リンクの全文字が文字送りで表示済みかを返す。
    /// </summary>
    private bool IsLinkFullyVisible(TMP_LinkInfo link)
    {
        return link.linkTextfirstCharacterIndex + link.linkTextLength <= _text.maxVisibleCharacters;
    }

    /// <summary>
    /// 現在のセリフデータの targets から targetId に対応するガヤ対象を検索する。
    /// </summary>
    /// <param name="targetId">リンク ID。</param>
    /// <param name="result">見つかったガヤ対象。</param>
    /// <returns>正解ガヤワードとして登録されていれば true。</returns>
    private bool TryFindGayaTarget(string targetId, out GayaTargetData result)
    {
        if (_currentLine?.targets != null)
        {
            foreach (GayaTargetData target in _currentLine.targets)
            {
                if (target != null && target.targetId == targetId)
                {
                    result = target;
                    return true;
                }
            }
        }
        result = null;
        return false;
    }

    /// <summary>
    /// おてつき時の吹き出しテキストを、<c>_missBubbleFormat</c> を適用して生成する。
    /// フォーマットが不正な場合は単語をそのまま返す。
    /// </summary>
    /// <param name="linkText">クリックされたリンクの元テキスト。</param>
    /// <returns>吹き出しに表示するテキスト。</returns>
    private string BuildMissPhrase(string linkText)
    {
        if (string.IsNullOrEmpty(_missBubbleFormat))
        {
            return linkText;
        }
        try
        {
            return string.Format(_missBubbleFormat, linkText);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[mmm_GayaSystem] おてつき吹き出しのフォーマットが不正です: {_missBubbleFormat}", this);
            return linkText;
        }
    }

    /// <summary>
    /// 使用するガヤ文章を外部から書き換える関数
    /// </summary>
    /// <param name="newLines"></param>
    public void SetGayaLines(List<DialogueLineData> newLines)
    {
        _lines = newLines;
    }
}
