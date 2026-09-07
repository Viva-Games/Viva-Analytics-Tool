using System.Collections.Generic;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Evento interno para registrar los pasos del FTUE.
    /// </summary>
    internal class EV_FTUE : IAnalyticsEvent
    {
        // Se crea una sola vez para no generar basura en cada paso.
        private readonly Dictionary<string, object> _fields = new Dictionary<string, object>();

        public string GetEventKey()
        {
            return AnalyticsService.Config.FtueEventName;
        }

        /// <summary>
        /// Actualiza el paso que enviará este evento.
        /// </summary>
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
