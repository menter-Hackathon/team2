using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 入力デバイス（マウス／タップ／レティクル）の差異を吸収し、
/// クリックされたガヤ候補を <see cref="GayaSystem"/> に通知する入力専用クラス。
/// </summary>
public class GayaCursorRaycaster : MonoBehaviour
{
    /// <summary>ポインター位置の取得方法。</summary>
    public enum PointerMode
    {
        Mouse,
        Touch,
        ScreenCenterReticle,
    }

    private const string LocalPlayerId = "LocalPlayer";

    [Header("参照")]
    [Tooltip("クリック結果を通知する GayaSystem")]
    [SerializeField] private GayaSystem _gayaSystem;
    [Tooltip("リンク判定対象のセリフ TextMeshProUGUI（GayaSystem と同じもの）")]
    [SerializeField] private TextMeshProUGUI _text;
    [Tooltip("Canvas を描画するカメラ。Screen Space - Overlay の場合は空でOK")]
    [SerializeField] private Camera _uiCamera;

    [Header("入力")]
    [Tooltip("ポインター位置の取得方法（マウス／タップ／画面中央レティクル）")]
    [SerializeField] private PointerMode _pointerMode = PointerMode.Mouse;
    [Tooltip("マウス位置に追従させるレティクル画像（任意・Mouse モード時のみ表示）")]
    [SerializeField] private Image _reticleImage;

    private void Update()
    {
        UpdateReticle();

        if (_gayaSystem == null || _text == null)
        {
            return;
        }

        bool hasPointer = TryGetPointer(out Vector3 pointerPosition, out bool isPressed);
        string hoveredId = hasPointer ? FindLinkId(pointerPosition) : null;

        if (isPressed && !string.IsNullOrEmpty(hoveredId))
        {
            _gayaSystem.TryClaimWord(hoveredId, LocalPlayerId);
        }
    }

    /// <summary>レティクル画像をマウス位置へ移動させる（Mouse モード時のみ表示）。</summary>
    private void UpdateReticle()
    {
        if (_reticleImage == null)
        {
            return;
        }

        bool showReticle = _pointerMode == PointerMode.Mouse;
        _reticleImage.enabled = showReticle;
        if (!showReticle)
        {
            return;
        }

        RectTransform parentRect = _reticleImage.rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogError("Reticle Image must have a RectTransform parent.", _reticleImage);
            _reticleImage.enabled = false;
            return;
        }

        Canvas canvas = _reticleImage.canvas;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (_uiCamera != null ? _uiCamera : canvas.worldCamera)
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                Input.mousePosition,
                eventCamera,
                out Vector2 localPoint))
        {
            _reticleImage.rectTransform.localPosition = localPoint;
        }
    }

    /// <summary>
    /// 現在のモードに応じてポインター座標と押下状態を取得する。
    /// </summary>
    /// <returns>有効なポインターが存在すれば true。</returns>
    private bool TryGetPointer(out Vector3 position, out bool isPressed)
    {
        switch (_pointerMode)
        {
            case PointerMode.Touch:
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);
                    position = touch.position;
                    isPressed = touch.phase == TouchPhase.Began;
                    return true;
                }
                position = Vector3.zero;
                isPressed = false;
                return false;

            case PointerMode.ScreenCenterReticle:
                position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                isPressed = Input.GetMouseButtonDown(0);
                return true;

            default:
                position = Input.mousePosition;
                isPressed = Input.GetMouseButtonDown(0);
                return true;
        }
    }

    /// <summary>
    /// 指定スクリーン座標と交差するリンクの ID を取得する。
    /// </summary>
    /// <returns>リンク ID。交差しない場合は null。</returns>
    private string FindLinkId(Vector3 screenPosition)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, screenPosition, _uiCamera);
        if (linkIndex < 0 || linkIndex >= _text.textInfo.linkCount)
        {
            return null;
        }
        return _text.textInfo.linkInfo[linkIndex].GetLinkID();
    }
}
