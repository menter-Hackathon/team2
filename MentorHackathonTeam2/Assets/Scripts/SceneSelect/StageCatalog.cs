using System;
using UnityEngine;

/// <summary>Day1～Day3 と Online の表示・遷移設定。</summary>
[CreateAssetMenu(fileName = "StageCatalog", menuName = "Scene Select/Stage Catalog")]
public sealed class StageCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Stage
    {
        [SerializeField] private string _name;
        [SerializeField] private string _sceneName;
        [TextArea(3, 6)] [SerializeField] private string _description;
        [SerializeField] private Color _mainColor = Color.white;

        public string Name => _name;
        public string SceneName => _sceneName;
        public string Description => _description;
        public Color MainColor => _mainColor;

    }

    [SerializeField] private Stage[] _stages = new Stage[4];

    public int Count => _stages == null ? 0 : _stages.Length;
    public Stage GetStage(int index) => _stages[index];

    private void OnValidate()
    {
        if (_stages == null)
        {
            return;
        }

        if (_stages.Length != 4)
        {
            Debug.LogWarning("[StageCatalog] Day1～Day3 と Online の4件を順番に設定してください。", this);
        }

        for (int i = 0; i < _stages.Length; i++)
        {
            string expectedName = i == 3 ? "Online" : $"Day{i + 1}";
            if (_stages[i] != null && _stages[i].Name != expectedName)
            {
                Debug.LogWarning($"[StageCatalog] {i + 1}番目は {expectedName} にしてください。", this);
            }
        }
    }
}
