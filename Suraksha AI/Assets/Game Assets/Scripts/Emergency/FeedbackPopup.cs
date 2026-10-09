using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Suraksha.Sos
{
    public class FeedbackPopup : MonoBehaviour
    {
        [SerializeField] private UITweenerController popup;   
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [SerializeField] private float autoHideSeconds = 20f;

        public Func<string, bool> SendToServer;

        private float _hideAt;
        private bool _open;

        private void Awake()
        {
            if (popup != null && popup.gameObject == gameObject)
                Debug.LogError("[Feedback] FeedbackPopup must NOT be on the tweened panel. Put it on an always-active object.");

            if (popup != null)
            {
                if (!popup.gameObject.activeSelf) popup.gameObject.SetActive(true);   
                popup.SetInactive();
            }
            if (yesButton != null) yesButton.onClick.AddListener(() => Answer("real_threat"));
            if (noButton != null) noButton.onClick.AddListener(() => Answer("false_alarm"));
        }

        public void Show()
        {
            if (popup == null || _open) return;
            if (questionText != null) questionText.text = "Was that a real threat?";
            _open = true;
            _hideAt = Time.unscaledTime + autoHideSeconds;
            popup.Init();
        }

        private void Update()
        {
            if (_open && Time.unscaledTime >= _hideAt) Hide();
        }

        private void Answer(string label)
        {
            if (!_open) return;
            SendToServer?.Invoke("{\"type\":\"feedback\",\"label\":\"" + label + "\"}");
            Hide();
        }

        private void Hide()
        {
            if (!_open) return;
            _open = false;
            if (popup != null) popup.Hide();
        }
    }
}