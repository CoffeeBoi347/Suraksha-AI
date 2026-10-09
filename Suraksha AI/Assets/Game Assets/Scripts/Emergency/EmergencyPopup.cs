using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Suraksha.Sos
{
    [Serializable]
    public class SosDetails
    {
        public string source, pattern, instruction, thumb_b64;
        public float confidence, distance_m;
    }

    [Serializable]
    public class SosMessage
    {
        public string type, title, reason;
        public int seconds;
        public SosDetails details;
    }

    public class EmergencyPopup : MonoBehaviour
    {
        private const string MANUAL_REASON = "Manual SOS";

        [Header("UI")]
        [SerializeField] private UITweenerController popup;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private RawImage thumbImage;
        [SerializeField] private FeedbackPopup feedback;  

        [Header("Behaviour")]
        [SerializeField] private int defaultSeconds = 10;
        [SerializeField] private float closeIfNoReplySeconds = 15f;

        public Func<string, bool> SendToServer;

        private Coroutine _routine;
        private bool _open;
        private bool _answered;
        private bool _manual;
        private bool _askFeedback;
        private Texture2D _thumb;
        private float _lastManualOpen = -999f;

        private void Awake()
        {
            if (popup != null && popup.gameObject == gameObject)
                Debug.LogError("[SOS] EmergencyPopup must NOT be on the tweened panel. Move it to an always-active object.");

            WarmUpPopup();

            if (sendButton != null) sendButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (closeButton != null) closeButton.onClick.AddListener(Cancel);
        }

        private void WarmUpPopup()
        {
            if (popup == null) return;
            if (!popup.gameObject.activeSelf)
                popup.gameObject.SetActive(true);
            popup.SetInactive();
        }

        public bool TryHandle(string json)
        {
            SosMessage m;
            try { m = JsonUtility.FromJson<SosMessage>(json); }
            catch { return false; }
            if (m == null || string.IsNullOrEmpty(m.type)) return false;

            switch (m.type)
            {
                case "sos_prompt":
                    Open(m);
                    return true;
                case "sos_fired":
                    Debug.Log("[SOS] Server confirmed SOS sent.");
                    Close();
                    return true;
                case "sos_cancelled":
                case "sos_already_sent":
                    Close();
                    return true;
                case "sos_failed":
                    Debug.LogError("[SOS] Server reported SOS failure: dialling 112.");
                    _askFeedback = false;
                    Close();
                    Application.OpenURL("tel:112");
                    return true;
                default:
                    return false;
            }
        }

        private void Open(SosMessage m)
        {
            if (_open) return;

            if (popup == null)
            {
                Debug.LogError("[SOS] popup reference missing.");
                return;
            }

            popup.Init();
            if (!popup.gameObject.activeInHierarchy)
            {
                Debug.LogError("[SOS] popup panel didn't activate (parent inactive?).");
                return;
            }

            _open = true;
            _answered = false;
            _manual = m.reason == MANUAL_REASON;
            _askFeedback = !_manual;          
            SetButtons(true);
            ShowThumb(m.details != null ? m.details.thumb_b64 : null);

            _routine = StartCoroutine(CountdownRoutine(m.seconds > 0 ? m.seconds : defaultSeconds));
            Debug.Log($"[SOS] Popup opened ({(_manual ? "manual" : "server")})");
        }

        private IEnumerator CountdownRoutine(int seconds)
        {
            for (int left = seconds; left > 0; left--)
            {
                SetText($"Emergency messages will be sent to your contacts in {left} seconds.");
                yield return new WaitForSecondsRealtime(1f);
            }
            Confirm();
        }

        private void Confirm()
        {
            if (!_open || _answered) return;
            _answered = true;
            StopRoutine();
            SetButtons(false);
            SetText("Sending emergency messages...");

            string msg = _manual ? "{\"type\":\"manual_sos\"}" : "{\"type\":\"confirm_sos\"}";
            bool sent = SendToServer != null && SendToServer(msg);

            if (!sent)
            {
                Debug.LogError("[SOS] Not connected: dialling 112.");
                _askFeedback = false;
                SetText("No connection. Calling 112.");
                Application.OpenURL("tel:112");
                _routine = StartCoroutine(CloseAfter(2f, false));
                return;
            }

            Debug.Log($"[SOS] Sent {msg}");
            _routine = StartCoroutine(CloseAfter(closeIfNoReplySeconds, true));
        }

        private void Cancel()
        {
            if (!_open || _answered) return;
            _answered = true;
            StopRoutine();
            if (!_manual)
                SendToServer?.Invoke("{\"type\":\"cancel_sos\"}");
            Close();
        }

        private IEnumerator CloseAfter(float seconds, bool dialIfNoReply)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _routine = null;
            if (dialIfNoReply)
            {
                Debug.LogError("[SOS] Server never confirmed: dialling 112.");
                _askFeedback = false;
                Application.OpenURL("tel:112");
            }
            Close();
        }

        private void Close()
        {
            StopRoutine();
            bool ask = _open && _askFeedback;
            _open = false;
            _answered = false;
            _manual = false;
            _askFeedback = false;
            if (popup != null) popup.SetInactive();
            if (ask && feedback != null) feedback.Show();
        }

        private void StopRoutine()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        }

        private void SetButtons(bool on)
        {
            if (sendButton != null) sendButton.interactable = on;
            if (cancelButton != null) cancelButton.interactable = on;
            if (closeButton != null) closeButton.interactable = on;
        }

        private void SetText(string t)
        {
            if (countdownText != null) countdownText.text = t;
        }

        private void ShowThumb(string b64)
        {
            if (thumbImage == null) return;
            if (_thumb != null) { Destroy(_thumb); _thumb = null; }
            if (string.IsNullOrEmpty(b64)) { thumbImage.gameObject.SetActive(false); return; }
            try
            {
                _thumb = new Texture2D(2, 2);
                _thumb.LoadImage(Convert.FromBase64String(b64));
                thumbImage.texture = _thumb;
                thumbImage.gameObject.SetActive(true);
            }
            catch { thumbImage.gameObject.SetActive(false); }
        }

        public void OpenManually()
        {
            if (_open) return;
            if (Time.unscaledTime - _lastManualOpen < 2f) return;   
            _lastManualOpen = Time.unscaledTime;

            Open(new SosMessage
            {
                type = "sos_prompt",
                title = "Emergency Assistance",
                reason = MANUAL_REASON,
                seconds = 0,
                details = null
            });
        }

        private void OnDestroy()
        {
            if (_thumb != null) Destroy(_thumb);
        }
    }
}