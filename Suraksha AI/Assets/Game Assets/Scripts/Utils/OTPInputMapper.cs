using TMPro;
using UnityEngine;

public class OTPInputMapper : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] _digitTexts;
    [SerializeField] private TMP_InputField _otpText;

    private void Start()
    {
        if (_otpText != null)
        {
            _otpText.onValueChanged.AddListener(OnOTPValueChanged);
            _otpText.characterLimit = _digitTexts.Length;
        }
    }

    private void OnOTPValueChanged(string value)
    {
        for (int i = 0; i <  _digitTexts.Length; i++)
        {
            if (i < value.Length)
            {
                _digitTexts[i].text = value[i].ToString();
            }

            else
            {
                _digitTexts[i].text = string.Empty;
            }
        }
    }

    public string GetOTPValue()
    {
        return _otpText != null ? _otpText.text.Trim() : string.Empty;
    }
}