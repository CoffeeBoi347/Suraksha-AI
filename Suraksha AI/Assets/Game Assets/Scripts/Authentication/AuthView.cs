using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Note: needs UnityEngine.UI.Image for the eye icon sprite swap

namespace Suraksha.Auth
{
    public class AuthView : MonoBehaviour
    {
        [Header("Popups")]
        public UITweenerController signUpPopup;
        public UITweenerController forgotPasswordPopup;
        public UITweenerController phoneOTPPopup;

        [Header("Screens")]
        public UITweenerController _loading;

        [Header("Login")]
        [SerializeField] private TMP_InputField _emailField;
        [SerializeField] private TMP_InputField _passwordField;
        [SerializeField] private Button _loginPasswordEyeButton;
        [SerializeField] private Image _loginPasswordEyeIcon;

        [Header("Sign Up")]
        [SerializeField] private TMP_InputField _emailFieldSignUp;
        [SerializeField] private TMP_InputField _passwordFieldSignUp;
        [SerializeField] private Button _signUpPasswordEyeButton;
        [SerializeField] private Image _signUpPasswordEyeIcon;
        [SerializeField] private TMP_InputField _fullNameFieldSignUp;
        [SerializeField] private TMP_InputField _phoneFieldSignUp;

        [Header("Password Visibility Sprites")]
        [Tooltip("Icon shown while the password is hidden (****)")]
        [SerializeField] private Sprite _eyeClosedSprite;
        [Tooltip("Icon shown while the password is visible (plain text)")]
        [SerializeField] private Sprite _eyeOpenSprite;

        [Header("Forgot Password")]
        [SerializeField] private TMP_InputField _resetPasswordInputField;

        [Header("Phone OTP")]
        [SerializeField] private TMP_InputField _otpInputField;
        [SerializeField] private Button _submitOtpButton;
        [SerializeField] private Button _resendOtpButton;
        [SerializeField] private TextMeshProUGUI _resendOtpButtonLabel; 
        [SerializeField] private TextMeshProUGUI _otpPhoneLabel; 

        private Coroutine _resendCooldownRoutine;
        private const string ResendDefaultText = "Resend OTP";

        [Header("Buttons")]
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _forgotPasswordButton;
        [SerializeField] private Button _signUpButton;

        [Header("Actions")]
        public Action<string, string, string, string> OnSignUpClicked;
        public Action<string, string> OnLoginClicked;
        public Action<string> OnForgotPasswordClicked;
        public Action<string> OnPhoneOTPClicked;
        public Action OnResendOtpClicked;

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

            _submitOtpButton.onClick.AddListener(() =>
                OnPhoneOTPClicked?.Invoke(_otpInputField.text.Trim()));

            _resendOtpButton.onClick.AddListener(() =>
                OnResendOtpClicked?.Invoke());

            SetupPasswordToggle(_passwordField, _loginPasswordEyeButton, _loginPasswordEyeIcon);
            SetupPasswordToggle(_passwordFieldSignUp, _signUpPasswordEyeButton, _signUpPasswordEyeIcon);
        }

        private void SetupPasswordToggle(TMP_InputField field, Button eyeButton, Image eyeIcon)
        {
            if (field == null) return;

            field.contentType = TMP_InputField.ContentType.Password;
            field.asteriskChar = '*';
            field.ForceLabelUpdate();

            if (eyeIcon != null && _eyeClosedSprite != null)
                eyeIcon.sprite = _eyeClosedSprite;

            if (eyeButton == null) return;

            bool visible = false;
            eyeButton.onClick.AddListener(() =>
            {
                visible = !visible;
                field.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                field.asteriskChar = '*';
                field.ForceLabelUpdate();

                if (eyeIcon != null)
                    eyeIcon.sprite = visible ? _eyeOpenSprite : _eyeClosedSprite;
            });
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

        public void SetOtpInteractable(bool state)
        {
            _otpInputField.interactable = state;
            _submitOtpButton.interactable = state;
            _resendOtpButton.interactable = state;
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

        public void OtpCleaned()
        {
            _otpInputField.text = string.Empty;
        }

        public void SetOtpPhoneLabel(string phoneNumber)
        {
            if (_otpPhoneLabel != null)
                _otpPhoneLabel.text = $"Code sent to {phoneNumber}";
        }

        public void StartResendCooldown(int seconds = 30)
        {
            if (_resendCooldownRoutine != null) StopCoroutine(_resendCooldownRoutine);
            _resendCooldownRoutine = StartCoroutine(ResendCooldownRoutine(seconds));
        }

        private IEnumerator ResendCooldownRoutine(int seconds)
        {
            _resendOtpButton.interactable = false;
            int remaining = seconds;
            while (remaining > 0)
            {
                if (_resendOtpButtonLabel != null) _resendOtpButtonLabel.text = $"Resend in {remaining}s";
                yield return new WaitForSeconds(1f);
                remaining--;
            }
            if (_resendOtpButtonLabel != null) _resendOtpButtonLabel.text = ResendDefaultText;
            _resendOtpButton.interactable = true;
            _resendCooldownRoutine = null;
        }

        public void OpenForgotPassword() { ResetPasswordCleaned(); forgotPasswordPopup.Show(); }
        public void OpenSignUp() { SignUpCleaned(); signUpPopup.Show(); }
    }
}