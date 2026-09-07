using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Tracker que solo escribe los eventos en la consola de Unity.
    /// Útil en el editor, en pruebas y mientras el proyecto aún no tiene ningún SDK de analíticas instalado.
    /// </summary>
    public class ConsoleAnalyticsTracker : AAnalyticsTracker
    {
        private bool _isInitialized;

        protected internal override void Initialize()
        {
            _isInitialized = true;
        }

        protected internal override bool IsInitialized() => _isInitialized;

        public override void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            Debug.Log(Format(eventToTrack));
        }

        /// <summary>
        /// Representación de un evento para la consola: ANALYTICS: event_key (param = value, ...).
        /// </summary>
        public static string Format(IAnalyticsEvent eventToTrack)
        {
            var builder = new StringBuilder("ANALYTICS: ");
            builder.Append(eventToTrack.GetEventKey());
            builder.Append(" (");

            Dictionary<string, object> fields = eventToTrack.GetTrackingFields();
            if (fields != null)
            {
                bool first = true;
                foreach (var pair in fields)
                {
                    if (!first) builder.Append(", ");
                    first = false;
                    builder.Append(pair.Key).Append(" = ").Append(pair.Value);
                }
            }

            builder.Append(")");
            return builder.ToString();
        }
    }
}
