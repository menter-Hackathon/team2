using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 入力デバイス（マウス／タップ／レティクル）の差異を吸収し、
/// ホバー中のガヤ候補を <see cref="GayaSystem"/> に通知する入力専用クラス。
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

    [SerializeField] private GayaSystem _gayaSystem;
    [SerializeField] private TextMeshProUGUI _text;
    [Tooltip("Screen Space - Overlay の Canvas なら空でOK")]
    [SerializeField] private Camera _uiCamera;
    [SerializeField] private PointerMode _pointerMode = PointerMode.Mouse;
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

        _gayaSystem.SetHoveredTarget(hoveredId);

        if (isPressed && !string.IsNullOrEmpty(hoveredId))
        {
            _gayaSystem.TryClaimWord(hoveredId, LocalPlayerId);
        }
    }

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
