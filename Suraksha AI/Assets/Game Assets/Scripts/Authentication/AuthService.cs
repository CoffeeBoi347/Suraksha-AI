using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Suraksha.Auth
{
    public class AuthService
    {
        private readonly string baseUrl = "http://localhost:8000/auth";

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var requestData = new LoginRequest { email = email, password = password };
            string json = JsonUtility.ToJson(requestData);

            using (UnityWebRequest req = CreatePostRequest($"{baseUrl}/login", json))
            {
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new System.Exception($"Login failed: {req.downloadHandler.text}");
                }

                return JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
            }
        }

        private UnityWebRequest CreatePostRequest(string url, string jsonBody)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
    }
}