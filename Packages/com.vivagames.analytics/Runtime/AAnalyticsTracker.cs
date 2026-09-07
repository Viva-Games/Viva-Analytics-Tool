namespace Viva.Services.Analytics
{
    /// <summary>
    /// Class in charge of tracking analitycs events.
    /// </summary>
    public abstract class AAnalyticsTracker
    {
        /// <summary>
        /// Performs operations needed to initialize this tracker.
        /// </summary>
        /// <returns>Whether the initialization has been succeed.</returns>
        protected internal abstract void Initialize();

        /// <summary>
        /// Checks if this tracker is initialized
        /// </summary>
        protected internal abstract bool IsInitialized();

        /// <summary>
        /// Tracks the given event.
        /// </summary>
        public abstract void TrackEvent(IAnalyticsEvent eventToTrack);

        /// <summary>
        /// Propiedad de usuario (segmento, país...). Por defecto no hace nada, para que los trackers
        /// antiguos sigan compilando; cada SDK la implementa como pueda.
        /// </summary>
        public virtual void SetUserProperty(string name, string value)
        {
        }

        /// <summary>
        /// Identificador del usuario. Por defecto no hace nada.
        /// </summary>
        public virtual void SetUserId(string userId)
        {
        }
    }
}
