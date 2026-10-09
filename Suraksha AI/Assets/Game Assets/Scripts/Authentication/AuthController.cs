using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Suraksha.Auth
{
    [RequireComponent(typeof(AuthView))]
    public class AuthController : MonoBehaviour
    {
        private const int OtpResendCooldownSeconds = 60;

        private AuthView authView;
        private AuthService authService;

        // Held in memory ONLY until the account is verified. Never touch PlayerPrefs before that,
        // or auto-login will skip OTP on the next launch.
        private string _pendingPhoneNumber;
        private string _pendingEmail;
        private string _pendingAccessToken;
        private string _pendingRefreshToken;

        private void Awake()
        {
            authView = GetComponent<AuthView>();
            authService = new AuthService();
        }

        private async void Start()
        {
            await TryAutoLoginAsync();
        }

        private void OnEnable()
        {
            authView.OnLoginClicked += HandleLogin;
            authView.OnForgotPasswordClicked += HandleForgotPassword;
            authView.OnSignUpClicked += HandleSignUp;
            authView.OnPhoneOTPClicked += HandleVerifyOtp;
            authView.OnResendOtpClicked += HandleResendOtp;
        }

        private void OnDisable()
        {
            authView.OnLoginClicked -= HandleLogin;
            authView.OnForgotPasswordClicked -= HandleForgotPassword;
            authView.OnSignUpClicked -= HandleSignUp;
            authView.OnPhoneOTPClicked -= HandleVerifyOtp;
            authView.OnResendOtpClicked -= HandleResendOtp;
        }

        private void SaveSession(string access, string refresh)
        {
            PlayerPrefs.SetString("access_token", access);
            PlayerPrefs.SetString("refresh_token", refresh);
            PlayerPrefs.Save();
        }

        private void ClearSession()
        {
            PlayerPrefs.DeleteKey("access_token");
            PlayerPrefs.DeleteKey("refresh_token");
            PlayerPrefs.Save();
        }

        private void ClearPending()
        {
            _pendingPhoneNumber = null;
            _pendingEmail = null;
            _pendingAccessToken = null;
            _pendingRefreshToken = null;
        }

        // OTP is looked up by phone number on the server and delivered to the account's email.
        private async Task StartPhoneVerificationAsync(string phoneNumber, string email)
        {
            _pendingPhoneNumber = phoneNumber;
            _pendingEmail = email;

            authView.OtpCleaned();
            authView.SetOtpPhoneLabel(email);   // popup label reads "Code sent to <email>"
            authView.phoneOTPPopup.Init();

            try
            {
                await authService.RequestPhoneOtpAsync(phoneNumber);
                authView.StartResendCooldown(OtpResendCooldownSeconds);
                Notification.Instance.ShowMessage("Success", $"Verification code sent to {email}");
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not send code. Tap Resend to try again.");
                Debug.LogError(ex.Message);
            }
        }

        public async void HandleSignUp(string email, string password, string fullName, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phoneNumber))
            {
                Notification.Instance.ShowMessage("Warning", "All fields are required for Sign Up.");
                return;
            }

            authView.SetInteractable(false);

            try
            {
                SignUpResponse response = await authService.SignUpAsync(email, password, fullName, phoneNumber);

                // NOT saved to PlayerPrefs yet: only after the OTP is verified
                _pendingAccessToken = response.access_token;
                _pendingRefreshToken = response.refresh_token;
                ClearSession(); // wipe any stale session from earlier tests
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not create account. Check your details.");
                Debug.LogError(ex.Message);
                authView.SetInteractable(true);
                return;
            }

            authView.signUpPopup.SetInactive();

            try
            {
                await StartPhoneVerificationAsync(phoneNumber, email.Trim());
            }
            finally
            {
                authView.SetInteractable(true);
            }
        }

        public async void HandleVerifyOtp(string otp)
        {
            if (string.IsNullOrWhiteSpace(otp))
            {
                Notification.Instance.ShowMessage("Warning", "Enter the code you received.");
                return;
            }
            if (string.IsNullOrEmpty(_pendingPhoneNumber))
            {
                Notification.Instance.ShowMessage("Error", "Nothing pending verification.");
                return;
            }

            authView.SetOtpInteractable(false);

            bool verified = false;
            try
            {
                await authService.VerifyPhoneOtpAsync(_pendingPhoneNumber, otp);
                verified = true;
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Incorrect or expired code. Try again.");
                Debug.LogError(ex.Message);
            }
            finally
            {
                authView.SetOtpInteractable(true);
                authView.OtpCleaned();
            }

            if (!verified) return;

            Notification.Instance.ShowMessage("Success", "Account verified!");
            authView.phoneOTPPopup.SetInactive();

            // Verified: NOW it's safe to persist the session
            bool hasSession = !string.IsNullOrEmpty(_pendingAccessToken);
            if (hasSession)
                SaveSession(_pendingAccessToken, _pendingRefreshToken);
            ClearPending();

            if (hasSession)
                await LoadMainSceneAsync();
        }

        public async void HandleResendOtp()
        {
            if (string.IsNullOrEmpty(_pendingPhoneNumber))
            {
                Notification.Instance.ShowMessage("Error", "Nothing pending verification.");
                return;
            }

            authView.SetOtpInteractable(false);

            try
            {
                await authService.RequestPhoneOtpAsync(_pendingPhoneNumber);
                Notification.Instance.ShowMessage("Success", $"New code sent to {_pendingEmail}");
                authView.StartResendCooldown(OtpResendCooldownSeconds);
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not resend code yet, wait a moment.");
                Debug.LogError(ex.Message);
            }
            finally
            {
                authView.SetOtpInteractable(true);
            }
        }

        public async void HandleLogin(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Notification.Instance.ShowMessage("Warning", "Email and password cannot be empty.");
                return;
            }

            authView.SetInteractable(false);

            try
            {
                LoginResponse response = await authService.LoginAsync(email,
                    password,
                    onSuccess: response => authView._loading.Init(),
                    onError: error => Notification.Instance.ShowMessage("Warning", $"{error}")
                );

                if (!response.phone_verified)
                {
                    authView._loading.SetInactive();   // onSuccess already showed the loading screen

                    _pendingAccessToken = response.access_token;
                    _pendingRefreshToken = response.refresh_token;
                    ClearSession();

                    Notification.Instance.ShowMessage("Warning", "Verify your account to continue.");
                    await StartPhoneVerificationAsync(response.phone_number, email.Trim());
                    authView.SetInteractable(true);
                    return;
                }

                SaveSession(response.access_token, response.refresh_token);

                Notification.Instance.ShowMessage("Success", $"Welcome back, {response.full_name}!");

                await LoadMainSceneAsync();
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Invalid email or password.");
                Debug.LogError(ex.Message);
                authView.SetInteractable(true);
            }
        }

        public async void HandleForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                Notification.Instance.ShowMessage("Warning", "Please enter your email address first.");
                return;
            }

            authView.SetInteractable(false);

            try
            {
                await authService.ForgotPasswordAsync(email);
                Notification.Instance.ShowMessage("Success", "Reset link sent to your email.");
                authView.forgotPasswordPopup.SetInactive();
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Failed to send reset link. Try again.");
                Debug.LogError(ex.Message);
            }
            finally
            {
                authView.SetInteractable(true);
            }
        }

        private async Task TryAutoLoginAsync()
        {
            if (!PlayerPrefs.HasKey("access_token"))
                return;

            authView.SetInteractable(false);

            try
            {
                bool isValid = await authService.ValidateSessionAsync();

                if (isValid)
                {
                    await LoadMainSceneAsync();
                    return;
                }

                ClearSession();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Auto-login failed: {ex.Message}");
            }
            finally
            {
                authView.SetInteractable(true);
            }
        }

        private async Task LoadMainSceneAsync()
        {
            const string sceneName = "MainScene";

            AsyncOperation operation =
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                throw new System.Exception(
                    $"Could not start loading scene: {sceneName}"
                );
            }

            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }
    }
}