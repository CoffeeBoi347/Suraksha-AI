using System;

namespace Suraksha.SOS
{
    [Serializable]
    public class EmergencyContact
    {
        public string id;
        public string name;
        public string phone_number;
        public string created_at;
    }

    [Serializable]
    public class EmergencyContactsResponse
    {
        public EmergencyContact[] contacts;
    }

    [Serializable]
    public class AddEmergencyContactRequest
    {
        public string name;
        public string phone_number;
    }

    [Serializable]
    public class AddEmergencyContactResponse
    {
        public string message;
        public EmergencyContact[] data;
    }
}