using System;

namespace Suraksha.Auth
{
    [Serializable]
    public class SignUpRequest
    {
        public string email;
        public string password;
        public string full_name;
        public string phone_number;
    }

    [Serializable]
    public class SignUpResponse
    {
        public string message;
        public string user_id;
        public string access_token;
        public string refresh_token;
        public string token_type;
    }

    [Serializable]
    public class LoginRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class LoginResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string token_type;
        public string user_id;
        public string full_name;
        public string phone_number;
    }

    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }
}