using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthView : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField _emailField;
    [SerializeField] private TMP_InputField _passwordField;

    [Header("Buttons")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _forgotPasswordButton;

    [Header("Actions")]
    public Action<string, string> OnLoginClicked;
    public Action<string> OnForgotPasswordClicked;

    private void Start()
    {
        _loginButton.onClick.AddListener(() => OnLoginClicked?.Invoke(_emailField.text, _passwordField.text));
        _forgotPasswordButton.onClick.AddListener(() => OnForgotPasswordClicked?.Invoke(_emailField.text));
    }

    [Tooltip("TO USE: When user's network is down")]
    public void SetInteractable(bool state)
    {
        _emailField.interactable = state;
        _passwordField.interactable = state;
        _loginButton.interactable = state;
        _forgotPasswordButton.interactable= state;
    }
}
