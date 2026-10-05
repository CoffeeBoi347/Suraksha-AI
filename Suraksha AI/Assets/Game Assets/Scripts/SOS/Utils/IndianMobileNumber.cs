
using System.Text;

namespace Suraksha.SOS
{
    public static class IndianMobileNumber
    {
        public static bool TryNormalize(
            string input,
            out string e164,
            out string localNumber,
            out string error)
        {
            e164 = null;
            localNumber = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Enter a mobile number.";
                return false;
            }

            var digits = new StringBuilder();

            foreach (char c in input.Trim())
            {
                if (c >= '0' && c <= '9')
                {
                    digits.Append(c);
                }
                else if (c == ' ' || c == '-')
                {
                    // Allow spaces and hyphens.
                }
                else
                {
                    error = "Enter your 10-digit mobile number only.";
                    return false;
                }
            }

            string number = digits.ToString();

            if (number.Length != 10)
            {
                error = "Mobile number must contain exactly 10 digits.";
                return false;
            }

            if (number[0] < '6' || number[0] > '9')
            {
                error = "Mobile number must start with 6, 7, 8, or 9.";
                return false;
            }

            localNumber = number;
            e164 = "+91" + number;

            return true;
        }

        public static string GetLocalNumber(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
                return string.Empty;

            number = number.Trim();

            if (number.StartsWith("+91"))
                number = number.Substring(3);
            else if (number.StartsWith("91") && number.Length == 12)
                number = number.Substring(2);

            return number;
        }
    }
}
