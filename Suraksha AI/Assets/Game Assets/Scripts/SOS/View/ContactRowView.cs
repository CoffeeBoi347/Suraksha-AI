using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Suraksha.SOS
{
    public class ContactRowView : MonoBehaviour
    {
        public Button _deleteButton;
        public TMP_InputField _contactNameField;

        [HideInInspector]
        public EmergencyContact savedContact;

        public void Init(EmergencyContact contact)
        {
            savedContact = contact;

            if (_contactNameField != null)
            {
                _contactNameField.text = contact != null
                    ? IndianMobileNumber.GetLocalNumber(contact.phone_number)
                    : string.Empty;

                _contactNameField.interactable = true;
            }
        }
    }
}