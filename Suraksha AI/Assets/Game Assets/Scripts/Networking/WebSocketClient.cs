using NativeWebSocket;
using Suraksha.Sos;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class AlertMessage
{
    public string type;
    public string unity_instruction;
    public string reason;
    public int seconds_left;
    public float scream;
    public int snooze_s;
}

public class WebSocketClient : MonoBehaviour
{
    private WebSocket _webSocket;

    private bool _isConnecting;
    private bool _hasReceivedFirstFrame;
    private bool _isShuttingDown;
    private bool _reconnectAfterClose;
    private float _lastManualSosTime = -999f;

    public bool isConnected =>
        _webSocket != null &&
        _webSocket.State == WebSocketState.Open;

    [Header("References")]
    [SerializeField] private Loading _loading;
    [SerializeField] private WebcamCapture _cameraCapture;
    [SerializeField] private BoundingBoxOverlay _boundingBoxOverlay;
    [SerializeField] private EmergencyPopup _emergencyPopup;

    public event Action<AlertMessage> OnAlert;

    private void Start()
    {
        if (_loading == null)
        {
            Debug.LogError("[Vision] Loading reference is missing.");
            return;
        }

        if (_cameraCapture == null)
        {
            Debug.LogError("[Vision] Camera Capture reference is missing.");
            return;
        }

        if (_boundingBoxOverlay == null)
            Debug.LogWarning("[Vision] BoundingBoxOverlay reference is missing.");

        if (_emergencyPopup == null)
            Debug.LogWarning("[Vision] EmergencyPopup reference is missing: the SOS popup will never open.");
        else
            _emergencyPopup.SendToServer = SendJson;   // Send / Cancel / countdown timeout go out over this socket.

        _ = Connect();
    }

    // Returns true only if the message went out on an open socket.
    public bool SendJson(string json)
    {
        if (!isConnected)
        {
            Debug.LogWarning($"[Vision] Not connected, dropped: {json}");
            return false;
        }

        _ = SendTextSafe(json);
        return true;
    }

    private async Task SendTextSafe(string json)
    {
        try
        {
            await _webSocket.SendText(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Vision] Failed to send text: {ex.Message}\n{json}");
        }
    }

    public void SendCancelSOS() => SendJson("{\"type\":\"cancel_sos\"}");
    public void SendManualSOS()
    {
        // Client-side debounce: the server also limits manual SOS, this just stops double-taps.
        if (Time.unscaledTime - _lastManualSosTime < 3f)
            return;
        _lastManualSosTime = Time.unscaledTime;
        SendJson("{\"type\":\"manual_sos\"}");
    }

    // Dev only. Needs SOS_TEST_POPUP=1 in the backend .env. In Play mode: right-click this component > Test SOS Popup.
    // Press Cancel in the popup when testing: Send is a REAL SOS.
    [ContextMenu("Test SOS Popup")]
    public void SendTestPopup() => SendJson("{\"type\":\"test_popup\"}");

    public void SendFalseAlarm() => SendJson("{\"type\":\"feedback\",\"label\":\"false_alarm\"}");
    public void SendRealThreat() => SendJson("{\"type\":\"feedback\",\"label\":\"real_threat\"}");
    public void SendUnsure() => SendJson("{\"type\":\"feedback\",\"label\":\"unsure\"}");

    public async Task Connect()
    {
        if (isConnected || _isConnecting || _isShuttingDown)
            return;

        string token = PlayerPrefs.GetString("access_token", "");

        if (string.IsNullOrWhiteSpace(token))
        {
            SetLoading("Please log in to continue.");
            Debug.LogError("[Vision] No access token found.");
            return;
        }

        _isConnecting = true;
        _hasReceivedFirstFrame = false;

        SetLoading("Connecting to Suraksha...");

        try
        {
            string url = Constants.SOCKET_URL + Constants.MAIN_TYPE + "?hfov=65";
            Debug.Log($"[Vision] Connecting to {url}");

            var headers = new Dictionary<string, string> { { "Authorization", "Bearer " + token } };
            var socket = new WebSocket(url, headers);
            _webSocket = socket;

            socket.OnOpen += () =>
            {
                if (socket != _webSocket || _isShuttingDown)
                    return;

                Debug.Log("[Vision] WebSocket connected.");
                _isConnecting = false;
                SetLoading("Connected. Starting AI vision...");
            };

            socket.OnError += error =>
            {
                if (socket != _webSocket)
                    return;

                Debug.LogError($"[Vision] WebSocket error: {error}");
                _isConnecting = false;
                SetLoading("Connection error. Please try again.");
            };

            socket.OnClose += code =>
            {
                if (socket != _webSocket)
                    return;

                Debug.Log($"[Vision] WebSocket closed: {code}");
                _isConnecting = false;

                if (_reconnectAfterClose && !_isShuttingDown)
                {
                    _reconnectAfterClose = false;
                    _ = ReconnectSoon();
                }
                else if (!_isShuttingDown)
                    SetLoading("Connection lost. Please reconnect.");
            };

            socket.OnMessage += data =>
            {
                if (socket != _webSocket || _isShuttingDown)
                    return;

                HandleMessage(data);
            };

            await socket.Connect();
        }
        catch (Exception ex)
        {
            _isConnecting = false;
            Debug.LogError($"[Vision] Connection failed: {ex.Message}");
            SetLoading("Unable to connect. Please try again.");
        }
    }

