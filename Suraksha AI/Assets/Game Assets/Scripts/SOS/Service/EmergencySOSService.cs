using Suraksha.Auth;
using Suraksha.SOS;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EmergencySOSService
{
    private readonly string contactsUrl;
    private readonly AuthService authService;

    public EmergencySOSService(AuthService authService)
    {
        this.authService = authService;
        contactsUrl = $"{Constants.BASE_URL}".TrimEnd('/') + "/sos/contacts";
    }

    public async Task<EmergencyContact[]> GetContactsAsync()
    {
        using (UnityWebRequest req = UnityWebRequest.Get(contactsUrl))
        {
            await SendAsync(req);

            var response = JsonUtility.FromJson<EmergencyContactsResponse>(
                req.downloadHandler.text);

            return response?.contacts ?? Array.Empty<EmergencyContact>();
        }
    }

    // POST only. Server enforces max contacts + duplicates.
    public async Task AddContactAsync(string name, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a contact name.");

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Enter a phone number.");

        var payload = new AddEmergencyContactRequest
        {
            name = name.Trim(),
            phone_number = phoneNumber.Trim()
        };

        using (UnityWebRequest req =
               CreateJsonRequest(contactsUrl, "POST", JsonUtility.ToJson(payload)))
        {
            await SendAsync(req);
        }
    }

    public async Task DeleteContactAsync(string contactId)
    {
        if (string.IsNullOrWhiteSpace(contactId))
            throw new ArgumentException("Invalid contact ID.");

        string url = contactsUrl + "/" + UnityWebRequest.EscapeURL(contactId);

        using (UnityWebRequest req = new UnityWebRequest(url, "DELETE"))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            await SendAsync(req);
        }
    }

    private UnityWebRequest CreateJsonRequest(string url, string method, string json)
    {
        var req = new UnityWebRequest(url, method);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }

    private async Task SendAsync(UnityWebRequest req)
    {
        string token = authService?.AccessToken;

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "You must be logged in to manage emergency contacts.");

        req.SetRequestHeader("Authorization", "Bearer " + token);

        var operation = req.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";

            throw new Exception(
                $"Emergency contacts API failed (HTTP {req.responseCode}): {req.error} | {body}");
        }
    }
}