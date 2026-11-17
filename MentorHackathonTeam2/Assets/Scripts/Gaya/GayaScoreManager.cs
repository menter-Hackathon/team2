using System;
using UnityEngine;

/// <summary>
/// <see cref="GayaSystem"/> のクリック結果を受けてスコアを加減算し、
/// <see cref="DualSliderGauge"/> と <see cref="ScorePopup"/> へ反映する橋渡しクラス。
/// </summary>
public class GayaScoreManager : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("イベントを購読する GayaSystem")]
    [SerializeField] private GayaSystem _gayaSystem;
    [Tooltip("スコアを表示する2重ゲージ")]
    [SerializeField] private DualSliderGauge _gauge;
    [Tooltip("加点・減点時に生成するポップアップのプレハブ")]
    [SerializeField] private ScorePopup _scorePopupPrefab;
    [Tooltip("吹き出しの出現地点がない場合に使うポップアップの生成先")]
    [SerializeField] private RectTransform _popupParent;

    [Header("ポップアップ位置")]
    [Tooltip("吹き出しの出現地点からのオフセット（px）")]
    [SerializeField] private Vector2 _popupOffset = new Vector2(0f, 60f);

    [Header("スコア")]
    [Tooltip("スコアの最大値")]
    [SerializeField] private int _maxScore = 1000;
    [Tooltip("スコアの初期値")]
    [SerializeField] private int _initialScore = 0;
    [Tooltip("正解ガヤ時の加点")]
    [SerializeField] private int _gayaPoint = 100;
    [Tooltip("非ガヤ（おてつき）時の減点（正の値で指定）")]
    [SerializeField] private int _missPenalty = 50;

    private int _score;

    /// <summary>現在のスコア。</summary>
    public int Score => _score;

    /// <summary>スコアの最大値。</summary>
    public int MaxScore => _maxScore;

    /// <summary>スコアが変化した際に発火する（新しいスコア, 実際の増減値）。</summary>
    public event Action<int, int> OnScoreChanged;

    private void OnEnable()
    {
        if (_gayaSystem != null)
        {
            _gayaSystem.OnWordClaimedAt += HandleWordClaimed;
        }
    }

    private void OnDisable()
    {
        if (_gayaSystem != null)
        {
            _gayaSystem.OnWordClaimedAt -= HandleWordClaimed;
        }
    }

    private void Start()
    {
        ResetScore();
    }

    /// <summary>
    /// スコアを初期値に戻し、ゲージを即座に同期する。
    /// </summary>
    public void ResetScore()
    {
        _score = Mathf.Clamp(_initialScore, 0, _maxScore);
        if (_gauge != null)
        {
            _gauge.Initialize(_score, _maxScore);
        }
    }

    /// <summary>
    /// 表示中のスコアポップアップをすべて破棄する。
    /// </summary>
    public void ClearPopups()
    {
        if (_popupParent == null)
        {
            return;
        }
        foreach (ScorePopup popup in _popupParent.GetComponentsInChildren<ScorePopup>(true))
        {
            Destroy(popup.gameObject);
        }
    }

    /// <summary>
    /// 単語確定時の処理。正解なら加点、おてつきなら減点し、ゲージとポップアップへ反映する。
    /// </summary>
    private void HandleWordClaimed(string targetId, string playerId, string phrase, bool isGayaWord, RectTransform spawnPoint)
    {
        int requested = isGayaWord ? _gayaPoint : -Mathf.Abs(_missPenalty);
        int newScore = Mathf.Clamp(_score + requested, 0, _maxScore);
        int actualDelta = newScore - _score;
        _score = newScore;

        if (_gauge != null)
        {
            _gauge.SetValue(_score);
        }

        // ゲージ上限・下限で実増減が0でも、プレイヤーへのフィードバックとして本来の値を表示する
        SpawnPopup(requested, spawnPoint);

        if (actualDelta != 0)
        {
            OnScoreChanged?.Invoke(_score, actualDelta);
        }
    }

    /// <summary>
    /// 吹き出しの出現地点（＋オフセット）にスコアポップアップを生成・再生する。
    /// 出現地点が null の場合は生成先の中央に表示する。
    /// </summary>
    private void SpawnPopup(int delta, RectTransform spawnPoint)
    {
        if (_scorePopupPrefab == null)
        {
            Debug.LogWarning("[GayaScoreManager] Score popup prefab is not assigned.", this);
            return;
        }

        RectTransform parent = spawnPoint != null ? spawnPoint : _popupParent;
        if (parent == null)
        {
            Debug.LogWarning("[GayaScoreManager] No score popup parent or Gaya bubble spawn point is available.", this);
            return;
        }

        ScorePopup popup = Instantiate(_scorePopupPrefab, parent, false);
        RectTransform rect = (RectTransform)popup.transform;
        rect.anchoredPosition = _popupOffset;
        rect.SetAsLastSibling();
        popup.Play(delta);
    }
}
