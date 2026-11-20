using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using System.Text;
using Unity.Collections;
using Newtonsoft.Json;

public class RunWhisper : MonoBehaviour
{
    Worker decoder1, decoder2, encoder, spectrogram;
    Worker argmax;

    // This is how many tokens you want. It can be adjusted.
    const int maxTokens = 20;

    // Special tokens see added tokens file for details
    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int GERMAN = 50261;
    const int FRENCH = 50265;
    const int JAPANESE = 50266;
    const int TRANSCRIBE = 50359; //for speech-to-text in specified language
    const int TRANSLATE = 50358;  //for speech-to-text then translate to English
    const int NO_TIME_STAMPS = 50363;
    const int START_TIME = 50364;

    string[] tokens;

    // Used for special character decoding
    int[] whiteSpaceCharacters = new int[256];

    // Maximum size of audioClip (30s at 16kHz)
    const int maxSamples = 30 * 16000;

    public float latestVolume = 0f; // しゃべり終わったときの平均音量

    [Header("マイクUI設定")]
    public GameObject micOn; 
    public GameObject micMute;

    [Header("マイク＆キーワード設定")]
    public float voiceThreshold = 0.04f;  // 話し始めとみなす音量
    public float minSpeechVolume = 0.008f; //音量のしきい値
    public float silenceDuration = 0.7f; // 話し終わりとみなす無音秒数
    public string[] targetKeywords = new string[] { "おはよう", "オハヨウ" }; //キーワード

    const int sampleRate = 16000; //Whisperモデルのサンプリングレート

    AudioClip micClip;
    string micDevice;
    bool isSpeaking = false;
    bool isInferencing = false;
    int speechStartPos = 0;
    float silenceTimer = 0f;

    public ModelAsset audioDecoder1, audioDecoder2;
    public ModelAsset audioEncoder;
    public ModelAsset logMelSpectro;

    public event System.Action<string, float> OnSpeechRecognized;

