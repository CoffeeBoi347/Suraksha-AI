using Suraksha.Auth;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Suraksha.SOS
{
    public class SOSContactsView : MonoBehaviour
    {
        public const int MAX_CONTACTS = 3;

        [Header("Popup")]
        [SerializeField] private UITweenerController _popupController;

        [Header("Contact Rows")]
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private ContactRowView _rowView;

        [Header("Buttons")]
        [SerializeField] private Button _addButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _sosButton;

        private EmergencySOSService contactService;
        private readonly List<ContactRowView> rows = new List<ContactRowView>();
        private bool isBusy;

        private void Awake()
        {
            AuthService authService = new AuthService();
            contactService = new EmergencySOSService(authService);

            _sosButton.onClick.AddListener(OpenPopup);
            _closeButton.onClick.AddListener(ClosePopup);
            _addButton.onClick.AddListener(AddRow);
            _saveButton.onClick.AddListener(() => _ = SaveContactsAsync());
        }

        private void AddRow()
        {
            if (isBusy || rows.Count >= MAX_CONTACTS)
            {
                Notification.Instance.ShowMessage(
                    "SOS",
                    $"You can save a maximum of {MAX_CONTACTS} emergency contacts.");
                return;
            }

            CreateRow(null);
        }

        public void OpenPopup()
        {
            _popupController.Init();
            _ = LoadContactsAsync();
        }

        public void ClosePopup()
        {
            if (isBusy) return;
            _popupController.SetInactive();
        }

        private void CreateRow(EmergencyContact contact)
        {
            if (rows.Count >= MAX_CONTACTS)
                return;

            ContactRowView row = Instantiate(_rowView, rowsContainer);
            row.Init(contact);

            if (row._deleteButton != null)
                row._deleteButton.onClick.AddListener(() => _ = RemoveRowAsync(row));

            rows.Add(row);
            UpdateButtons();
        }

        private void ClearRows()
        {
            foreach (ContactRowView row in rows)
                if (row != null) Destroy(row.gameObject);

            rows.Clear();
        }

        // Rebuilds UI rows from server state. Never throws.
        private async Task ReloadRowsAsync()
        {
            try
            {
                EmergencyContact[] contacts = await contactService.GetContactsAsync();

                ClearRows();

                if (contacts != null)
                {
                    foreach (EmergencyContact c in contacts)
                    {
                        if (rows.Count >= MAX_CONTACTS) break;
                        CreateRow(c);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load emergency contacts: {ex.Message}");
            }

            if (rows.Count == 0)
                CreateRow(null);
        }

        private async Task SaveContactsAsync()
        {
            if (isBusy) return;

            isBusy = true;
            UpdateButtons();

            bool ok = true;
            bool validationFailed = false;

            try
            {
                // ---- validate ----
                HashSet<string> entered = new HashSet<string>();

                foreach (ContactRowView row in rows)
                {
                    if (row.savedContact != null) continue;

                    string input = row._contactNameField != null
                        ? row._contactNameField.text
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(input)) continue;

                    if (!IndianMobileNumber.TryNormalize(
                            input, out string n, out _, out string error))
                    {
                        Notification.Instance.ShowMessage("SOS", error);
                        validationFailed = true;
                        return;
                    }

                    if (!entered.Add(n))
                    {
                        Notification.Instance.ShowMessage(
                            "SOS", "Duplicate mobile numbers are not allowed.");
                        validationFailed = true;
                        return;
                    }
                }

                // ---- add ----
                for (int i = 0; i < rows.Count; i++)
                {
                    ContactRowView row = rows[i];
                    if (row.savedContact != null) continue;

                    string input = row._contactNameField != null
                        ? row._contactNameField.text
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(input)) continue;

                    IndianMobileNumber.TryNormalize(
                        input, out string normalized, out _, out _);

                    try
                    {
                        await contactService.AddContactAsync($"Contact {i + 1}", normalized);
                    }
                    catch (System.Exception ex) when (ex.Message.Contains("HTTP 409"))
                    {
                        // already saved on an earlier attempt
                    }
                }
            }
            catch (System.Exception ex)
            {
                ok = false;
                Debug.LogError($"Failed to save SOS contacts: {ex.Message}");
            }
            finally
            {
                isBusy = false;
            }

            if (validationFailed)
            {
                UpdateButtons();
                return;
            }

            // always resync so UI matches DB
            await ReloadRowsAsync();

            Notification.Instance.ShowMessage(
                "SOS",
                ok
                    ? "Emergency contacts saved successfully."
                    : "Failed to save contacts. Please try again.");

            UpdateButtons();
        }

        public async Task LoadContactsAsync()
        {
            if (isBusy) return;

            isBusy = true;
            UpdateButtons();

            try
            {
                ClearRows();
                await ReloadRowsAsync();
            }
            finally
            {
                isBusy = false;
                UpdateButtons();
            }
        }

        private async Task RemoveRowAsync(ContactRowView view)
        {
            if (isBusy || view == null) return;

            if (view.savedContact != null)
            {
                isBusy = true;
                UpdateButtons();
                Notification.Instance.ShowMessage("SOS", "Removing contact...");

                try
                {
                    await contactService.DeleteContactAsync(view.savedContact.id);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to remove contact: {ex.Message}");
                    Notification.Instance.ShowMessage(
                        "SOS", "Failed to remove contact. Please try again.");
                    return;
                }
                finally
                {
                    isBusy = false;
                    UpdateButtons();
                }
            }

            rows.Remove(view);
            Destroy(view.gameObject);

            if (rows.Count == 0)
                CreateRow(null);

            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_saveButton != null)
                _saveButton.interactable = !isBusy;

            if (_closeButton != null)
                _closeButton.interactable = !isBusy;
        }
    }
}