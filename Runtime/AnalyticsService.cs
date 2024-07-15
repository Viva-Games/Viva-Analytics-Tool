using System;
using Assets.Analytics.Runtime;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Entry point to the analytics generic service.
    /// </summary>
    public static class AnalyticsService
    {
        private const string FTUE_PREF_KEY = "viva_analytics_last_ftue_tracked";

        /// <summary>
        /// Tells if the services has been initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                if (Config.EventTracker == null)
                {
                    return false;
                }
                else
                {
                    return Config.EventTracker.IsInitialized();
                }
            }
        }

        internal static AnalyticsConfig Config;

        // Cached to avoid constantly instantiating a new one everytime.
        private static readonly EV_FTUE _ftueEvent = new EV_FTUE();

        /// <summary>
        /// Initializes the analitycs service using the given tracker.
        /// </summary>
        public static void Initialize(AAnalyticsTracker tracker)
        {
            AnalyticsConfig config = new AnalyticsConfig()
            {
                EventTracker = tracker,
                FtueEventName = "ftue_landmark",
                FtueStepParameterName = "ftue_step_number"
            };

            Initialize(config);
        }

        public static void Initialize(AnalyticsConfig config)
        {
            if (config.EventTracker == null)
            {
                throw new ArgumentNullException("[Analytics] The Analytics tracker cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(config.FtueEventName) || string.IsNullOrWhiteSpace(config.FtueStepParameterName))
            {
                Debug.LogWarning("[Analytics] FTUE event name and step parameter name not properly configured. FTUE steps won't be tracked.");
            }

            if (config.EventTracker != Config.EventTracker)
            {
                Config = config;
                Config.EventTracker.Initialize();
            }
        }

        /// <summary>
        /// Tracks the given analytics event.
        /// Performs no operation if analytics are not initialized.
        /// </summary>
        /// <param name="eventToTrack">The event to track.</param>
        public static void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            if (IsInitialized)
            {
                Config.EventTracker.TrackEvent(eventToTrack);
            }
            else
            {
                throw new InvalidOperationException("[Analytics] Service not initialized. Cannot track events.");
            }
        }

        /// <summary>
        /// Tracks a preconfigured event for FTUE step.
        /// Keeps track on which was the last step tracked and does nothing if try to track
        /// a step behind the last one tracked.
        /// </summary>
        /// <param name="step">The step number to track.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the service config is not properly setted.
        /// This is NOT thrown if trying to track a step behind the last one tracked (only a warnign is printed out).
        /// </exception>
        public static void TrackFTUE(int step)
        {
            if (string.IsNullOrWhiteSpace(Config.FtueEventName) || string.IsNullOrWhiteSpace(Config.FtueStepParameterName))
            {
                throw new InvalidOperationException("[Analytics] Cannot track FTUE steps: Event name and step parameter name not properly configured.");
            }

            int lastStepTracked = PlayerPrefs.GetInt(FTUE_PREF_KEY);

            if (lastStepTracked < step)
            {
                _ftueEvent.SetStep(step);
                TrackEvent(_ftueEvent);
                PlayerPrefs.SetInt(FTUE_PREF_KEY, step);
            }
            else
            {
                Debug.LogWarning($"[Analytics] Trying to track FTUE Step {step}, but last step tracked is {lastStepTracked}. Skipping.");
            }
        }
    }
}
