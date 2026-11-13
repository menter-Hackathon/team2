using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Day カードの表示と解放演出を担当する。</summary>
[Serializable]
public sealed class StageCardView
{
    public Button button;
    public Image background;
    public Image accent;
    public Outline outline;
    public TMP_Text nameText;
    public TMP_Text recordText;
    public TMP_Text stateText;

    public void Render(StageCatalog.Stage stage, bool unlocked, bool selected, bool hasResult, float best, bool newUnlock)
    {
        nameText.text = stage.Name;
        recordText.text = hasResult ? $"最高精度  {best:0.#}%" : "最高精度  --";
        stateText.text = unlocked ? (newUnlock ? "NEW" : "OPEN") : "LOCKED";
        stateText.color = unlocked ? new Color(0.16f, 0.37f, 0.32f) : new Color(0.45f, 0.47f, 0.53f);
        background.color = selected ? new Color(1f, 0.98f, 0.94f) : new Color(0.96f, 0.96f, 0.95f);
        accent.color = unlocked ? stage.MainColor : new Color(0.67f, 0.69f, 0.72f);
        outline.effectColor = selected ? stage.MainColor : new Color(0.79f, 0.80f, 0.79f);
    }

    /// <summary>解放色と拡大を使った演出。キャンセル時は元の表示に戻す。</summary>
    public async UniTask UnlockAsync(Color mainColor, CancellationToken cancellationToken)
    {
        RectTransform rect = button.transform as RectTransform;
        Vector3 originalScale = rect.localScale;
        Color originalBackground = background.color;
        Color originalAccent = accent.color;
        Color originalStateColor = stateText.color;
        string originalState = stateText.text;

        try
        {
            stateText.text = "UNLOCK";
            stateText.color = mainColor;
            const float duration = 0.8f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                rect.localScale = originalScale * (1f + 0.08f * pulse);
                accent.color = Color.Lerp(new Color(0.67f, 0.69f, 0.72f), mainColor, progress);
                background.color = Color.Lerp(originalBackground, mainColor, 0.18f * pulse);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.unscaledDeltaTime;
            }
        }
        finally
        {
            if (rect != null) rect.localScale = originalScale;
            if (background != null) background.color = originalBackground;
            if (accent != null) accent.color = originalAccent;
            if (stateText != null)
            {
                stateText.color = originalStateColor;
                stateText.text = originalState;
            }
        }
    }
}
