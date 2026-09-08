using System;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Destinos de un evento. Firebase recibe siempre; Facebook y Singular solo los eventos marcados en el
    /// Event Manager. Un tracker declara qué destinos atiende (la consola, todos).
    /// </summary>
    [Flags]
    public enum AnalyticsTargets
    {
        None = 0,
        Firebase = 1,
        Facebook = 2,
        Singular = 4,
        All = Firebase | Facebook | Singular
    }

    /// <summary>Evento que declara sus destinos. Los generados sin destinos extra no lo implementan y cuentan como solo Firebase.</summary>
    public interface IRoutedAnalyticsEvent : IAnalyticsEvent
    {
        AnalyticsTargets Targets { get; }
    }

    public static class AnalyticsTargetsExtensions
    {
        /// <summary>Destinos del evento: los declarados, o solo Firebase si no declara ninguno.</summary>
        public static AnalyticsTargets TargetsOf(this IAnalyticsEvent analyticsEvent)
        {
            return analyticsEvent is IRoutedAnalyticsEvent routed ? routed.Targets : AnalyticsTargets.Firebase;
        }

        /// <summary>true si comparten al menos un destino.</summary>
        public static bool Includes(this AnalyticsTargets targets, AnalyticsTargets other)
        {
            return (targets & other) != 0;
        }
    }
}
