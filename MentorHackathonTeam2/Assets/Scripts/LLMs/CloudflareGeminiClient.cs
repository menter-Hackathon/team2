using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

[System.Serializable]
public class GameRequest
{
    public string word;
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

public class CloudflareGeminiClient : MonoBehaviour
{
    [SerializeField] 
    private string proxyUrl = "https://gaya-gen.hsmtyuusei.workers.dev/";

    private async void Start()
    {
        string target = "AIツール";
        string response = await GenerateGayaAsync(target);
        Debug.Log(response + Time.realtimeSinceStartup);
    }

    public async UniTask<string> GenerateGayaAsync(string targetWord)
    {
        var requestData = new GameRequest { word = targetWord };
        string jsonBody = JsonUtility.ToJson(requestData);

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
                Debug.Log(request.downloadHandler.text);
                
                if (responseData != null && responseData.candidates != null && responseData.candidates.Length > 0)
                {
                    return responseData.candidates[0].content.parts[0].text;
                }
                
                return string.Empty;
            }
            catch (UnityWebRequestException e)
            {
                Debug.LogError($"Error: {e.Message}");
                return string.Empty;
            }
        }
    }
}