    private async Task ReconnectSoon()
    {
        await Task.Delay(1000);
        if (!_isShuttingDown)
            await Connect();
    }

    private void HandleMessage(byte[] data)
    {
        string message = Encoding.UTF8.GetString(data);

        try
        {
            DataPayload payload = JsonUtility.FromJson<DataPayload>(message);

            if (payload == null)
            {
                Debug.LogWarning("[Vision] Empty or invalid JSON response.");
                return;
            }

            if (payload.type == "frame")
            {
                if (!_hasReceivedFirstFrame)
                {
                    _hasReceivedFirstFrame = true;
                    Debug.Log("[Vision] First AI frame received.");
                    SetLoadingVisible(false);
                }

                if (_boundingBoxOverlay != null)
                    _boundingBoxOverlay.UpdateBoxes(payload.targets);

                if (payload.targets == null)
                    return;

                foreach (ThreatBox target in payload.targets)
                {
                    if (target == null || target.level == "NORMAL")
                        continue;

                    Debug.Log(
                        $"[Vision] ID: {target.id} | Level: {target.level} | " +
                        $"Distance: {target.distance_m}m | {target.threat_reasoning}"
                    );
                }
                return;
            }

            // Server ended a long session on purpose: reconnect quietly.
            if (payload.type == "session_limit")
            {
                Debug.Log("[Vision] Session limit reached, reconnecting.");
                _reconnectAfterClose = true;
                return;
            }

            // SOS messages: sos_prompt opens the popup; sos_fired / sos_failed / sos_cancelled / sos_already_sent close it.
            if (payload.type != null && payload.type.StartsWith("sos_"))
            {
                Debug.Log($"[Vision] SOS message: {message}");
                if (_emergencyPopup != null && _emergencyPopup.TryHandle(message))
                    return;
            }

            AlertMessage alert = JsonUtility.FromJson<AlertMessage>(message);
            Debug.Log($"[Vision] Event: {payload.type} | {alert?.unity_instruction}");
            OnAlert?.Invoke(alert);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Vision] Failed to parse message: {ex.Message}\n{message}");
        }
    }

    private void Update()
    {
        _webSocket?.DispatchMessageQueue();

        if (!isConnected || _cameraCapture == null)
            return;

        if (!_cameraCapture.CanCapture())
            return;

        byte[] frame = _cameraCapture.GetCameraFrameJPG();

        if (frame == null)
            return;

        _ = SendFrame(frame);
    }

    private async Task SendFrame(byte[] frame)
    {
        WebSocket socket = _webSocket;

        if (socket == null || socket.State != WebSocketState.Open)
            return;

        try
        {
            await socket.Send(frame);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Vision] Failed to send frame: {ex.Message}");
        }
    }

    private void SetLoading(string message)
    {
        if (_loading == null)
            return;

        _loading.SetText(message);
        _loading.Show();
    }

    private void SetLoadingVisible(bool visible)
    {
        if (_loading == null)
            return;

        if (visible) _loading.Show();
        else _loading.Close();
    }

    public async Task Disconnect()
    {
        WebSocket socket = _webSocket;

        if (socket == null)
            return;

        _webSocket = null;
        _isConnecting = false;
        _hasReceivedFirstFrame = false;

        if (_boundingBoxOverlay != null)
            _boundingBoxOverlay.ClearBoxes();

        try
        {
            await socket.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Vision] Disconnect error: {ex.Message}");
        }
    }

    private async void OnApplicationQuit()
    {
        _isShuttingDown = true;
        await Disconnect();
    }

    private async void OnDestroy()
    {
        _isShuttingDown = true;
        await Disconnect();
    }
}