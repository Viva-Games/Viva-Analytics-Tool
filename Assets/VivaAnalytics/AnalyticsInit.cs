using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Initializes the analytics service. Add this component to a GameObject in the first scene of the game.
    /// This file belongs to your project: the Viva Analytics package never overwrites it when updating.
    /// </summary>
    public class AnalyticsInit : MonoBehaviour
    {
        private static bool _initialized;

        private void Awake()
        {
            if (_initialized)
                return;
            _initialized = true;

#if VIVA_FIREBASE_ANALYTICS
            // Firebase is initialized by the tracker. Events tracked before Firebase is ready are queued and sent afterwards.
            AnalyticsService.Initialize(new FirebaseAnalyticsTracker());
#else
            // The Firebase Analytics SDK is not in the project (VIVA_FIREBASE_ANALYTICS is not defined).
            // Events are only written to the console until the SDK is imported.
            Debug.LogWarning("[Analytics] Firebase Analytics SDK not found. Events will only be logged to the console.");
            AnalyticsService.Initialize(new ConsoleAnalyticsTracker());
#endif

            RegisterCommonParameters();
        }

        /// <summary>
        /// Common parameters are sent with every event. Each one is registered with a function that is
        /// evaluated every time an event is tracked, so point them to your live data (save file, player prefs...).
        /// </summary>
        private void RegisterCommonParameters()
        {
            // EXAMPLES:
            // AnalyticsService.RegisterCommonParameter("player_level", () => PlayerPrefs.GetInt("level_game"));
            // AnalyticsService.RegisterCommonParameter("hours_played", () => SaveManager.Data.HoursPlayed);
            // AnalyticsService.SetCommonParameter("build_type", Debug.isDebugBuild ? "debug" : "release");
        }
    }
}
