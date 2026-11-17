using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// ガヤゲームのテキストシステム本体。
/// TMP の文字送り、UniTask によるセリフ進行、回答済み単語の頂点カラー変更、
/// および早押し排他判定（<see cref="TryClaimWord"/>）を担当する。
/// </summary>
public class GayaSystem : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("セリフを表示する TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI _text;

    [Header("セリフ")]
    [Tooltip("再生するセリフデータ一覧（上から順に再生）")]
    [SerializeField] private List<DialogueLineData> _lines = new List<DialogueLineData>();
    [Tooltip("全セリフ再生後に先頭へ戻ってループするか")]
    [SerializeField] private bool _loop = true;
    [Tooltip("Start 時に自動でシーケンスを開始するか")]
    [SerializeField] private bool _playOnStart = true;

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
    [SerializeField] private Color _missColor = new Color32(0x00, 0x7A, 0xFF, 0xFF);

    [Header("おてつき")]
    [Tooltip("非ガヤワードをクリックした際の吹き出しテキストのフォーマット（{0} にクリックした単語が入る）")]
    [SerializeField] private string _missBubbleFormat = "{0}！？";

    /// <summary>
    /// 回答済み単語1つ分の記録（回答したプレイヤーと、正解ガヤだったか）。
    /// </summary>
    private readonly struct ClaimRecord
    {
        public readonly string PlayerId;
        public readonly bool IsGayaWord;

        /// <summary>
        /// 回答記録を生成する。
        /// </summary>
        public ClaimRecord(string playerId, bool isGayaWord)
        {
            PlayerId = playerId;
            IsGayaWord = isGayaWord;
        }
    }

    private readonly object _claimLock = new object();
    private readonly Dictionary<string, ClaimRecord> _claimedBy = new Dictionary<string, ClaimRecord>();
    private DialogueLineData _currentLine;
    private readonly Dictionary<RectTransform, GayaSpeechBubble> _occupiedSpawnPoints = new Dictionary<RectTransform, GayaSpeechBubble>();
    private readonly List<RectTransform> _spawnCandidates = new List<RectTransform>();

    /// <summary>
    /// 単語の確定に成功した際に発火する（targetId, playerId, 吹き出しテキスト, isGayaWord）。
    /// isGayaWord が true なら正解ガヤ（加点）、false なら非ガヤ（おてつき・減点）。
    /// </summary>
    public event Action<string, string, string, bool> OnWordClaimed;

    /// <summary>
    /// 単語の確定に成功した際に、吹き出しの出現地点付きで発火する
    /// （targetId, playerId, 吹き出しテキスト, isGayaWord, 出現地点）。
    /// 吹き出しを生成できなかった場合、出現地点は null になる。
    /// </summary>
    public event Action<string, string, string, bool, RectTransform> OnWordClaimedAt;

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

    private void OnEnable()
    {
        // レイアウト変更などで TMP がメッシュを再生成した場合にも色を維持するための保険
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    private void Start()
    {
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
            Debug.LogWarning("[GayaSystem] Text またはセリフが未設定です。");
            return;
        }

        try
        {
            do
            {
                foreach (DialogueLineData line in _lines)
                {
                    ct.ThrowIfCancellationRequested();
                    if (line != null)
                    {
                        await PlayLineAsync(line, ct);
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
    /// 単語の確定を試みる。最初に呼んだプレイヤーのみ成功する。
    /// 現在のセリフの targets に登録された単語なら正解ガヤ（赤・gayaPhrase の吹き出し）、
    /// 登録されていない単語なら非ガヤ（青・フォーマット適用済みの吹き出し）として扱う。
    /// </summary>
    /// <param name="targetId">対象のリンク ID。</param>
    /// <param name="playerId">確定を試みるプレイヤー ID。</param>
    /// <returns>確定に成功した場合 true。既に取得済み・未表示・存在しない場合は false。</returns>
    public bool TryClaimWord(string targetId, string playerId)
    {
        string phrase;
        bool isGayaWord;
        lock (_claimLock)
        {
            if (!IsTargetInteractable(targetId))
            {
                return false;
            }

            isGayaWord = TryFindGayaTarget(targetId, out GayaTargetData target);
            if (isGayaWord)
            {
                phrase = target.gayaPhrase;
            }
            else
            {
                string linkText = TryGetLinkInfo(targetId, out TMP_LinkInfo link) ? link.GetLinkText() : string.Empty;
                phrase = BuildMissPhrase(linkText);
            }
            _claimedBy[targetId] = new ClaimRecord(playerId, isGayaWord);
        }

        RefreshMeshAndColors();

        if (isGayaWord)
        {
            Debug.Log($"[GayaSystem] {playerId} がガヤ！「{phrase}」 (target: {targetId})");
        }
        else
        {
            Debug.Log($"[GayaSystem] {playerId} がおてつき…「{phrase}」 (target: {targetId})");
        }
        RectTransform spawnPoint = SpawnGayaSpeechBubble(phrase);
        OnWordClaimed?.Invoke(targetId, playerId, phrase, isGayaWord);
        OnWordClaimedAt?.Invoke(targetId, playerId, phrase, isGayaWord, spawnPoint);
        return true;
    }

    /// <summary>
    /// 指定 ID の単語が現在クリック可能か（表示済みかつ未回答か）を返す。
    /// </summary>
    /// <param name="targetId">リンク ID。</param>
    /// <returns>操作可能なら true。</returns>
    public bool IsTargetInteractable(string targetId)
    {
        if (string.IsNullOrEmpty(targetId) || _claimedBy.ContainsKey(targetId))
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
            Debug.LogWarning("[GayaSystem] Gaya speech bubble prefab is not assigned.", this);
            return null;
        }

        if (!TrySelectSpawnPoint(out RectTransform spawnPoint))
        {
            Debug.LogWarning("[GayaSystem] 空いている吹き出しの出現地点がないため、生成をスキップしました。", this);
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
    private async UniTask PlayLineAsync(DialogueLineData line, CancellationToken ct)
    {
        lock (_claimLock)
        {
            _claimedBy.Clear();
        }
        _currentLine = line;

        _text.text = line.markedUpText;
        _text.maxVisibleCharacters = 0;
        _text.ForceMeshUpdate();

        int totalChars = _text.textInfo.characterCount;
        await TypeCharactersAsync(totalChars, line.charInterval, ct);

        await UniTask.Delay(TimeSpan.FromSeconds(line.postDisplayWait), cancellationToken: ct);

        _text.maxVisibleCharacters = 0;
        _text.text = string.Empty;
        _currentLine = null;

        await UniTask.Delay(TimeSpan.FromSeconds(line.nextLineWait), cancellationToken: ct);
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
    /// 回答済み（<c>_claimedBy</c> に含まれる）全リンク範囲の頂点カラーを、
    /// 正解ガヤなら <c>_claimedColor</c>、非ガヤなら <c>_missColor</c> に書き換え、メッシュへ反映する。
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

        Color32 gayaColor = _claimedColor;
        Color32 missColor = _missColor;
        bool isModified = false;

        for (int i = 0; i < textInfo.linkCount; i++)
        {
            TMP_LinkInfo link = textInfo.linkInfo[i];
            if (!_claimedBy.TryGetValue(link.GetLinkID(), out ClaimRecord record))
            {
                continue;
            }
            Color32 color = record.IsGayaWord ? gayaColor : missColor;

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
            Debug.LogWarning($"[GayaSystem] おてつき吹き出しのフォーマットが不正です: {_missBubbleFormat}", this);
            return linkText;
        }
    }
}
