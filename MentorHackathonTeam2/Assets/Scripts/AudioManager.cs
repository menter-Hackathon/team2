using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// BGM、効果音、ボイスを一元管理し、シーン遷移後も維持する。
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class AudioManager : MonoBehaviour
{
    private const string VolumeKey = "Settings.Volume";

    [Header("Audio Sources")]
    [Tooltip("BGM再生用AudioSource。未設定時は自動生成")]
    [SerializeField] private AudioSource _bgmSource;
    [Tooltip("効果音再生用AudioSource。未設定時は自動生成")]
    [SerializeField] private AudioSource _seSource;
    [Tooltip("ボイス再生用AudioSource。未設定時は自動生成")]
    [SerializeField] private AudioSource _voiceSource;

    private readonly List<AudioSource> _pitchedSeSources = new List<AudioSource>();
    private Tween _bgmTween;
    private float _bgmVolume = 1f;

    /// <summary>シーンをまたいで共有するAudioManager。</summary>
    public static AudioManager Instance { get; private set; }

    /// <summary>重複インスタンスを破棄し、AudioSourceと保存音量を初期化する。</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        _bgmSource = EnsureSource(_bgmSource, "BGM");
        _seSource = EnsureSource(_seSource, "SE");
        _voiceSource = EnsureSource(_voiceSource, "Voice");
        if (_voiceSource.outputAudioMixerGroup == null)
            _voiceSource.outputAudioMixerGroup = _seSource.outputAudioMixerGroup;
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _seSource.playOnAwake = false;
        _voiceSource.playOnAwake = false;
        _bgmVolume = _bgmSource.volume;
    }

    /// <summary>指定BGMを再生し、別クリップならフェード切り替えする。</summary>
    /// <param name="clip">再生するBGM。nullなら何もしない。</param>
    /// <param name="fadeDuration">フェード時間（秒）。</param>
    /// <param name="loop">再生終了後にループするか。</param>
    public void PlayBgm(AudioClip clip, float fadeDuration = 0.3f, bool loop = true)
    {
        if (clip == null || _bgmSource == null)
        {
            return;
        }

        _bgmSource.loop = loop;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            KillBgmTween();
            _bgmSource.volume = _bgmVolume;
            return;
        }

        KillBgmTween();
        float duration = Mathf.Max(0f, fadeDuration);
        if (!_bgmSource.isPlaying || _bgmSource.clip == null || duration == 0f)
        {
            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.volume = duration == 0f ? _bgmVolume : 0f;
            _bgmSource.Play();
            if (duration > 0f)
            {
                _bgmTween = _bgmSource.DOFade(_bgmVolume, duration).SetLink(gameObject);
            }
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .Append(_bgmSource.DOFade(0f, duration * 0.5f))
            .AppendCallback(() =>
            {
                _bgmSource.Stop();
                _bgmSource.clip = clip;
                _bgmSource.Play();
            })
            .Append(_bgmSource.DOFade(_bgmVolume, duration * 0.5f))
            .SetLink(gameObject);
        _bgmTween = sequence;
    }

    /// <summary>BGMをフェードアウトして停止する。</summary>
    /// <param name="fadeDuration">フェード時間（秒）。</param>
    public void StopBgm(float fadeDuration = 0.3f)
    {
        if (_bgmSource == null)
        {
            return;
        }

        KillBgmTween();
        float duration = Mathf.Max(0f, fadeDuration);
        if (!_bgmSource.isPlaying || duration == 0f)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmSource.volume = _bgmVolume;
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .Append(_bgmSource.DOFade(0f, duration))
            .AppendCallback(() =>
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
                _bgmSource.volume = _bgmVolume;
            })
            .SetLink(gameObject);
        _bgmTween = sequence;
    }

    /// <summary>効果音を重ねて再生する。ランダムピッチ用ソースは個別にプールする。</summary>
    /// <param name="clip">再生する効果音。nullなら何もしない。</param>
    /// <param name="volumeScale">効果音の音量倍率。</param>
    /// <param name="pitchRandomRange">ピッチのランダム幅（1 ± 値）。</param>
    public void PlaySe(AudioClip clip, float volumeScale = 1f, float pitchRandomRange = 0f)
    {
        if (clip == null || _seSource == null)
        {
            return;
        }

        float randomRange = Mathf.Max(0f, pitchRandomRange);
        if (randomRange <= 0f)
        {
            _seSource.pitch = 1f;
            _seSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
            return;
        }

        AudioSource source = GetAvailablePitchedSource();
        source.pitch = Random.Range(1f - randomRange, 1f + randomRange);
        source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
    }

    /// <summary>現在のボイスを停止して新しいボイスを再生する。</summary>
    /// <param name="clip">再生するボイス。nullなら何もしない。</param>
    public void PlayVoice(AudioClip clip)
    {
        if (clip == null || _voiceSource == null)
        {
            return;
        }

        _voiceSource.Stop();
        _voiceSource.clip = clip;
        _voiceSource.Play();
    }

    /// <summary>再生中のボイスを停止する。</summary>
    public void StopVoice()
    {
        if (_voiceSource != null)
        {
            _voiceSource.Stop();
            _voiceSource.clip = null;
        }
    }

    /// <summary>未設定のAudioSourceを子オブジェクト上に生成する。</summary>
    /// <param name="source">インスペクターで指定されたAudioSource。</param>
    /// <param name="sourceName">生成時の子オブジェクト名。</param>
    /// <returns>使用するAudioSource。</returns>
    private AudioSource EnsureSource(AudioSource source, string sourceName)
    {
        if (source != null)
        {
            if (source.transform != transform)
            {
                source.transform.SetParent(transform, false);
            }
            return source;
        }

        var sourceObject = new GameObject(sourceName + "AudioSource");
        sourceObject.transform.SetParent(transform, false);
        return sourceObject.AddComponent<AudioSource>();
    }

    /// <summary>空いているランダムピッチ用AudioSourceを取得する。</summary>
    /// <returns>再生に使うAudioSource。</returns>
    private AudioSource GetAvailablePitchedSource()
    {
        foreach (AudioSource source in _pitchedSeSources)
        {
            if (source != null && !source.isPlaying)
            {
                source.pitch = 1f;
                return source;
            }
        }

        AudioSource newSource = EnsureSource(null, "PitchedSE" + _pitchedSeSources.Count);
        newSource.outputAudioMixerGroup = _seSource.outputAudioMixerGroup;
        newSource.volume = _seSource.volume;
        _pitchedSeSources.Add(newSource);
        return newSource;
    }

    /// <summary>実行中のBGMフェードを停止する。</summary>
    private void KillBgmTween()
    {
        if (_bgmTween != null && _bgmTween.IsActive())
        {
            _bgmTween.Kill();
        }
        _bgmTween = null;
    }

    /// <summary>現在のシングルトン登録を解除し、Tweenを停止する。</summary>
    private void OnDestroy()
    {
        KillBgmTween();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
