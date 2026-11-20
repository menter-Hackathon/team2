using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GayaVoiceBridge : MonoBehaviour
{
    private const int MinOneCharacterMatchLength = 4;

    [System.Serializable]
    public class VoiceRule
    {
        [Tooltip("メモ用の名前（例：朝の挨拶など）")]
        public string label;

        [Tooltip("発動させる GayaSystem 側のリンクID（例：00, 01, 02...）")]
        public string targetId;

        [Tooltip("この単語のいずれかを読み取ったら発動（部分一致・複数指定OK）")]
        public List<string> triggerWords = new List<string>();

        [Tooltip("漢字・かな・活用など、同じ発話として扱う認識結果の表記ゆれ")]
        public List<string> recognitionAliases = new List<string>();
    }

    [Header("参照")]
    [SerializeField] private RunWhisper _runWhisper;
    [SerializeField] private GayaSystem _gayaSystem;

    [Header("プレイヤー設定")]
    [SerializeField] private string _voicePlayerId = "Player1";

    [Header("音声 → ガヤ対応表")]
    [SerializeField] private List<VoiceRule> _voiceRules = new List<VoiceRule>();

    private void Start()
    {
        if (_runWhisper == null || _gayaSystem == null)
        {
            Debug.LogError($"[GayaVoiceBridge] 参照エラー！ RunWhisper: {_runWhisper}, GayaSystem: {_gayaSystem}");
            return;
        }
        Debug.Log($"[GayaVoiceBridge] 接続OK! 登録ルール数: {_voiceRules.Count}");
    }

    private void OnEnable()
    {
        if (_runWhisper != null)
            _runWhisper.OnSpeechRecognized += HandleSpeechRecognized;
    }

    private void OnDisable()
    {
        if (_runWhisper != null)
            _runWhisper.OnSpeechRecognized -= HandleSpeechRecognized;
    }

    private void HandleSpeechRecognized(string spokenText, float volume)
    {
        if (_gayaSystem == null || string.IsNullOrWhiteSpace(spokenText))
            return;

        if (_gayaSystem.IsTransitioning)
        {
            Debug.Log($"[GayaVoiceBridge] 遷移中のため判定しません: 「{spokenText}」");
            return;
        }

        string cleanSpoken = NormalizeForMatch(spokenText);
        if (string.IsNullOrEmpty(cleanSpoken))
            return;

        VoiceRule unavailableRule = null;
        // 完全一致を優先し、見つからない場合だけ1文字の欠け・余分を調べる。
        for (int pass = 0; pass < 2; pass++)
        {
            bool allowOneCharacterDifference = pass == 1;
            VoiceRule bestRule = null;
            string bestWord = null;
            int bestIndex = int.MaxValue;
            int bestScore = -1;
            bool ambiguous = false;
            string ambiguousTargetId = null;

            foreach (VoiceRule rule in _voiceRules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.targetId))
                    continue;

                string triggerWord;
                int triggerIndex;
                int triggerScore = allowOneCharacterDifference
                    ? FindOneCharacterMatchScore(cleanSpoken, rule.triggerWords, out triggerWord, out triggerIndex)
                    : FindMatchScore(cleanSpoken, rule.triggerWords, out triggerWord, out triggerIndex);
                string aliasWord;
                int aliasIndex;
                int aliasScore = allowOneCharacterDifference
                    ? FindOneCharacterMatchScore(cleanSpoken, rule.recognitionAliases, out aliasWord, out aliasIndex)
                    : FindMatchScore(cleanSpoken, rule.recognitionAliases, out aliasWord, out aliasIndex);
                bool useAlias = aliasScore >= 0 && (triggerScore < 0 || aliasIndex < triggerIndex ||
                                                      (aliasIndex == triggerIndex && aliasScore > triggerScore));
                int score = useAlias ? aliasScore : triggerScore;
                int matchIndex = useAlias ? aliasIndex : triggerIndex;
                string matchedWord = useAlias ? aliasWord : triggerWord;
                if (score < 0) continue;

                if (!_gayaSystem.IsTargetInteractable(rule.targetId))
                {
                    unavailableRule = rule;
                    continue;
                }

                if (matchIndex < bestIndex || (matchIndex == bestIndex && score > bestScore))
                {
                    bestRule = rule;
                    bestWord = matchedWord;
                    bestIndex = matchIndex;
                    bestScore = score;
                    ambiguous = false;
                    ambiguousTargetId = null;
                }
                else if (allowOneCharacterDifference && matchIndex == bestIndex && score == bestScore &&
                         bestRule.targetId != rule.targetId)
                {
                    ambiguous = true;
                    ambiguousTargetId = rule.targetId;
                }
            }

            if (bestRule == null)
                continue;

            if (ambiguous)
            {
                Debug.Log($"[GayaVoiceBridge] 1文字ゆれの候補 ID[{bestRule.targetId}] と ID[{ambiguousTargetId}] が同順位のため判定しません: 「{spokenText}」");
                return;
            }

            bool claimed = _gayaSystem.TryClaimWord(bestRule.targetId, _voicePlayerId);
            string matchType = allowOneCharacterDifference ? "1文字ゆれ一致" : "ルール一致";
            Debug.Log($"[GayaVoiceBridge] {(claimed ? matchType : "確定できませんでした")}({bestRule.label}): 「{spokenText}」 / 候補「{bestWord}」 -> ID[{bestRule.targetId}] (音量: {volume:F3})");
            return;
        }

        string reason = unavailableRule == null
            ? "対応表に一致する語がありません"
            : $"一致候補 ID[{unavailableRule.targetId}] は表示前・遷移中・確定済みです";
        Debug.Log($"[GayaVoiceBridge] {reason}: 「{spokenText}」");
    }

    private int FindMatchScore(string cleanSpoken, List<string> words, out string matchedWord, out int bestIndex)
    {
        matchedWord = null;
        bestIndex = int.MaxValue;
        int bestScore = -1;
        if (words == null) return bestScore;

        foreach (string word in words)
        {
            string cleanWord = NormalizeForMatch(word);
            if (cleanWord.Length == 0) continue;

            // 一文字の別名は、無関係な文中の漢字に反応しないよう完全一致だけにする。
            int matchIndex = cleanWord.Length == 1
                ? (cleanSpoken == cleanWord ? 0 : -1)
                : cleanSpoken.IndexOf(cleanWord, System.StringComparison.Ordinal);
            if (matchIndex >= 0 && (matchIndex < bestIndex ||
                                    (matchIndex == bestIndex && cleanWord.Length > bestScore)))
            {
                bestIndex = matchIndex;
                bestScore = cleanWord.Length;
                matchedWord = word;
            }
        }
        return bestScore;
    }

    private int FindOneCharacterMatchScore(string cleanSpoken, List<string> words, out string matchedWord, out int bestIndex)
    {
        matchedWord = null;
        bestIndex = int.MaxValue;
        int bestScore = -1;
        if (words == null) return bestScore;

        foreach (string word in words)
        {
            string cleanWord = NormalizeForMatch(word);
            if (cleanWord.Length < MinOneCharacterMatchLength) continue;

            // 登録語より1文字短い／長い区間だけを候補にする。
            for (int lengthDifference = -1; lengthDifference <= 1; lengthDifference += 2)
            {
                int windowLength = cleanWord.Length + lengthDifference;
                for (int start = 0; start + windowLength <= cleanSpoken.Length; start++)
                {
                    if ((start < bestIndex || (start == bestIndex && cleanWord.Length > bestScore)) &&
                        HasOneInsertionOrDeletion(cleanWord, cleanSpoken, start, windowLength))
                    {
                        bestIndex = start;
                        bestScore = cleanWord.Length;
                        matchedWord = word;
                    }
                }
            }
        }
        return bestScore;
    }

    private static bool HasOneInsertionOrDeletion(string word, string spoken, int start, int windowLength)
    {
        int wordIndex = 0;
        int spokenIndex = start;
        int end = start + windowLength;
        bool skipped = false;

        while (wordIndex < word.Length && spokenIndex < end)
        {
            if (word[wordIndex] == spoken[spokenIndex])
            {
                wordIndex++;
                spokenIndex++;
                continue;
            }
            if (skipped) return false;

            skipped = true;
            if (word.Length > windowLength)
                wordIndex++;
            else
                spokenIndex++;
        }
        return true;
    }

    /// <summary>
    /// 全角・半角、空白、記号、カタカナのゆれを吸収して比較用に整える。
    /// 漢字の読みや活用は recognitionAliases で補う。
    /// </summary>
    private string NormalizeForMatch(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        string s = input.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || c == 'ー' || c == '〜')
                continue;

            if (c >= 'ァ' && c <= 'ヶ')
                sb.Append((char)(c - 0x60));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