    void Start()
    {
        if (micOn != null) micOn.SetActive(false);
        if (micMute != null) micMute.SetActive(false);

        SetupWhiteSpaceShifts();
        GetTokens();

        decoder1 = new Worker(ModelLoader.Load(audioDecoder1), BackendType.GPUCompute);
        decoder2 = new Worker(ModelLoader.Load(audioDecoder2), BackendType.GPUCompute);

        FunctionalGraph graph = new FunctionalGraph();
        var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
        var amax = Functional.ArgMax(input, -1, false);
        var selectTokenModel = graph.Compile(amax);
        argmax = new Worker(selectTokenModel, BackendType.GPUCompute);

        encoder = new Worker(ModelLoader.Load(audioEncoder), BackendType.GPUCompute);
        spectrogram = new Worker(ModelLoader.Load(logMelSpectro), BackendType.GPUCompute);

        // マイクを16kHz・30秒のリングバッファで起動
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 30, sampleRate);
            SetMicUI(false);
            Debug.Log($"マイク待機中 ({micDevice})：話しかけてください");
        }
        else
        {
            Debug.LogError("マイクが見つかりません！");
        }
    }

    void Update()
    {
        // OSの既定マイクが変わった場合
        if (Microphone.devices.Length > 0 && Microphone.devices[0] != micDevice)
        {
            if (micDevice != null) Microphone.End(micDevice);
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 30, sampleRate);
            isSpeaking = false;
            SetMicUI(false);
            Debug.Log($"<color=yellow>マイクを切り替えました: {micDevice}</color>");
            return;
        }

        if (micClip == null || isInferencing) return;

        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos < 0) return;

        float currentVolume = GetCurrentVolume(currentPos);

        if (!isSpeaking)
        {
            if (currentVolume > voiceThreshold)
            {
                isSpeaking = true;
                SetMicUI(true);
                silenceTimer = 0f;
                int preBuffer = (int)(sampleRate * 0.2f);
                speechStartPos = (currentPos - preBuffer + micClip.samples) % micClip.samples;
                Debug.Log("音声聞き取り中");
            }
        }
        else
        {
            if (currentVolume < voiceThreshold)
            {
                silenceTimer += Time.deltaTime;
                if (silenceTimer >= silenceDuration) //話し終わりを判断
                {
                    isSpeaking = false;
                    SetMicUI(false);
                    float[] audioData = ExtractMicAudio(speechStartPos, currentPos); //音声データを切り出す
                    
                    // 平均音量の計算
                    int length = (currentPos - speechStartPos + micClip.samples) % micClip.samples;
                    float activeSum = 0f;
                    int activeCount = 0;
                    float totalSum = 0f;
                    for (int i = 0; i < length; i++)
                    {
                        float abs = Mathf.Abs(audioData[i]);
                        totalSum += abs;

                        if (abs > voiceThreshold * 0.5f) //話している部分だけを平均音調に反映
                        {
                            activeSum += abs;
                            activeCount++;
                        }
                    }
                    
                    float overallAvg = (length > 0) ? (totalSum / length) : 0f;
                    latestVolume = (activeCount > 0) ? (activeSum / activeCount) : 0f;

                    // ノイズの判断
                    if (overallAvg < minSpeechVolume || activeCount < sampleRate * 0.15f)
                    {
                        Debug.Log($"<color=gray>ノイズ処理 (全体平均: {overallAvg:F4})</color>");
                        return;
                    }

                    _ = RunWhisperAsync(audioData);
                }
            }
            else
            {
                silenceTimer = 0f;
            }
        }
    }

    //　UIの切り替え
    void SetMicUI(bool isListening)
    {
        micOn.SetActive(isListening);
        micMute.SetActive(!isListening);
    }

    float GetCurrentVolume(int currentPos)
    {
        int checkSamples = 1024;
        float[] waveData = new float[checkSamples];
        int startPos = (currentPos - checkSamples + micClip.samples) % micClip.samples;
        micClip.GetData(waveData, startPos);

        float maxVol = 0f;
        for (int i = 0; i < checkSamples; i++)
        {
            float abs = Mathf.Abs(waveData[i]);
            if (abs > maxVol) maxVol = abs;
        }
        return maxVol;
    }

    float[] ExtractMicAudio(int startPos, int endPos)
    {
        int totalSamples = micClip.samples;
        int length = (endPos - startPos + totalSamples) % totalSamples;
        if (length == 0) length = totalSamples;

        float[] rawSamples = new float[length];
        if (startPos + length <= totalSamples) //リングバッファの端をまたぐか確認
        {
            micClip.GetData(rawSamples, startPos);
        }
        else
        {
            int firstPart = totalSamples - startPos;
            float[] part1 = new float[firstPart];
            float[] part2 = new float[length - firstPart];
            micClip.GetData(part1, startPos);
            micClip.GetData(part2, 0);
            System.Array.Copy(part1, 0, rawSamples, 0, firstPart);
            System.Array.Copy(part2, 0, rawSamples, firstPart, length - firstPart);
        }

        float[] paddedData = new float[maxSamples];
        int copyLength = Mathf.Min(length, maxSamples);
        System.Array.Copy(rawSamples, paddedData, copyLength);
        return paddedData;
    }

    async Awaitable RunWhisperAsync(float[] data)
    {
        isInferencing = true;
        var outputTokens = new NativeArray<int>(maxTokens, Allocator.Persistent);
        var lastToken = new NativeArray<int>(1, Allocator.Persistent);


        try
        {
            using var audioInput = new Tensor<float>(new TensorShape(1, maxSamples), data);
            spectrogram.Schedule(audioInput);
            var logmel = spectrogram.PeekOutput() as Tensor<float>;
            encoder.Schedule(logmel);
            var encodedAudio = encoder.PeekOutput() as Tensor<float>;

            outputTokens[0] = START_OF_TRANSCRIPT;
            outputTokens[1] = JAPANESE;
            outputTokens[2] = TRANSCRIBE;
            int tokenCount = 3;

            using var tokensTensor = new Tensor<int>(new TensorShape(1, maxTokens));
            ComputeTensorData.Pin(tokensTensor);
            tokensTensor.Reshape(new TensorShape(1, tokenCount));
            tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);

            lastToken[0] = NO_TIME_STAMPS;
            using var lastTokenTensor = new Tensor<int>(new TensorShape(1, 1), new[] { NO_TIME_STAMPS });

            List<byte> outputBytes = new List<byte>();

            while (tokenCount < maxTokens - 1)
            {
                decoder1.SetInput("input_ids", tokensTensor);
                decoder1.SetInput("encoder_hidden_states", encodedAudio);
                decoder1.Schedule();

                decoder2.SetInput("input_ids", lastTokenTensor);
                for (int i = 0; i < 6; i++) //tinyなら４、baseなら６
                {
                    decoder2.SetInput($"past_key_values.{i}.decoder.key", decoder1.PeekOutput($"present.{i}.decoder.key") as Tensor<float>);
                    decoder2.SetInput($"past_key_values.{i}.decoder.value", decoder1.PeekOutput($"present.{i}.decoder.value") as Tensor<float>);
                    decoder2.SetInput($"past_key_values.{i}.encoder.key", decoder1.PeekOutput($"present.{i}.encoder.key") as Tensor<float>);
                    decoder2.SetInput($"past_key_values.{i}.encoder.value", decoder1.PeekOutput($"present.{i}.encoder.value") as Tensor<float>);
                }
                decoder2.Schedule();

                var logits = decoder2.PeekOutput("logits") as Tensor<float>;
                argmax.Schedule(logits);
                using var t_Token = await argmax.PeekOutput().ReadbackAndCloneAsync() as Tensor<int>;
                int index = t_Token[0];

                outputTokens[tokenCount] = lastToken[0];
                lastToken[0] = index;
                tokenCount++;
                tokensTensor.Reshape(new TensorShape(1, tokenCount));
                tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);
                lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

                if (index == END_OF_TEXT)
                {
                    break;
                }
                else if (index < tokens.Length)
                {
                    var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(tokens[index]));
                    outputBytes.AddRange(bytes);
                }

                await Awaitable.NextFrameAsync();
            }

            string outputString = Encoding.UTF8.GetString(outputBytes.ToArray());
            Debug.Log($"【最終結果】: 「{outputString}」 | 平均音量: {latestVolume:F3}");

            OnSpeechRecognized?.Invoke(outputString, latestVolume);

            foreach (string keyword in targetKeywords)
            {
                if (!string.IsNullOrEmpty(keyword) && outputString.Contains(keyword))
                {
                    Debug.Log($"<color=cyan>ワード「{keyword}」を検知！</color>");
                    break;
                }
            }
        }
        finally
        {
            if (outputTokens.IsCreated) outputTokens.Dispose();
            if (lastToken.IsCreated) lastToken.Dispose();
            isInferencing = false;
        }
    }
    // Tokenizer
    public TextAsset vocabAsset;
    void GetTokens()
    {
        var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabAsset.text);
        tokens = new string[vocab.Count];
        foreach (var item in vocab)
        {
            tokens[item.Value] = item.Key;
        }
    }

    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter : (char)whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('\u00A1' <= c && c <= '\u00AC') || ('\u00AE' <= c && c <= '\u00FF'));
    }

    private void OnDestroy()
    {
        if (micDevice != null) Microphone.End(micDevice);
        decoder1?.Dispose();
        decoder2?.Dispose();
        encoder?.Dispose();
        spectrogram?.Dispose();
        argmax?.Dispose();
    }
}
