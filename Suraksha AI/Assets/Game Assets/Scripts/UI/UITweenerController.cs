using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UITweenerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private RectTransform _rect;

    [Header("Style")]
    [SerializeField] private TweenModes _animationMode;
    [SerializeField] private float _slideDistance = 40f;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("In")]
    [SerializeField] private float _duration = 0.25f;
    [SerializeField] private float _delay = 0f;
    [SerializeField] private EaseType _easeType = EaseType.EaseOutCubic;

    [Header("Out")]
    [SerializeField] private float _outDuration = 0.2f;
    [SerializeField] private EaseType _outEaseType = EaseType.EaseInCubic;

    private Vector3 _originalScale;
    private Vector2 _originalAnchoredPosition;
    private Coroutine _activeTween;
    private bool _isInitialized;

    private float _visibility;

    private bool _targetShown;
    public bool IsShown => _targetShown;

    void Awake()
    {
        EnsureReferences();
        _visibility = gameObject.activeSelf ? 1f : 0f;
        _targetShown = _visibility > 0f;
    }

    void OnDisable()
    {
        _activeTween = null;
    }

    private void EnsureReferences()
    {
        if (_isInitialized) return;

        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_cg == null) _cg = GetComponent<CanvasGroup>();

        _originalScale = _rect.localScale;
        _originalAnchoredPosition = _rect.anchoredPosition;
        _isInitialized = true;
    }

    public void Show(Action onDone = null) => Play(true, onDone);
    public void Hide(Action onDone = null) => Play(false, onDone);

    public void Play(bool show, Action onDone = null)
    {
        EnsureReferences();
        StopActiveTween();
        _targetShown = show;

        if (show)
        {
            gameObject.SetActive(true);
        }
        else if (!gameObject.activeInHierarchy)
        {
            _visibility = 0f;
            onDone?.Invoke();
            return;
        }

        _activeTween = StartCoroutine(Run(show, onDone));
    }

    public bool TryInit(bool init)
    {
        EnsureReferences();

        if (init) Show();
        else SetInactive();

        return true;
    }

    public void Init() => Show();

    public void ChangeTweenMode(EaseType type) => _easeType = type;
    public void ChangeOutEase(EaseType type) => _outEaseType = type;

    public void StopShimmer()
    {
        if (_animationMode != TweenModes.Shimmer) return;
        StopActiveTween();
        _cg.alpha = 1f;
    }

    public void SetInactive()
    {
        StopActiveTween();
        EnsureReferences();
        _targetShown = false;
        _visibility = 0f;
        Apply(0f);
        SetUIState(false);
        gameObject.SetActive(false);
    }

    public void SetActive()
    {
        StopActiveTween();
        EnsureReferences();
        _targetShown = true;
        _visibility = 1f;
        Apply(1f);
        SetUIState(true);
        gameObject.SetActive(true);
    }

    private IEnumerator Run(bool show, Action onDone)
    {
        SetUIState(false);

        if (show && _delay > 0f)
        {
            if (_visibility >= 1f) _visibility = 0f;
            Apply(_visibility);

            float d = 0f;
            while (d < _delay)
            {
                d += DeltaTime();
                yield return null;
            }
        }

        float from = _visibility;
        float to = show ? 1f : 0f;
        float dur = show ? _duration : _outDuration;
        EaseType ease = show ? _easeType : _outEaseType;

        dur *= Mathf.Max(0.0001f, Mathf.Abs(to - from));

        float t = 0f;
        while (t < dur)
        {
            t += DeltaTime();
            float k = Ease(ease, Mathf.Min(t / dur, 1f));
            _visibility = Mathf.LerpUnclamped(from, to, k);
            Apply(_visibility);
            yield return null;
        }

        _visibility = to;
        Apply(to);

        if (show)
        {
            SetUIState(true);
            _activeTween = null;
            onDone?.Invoke();

            if (_animationMode == TweenModes.Shimmer)
            {
                _activeTween = StartCoroutine(ShimmerLoop());
            }
        }
        else
        {
            gameObject.SetActive(false);
            _activeTween = null;
            onDone?.Invoke();
        }
    }

    private IEnumerator ShimmerLoop()
    {
        SetUIState(true);
        while (true)
        {
            float time = _useUnscaledTime ? Time.unscaledTime : Time.time;
            float t = (Mathf.Sin(time * 2f) + 1f) * 0.5f;
            _cg.alpha = Mathf.Lerp(0.5f, 1f, t);
            yield return null;
        }
    }
    private void Apply(float v)
    {
        float clamped = Mathf.Clamp01(v);

        switch (_animationMode)
        {
            case TweenModes.Fade:
            case TweenModes.Shimmer:
                _cg.alpha = clamped;
                break;

            case TweenModes.PopIn:
            case TweenModes.PopOut:
                _rect.localScale = _originalScale * Mathf.Max(0f, v);
                _cg.alpha = clamped;
                break;

            case TweenModes.Bounce:
                _rect.localScale = _originalScale * Mathf.Max(0f, v);
                _cg.alpha = Mathf.Clamp01(v * 1.5f);
                break;

            case TweenModes.SlideUp:
                _rect.anchoredPosition = _originalAnchoredPosition + new Vector2(0f, -_slideDistance) * (1f - v);
                _cg.alpha = clamped;
                break;

            case TweenModes.SlideDown:
                _rect.anchoredPosition = _originalAnchoredPosition + new Vector2(0f, _slideDistance) * (1f - v);
                _cg.alpha = clamped;
                break;
        }
    }

    private float DeltaTime() => _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private void StopActiveTween()
    {
        if (_activeTween != null)
        {
            StopCoroutine(_activeTween);
            _activeTween = null;
        }
    }

    private void SetUIState(bool interactable)
    {
        _cg.interactable = interactable;
        _cg.blocksRaycasts = interactable;
    }

    private static float Ease(EaseType type, float p)
    {
        switch (type)
        {
            case EaseType.EaseOutCubic:
                return 1f - Mathf.Pow(1f - p, 3f);
            case EaseType.EaseOutQuint:
                return 1f - Mathf.Pow(1f - p, 5f);
            case EaseType.EaseOutBack:
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                return 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            case EaseType.EaseInOutCubic:
                return p < 0.5f
                    ? 4f * p * p * p
                    : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
            case EaseType.EaseInCubic:
                return p * p * p;
            default:
                return p;
        }
    }
}

public enum TweenModes { Fade, PopIn, PopOut, SlideUp, SlideDown, Bounce, Shimmer }
public enum EaseType { Linear, EaseOutCubic, EaseOutQuint, EaseOutBack, EaseInOutCubic, EaseInCubic }