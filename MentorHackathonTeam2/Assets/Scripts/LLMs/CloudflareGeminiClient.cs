using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

[Serializable]
public class WordData
{
    [SerializeField] private string word;
    [SerializeField] private string type;
    [SerializeField] private string gaya;

    public string Word => word;
    public string Type => type;
    public string Gaya => gaya;
}

[Serializable]
public class SentenceData
{
    [SerializeField] private string sentence;
    [SerializeField] private WordData[] words;

    public string Sentence => sentence;
    public WordData[] Words => words;
}

[Serializable]
public class ScriptDataList
{
    [SerializeField] private SentenceData[] items;
    
    public SentenceData[] Items => items;
}

[Serializable]
public class GeminiResponse
{
    [SerializeField] private CandidateData[] candidates;
    
    public CandidateData[] Candidates => candidates;
}

[Serializable]
public class CandidateData
{
    [SerializeField] private ContentData content;
    
    public ContentData Content => content;
}

[Serializable]
public class ContentData
{
    [SerializeField] private PartData[] parts;
    
    public PartData[] Parts => parts;
}

[Serializable]
public class PartData
{
    [SerializeField] private string text;
    
    public string Text => text;
}

public class CloudflareGeminiClient : MonoBehaviour
{
    [SerializeField] 
    private string proxyUrl = "https://gaya-gen.hsmtyuusei.workers.dev/";

    private async void Start()
    {
        var scriptData = await GenerateGayaAsync();
        
        if (scriptData != null && scriptData.Items != null)
        {
            // JSONデータから ScriptableObject のリストに変換
            List<DialogueLineData> generatedLines = ConvertToDialogueLines(scriptData);
            
            // 全体をパースして Debug.Log に流す
            string resultDump = DumpDialogueLines(generatedLines);
            Debug.Log(resultDump);
        }
        
        Debug.Log($"生成完了時間: {Time.realtimeSinceStartup}");
    }

    /// <summary>
    /// APIレスポンスから DialogueLineData のリストを生成し、マークアップとID割り当てを行う
    /// </summary>
    private List<DialogueLineData> ConvertToDialogueLines(ScriptDataList scriptData)
    {
        var resultList = new List<DialogueLineData>();
        int correctCount = 0; // "correct"の通し番号（00, 01, 02...）

        foreach (var sentence in scriptData.Items)
        {
            // ScriptableObjectのインスタンスを動的に生成
            var lineData = ScriptableObject.CreateInstance<DialogueLineData>();
            lineData.targets = new List<GayaTargetData>();
            
            var markupBuilder = new StringBuilder();

            foreach (var word in sentence.Words)
            {
                string targetId;
                
                if (word.Type == "correct")
                {
                    targetId = correctCount.ToString("D2"); // "00", "01" の形式
                    correctCount++;
                }
                else
                {
                    targetId = "m"; // miss の場合
                }

                // マークアップテキストの構築 (<link="00">単語</link>)
                markupBuilder.Append($"<link=\"{targetId}\">{word.Word}</link>");

                // ターゲットデータの登録
                lineData.targets.Add(new GayaTargetData
                {
                    targetId = targetId,
                    keyword = word.Word,
                    gayaPhrase = word.Gaya
                });
            }

            lineData.markedUpText = markupBuilder.ToString();
            resultList.Add(lineData);
        }

        return resultList;
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
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await request.SendWebRequest();

                var responseData = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                
                if (responseData != null && responseData.Candidates != null && responseData.Candidates.Length > 0)
                {
                    string rawJsonText = responseData.Candidates[0].Content.Parts[0].Text;
                    
                    rawJsonText = rawJsonText.Replace("```json", "").Replace("```", "").Trim();
                    string wrappedJson = $"{{\"items\":{rawJsonText}}}";
                    
                    return JsonUtility.FromJson<ScriptDataList>(wrappedJson);
                }
                
                return null;
            }
            catch (UnityWebRequestException e)
            {
                Debug.LogError($"Error: {e.Message}");
                return null;
            }
        }
    }
}