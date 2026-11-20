using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GayaVoiceBridge : MonoBehaviour
{
    [System.Serializable]
    public class VoiceRule
    {
        [Tooltip("メモ用の名前（例：朝の挨拶など）")]
        public string label;

        [Tooltip("発動させる GayaSystem 側のリンクID（例：00, 01, 02...）")]
        public string targetId;

        [Tooltip("この単語のいずれかを読み取ったら発動（部分一致・複数指定OK）")]
        public List<string> triggerWords = new List<string>();
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
        if (_gayaSystem == null || _gayaSystem.IsTransitioning || string.IsNullOrWhiteSpace(spokenText))
            return;

        string cleanSpoken = NormalizeForMatch(spokenText);
        if (string.IsNullOrEmpty(cleanSpoken))
            return;

        // 対応表を上から順番にチェック
        foreach (VoiceRule rule in _voiceRules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.targetId))
                continue;

            // 現在画面に表示されていて、かつ未回答のターゲットIDでなければスキップ
            if (!_gayaSystem.IsTargetInteractable(rule.targetId))
                continue;

            // 登録されたトリガー単語のいずれかに部分一致するかチェック
            foreach (string word in rule.triggerWords)
            {
                string cleanTrigger = NormalizeForMatch(word);
                if (!string.IsNullOrEmpty(cleanTrigger) && cleanSpoken.Contains(cleanTrigger))
                {
                    Debug.Log($"[GayaVoiceBridge] ルール一致({rule.label}): 「{spokenText}」 -> ID[{rule.targetId}] (音量: {volume:F3})");
                    _gayaSystem.TryClaimWord(rule.targetId, _voicePlayerId);
                    return; // 1回の発話につき1つ確定
                }
            }
        }
    }

    /// <summary>
    /// 記号・伸ばし棒・カタカナのゆれを吸収して比較用に整える
    /// </summary>
    private string NormalizeForMatch(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        string s = input.ToLowerInvariant();
        char[] removeChars = { ' ', ' ', '、', '。',  '？', '!', '?', '「', '」', 'ー', '～', '~' };
        foreach (char c in removeChars)
        {
            s = s.Replace(c.ToString(), "");
        }

        // カタカナをひらがなに統一
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c >= 'ァ' && c <= 'ヶ')
                sb.Append((char)(c - 0x60));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}