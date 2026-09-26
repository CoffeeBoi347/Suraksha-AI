using UnityEngine;

namespace Suraksha.Auth
{
    [RequireComponent(typeof(AuthView))]
    public class AuthController : MonoBehaviour
    {
        private AuthView authView;
        private AuthService authService;

        private void Awake()
        {
            authView = GetComponent<AuthView>();
            authService = new AuthService();
        }

        private void OnEnable()
        {
            authView.OnLoginClicked += HandleLogin;
            authView.OnForgotPasswordClicked += HandleForgotPassword;
        }

        private void OnDisable()
        {
            authView.OnLoginClicked -= HandleLogin;
            authView.OnForgotPasswordClicked -= HandleForgotPassword;
        }

        public void HandleLogin(string email, string password) { }
        public void HandleForgotPassword(string email) { }
    }
}