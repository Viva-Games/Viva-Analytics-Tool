using System.Text;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Clave de Remote Config → nombre de propiedad C# (PascalCase). Las claves no se normalizan nunca
    /// (deben coincidir con la consola de Firebase); solo se deriva el identificador del código generado.
    /// </summary>
    public static class RemoteConfigNaming
    {
        /// <summary>Miembros que ya existen en la clase generada y no pueden usarse como nombre de parámetro.</summary>
        public static readonly string[] ReservedIdentifiers = { "Keys", "Defaults" };

        /// <summary>
        /// startInterstitialsLevel → StartInterstitialsLevel, idfa_list → IdfaList, MAX_LIVES → MaxLives,
        /// HTTPTimeout → HttpTimeout, level10Bonus → Level10Bonus. Devuelve "_" si no queda nada.
        /// </summary>
        public static string ToIdentifier(string key)
        {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(key))
            {
                bool startOfWord = true;
                for (int i = 0; i < key.Length; i++)
                {
                    char c = key[i];
                    if (c == '_' || c == '-' || c == ' ' || c == '.')
                    {
                        startOfWord = true;
                        continue;
                    }
                    if (!char.IsLetterOrDigit(c)) continue;

                    if (i > 0 && char.IsUpper(c))
                    {
                        char previous = key[i - 1];
                        bool afterLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                        bool endOfAcronym = char.IsUpper(previous) && i + 1 < key.Length && char.IsLower(key[i + 1]);
                        if (afterLowerOrDigit || endOfAcronym) startOfWord = true;
                    }
                    else if (i > 0 && char.IsDigit(c) && char.IsLetter(key[i - 1]))
                    {
                        // Los dígitos se quedan pegados a la palabra: level10 → Level10.
                    }

                    result.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    startOfWord = false;
                }
            }

            if (result.Length == 0) return "_";
            if (char.IsDigit(result[0])) result.Insert(0, '_');
            return result.ToString();
        }

        public static bool IsReserved(string identifier)
        {
            foreach (var reserved in ReservedIdentifiers)
            {
                if (reserved == identifier) return true;
            }
            return false;
        }

        /// <summary>true si es un identificador C# válido (letra o _ inicial, letras, dígitos y _).</summary>
        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            if (!(char.IsLetter(identifier[0]) || identifier[0] == '_')) return false;
            for (int i = 1; i < identifier.Length; i++)
            {
                if (!(char.IsLetterOrDigit(identifier[i]) || identifier[i] == '_')) return false;
            }
            return true;
        }
    }
}
