using UnityEngine;
using UnityEngine.UI;

public class LaptopCameraPreview : MonoBehaviour
{
    [SerializeField] private RawImage _camFeed;
    [SerializeField, Range(1, 100)] private int jpegQuality = 70;
    [SerializeField] private float captureInterval = 0.2f;

    private WebCamTexture _webCamTex;
    private Texture2D _tex;
    private float _next;
    private int _dbgCount;

    private const int REQ_WIDTH = 640;
    private const int REQ_HEIGHT = 480;
    private const int REQ_FPS = 15;

    private void Start()
    {
        if (_camFeed == null)
        {
            Debug.LogError("[Camera] RawImage is not assigned!");
            return;
        }

        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log($"[Camera] Devices found: {devices.Length}");

        foreach (var device in devices)
            Debug.Log($"[Camera] Available: {device.name}");

        if (devices.Length == 0)
        {
            Debug.LogError("[Camera] No webcam detected by Unity.");
            return;
        }

        string camName = devices[0].name;

        foreach (var device in devices)
        {
            if (!device.name.Contains("OBS",
                System.StringComparison.OrdinalIgnoreCase))
            {
                camName = device.name;
                break;
            }
        }

        _webCamTex = new WebCamTexture(camName, 640, 480, 15);

        _camFeed.texture = _webCamTex;
        _camFeed.color = Color.white;
        _camFeed.enabled = true;

        _webCamTex.Play();

        Debug.Log($"[Camera] Started: {camName}");
    }

    private void Update()
    {
        if (_webCamTex == null)
            return;

        if (_webCamTex.didUpdateThisFrame)
        {
            Debug.Log(
                $"[Camera] Receiving frames: " +
                $"{_webCamTex.width}x{_webCamTex.height}"
            );
        }
    }

    public bool CanCapture()
    {
        if (Time.time < _next) return false;
        _next = Time.time + captureInterval;
        return true;
    }

    public byte[] GetCameraFrameJPG()
    {
        if (_webCamTex == null || !_webCamTex.isPlaying || _webCamTex.width < 100)
            return null;

        if (_tex == null || _tex.width != _webCamTex.width || _tex.height != _webCamTex.height)
            _tex = new Texture2D(_webCamTex.width, _webCamTex.height, TextureFormat.RGB24, false);

        _tex.SetPixels32(_webCamTex.GetPixels32());
        _tex.Apply();
        byte[] jpg = _tex.EncodeToJPG(jpegQuality);

        if (++_dbgCount % 30 == 1)
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "sent.jpg");
            System.IO.File.WriteAllBytes(path, jpg);
            Debug.Log($"[Vision] Sent {jpg.Length} bytes -> {path}");
        }
        return jpg;
    }

    private void OnDestroy()
    {
        if (_webCamTex != null && _webCamTex.isPlaying) _webCamTex.Stop();
    }
}