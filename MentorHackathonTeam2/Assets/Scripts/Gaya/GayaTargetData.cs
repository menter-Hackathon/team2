using System;
using UnityEngine;

/// <summary>
/// ガヤ候補となる単語1つ分のデータ。
/// セリフ中の &lt;link="targetId"&gt;keyword&lt;/link&gt; と targetId で対応付けられる。
/// </summary>
[Serializable]
public class GayaTargetData
{
    /// <summary>link タグの ID と一致させる識別子。</summary>
    public string targetId;

    /// <summary>対象となる単語（確認用）。</summary>
    public string keyword;

    /// <summary>クリック成功時に発生するガヤのセリフ。</summary>
    public string gayaPhrase;
}
