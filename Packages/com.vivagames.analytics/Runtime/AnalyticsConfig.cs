namespace Viva.Services.Analytics
{
    /// <summary>
    /// Configuración del servicio de analíticas.
    /// </summary>
    public struct AnalyticsConfig
    {
        /// <summary>
        /// Tracker principal. Se mantiene por compatibilidad con la inicialización antigua:
        /// con <see cref="AnalyticsService.Initialize(AnalyticsConfig, AAnalyticsTracker[])"/> se pueden pasar varios.
        /// </summary>
        public AAnalyticsTracker EventTracker;

        /// <summary>
        /// Nombre del evento de FTUE. Normalmente ftue_landmark.
        /// </summary>
        public string FtueEventName;

        /// <summary>
        /// Nombre del parámetro que lleva el número de paso del FTUE. Normalmente ftue_step_number.
        /// </summary>
        public string FtueStepParameterName;
    }
}
