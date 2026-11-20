using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;

using Photon.Pun;
using ExitGames.Client.Photon;

public class mmm_CloudflareGeminiClient : MonoBehaviourPunCallbacks
{
    private const string ScriptPropertyKey = "mmm_gaya_script_v1";
    private const string FallbackPayload = "#fallback";

    [SerializeField] private mmm_GayaSystem lineTarget;
    [SerializeField] private Button startButton;
    [SerializeField] private mmm_GayaScoreManager scoreManager;
    
    [SerializeField] 
    private string proxyUrl = "https://gaya-gen.hsmtyuusei.workers.dev/";

    public bool IsContentReady { get; private set; }

    private void Awake()
    {
        if (startButton == null || lineTarget == null || scoreManager == null)
        {
            Debug.LogError("[mmm_CloudflareGeminiClient] Start Button、mmm_GayaSystem、mmm_GayaScoreManager の参照を設定してください。", this);
            return;
        }

        startButton.interactable = false;
    }

    private void Start()
    {
        if (IsContentReady) return;
        if (startButton != null)
        {
            startButton.interactable = false;
        }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogError("[mmm_CloudflareGeminiClient] ルームに参加していません。", this);
            return;
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ScriptPropertyKey, out object value)
            && value is string payload)
        {
            ApplyPayload(payload);
            return;
        }
        if (PhotonNetwork.IsMasterClient)
        {
            GenerateOnMasterAsync().Forget();
        }
    }

    private async UniTaskVoid GenerateOnMasterAsync()
    {
        string payload = FallbackPayload;
        try
        {
            ScriptDataList scriptData = await GenerateGayaAsync();
            if (this == null) return;
            if (TryValidateScriptData(scriptData, out _, out string reason))
            {
                string serialized = JsonUtility.ToJson(scriptData);
                ScriptDataList roundTrip = JsonUtility.FromJson<ScriptDataList>(serialized);
                if (TryValidateScriptData(roundTrip, out _, out reason))
                {
                    payload = serialized;
                }
                else
                {
                    Debug.LogWarning($"[mmm_CloudflareGeminiClient] 同期用の変換に失敗しました: {reason}", this);
                }
            }
            else
            {
                Debug.LogWarning($"[mmm_CloudflareGeminiClient] 固定セリフを使用します: {reason}", this);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            Debug.LogError($"[mmm_CloudflareGeminiClient] 固定セリフを使用します: {error}", this);
        }

        if (this == null || PhotonNetwork.CurrentRoom == null) return;
        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ScriptPropertyKey, payload } }))
        {
            Debug.LogError("[mmm_CloudflareGeminiClient] 台本の同期を開始できませんでした。", this);
            return;
        }
        ApplyPayload(payload);
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.TryGetValue(ScriptPropertyKey, out object value)
            && value is string payload)
        {
            ApplyPayload(payload);
        }
    }

    private void ApplyPayload(string payload)
    {
        if (IsContentReady || lineTarget == null || scoreManager == null || startButton == null)
        {
            return;
        }
        if (payload != FallbackPayload)
        {
            ScriptDataList scriptData;
            try
            {
                scriptData = JsonUtility.FromJson<ScriptDataList>(payload);
            }
            catch (Exception error)
            {
                Debug.LogError($"[mmm_CloudflareGeminiClient] 同期データを読めません: {error}", this);
                return;
            }
            if (!TryValidateScriptData(scriptData, out int correctCount, out string reason))
            {
                Debug.LogError($"[mmm_CloudflareGeminiClient] 同期データが不正です: {reason}", this);
                return;
            }
            List<DialogueLineData> lines = ConvertToDialogueLines(scriptData);
            scoreManager.SetMaxScoreFromCorrectCount(correctCount);
            lineTarget.SetGayaLines(lines);
            Debug.Log(DumpDialogueLines(lines), this);
        }

        IsContentReady = true;
        startButton.interactable = true;
    }

    private static bool TryValidateScriptData(ScriptDataList scriptData, out int correctCount, out string reason)
    {
        correctCount = 0;
        reason = null;
        if (scriptData?.Items == null || scriptData.Items.Length == 0)
        {
            reason = "文章がありません。";
            return false;
        }

        foreach (var sentence in scriptData.Items)
        {
            if (sentence == null || sentence.Words == null || sentence.Words.Length == 0)
            {
                reason = "単語配列がありません。";
                return false;
            }

            var reconstructed = new StringBuilder();
            foreach (var word in sentence.Words)
            {
                if (word == null || string.IsNullOrEmpty(word.Word) ||
                    (word.Type != "correct" && word.Type != "miss" && word.Type != "plain") ||
                    word.Word.Contains("<") || word.Word.Contains(">"))
                {
                    reason = "単語または type が不正です。";
                    return false;
                }
                if (word.Type == "correct")
                {
                    if (string.IsNullOrWhiteSpace(word.Gaya))
                    {
                        reason = "正解単語の gaya がありません。";
                        return false;
                    }
                    correctCount++;
                }
                reconstructed.Append(word.Word);
            }

            if (reconstructed.ToString() != sentence.Sentence)
            {
                reason = "sentence と words の連結結果が一致しません。";
                return false;
            }
        }

        if (correctCount == 0)
        {
            reason = "正解単語がありません。";
            return false;
        }
        return true;
    }

    /// <summary>
    /// APIレスポンスから DialogueLineData のリストを生成し、マークアップとID割り当てを行う
    /// </summary>
    private List<DialogueLineData> ConvertToDialogueLines(ScriptDataList scriptData)
    {
        var resultList = new List<DialogueLineData>();
        int correctCount = 0; // "correct"の通し番号（00, 01, 02...）
        int missCount = 0;

        foreach (var sentence in scriptData.Items)
        {
            // ScriptableObjectのインスタンスを動的に生成
            var lineData = ScriptableObject.CreateInstance<DialogueLineData>();
            lineData.targets = new List<GayaTargetData>();
            
            var markupBuilder = new StringBuilder();

            foreach (var word in sentence.Words)
            {
                if (word.Type == "plain" && IsPlainFragment(word.Word))
                {
                    markupBuilder.Append(word.Word);
                    continue;
                }

                string targetId;
                
                if (word.Type == "correct")
                {
                    targetId = correctCount.ToString("D2"); // "00", "01" の形式
                    correctCount++;
                }
                else
                {
                    targetId = $"m{missCount++:D2}";
                }

                // マークアップテキストの構築 (<link="00">単語</link>)
                markupBuilder.Append($"<link=\"{targetId}\">{word.Word}</link>");

                // mmm_GayaSystem は targets に存在する ID を正解と判定する
                if (word.Type == "correct")
                {
                    lineData.targets.Add(new GayaTargetData
                    {
                        targetId = targetId,
                        keyword = word.Word,
                        gayaPhrase = word.Gaya
                    });
                }
            }

            lineData.markedUpText = markupBuilder.ToString();
            resultList.Add(lineData);
        }

        return resultList;
    }

    // plain の指定が広すぎても、意味のあるまとまりをリンクから外さない。
    private static bool IsPlainFragment(string word)
    {
        string core = word.Trim().Trim('、', '。', '！', '!', '？', '?');
        switch (core)
        {
            case "":
            case "が": case "は": case "を": case "に": case "で": case "と":
            case "の": case "へ": case "も": case "や": case "か":
            case "ね": case "よ": case "から": case "まで": case "より":
            case "です": case "ます": case "でした": case "ました":
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 生成結果確認用の文字列ダンプ
    /// </summary>
    private string DumpDialogueLines(List<DialogueLineData> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 生成された台本データ ===");

        for (int i = 0; i < lines.Count; i++)
        {
            sb.AppendLine($"\n【Line {i}】");
            sb.AppendLine($"Text: {lines[i].markedUpText}");
            
            foreach (var target in lines[i].targets)
            {
                sb.AppendLine($"  - ID: [{target.targetId}] | Keyword: '{target.keyword}' | Gaya: '{target.gayaPhrase}'");
            }
        }

        sb.AppendLine("\n===========================");
        return sb.ToString();
    }

    public async UniTask<ScriptDataList> GenerateGayaAsync()
    {
        string jsonBody = "{}";

        using (var request = new UnityWebRequest(proxyUrl, "POST"))
        {
            request.timeout = 30;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await request.SendWebRequest().WithCancellation(destroyCancellationToken);

                var responseData = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                
                if (responseData?.Candidates != null && responseData.Candidates.Length > 0 &&
                    responseData.Candidates[0]?.Content?.Parts != null &&
                    responseData.Candidates[0].Content.Parts.Length > 0 &&
                    !string.IsNullOrWhiteSpace(responseData.Candidates[0].Content.Parts[0]?.Text))
                {
                    string rawJsonText = responseData.Candidates[0].Content.Parts[0].Text;
                    
                    rawJsonText = rawJsonText.Replace("```json", "").Replace("```", "").Trim();
                    string wrappedJson = $"{{\"items\":{rawJsonText}}}";
                    
                    return JsonUtility.FromJson<ScriptDataList>(wrappedJson);
                }
                
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[mmm_CloudflareGeminiClient] 生成に失敗しました: {e.Message}", this);
                return null;
            }
        }
    }
}
