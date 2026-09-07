using System;
using System.Text;
using UnityEngine;

namespace Viva.Services.Utils
{
    public static class StringUtils
    {
        public static string ToUpperCamelCase(string stringToChange)
        {
            if (string.IsNullOrEmpty(stringToChange)) return stringToChange;

            // Replace spaces with underscores to handle both formats uniformly
            stringToChange = stringToChange.Replace(" ", "_");
            var words = stringToChange.Split('_');
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    result.Append(char.ToUpper(word[0]) + word.Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        public static string ToLowerCamelCase(string snakeCaseString)
        {
            if (string.IsNullOrEmpty(snakeCaseString)) return snakeCaseString;

            var words = snakeCaseString.Split('_');
            var result = new StringBuilder(words[0].ToLower());

            for (int i = 1; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    result.Append(char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        public static string ToSnakeCase(string stringToConvert)
        {
            if (string.IsNullOrEmpty(stringToConvert)) return stringToConvert;

            var result = new StringBuilder();
            result.Append(char.ToLower(stringToConvert[0]));

            for (int i = 1; i < stringToConvert.Length; i++)
            {
                // If the character is uppercase or a space, add an underscore
                if (char.IsUpper(stringToConvert[i]) || stringToConvert[i] == ' ')
                {
                    result.Append('_');
                }

                result.Append(char.ToLower(stringToConvert[i]));
            }

            return result.ToString().Replace(" ", "");
        }

        /// <summary>
        /// Compare two strings and log the differences.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>true if the strings are identical, false if the strings are different
        /// </returns>
        public static bool CompareStringsWithLogs(string a, string b)
        {
            if (a == b)
            {
                Debug.Log("The strings are identical.");
                return true;
            }

            Debug.Log("The strings are different. Differences are highlighted below:");

            int minLength = Math.Min(a.Length, b.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (a[i] != b[i])
                {
                    Debug.Log($"Difference at index {i}: '{a[i]}' != '{b[i]}'");
                }
            }

            if (a.Length != b.Length)
            {
                Debug.Log($"The strings have different lengths: {a.Length} != {b.Length}");
            }

            return false;
            //string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string content)
        {
            return content.Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace(" ", "")
                .Replace("\t", "");
        }
    }
}