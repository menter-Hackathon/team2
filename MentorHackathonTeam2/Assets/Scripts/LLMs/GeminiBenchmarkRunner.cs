using System.Diagnostics;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GeminiBenchmarkRunner : MonoBehaviour
{
    [SerializeField] private GeminiDirectTest directClient;
    [SerializeField] private CloudflareGeminiClient proxyClient;
    [SerializeField] private int testCount = 5;
    [SerializeField] private string targetWord = "AIツール";

    private async void Start()
    {
        await RunBenchmarkAsync();
    }

    private async UniTask RunBenchmarkAsync()
    {
        UnityEngine.Debug.Log($"--- ベンチマーク開始 (各 {testCount} 回) ---");

        string prompt = $"「{targetWord}」に対する驚きのガヤを短いテキストで生成してください。";

        long directTotalTime = 0;
        for (int i = 0; i < testCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await directClient.GenerateTextAsync(prompt);
            sw.Stop();
            
            directTotalTime += sw.ElapsedMilliseconds;
            UnityEngine.Debug.Log($"[Direct API] {i + 1}回目: {sw.ElapsedMilliseconds} ms");
            
            await UniTask.Delay(500);
        }
        
        double directAverage = (double)directTotalTime / testCount;

        long proxyTotalTime = 0;
        for (int i = 0; i < testCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await proxyClient.GenerateGayaAsync(targetWord);
            sw.Stop();
            
            proxyTotalTime += sw.ElapsedMilliseconds;
            UnityEngine.Debug.Log($"[Proxy API] {i + 1}回目: {sw.ElapsedMilliseconds} ms");
            
            await UniTask.Delay(500);
        }
        
        double proxyAverage = (double)proxyTotalTime / testCount;

        UnityEngine.Debug.Log("--- ベンチマーク結果 ---");
        UnityEngine.Debug.Log($"Direct API 平均: {directAverage:F2} ms");
        UnityEngine.Debug.Log($"Proxy API 平均: {proxyAverage:F2} ms");
    }
}