using System.Collections.Generic;
using Viva.Services.Analytics;

namespace Assets.Analytics.Runtime
{
    /// <summary>
    /// Internal event to track FTUE steps
    /// </summary>
    internal class EV_FTUE : IAnalyticsEvent
    {
        // Created just once to avoid memory garbage.
        private readonly Dictionary<string, object> _fields = new Dictionary<string, object>();

        public string GetEventKey()
        {
            return AnalyticsService.Config.FtueEventName;
        }

        /// <summary>
        /// Updates the step to be tracked by this event.
        /// </summary>
        /// <param name="step">The new step to track.</param>
        public void SetStep(int step)
        {
            _fields[AnalyticsService.Config.FtueStepParameterName] = step;
        }

        public Dictionary<string, object> GetTrackingFields()
        {
            return _fields;
        }
    }
}
