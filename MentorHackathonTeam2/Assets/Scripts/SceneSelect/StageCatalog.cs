using System;
using UnityEngine;

/// <summary>Day1～Day4 の表示・遷移設定。プレイヤーの記録は DataManager が保持する。</summary>
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
        [Range(0f, 100f)] [SerializeField] private float _requiredPreviousAccuracy = 90f;

        public string Name => _name;
        public string SceneName => _sceneName;
        public string Description => _description;
        public Color MainColor => _mainColor;
        public float RequiredPreviousAccuracy => _requiredPreviousAccuracy;

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
            Debug.LogWarning("[StageCatalog] Day1～Day4 の4件を順番に設定してください。", this);
        }

        for (int i = 0; i < _stages.Length; i++)
        {
            if (_stages[i] != null && _stages[i].Name != $"Day{i + 1}")
            {
                Debug.LogWarning($"[StageCatalog] {i + 1}番目は Day{i + 1} にしてください。", this);
            }
        }
    }
}
