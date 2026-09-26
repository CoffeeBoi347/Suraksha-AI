using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UITweenerController))]
public class Notification : MonoBehaviour
{
    public static Notification Instance { get; private set; }

    [Header("Text References")]
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _bodyText;

    [Header("UI Controls")]
    [SerializeField] private Button _closeButton;

    [Header("Auto Dismiss Settings")]
    [SerializeField] private bool _autoHide = true;
    [SerializeField] private float _defaultDisplayDuration = 2.5f;
    [SerializeField] private bool _useUnscaledTime = true;

    private UITweenerController _tweenerController;
    private Coroutine _autoHideCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _tweenerController = GetComponent<UITweenerController>();
    }

    private void Start()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(CloseNotification);
        }
    }

    private void OnDisable()
    {
        StopAutoHideCoroutine();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(CloseNotification);
        }
    }

    public void ShowMessage(string header, string message, float overrideDuration = -1f)
    {
        if (_tweenerController == null)
            _tweenerController = GetComponent<UITweenerController>();

        StopAutoHideCoroutine();

        if (_headerText != null) _headerText.text = header;
        if (_bodyText != null) _bodyText.text = message;

        _tweenerController.Show();

        if (_autoHide)
        {
            float duration = overrideDuration > 0f ? overrideDuration : _defaultDisplayDuration;
            _autoHideCoroutine = StartCoroutine(AutoHideRoutine(duration));
        }
    }

    public void CloseNotification()
    {
        StopAutoHideCoroutine();

        if (_tweenerController != null)
        {
            _tweenerController.Hide();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        if (_useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(delay);
        }
        else
        {
            yield return new WaitForSeconds(delay);
        }

        CloseNotification();
    }

    private void StopAutoHideCoroutine()
    {
        if (_autoHideCoroutine != null)
        {
            StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = null;
        }
    }
}