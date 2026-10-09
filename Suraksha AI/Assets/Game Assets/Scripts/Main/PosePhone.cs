using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using Unity.Sentis;
using UnityEngine;
using Debug = UnityEngine.Debug;

// On-device YOLO11n-pose. Unity 2022.3 + Sentis 2.x (com.unity.sentis 2.x).
// Put on any object next to WebcamCapture. Assign the .onnx ModelAsset. Dev build shows a debug overlay (OnGUI).
[RequireComponent(typeof(WebcamCapture))]
public class PosePhone : MonoBehaviour
{
    public class Person
    {
        public Rect box;            // normalised 0..1 in the square model input (letterboxed)
        public float conf;
        public Vector3[] kp = new Vector3[17];   // x,y normalised in the square input, z = keypoint confidence
    }

    [Header("Model")]
    [SerializeField] private ModelAsset model;                  // yolo11n-pose.onnx exported with imgsz=320
    [SerializeField] private int inputSize = 320;               // must match the export
    [SerializeField, Range(0.05f, 0.9f)] private float confThreshold = 0.35f;
    [SerializeField, Range(0.1f, 0.9f)] private float nmsIou = 0.5f;
    [SerializeField] private int maxPeople = 10;

    [Header("Pacing")]
    [SerializeField] private float targetFps = 10f;
    [SerializeField] private float maxMedianMs = 80f;           // slower than this -> DeviceOk = false (use server)

    [Header("Orientation fixes (use the debug view, change until the dots sit on the body)")]
    [SerializeField] private int rotationOffset = 0;            // 0, 90, 180, 270 added to the camera's rotation
    [SerializeField] private bool flipY = false;
    [SerializeField] private bool debugOverlay = true;

    public IReadOnlyList<Person> People => _people;
    public float AvgMs { get; private set; }
    public bool DeviceOk { get; private set; } = true;
    public bool Benchmarked { get; private set; }
    public event System.Action<IReadOnlyList<Person>> OnPeople;

    private WebcamCapture _cap;
    private Worker _worker;
    private Tensor<float> _input;
    private RenderTexture _rt;
    private readonly List<Person> _people = new();
    private readonly List<float> _bench = new();
    private bool _camUpdated;
    private float _next;
    private bool _running = true;
    private float _lastLog;

