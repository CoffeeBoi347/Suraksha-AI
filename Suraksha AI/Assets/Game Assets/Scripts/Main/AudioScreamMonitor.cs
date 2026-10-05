using System.Globalization;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// Mic -> heuristic scream score (0..1) -> backend {"type":"audio","scream":x}
/// Heuristic = loud AND energy concentrated in 1-4 kHz. Placeholder until YAMNet (Sentis) is plugged in.
public class AudioScreamMonitor : MonoBehaviour
{
    [SerializeField] private WebSocketClient client;
    [SerializeField] private float sendInterval = 0.5f;

    [Header("Tuning (watch the debug value, then adjust)")]
    [SerializeField] private float quietRms = 0.02f;   // at/below this = score 0
    [SerializeField] private float loudRms = 0.15f;    // at/above this = full loudness
    [SerializeField] private float bandLow = 0.25f;    // 1-4 kHz energy ratio at/below = 0
    [SerializeField] private float bandHigh = 0.55f;   // at/above = full
    [SerializeField] private bool debugLog = false;

    private const int SampleRate = 16000;
    private const int FftSize = 1024;

    private AudioClip _mic;
    private string _device;
    private float _next;
    private readonly float[] _buf = new float[FftSize];
    private readonly float[] _re = new float[FftSize];
    private readonly float[] _im = new float[FftSize];
    private float _last;

    private void Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            Permission.RequestUserPermission(Permission.Microphone);
#endif
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[Audio] No microphone found.");
            enabled = false;
            return;
        }

        _device = Microphone.devices[0];
        _mic = Microphone.Start(_device, true, 1, SampleRate); // 1 s looping buffer
        Debug.Log($"[Audio] Mic started: {_device}");
    }

    private void Update()
    {
        if (_mic == null || client == null || !client.isConnected) return;
        if (Time.time < _next) return;
        _next = Time.time + sendInterval;

        int pos = Microphone.GetPosition(_device);
        if (pos < FftSize) return; // wrapped just now; try next tick

        _mic.GetData(_buf, pos - FftSize);

        // RMS loudness
        double sum = 0;
        for (int i = 0; i < FftSize; i++) sum += _buf[i] * _buf[i];
        float rms = Mathf.Sqrt((float)(sum / FftSize));
        float loud = Mathf.Clamp01((rms - quietRms) / Mathf.Max(0.001f, loudRms - quietRms));

        float score = 0f;
        if (loud > 0.05f)
        {
            // Hann window + FFT
            for (int i = 0; i < FftSize; i++)
            {
                float w = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (FftSize - 1));
                _re[i] = _buf[i] * w;
                _im[i] = 0f;
            }
            FFT(_re, _im);

            float binHz = (float)SampleRate / FftSize;
            int lo = Mathf.RoundToInt(1000f / binHz), hi = Mathf.RoundToInt(4000f / binHz);
            double band = 0, total = 0;
            for (int k = 2; k < FftSize / 2; k++)
            {
                double e = _re[k] * _re[k] + _im[k] * _im[k];
                total += e;
                if (k >= lo && k <= hi) band += e;
            }
            float ratio = total > 1e-9 ? (float)(band / total) : 0f;
            float bandScore = Mathf.Clamp01((ratio - bandLow) / Mathf.Max(0.001f, bandHigh - bandLow));
            score = loud * bandScore;
        }

        _last = score;
        if (debugLog) Debug.Log($"[Audio] rms={rms:F3} scream={score:F2}");

        client.SendJson("{\"type\":\"audio\",\"scream\":" +
                        score.ToString("F2", CultureInfo.InvariantCulture) + "}");
    }

    private static void FFT(float[] re, float[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            float ang = -2f * Mathf.PI / len;
            float wr = Mathf.Cos(ang), wi = Mathf.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float cr = 1f, ci = 0f;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = i + k + len / 2;
                    float tr = re[b] * cr - im[b] * ci;
                    float ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti;
                    re[a] += tr; im[a] += ti;
                    float ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = ncr;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_device != null && Microphone.IsRecording(_device)) Microphone.End(_device);
    }
}