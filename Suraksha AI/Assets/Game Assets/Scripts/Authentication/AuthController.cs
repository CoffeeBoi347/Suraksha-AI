using UnityEngine;

namespace Suraksha.Auth
{
    [RequireComponent(typeof(AuthView))]
    public class AuthController : MonoBehaviour
    {
        private AuthView authView;
        private AuthService authService;

        [SerializeField] private UITweenerController _forgotPasswordPopup;
        [SerializeField] private UITweenerController _signUpPopup;

        private void Awake()
        {
            authView = GetComponent<AuthView>();
            authService = new AuthService();
        }

        private void OnEnable()
        {
            authView.OnLoginClicked += HandleLogin;
            authView.OnForgotPasswordClicked += HandleForgotPassword;
            authView.OnSignUpClicked += HandleSignUp;
        }

        private void OnDisable()
        {
            authView.OnLoginClicked -= HandleLogin;
            authView.OnForgotPasswordClicked -= HandleForgotPassword;
            authView.OnSignUpClicked -= HandleSignUp;
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

                PlayerPrefs.SetString("access_token", response.access_token);
                PlayerPrefs.SetString("refresh_token", response.refresh_token);

                Notification.Instance.ShowMessage("Success", "Account created securely!");
                _signUpPopup.SetInactive();

            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Could not create account. Check your details.");
                Debug.LogError(ex.Message);
            }
            finally
            {
                authView.SetInteractable(true);
                _signUpPopup.SetInactive();
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
                LoginResponse response = await authService.LoginAsync(email, password);

                PlayerPrefs.SetString("access_token", response.access_token);
                PlayerPrefs.SetString("refresh_token", response.refresh_token);

                Notification.Instance.ShowMessage("Success", $"Welcome back, {response.full_name}!");
            }
            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Invalid email or password.");
                Debug.LogError(ex.Message);
            }
            finally
            {
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
                _forgotPasswordPopup.SetInactive();
            }

            catch (System.Exception ex)
            {
                Notification.Instance.ShowMessage("Error", "Failed to send reset link. Try again.");
                Debug.LogError(ex.Message);
            }
            finally
            {
                authView.SetInteractable(true);
                _forgotPasswordPopup.SetInactive();
            }
        }

        public void OpenForgotPassword() { authView.ResetPasswordCleaned();  _forgotPasswordPopup.Show(); }
        public void OpenSignUp() { authView.SignUpCleaned();  _signUpPopup.Show(); }
    }
}