    private void Start()
    {
        _cap = GetComponent<WebcamCapture>();
        if (model == null) { Debug.LogError("[Pose] Assign the ONNX ModelAsset."); return; }

        var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
        _worker = new Worker(ModelLoader.Load(model), backend);
        _input = new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize));
        _rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        Debug.Log($"[Pose] backend={backend} device={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName}");

        StartCoroutine(RunLoop());
    }

    private void Update()
    {
        if (_cap != null && _cap.IsReady && _cap.Cam.didUpdateThisFrame) _camUpdated = true;
    }

    private IEnumerator RunLoop()
    {
        while (_running)
        {
            yield return null;
            if (!_running || _worker == null) break;
            if (!_camUpdated || !_cap.IsReady || Time.unscaledTime < _next) continue;
            _camUpdated = false;
            _next = Time.unscaledTime + 1f / targetFps;

            var sw = Stopwatch.StartNew();
            Letterbox();

            TextureConverter.ToTensor(_rt, _input, new TextureTransform());
            _worker.Schedule(_input);
            var output = _worker.PeekOutput() as Tensor<float>;
            output.ReadbackRequest();                        // async GPU -> CPU, no stall
            while (!output.IsReadbackRequestDone()) yield return null;
            using (var cpu = output.ReadbackAndClone())
                Decode(cpu);

            float ms = (float)sw.Elapsed.TotalMilliseconds;
            AvgMs = AvgMs <= 0 ? ms : Mathf.Lerp(AvgMs, ms, 0.1f);
            Benchmark(ms);
            if (Time.unscaledTime - _lastLog > 2f)
            {
                _lastLog = Time.unscaledTime;
                Debug.Log($"[Pose] avg {AvgMs:F0} ms, people {_people.Count}, ok={DeviceOk}");
            }
            OnPeople?.Invoke(_people);
        }
    }

    private void Benchmark(float ms)
    {
        if (Benchmarked) return;
        _bench.Add(ms);
        if (_bench.Count < 20) return;
        _bench.Sort();
        float median = _bench[_bench.Count / 2];
        DeviceOk = median <= maxMedianMs;
        Benchmarked = true;
        Debug.Log($"[Pose] benchmark median {median:F0} ms -> {(DeviceOk ? "ON-DEVICE OK" : "TOO SLOW, use server")}");
    }

    // Camera texture -> square model input, grey letterbox (114), rotated/mirrored to upright.
    private void Letterbox()
    {
        var cam = _cap.Cam;
        int rot = ((cam.videoRotationAngle + rotationOffset) % 360 + 360) % 360;
        bool swap = rot == 90 || rot == 270;
        float sw = swap ? cam.height : cam.width, sh = swap ? cam.width : cam.height;
        float s = Mathf.Min(inputSize / sw, inputSize / sh);
        float cw = cam.width * s, ch = cam.height * s;
        float S = inputSize;

        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        GL.Clear(true, true, new Color(114f / 255f, 114f / 255f, 114f / 255f, 1f));
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, S, S, 0);
        var c = new Vector3(S * 0.5f, S * 0.5f, 0);
        GL.MultMatrix(Matrix4x4.Translate(c) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, rot)) * Matrix4x4.Translate(-c));
        var r = new Rect((S - cw) * 0.5f, (S - ch) * 0.5f, cw, ch);
        bool flip = cam.videoVerticallyMirrored ^ flipY;
        if (flip) r = new Rect(r.x, r.yMax, r.width, -r.height);
        Graphics.DrawTexture(r, cam);
        GL.PopMatrix();
        RenderTexture.active = prev;
    }

    // YOLO pose ONNX output: [1, 56, N]  rows: cx, cy, w, h, conf, then 17 x (x, y, kpConf), all in input pixels.
    private void Decode(Tensor<float> t)
    {
        _people.Clear();
        float[] a = t.DownloadToArray();
        int N = t.shape[2];
        float S = inputSize;

        var cand = new List<Person>();
        for (int i = 0; i < N; i++)
        {
            float conf = a[4 * N + i];
            if (conf < confThreshold) continue;
            float cx = a[i], cy = a[N + i], w = a[2 * N + i], h = a[3 * N + i];
            var p = new Person { conf = conf, box = new Rect((cx - w / 2) / S, (cy - h / 2) / S, w / S, h / S) };
            for (int k = 0; k < 17; k++)
                p.kp[k] = new Vector3(a[(5 + 3 * k) * N + i] / S, a[(6 + 3 * k) * N + i] / S, a[(7 + 3 * k) * N + i]);
            cand.Add(p);
        }

        cand.Sort((x, y) => y.conf.CompareTo(x.conf));
        foreach (var p in cand)
        {
            bool keep = true;
            foreach (var q in _people)
                if (Iou(p.box, q.box) > nmsIou) { keep = false; break; }
            if (!keep) continue;
            _people.Add(p);
            if (_people.Count >= maxPeople) break;
        }
    }

    private static float Iou(Rect a, Rect b)
    {
        float ix = Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.x, b.x));
        float iy = Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y));
        float inter = ix * iy, u = a.width * a.height + b.width * b.height - inter;
        return u > 0 ? inter / u : 0;
    }

    private void OnGUI()
    {
        if (!debugOverlay || _rt == null) return;
        float size = Mathf.Min(Screen.width, Screen.height) * 0.6f;
        var area = new Rect(10, 10, size, size);
        GUI.DrawTexture(area, _rt);
        GUI.color = Color.green;
        foreach (var p in _people)
        {
            var b = new Rect(area.x + p.box.x * size, area.y + p.box.y * size, p.box.width * size, p.box.height * size);
            Line(new Rect(b.x, b.y, b.width, 2)); Line(new Rect(b.x, b.yMax, b.width, 2));
            Line(new Rect(b.x, b.y, 2, b.height)); Line(new Rect(b.xMax, b.y, 2, b.height));
            foreach (var k in p.kp)
                if (k.z > 0.4f) Line(new Rect(area.x + k.x * size - 3, area.y + k.y * size - 3, 6, 6));
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(area.x, area.yMax + 4, 400, 24), $"{AvgMs:F0} ms  people {_people.Count}  ok={DeviceOk}");
    }

    private static void Line(Rect r) => GUI.DrawTexture(r, Texture2D.whiteTexture);

    private void OnDestroy()
    {
        _running = false;
        _worker?.Dispose();
        _input?.Dispose();
        if (_rt != null) _rt.Release();
    }
}