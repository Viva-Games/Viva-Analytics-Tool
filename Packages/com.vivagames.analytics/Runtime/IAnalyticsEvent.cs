using System.Collections.Generic;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Stores data for an event to be tracked by the analytics service.
    /// </summary>
    public interface IAnalyticsEvent
    {
        /// <summary>
        /// Gets the event analitycs key.
        /// </summary>
        string GetEventKey();

        /// <summary>
        /// Gets all fields to be tracked paired with their analitycs parameter name.
        /// </summary>
        Dictionary<string, object> GetTrackingFields();
    }
}
