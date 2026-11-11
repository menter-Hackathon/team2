using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MC のセリフ1行分のデータ。ガヤ候補は &lt;link="targetId"&gt;単語&lt;/link&gt; でマークアップする。
/// </summary>
[CreateAssetMenu(fileName = "DialogueLine", menuName = "Gaya/Dialogue Line Data")]
public class DialogueLineData : ScriptableObject
{
    /// <summary>リンクタグでマークアップされたセリフ本文。</summary>
    [TextArea] public string markedUpText;

    /// <summary>このセリフに含まれるガヤ候補一覧。</summary>
    public List<GayaTargetData> targets = new List<GayaTargetData>();

    /// <summary>1文字あたりの表示間隔（秒）。</summary>
    public float charInterval = 0.2f;

    /// <summary>全文字表示後の維持時間（秒）。</summary>
    public float postDisplayWait = 2.0f;

    /// <summary>消去後、次のセリフまでの待機時間（秒）。</summary>
    public float nextLineWait = 0.5f;
}
