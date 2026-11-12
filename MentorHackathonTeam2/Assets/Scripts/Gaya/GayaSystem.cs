using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// ガヤゲームのテキストシステム本体。
/// TMP の文字送り、UniTask によるセリフ進行、
/// および早押し排他判定（<see cref="TryClaimWord"/>）を担当する。
/// </summary>
public class GayaSystem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private List<DialogueLineData> _lines = new List<DialogueLineData>();
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private GayaSpeechBubble _gayaSpeechBubblePrefab;
    [SerializeField] private RectTransform _gayaBubbleParent;
    [SerializeField] private Color _claimedColor = new Color32(0xFF, 0x3B, 0x30, 0xFF);

    private readonly object _claimLock = new object();
    private readonly Dictionary<string, string> _claimedBy = new Dictionary<string, string>();
    private DialogueLineData _currentLine;
    private string _hoveredId;

    /// <summary>単語の確定に成功した際に発火する（targetId, playerId, gayaPhrase）。</summary>
    public event Action<string, string, string> OnWordClaimed;

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
    /// ホバー中の targetId を設定する。表示前・回答済みの単語は無視される。
    /// </summary>
    /// <param name="targetId">ホバー中の ID。何もなければ null。</param>
    public void SetHoveredTarget(string targetId)
    {
        string next = IsTargetInteractable(targetId) ? targetId : null;
        if (next == _hoveredId)
        {
            return;
        }
        _hoveredId = next;
    }

    /// <summary>
    /// 単語の確定を試みる。最初に呼んだプレイヤーのみ成功する。
    /// </summary>
    /// <param name="targetId">対象のリンク ID。</param>
    /// <param name="playerId">確定を試みるプレイヤー ID。</param>
    /// <returns>確定に成功した場合 true。既に取得済み・未表示・存在しない場合は false。</returns>
    public bool TryClaimWord(string targetId, string playerId)
    {
        string phrase;
        lock (_claimLock)
        {
            if (!IsTargetInteractable(targetId))
            {
                return false;
            }
            _claimedBy[targetId] = playerId;
            phrase = FindGayaPhrase(targetId);
        }

        if (_hoveredId == targetId)
        {
            _hoveredId = null;
        }

        Debug.Log($"[GayaSystem] {playerId} がガヤ！「{phrase}」 (target: {targetId})");
        SpawnGayaSpeechBubble(phrase);
        OnWordClaimed?.Invoke(targetId, playerId, phrase);
        return true;
    }


    private void SpawnGayaSpeechBubble(string gayaPhrase)
    {
        if (_gayaSpeechBubblePrefab == null)
        {
            Debug.LogWarning("[GayaSystem] Gaya speech bubble prefab is not assigned.", this);
            return;
        }

        GayaSpeechBubble bubble = Instantiate(_gayaSpeechBubblePrefab, _gayaBubbleParent, false);
        bubble.PlayAnimation(gayaPhrase);
    }

    /// <summary>
    /// 指定 ID の単語が現在ホバー・クリック可能か（表示済みかつ未回答か）を返す。
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

    private async UniTask PlayLineAsync(DialogueLineData line, CancellationToken ct)
    {
        lock (_claimLock)
        {
            _claimedBy.Clear();
        }
        _hoveredId = null;
        _currentLine = line;

        _text.text = line.markedUpText;
        _text.maxVisibleCharacters = 0;
        _text.ForceMeshUpdate();

        int totalChars = _text.textInfo.characterCount;
        await TypeCharactersAsync(totalChars, line.charInterval, ct);

        await UniTask.Delay(TimeSpan.FromSeconds(line.postDisplayWait), cancellationToken: ct);

        _hoveredId = null;
        _text.maxVisibleCharacters = 0;
        _text.text = string.Empty;
        _currentLine = null;

        await UniTask.Delay(TimeSpan.FromSeconds(line.nextLineWait), cancellationToken: ct);
    }

    /// <summary>
    /// DOTween で maxVisibleCharacters を1文字ずつ進め、完了まで待機する。
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
                visible = x;
                _text.maxVisibleCharacters = x;
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
    }

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

    private bool IsLinkFullyVisible(TMP_LinkInfo link)
    {
        return link.linkTextfirstCharacterIndex + link.linkTextLength <= _text.maxVisibleCharacters;
    }

    private string FindGayaPhrase(string targetId)
    {
        if (_currentLine?.targets != null)
        {
            foreach (GayaTargetData target in _currentLine.targets)
            {
                if (target != null && target.targetId == targetId)
                {
                    return target.gayaPhrase;
                }
            }
        }
        return string.Empty;
    }
}
