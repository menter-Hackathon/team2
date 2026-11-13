using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>シーンに配置済みの枠と塗りへDayの色を反映する。</summary>
public sealed class DayButtonStyle : MonoBehaviour
{
    [SerializeField] private Image _border;
    [SerializeField] private Image _fill;

    public bool HasRequiredReferences => _border != null && _fill != null
        && _border.sprite != null && _fill.sprite != null;

    public static void Apply(Button button, TMP_Text label, Color dayColor)
    {
        DayButtonStyle style = button.GetComponent<DayButtonStyle>();
        if (style == null || !style.HasRequiredReferences)
        {
            Debug.LogError("[DayButtonStyle] ボタンの枠・塗りをシーンで設定してください。", button);
            return;
        }

        style._border.color = dayColor;
        style._fill.color = Color.Lerp(new Color(1f, 0.985f, 0.955f), dayColor, 0.12f);
        if (label != null)
            label.color = button.interactable
                ? Color.Lerp(dayColor, Color.black, 0.4f)
                : new Color(0.45f, 0.46f, 0.47f);
    }
}
