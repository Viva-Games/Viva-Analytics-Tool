using System;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Identificador de usuario del estudio: un GUID generado la primera vez y guardado en PlayerPrefs. Se
    /// pasa a <see cref="AnalyticsService.SetUserId"/> y llega a Firebase como user id y a Singular como
    /// custom user id, así el mismo jugador se ve igual en las dos plataformas.
    /// </summary>
    public static class VivaUserId
    {
        public const string PLAYER_PREFS_KEY = "viva_user_id";

        /// <summary>El identificador guardado, o uno nuevo si aún no había.</summary>
        public static string GetOrCreate()
        {
            string existing = PlayerPrefs.GetString(PLAYER_PREFS_KEY, string.Empty);
            if (!string.IsNullOrEmpty(existing)) return existing;
            return Reset();
        }

        public static bool Exists => !string.IsNullOrEmpty(PlayerPrefs.GetString(PLAYER_PREFS_KEY, string.Empty));

        /// <summary>Genera y guarda un identificador nuevo (por ejemplo, al borrar la cuenta del jugador).</summary>
        public static string Reset()
        {
            string id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PLAYER_PREFS_KEY, id);
            PlayerPrefs.Save();
            return id;
        }

        /// <summary>Borra el identificador guardado: la siguiente llamada a GetOrCreate crea uno nuevo.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PLAYER_PREFS_KEY);
        }
    }
}
