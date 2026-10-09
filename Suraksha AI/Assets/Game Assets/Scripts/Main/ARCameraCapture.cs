using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class WebcamCapture : MonoBehaviour
{
    [SerializeField] private RawImage preview;
    [SerializeField] private float captureInterval = 0.25f;         
    [SerializeField, Range(1, 100)] private int jpegQuality = 60;
    [SerializeField] private bool preferBackCamera = true;           
    [SerializeField] private bool stampFrames = true;               

    private WebCamTexture _cam;
    private Color32[] _px, _rot;
    private float _next;
    private bool _hasNew;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public WebCamTexture Cam => _cam;
    public bool IsReady => _cam != null && _cam.isPlaying && _cam.width > 16;

    private void Start()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0) { Debug.LogError("[Camera] No webcam"); return; }

        string name = devices[0].name;
        foreach (var d in devices)
        {
            if (d.name.Contains("OBS")) continue;
            if (d.isFrontFacing == preferBackCamera) continue;   // wrong facing, keep looking
            name = d.name; break;
        }

        _cam = new WebCamTexture(name, 640, 480, 15);
        if (preview != null) { preview.texture = _cam; preview.color = Color.white; }
        _cam.Play();
        Debug.Log($"[Camera] Started: {name}");
    }

    private void Update()
    {
        if (!IsReady) return;
        if (_cam.didUpdateThisFrame) _hasNew = true;

        if (preview != null)
        {
            preview.uvRect = _cam.videoVerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
            preview.rectTransform.localEulerAngles = new Vector3(0, 0, -_cam.videoRotationAngle);
        }
    }

    // Only true when the camera produced a NEW frame and the interval has passed.
    public bool CanCapture()
    {
        if (!_hasNew || Time.unscaledTime < _next) return false;
        _next = Time.unscaledTime + captureInterval;
        return true;
    }

    public byte[] GetCameraFrameJPG()
    {
        if (!IsReady) return null;
        _hasNew = false;
        long stampMs = _clock.ElapsedMilliseconds;

        int w = _cam.width, h = _cam.height;
        if (_px == null || _px.Length != w * h) _px = new Color32[w * h];
        _cam.GetPixels32(_px);                                    // reused buffer: no 1.2 MB alloc per frame

        if (_cam.videoVerticallyMirrored) FlipRows(_px, w, h);

        Color32[] src = _px; int ow = w, oh = h;
        int rot = _cam.videoRotationAngle;                         // phones deliver sideways frames
        if (rot == 90 || rot == 270)
        {
            if (_rot == null || _rot.Length != _px.Length) _rot = new Color32[_px.Length];
            Rotate(_px, _rot, w, h, rot);
            src = _rot; ow = h; oh = w;
        }

        // encodes straight from the array: no Texture2D, no SetPixels32/Apply upload
        byte[] jpg = ImageConversion.EncodeArrayToJPG(src, GraphicsFormat.R8G8B8A8_UNorm, (uint)ow, (uint)oh, 0, jpegQuality);
        if (!stampFrames) return jpg;

        var outp = new byte[12 + jpg.Length];
        outp[0] = (byte)'S'; outp[1] = (byte)'R'; outp[2] = (byte)'K'; outp[3] = (byte)'1';
        BitConverter.GetBytes(stampMs).CopyTo(outp, 4);            // little-endian on all Unity platforms
        Buffer.BlockCopy(jpg, 0, outp, 12, jpg.Length);
        return outp;
    }

    private static void FlipRows(Color32[] a, int w, int h)
    {
        var tmp = new Color32[w];
        for (int y = 0; y < h / 2; y++)
        {
            int t = y * w, b = (h - 1 - y) * w;
            Array.Copy(a, t, tmp, 0, w); Array.Copy(a, b, a, t, w); Array.Copy(tmp, 0, a, b, w);
        }
    }

    private static void Rotate(Color32[] s, Color32[] d, int w, int h, int angle)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx, dy;
                if (angle == 90) { dx = y; dy = w - 1 - x; }
                else { dx = h - 1 - y; dy = x; }
                d[dy * h + dx] = s[y * w + x];
            }
    }

    private void OnApplicationPause(bool paused)
    {
        if (_cam == null) return;
        if (paused) _cam.Stop(); else _cam.Play();                
    }

    private void OnDestroy() { if (_cam != null) _cam.Stop(); }
}