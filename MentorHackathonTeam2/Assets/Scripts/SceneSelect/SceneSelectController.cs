using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Day 一覧と詳細表示、解放判定、シーン遷移を担当する。</summary>
public sealed class SceneSelectController : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private StageCatalog _catalog;
    [SerializeField] private StageCardView[] _cards;

    [Header("詳細")]
    [SerializeField] private Image _detailAccent;
    [SerializeField] private TMP_Text _detailNameText;
    [SerializeField] private TMP_Text _detailDescriptionText;
    [SerializeField] private TMP_Text _detailRecordText;
    [SerializeField] private TMP_Text _detailStateText;
    [SerializeField] private Button _startButton;
    [SerializeField] private TMP_Text _startButtonText;
    [SerializeField] private Button _backButton;

    private int _selectedIndex;

    private void Start()
    {
        if (_catalog == null || _catalog.Count != 4 || _cards == null || _cards.Length != 4)
        {
            Debug.LogError("[SceneSelectController] StageCatalog と4件のカードを設定してください。", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < _cards.Length; i++)
        {
            if (_catalog.GetStage(i) == null || _catalog.GetStage(i).Name != $"Day{i + 1}"
                || _cards[i] == null || _cards[i].button == null)
            {
                Debug.LogError($"[SceneSelectController] Day{i + 1} の設定またはカード参照が不正です。", this);
                enabled = false;
                return;
            }

            int index = i;
            _cards[i].button.onClick.AddListener(() => SelectStage(index));
        }

        _startButton.onClick.AddListener(StartSelectedStage);
        _backButton.onClick.AddListener(ReturnToTitle);
        SelectStage(0);
        PlayPendingUnlockAnimationsAsync(destroyCancellationToken).Forget();
    }

    /// <summary>最高精度の更新後などに一覧と詳細を描き直す。</summary>
    public void Refresh()
    {
        for (int i = 0; i < _catalog.Count; i++)
        {
            RefreshCard(i);
        }

        RefreshDetail();
    }

    /// <summary>画面を開いた時に未演出の解放を順に再生し、完了した Day だけ記録する。</summary>
    private async UniTask PlayPendingUnlockAnimationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (int i = 1; i < _catalog.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StageCatalog.Stage stage = _catalog.GetStage(i);
                if (!IsUnlocked(i) || DataManager.Selection.WasUnlockAnimationPlayed(stage.Name))
                {
                    continue;
                }

                await _cards[i].UnlockAsync(stage.MainColor, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                DataManager.Selection.MarkUnlockAnimationPlayed(stage.Name);
                Refresh();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 画面遷移などで破棄された場合は、演出済みにせず次回やり直す。
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private bool IsUnlocked(int index)
    {
        if (index == 0)
        {
            return true;
        }

        StageCatalog.Stage previous = _catalog.GetStage(index - 1);
        StageCatalog.Stage current = _catalog.GetStage(index);
        return DataManager.Selection.TryGetBestAccuracy(previous.Name, out float best)
            && best >= current.RequiredPreviousAccuracy;
    }

    private void SelectStage(int index)
    {
        _selectedIndex = index;
        Refresh();
    }

    private void RefreshCard(int index)
    {
        StageCatalog.Stage stage = _catalog.GetStage(index);
        StageCardView card = _cards[index];
        bool unlocked = IsUnlocked(index);
        bool selected = index == _selectedIndex;
        bool hasResult = DataManager.Selection.TryGetBestAccuracy(stage.Name, out float best);
        bool newUnlock = index > 0 && unlocked && !DataManager.Selection.WasUnlockAnimationPlayed(stage.Name);

        card.Render(stage, unlocked, selected, hasResult, best, newUnlock);
    }

    private void RefreshDetail()
    {
        StageCatalog.Stage stage = _catalog.GetStage(_selectedIndex);
        bool unlocked = IsUnlocked(_selectedIndex);
        bool hasResult = DataManager.Selection.TryGetBestAccuracy(stage.Name, out float best);

        _detailAccent.color = unlocked ? stage.MainColor : new Color(0.67f, 0.69f, 0.72f);
        _detailNameText.text = stage.Name;
        _detailDescriptionText.text = stage.Description;
        _detailRecordText.text = hasResult ? $"最高精度  {best:0.#}%" : "最高精度  --";
        _detailStateText.text = unlocked
            ? "このステージをプレイできます"
            : $"{_catalog.GetStage(_selectedIndex - 1).Name} の最高精度 {stage.RequiredPreviousAccuracy:0.#}% 以上で解放";
        _startButton.interactable = unlocked;
        _startButton.image.color = unlocked
            ? new Color(0.15f, 0.20f, 0.27f)
            : new Color(0.67f, 0.69f, 0.71f);
        _startButtonText.text = unlocked ? "START" : "LOCKED";
    }

    private void StartSelectedStage()
    {
        if (!IsUnlocked(_selectedIndex))
        {
            return;
        }

        LoadScene(_catalog.GetStage(_selectedIndex).SceneName);
    }

    private void ReturnToTitle() => LoadScene("Title");

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[SceneSelectController] 遷移先シーンを読み込めません: {sceneName}", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
