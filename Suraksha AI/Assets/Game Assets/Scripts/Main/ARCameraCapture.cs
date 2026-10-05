using UnityEngine;
using UnityEngine.UI;

public class WebcamCapture : MonoBehaviour
{
    [SerializeField] private RawImage preview;   // drag RawImage here
    [SerializeField] private float captureInterval = 0.2f;
    [SerializeField, Range(1, 100)] private int jpegQuality = 70;

    private WebCamTexture _cam;
    private Texture2D _tex;
    private float _next;

    public WebCamTexture Cam => _cam;
    public bool IsReady => _cam != null && _cam.isPlaying && _cam.width > 16;

    private void Start()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0) { Debug.LogError("[Camera] No webcam"); return; }

        string name = devices[0].name;
        foreach (var d in devices)
            if (!d.name.Contains("OBS")) { name = d.name; break; }

        _cam = new WebCamTexture(name, 640, 480, 15);

        if (preview != null)
        {
            preview.texture = _cam;
            preview.color = Color.white;
        }

        _cam.Play();
        Debug.Log($"[Camera] Started: {name}");
    }

    private void Update()
    {
        if (preview == null || !IsReady) return;
        // fix upside-down feed
        preview.uvRect = _cam.videoVerticallyMirrored
            ? new Rect(0, 1, 1, -1)
            : new Rect(0, 0, 1, 1);
    }

    public bool CanCapture()
    {
        if (Time.time < _next) return false;
        _next = Time.time + captureInterval;
        return true;
    }

    public byte[] GetCameraFrameJPG()
    {
        if (!IsReady) return null;

        if (_tex == null || _tex.width != _cam.width || _tex.height != _cam.height)
            _tex = new Texture2D(_cam.width, _cam.height, TextureFormat.RGB24, false);

        _tex.SetPixels32(_cam.GetPixels32());
        _tex.Apply();
        return _tex.EncodeToJPG(jpegQuality);
    }

    private void OnDestroy() { if (_cam != null) _cam.Stop(); }
}