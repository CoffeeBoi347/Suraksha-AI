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

        private string _pendingPhoneNumber;

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

                if (!string.IsNullOrEmpty(response.access_token))
                {
                    PlayerPrefs.SetString("access_token", response.access_token);
                    PlayerPrefs.SetString("refresh_token", response.refresh_token);
                }

                _pendingPhoneNumber = phoneNumber;
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not create account. Check your details.");
                Debug.LogError(ex.Message);
                authView.SetInteractable(true);
                return;
            }

            Notification.Instance.ShowMessage("Success", "Account created securely!");
            authView.signUpPopup.SetInactive();

            authView.OtpCleaned();
            authView.SetOtpPhoneLabel(phoneNumber);
            authView.phoneOTPPopup.Init();

            try
            {
                await authService.RequestPhoneOtpAsync(phoneNumber);
                authView.StartResendCooldown(OtpResendCooldownSeconds);
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not send code. Tap Resend to try again.");
                Debug.LogError(ex.Message);
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
                Notification.Instance.ShowMessage("Error", "No phone number pending verification.");
                return;
            }

            authView.SetOtpInteractable(false);

            try
            {
                await authService.VerifyPhoneOtpAsync(_pendingPhoneNumber, otp);

                Notification.Instance.ShowMessage("Success", "Phone number verified!");
                authView.phoneOTPPopup.SetInactive();
                _pendingPhoneNumber = null;
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
        }

        public async void HandleResendOtp()
        {
            if (string.IsNullOrEmpty(_pendingPhoneNumber))
            {
                Notification.Instance.ShowMessage("Error", "No phone number pending verification.");
                return;
            }

            authView.SetOtpInteractable(false);

            try
            {
                await authService.RequestPhoneOtpAsync(_pendingPhoneNumber);
                Notification.Instance.ShowMessage("Success", "New code sent.");
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

                PlayerPrefs.SetString("access_token", response.access_token);
                PlayerPrefs.SetString("refresh_token", response.refresh_token);

                Notification.Instance.ShowMessage("Success", $"Welcome back, {response.full_name}!");

                await LoadMainSceneAsync();
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Invalid email or password.");
                Debug.LogError(ex.Message);
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

                PlayerPrefs.DeleteKey("access_token");
                PlayerPrefs.DeleteKey("refresh_token");
                PlayerPrefs.Save();
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