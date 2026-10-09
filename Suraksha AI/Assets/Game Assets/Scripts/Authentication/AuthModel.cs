using System;

namespace Suraksha.Auth
{
    // ---------- Request Models ----------

    [Serializable]
    public class SignUpRequest
    {
        public string email;
        public string password;
        public string full_name;
        public string phone_number;
    }

    [Serializable]
    public class LoginRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }

    [Serializable]
    public class PhoneOtpRequest
    {
        public string phone_number;
    }

    [Serializable]
    public class PhoneOtpVerifyRequest
    {
        public string phone_number;
        public string otp;
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
    public class LoginResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string token_type;
        public string user_id;
        public string full_name;
        public string phone_number;
        public bool phone_verified;
    }

    [Serializable]
    public class MessageResponse
    {
        public string message;
    }

    [System.Serializable]
    public class RefreshRequest
    {
        public string refresh_token;
    }

    [System.Serializable]
    public class RefreshResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string token_type;
    }

    [System.Serializable]
    public class CurrentUserResponse
    {
        public string user_id;
        public string email;
        public string full_name;
        public string phone_number;
        public string created_at;
    }
}