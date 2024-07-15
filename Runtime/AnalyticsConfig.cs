namespace Viva.Services.Analytics
{
    /// <summary>
    /// Stores the configuration for the Analytics service.
    /// </summary>
    public struct AnalyticsConfig
    {
        /// <summary>
        /// The analytics tracker to use.
        /// </summary>
        public AAnalyticsTracker EventTracker;

        /// <summary>
        /// The name of the FTUE event. Typically ftue_landmark.
        /// </summary>
        public string FtueEventName;

        /// <summary>
        /// The parameter name holding the step nuber of th FTUE. Typically ftue_step_number.
        /// </summary>
        public string FtueStepParameterName;
    }
}
