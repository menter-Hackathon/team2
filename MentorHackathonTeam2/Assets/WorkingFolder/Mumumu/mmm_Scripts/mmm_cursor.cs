using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>自分と相手のカーソル位置を同期して表示する。</summary>
public class mmm_cursor : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private Camera _uiCamera;
    [SerializeField] private Image _reticleImage;

    private Vector2 _remotePointerPosition;
    private bool _receivedPointer;
    private bool? _wasMine;
    private Sprite _originalSprite;
    private Sprite _graySprite;
    private Texture2D _grayTexture;

    private void Awake()
    {
        if (_reticleImage != null)
        {
            _originalSprite = _reticleImage.sprite;
        }
    }

    private void Update()
    {
        if (_reticleImage == null)
        {
            return;
        }

        bool isMine = photonView.IsMine;
        if (_wasMine != isMine)
        {
            SetAppearance(isMine);
            _wasMine = isMine;
        }

        if (isMine)
        {
            UpdateReticle(Input.mousePosition);
        }
        else if (_receivedPointer)
        {
            UpdateReticle(new Vector2(
                _remotePointerPosition.x * Screen.width,
                _remotePointerPosition.y * Screen.height));
        }
        else
        {
            _reticleImage.enabled = false;
        }
    }

    private void SetAppearance(bool isMine)
    {
        _reticleImage.color = Color.white;
        if (isMine)
        {
            _reticleImage.sprite = _originalSprite;
            return;
        }

        // 元画像自体が赤いので、Image.color の乗算だけでは灰色にならない。
        if (_graySprite == null && _originalSprite != null)
        {
            Texture2D source = _originalSprite.texture;
            RenderTexture buffer = RenderTexture.GetTemporary(source.width, source.height, 0);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, buffer);
                RenderTexture.active = buffer;
                _grayTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                _grayTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                Color32[] pixels = _grayTexture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i].r = 128;
                    pixels[i].g = 128;
                    pixels[i].b = 128;
                }
                _grayTexture.SetPixels32(pixels);
                _grayTexture.Apply();
                Vector2 pivot = new Vector2(
                    _originalSprite.pivot.x / _originalSprite.rect.width,
                    _originalSprite.pivot.y / _originalSprite.rect.height);
                _graySprite = Sprite.Create(_grayTexture,
                    new Rect(0, 0, source.width, source.height), pivot,
                    _originalSprite.pixelsPerUnit);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
            }
        }
        _reticleImage.sprite = _graySprite;
    }

    private void UpdateReticle(Vector2 screenPosition)
    {
        _reticleImage.enabled = true;
        RectTransform parentRect = _reticleImage.rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            _reticleImage.enabled = false;
            return;
        }

        Canvas canvas = _reticleImage.canvas;
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (_uiCamera != null ? _uiCamera : canvas.worldCamera)
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            _reticleImage.rectTransform.localPosition = localPoint;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(new Vector2(
                Input.mousePosition.x / Mathf.Max(1, Screen.width),
                Input.mousePosition.y / Mathf.Max(1, Screen.height)));
        }
        else
        {
            _remotePointerPosition = (Vector2)stream.ReceiveNext();
            _receivedPointer = true;
        }
    }

    private void OnDestroy()
    {
        if (_graySprite != null) Destroy(_graySprite);
        if (_grayTexture != null) Destroy(_grayTexture);
    }
}
