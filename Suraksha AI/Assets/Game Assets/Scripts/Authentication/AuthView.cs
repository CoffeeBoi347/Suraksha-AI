using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Suraksha.Auth
{
    public class AuthView : MonoBehaviour
    {
        [Header("Login")]
        [SerializeField] private TMP_InputField _emailField;
        [SerializeField] private TMP_InputField _passwordField;

        [Header("Sign Up")]
        [SerializeField] private TMP_InputField _emailFieldSignUp;
        [SerializeField] private TMP_InputField _passwordFieldSignUp;
        [SerializeField] private TMP_InputField _fullNameFieldSignUp;
        [SerializeField] private TMP_InputField _phoneFieldSignUp;

        [Header("Forgot Password")]
        [SerializeField] private TMP_InputField _resetPasswordInputField;

        [Header("Buttons")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _forgotPasswordButton;
        [SerializeField] private Button _signUpButton;

        [Header("Actions")]
        public Action<string, string, string, string> OnSignUpClicked;
        public Action<string, string> OnLoginClicked;
        public Action<string> OnForgotPasswordClicked;

        private void Start()
        {
            _loginButton.onClick.AddListener(() =>
                OnLoginClicked?.Invoke(_emailField.text.Trim(), _passwordField.text.Trim()));

            _forgotPasswordButton.onClick.AddListener(() =>
                OnForgotPasswordClicked?.Invoke(_resetPasswordInputField.text.Trim()));

            _signUpButton.onClick.AddListener(() =>
                OnSignUpClicked?.Invoke(
                    _emailFieldSignUp.text.Trim(),
                    _passwordFieldSignUp.text.Trim(),
                    _fullNameFieldSignUp.text.Trim(),
                    _phoneFieldSignUp.text.Trim()));
        }

        [Tooltip("TO USE: When user's network is down or awaiting response")]
        public void SetInteractable(bool state)
        {
            _emailField.interactable = state;
            _passwordField.interactable = state;
            _emailFieldSignUp.interactable = state;
            _passwordFieldSignUp.interactable = state;
            _fullNameFieldSignUp.interactable = state;
            _phoneFieldSignUp.interactable = state;

            _loginButton.interactable = state;
            _forgotPasswordButton.interactable = state;
            _signUpButton.interactable = state;
        }

        public void SignUpCleaned()
        {
            _emailFieldSignUp.text = string.Empty;
            _passwordFieldSignUp.text = string.Empty;
            _fullNameFieldSignUp.text = string.Empty;
            _phoneFieldSignUp.text = string.Empty;
        }

        public void ResetPasswordCleaned()
        {
            _resetPasswordInputField.text = string.Empty;
        }
    }
}