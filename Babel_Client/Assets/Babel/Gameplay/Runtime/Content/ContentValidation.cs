using System;

namespace Babel.Gameplay.Content
{
    internal static class ContentValidation
    {
        public static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Content IDs cannot be empty or contain leading/trailing whitespace.", parameterName);

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-' && character != '.')
                    throw new ArgumentException("Content IDs may contain only letters, digits, '_', '-' and '.'.", parameterName);
            }

            return value;
        }

        public static string OptionalId(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return RequireId(value, parameterName);
        }

        public static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty.", parameterName);
            return value;
        }

        public static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            return value;
        }

        public static float RequireNonNegative(float value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0f)
                throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
            return value;
        }

        public static float RequirePositive(float value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0f)
                throw new ArgumentOutOfRangeException(parameterName, "Value must be greater than zero.");
            return value;
        }
    }
}
