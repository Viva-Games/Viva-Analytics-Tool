using UnityEngine;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Initializes Remote Config. Add this component to a GameObject in the first scene of the game.
    /// This file belongs to your project: the Viva Remote Config package never overwrites it when updating.
    /// </summary>
    public class RemoteConfigInit : MonoBehaviour
    {
        private static bool _initialized;

        private void Awake()
        {
            if (_initialized)
                return;
            _initialized = true;

            // The defaults come from the generated RemoteConfigParameters class (Viva > Remote Config > Parameters).
            // With the Firebase Remote Config SDK in the project the values are fetched from Firebase once Viva Core has
            // run the shared Firebase dependency check; without the SDK the defaults are used. The order between this
            // component and AnalyticsInit does not matter: whoever runs first starts the check and the other one waits.
            RemoteConfigService.Initialize(RemoteConfigParameters.Defaults(), new RemoteConfigOptions
            {
                // MinimumFetchInterval = System.TimeSpan.Zero,    // fetch on every cold start (default). Firebase's own default is 12 hours.
                // FetchTimeout = System.TimeSpan.FromSeconds(30),  // null keeps the SDK default (60 seconds).
                // ActivateCachedValuesOnStart = true,              // values of the previous session are readable before this fetch ends.
                // LogToConsole = true,
            });

            // Read RemoteConfigParameters.X at any time: before the fetch ends you get the cached or default value.
            // For code that must wait for the fetch: RemoteConfigService.WhenReady(() => ...); RemoteConfigService.Source says
            // whether the values come from the server, the cache of a previous session or the defaults.
        }
    }
}
