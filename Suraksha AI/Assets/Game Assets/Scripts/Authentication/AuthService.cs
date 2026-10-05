using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Suraksha.Auth
{
    public class AuthService
    {
        private readonly string baseUrl = Constants.BASE_URL + Constants.AUTH_TYPE;
        public string AccessToken { get; private set; }

        public AuthService()
        {
            if (PlayerPrefs.HasKey("access_token"))
            {
                AccessToken = PlayerPrefs.GetString("access_token");
            }
        }

        public async Task<SignUpResponse> SignUpAsync(string email, string password, string fullName, string phoneNumber)
        {
            var requestData = new SignUpRequest
            {
                email = email,
                password = password,
                full_name = fullName,
                phone_number = phoneNumber
            };
            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/signup", json))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Sign Up failed ({req.result}): {req.error} | {req.downloadHandler.text}");
                }

                Debug.Log($"[Auth] /signup raw response: {req.downloadHandler.text}");

                var res = JsonUtility.FromJson<SignUpResponse>(req.downloadHandler.text);

                Debug.Log($"[Auth] /signup parsed access_token length={res?.access_token?.Length ?? -1}");

                if (!string.IsNullOrEmpty(res.access_token)) AccessToken = res.access_token;
                return res;
            }
        }

        public async Task<LoginResponse> LoginAsync(string email, string password, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            try
            {
                var requestData = new LoginRequest { email = email, password = password };
                string json = JsonUtility.ToJson(requestData);

                using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/login", json))
                {
                    var operation = req.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        throw new Exception($"Login failed ({req.result}): {req.error} | {req.downloadHandler.text}");
                    }

                    var res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                    if (!string.IsNullOrEmpty(res.access_token)) AccessToken = res.access_token;
                    onSuccess?.Invoke(res);
                    return res;
                }
            }

            catch (Exception ex)
            {
                if (!ex.Message.StartsWith("Login failed"))
                {
                    onError?.Invoke(ex.Message);
                }
                throw;
            }
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            var requestData = new ForgotPasswordRequest { email = email };
            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/forgot-password", json))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Forgot password failed ({req.result}): {req.error} | {req.downloadHandler.text}");
                }
                return true;
            }
        }

        public async Task<MessageResponse> RequestPhoneOtpAsync(string phoneNumber)
        {
            var requestData = new PhoneOtpRequest { phone_number = phoneNumber };
            string json = JsonUtility.ToJson(requestData);

            Debug.Log($"[Auth] /phone/request-otp using AccessToken length={AccessToken?.Length ?? -1}");

            using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/phone/request-otp", json, AccessToken))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Request OTP failed ({req.result}): {req.error} | {req.downloadHandler.text}");
                }
                return JsonUtility.FromJson<MessageResponse>(req.downloadHandler.text);
            }
        }

        public async Task<MessageResponse> VerifyPhoneOtpAsync(string phoneNumber, string otp)
        {
            var requestData = new PhoneOtpVerifyRequest { phone_number = phoneNumber, otp = otp };
            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/phone/verify-otp", json, AccessToken))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Verify OTP failed ({req.result}): {req.error} | {req.downloadHandler.text}");
                }
                return JsonUtility.FromJson<MessageResponse>(req.downloadHandler.text);
            }
        }

        public async Task<bool> ValidateSessionAsync()
        {
            if (string.IsNullOrEmpty(AccessToken))
                return false;

            using (UnityWebRequest req = UnityWebRequest.Get($"{baseUrl}/me"))
            {
                req.SetRequestHeader(
                    "Authorization",
                    $"Bearer {AccessToken}"
                );

                var operation = req.SendWebRequest();

                while (!operation.isDone)
                    await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"Auto-login validation failed: {req.responseCode} {req.downloadHandler.text}"
                    );

                    return false;
                }

                return true;
            }
        }

        private UnityWebRequest CreatePostRequest(string url, string jsonBody, string accessToken = null)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(accessToken))
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            return request;
        }
    }
}