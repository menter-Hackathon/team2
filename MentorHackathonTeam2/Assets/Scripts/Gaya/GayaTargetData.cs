using System;
using UnityEngine;

/// <summary>
/// ガヤ候補となる単語1つ分のデータ。
/// セリフ中の &lt;link="targetId"&gt;keyword&lt;/link&gt; と targetId で対応付けられる。
/// </summary>
[Serializable]
public class GayaTargetData
{
    [Tooltip("link タグの ID と一致させる識別子")]
    public string targetId;
    [Tooltip("対象となる単語（確認用）")]
    public string keyword;
    [Tooltip("クリック成功時に吹き出しへ表示するガヤのセリフ")]
    public string gayaPhrase;
}
