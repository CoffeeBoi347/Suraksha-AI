using System;
using System.Collections.Generic;

[Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[Serializable]
public class SignUpRequest
{
    public string email;
    public string password;
    public string full_name;
    public string phone_number;
}

[Serializable]
public class RefreshRequest
{
    public string refresh_token;
}

[Serializable]
public class ForgotPasswordRequest
{
    public string email;
}

[Serializable]
public class ResetPasswordRequest
{
    public string access_token;
    public string new_password;
}

[Serializable]
public class PhoneOTPRequest
{
    public string phone_number;
}

[Serializable]
public class PhoneOTPVerifyRequest
{
    public string phone_number;
    public string otp;
}


[Serializable]
public class AuthResponse
{
    public string access_token;
    public string refresh_token;
    public int expires_in;
    public string token_type;

    public string user_id;
    public string full_name;
    public string phone_number;
    public string email;
    public string message;
}

[Serializable]
public class RefreshResponse
{
    public string access_token;
    public string refresh_token;
    public int expires_in;
    public string token_type;
}

[Serializable]
public class CurrentUserResponse
{
    public string user_id;
    public string email;
    public string full_name;
    public string phone_number;
    public string created_at;
}

[Serializable]
public class MessageResponse
{
    public string message;
}


[Serializable]
public class DataPayload
{
    public string type;
    public long timestamp_ms;
    public long frame_latency_ms;
    public List<ThreatBox> targets;
}

[Serializable]
public class ThreatBox
{
    public string id;
    public string level;
    public float threat_score;
    public float approach_mps;
    public float dwell_s;
    public bool verified_threat;
    public bool verification_pending;
    public string threat_reasoning;
    public string unity_instruction;
    public float distance_m;
    public BoundaryBox bbox;
}

[Serializable]
public class BoundaryBox
{
    public float x_min;
    public float y_min;
    public float width;
    public float height;
}