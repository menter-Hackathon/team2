using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

[System.Serializable]
public class GeminiRequest
{
    public ContentData[] contents;
}

[System.Serializable]
public class ContentData
{
    public PartData[] parts;
}

[System.Serializable]
public class PartData
{
    public string text;
}

[System.Serializable]
public class GeminiResponse
{
    public CandidateData[] candidates;
}

[System.Serializable]
public class CandidateData
{
    public ContentData content;
}

public class GeminiTest : MonoBehaviour
{
    [SerializeField] private string apiKey = "";
    [SerializeField] private string modelName = "gemini-1.5-flash";
    [SerializeField] private int maxRetryCount = 3;
    [SerializeField] private float baseRetryDelaySeconds = 2.0f;

    private async void Start()
    {
        string prompt = "「AIツール」に対する驚きのガヤを短いテキストで生成してください。";
        string response = await GenerateTextAsync(prompt);
        Debug.Log(response);
    }

    public async UniTask<string> GenerateTextAsync(string prompt)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        var requestData = new GeminiRequest
        {
            contents = new[]
            {
                new ContentData
                {
                    parts = new[] { new PartData { text = prompt } }
                }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestData);

        for (int i = 0; i <= maxRetryCount; i++)
        {
            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await request.SendWebRequest();

                    var responseData = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    if (responseData != null && responseData.candidates != null && responseData.candidates.Length > 0)
                    {
                        return responseData.candidates[0].content.parts[0].text;
                    }
                    
                    return string.Empty;
                }
                catch (UnityWebRequestException e)
                {
                    if (e.ResponseCode == 503)
                    {
                        if (i == maxRetryCount)
                        {
                            Debug.LogError($"[Gemini API] 503 Retry failed after {maxRetryCount} attempts.");
                            return string.Empty;
                        }

                        float waitTime = baseRetryDelaySeconds * (i + 1);
                        Debug.LogWarning($"[Gemini API] 503 Service Unavailable. Retrying in {waitTime} seconds... ({i + 1}/{maxRetryCount})");
                        
                        await UniTask.Delay(TimeSpan.FromSeconds(waitTime));
                    }
                    else
                    {
                        Debug.LogError($"[Gemini API] Error: {e.Message}");
                        return string.Empty;
                    }
                }
            }
        }

        return string.Empty;
    }
}