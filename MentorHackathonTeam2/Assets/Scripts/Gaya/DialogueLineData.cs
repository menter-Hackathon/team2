using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MC のセリフ1行分のデータ。ガヤ候補は &lt;link="targetId"&gt;単語&lt;/link&gt; でマークアップする。
/// </summary>
[CreateAssetMenu(fileName = "DialogueLine", menuName = "Gaya/Dialogue Line Data")]
public class DialogueLineData : ScriptableObject
{
    [Header("本文")]
    [Tooltip("セリフ本文。ガヤ候補は <link=\"targetId\">単語</link> でマークアップ")]
    [TextArea] public string markedUpText;
    [Tooltip("このセリフに含まれるガヤ候補一覧（targetId を link タグと一致させる）")]
    public List<GayaTargetData> targets = new List<GayaTargetData>();

    [Header("タイミング（秒）")]
    [Tooltip("1文字あたりの表示間隔（秒）")]
    public float charInterval = 0.2f;
    [Tooltip("全文字表示後の維持時間（秒）")]
    public float postDisplayWait = 2.0f;
    [Tooltip("消去後、次のセリフまでの待機時間（秒）")]
    public float nextLineWait = 0.5f;
}
