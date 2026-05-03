using System;

namespace Murang.Multiplayer.Room.Common
{
    internal static class RoomAutomationCommandLine
    {
        public static string GetString(string argumentName, string defaultValue = "")
        {
            return TryGetArgumentValue(argumentName, out string value)
                ? value
                : defaultValue;
        }

        public static bool GetBool(string argumentName, bool defaultValue = false)
        {
            if (!TryGetArgumentValue(argumentName, out string value))
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
        }

        public static int GetInt(string argumentName, int defaultValue)
        {
            return TryGetArgumentValue(argumentName, out string value)
                   && int.TryParse(value, out int parsed)
                ? parsed
                : defaultValue;
        }

        public static float GetFloat(string argumentName, float defaultValue)
        {
            return TryGetArgumentValue(argumentName, out string value)
                   && float.TryParse(value, out float parsed)
                ? parsed
                : defaultValue;
        }

        public static bool TryGetArgumentValue(string argumentName, out string value)
        {
            value = null;
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments == null)
            {
                return false;
            }

            for (int index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index == arguments.Length - 1 || IsArgumentName(arguments[index + 1]))
                {
                    value = string.Empty;
                    return true;
                }

                value = arguments[index + 1];
                return true;
            }

            return false;
        }

        private static bool IsArgumentName(string value)
        {
            return !string.IsNullOrEmpty(value) && value[0] == '-';
        }
    }
}
