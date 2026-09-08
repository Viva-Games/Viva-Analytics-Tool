using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Viva.Core
{
    /// <summary>
    /// Una impresión de anuncio con ingresos, tal y como la reporta la red de mediación. La publica el módulo
    /// de anuncios (Viva Ads con AppLovin MAX) y la consumen los que atribuyen ingresos (el tracker de Singular
    /// de Viva Analytics, por ejemplo).
    /// </summary>
    public sealed class AdRevenueEvent
    {
        /// <summary>Red de mediación: "AppLovin".</summary>
        public string Platform = "AppLovin";

        /// <summary>Formato como lo reporta el SDK (REWARDED, INTER, BANNER).</summary>
        public string Format;

        public string Placement;
        public string AdUnitId;
        public string NetworkName;
        public string NetworkPlacement;
        public string CreativeId;
        public double Revenue;

        /// <summary>exact, estimated, publisher_defined, undefined.</summary>
        public string RevenuePrecision;

        public string Currency = "USD";

        public override string ToString()
        {
            return $"{Platform} {Format} placement={Placement} network={NetworkName} revenue={Revenue.ToString(CultureInfo.InvariantCulture)} {Currency} ({RevenuePrecision})";
        }
    }

    /// <summary>
    /// Flujo de impresiones con ingresos entre módulos Viva, que no se referencian entre sí. A diferencia de
    /// <see cref="VivaConsent"/> no guarda estado: quien se suscribe recibe las impresiones a partir de entonces.
    /// Hilo principal.
    /// </summary>
    public static class VivaAdRevenue
    {
        private static readonly List<Action<AdRevenueEvent>> _subscribers = new List<Action<AdRevenueEvent>>();

        /// <summary>Impresiones publicadas desde que arrancó el juego.</summary>
        public static int PublishedCount { get; private set; }

        /// <summary>Apunta el callback una sola vez.</summary>
        public static void Subscribe(Action<AdRevenueEvent> callback)
        {
            if (callback == null) return;
            if (!_subscribers.Contains(callback)) _subscribers.Add(callback);
        }

        public static void Unsubscribe(Action<AdRevenueEvent> callback)
        {
            if (callback == null) return;
            _subscribers.Remove(callback);
        }

        /// <summary>Publica una impresión a los suscritos, en orden de suscripción. Una excepción en uno no afecta al resto.</summary>
        public static void Publish(AdRevenueEvent revenue)
        {
            if (revenue == null)
            {
                Debug.LogWarning("[Viva] VivaAdRevenue.Publish ignored a null event.");
                return;
            }

            PublishedCount++;
            foreach (var subscriber in _subscribers.ToArray())
            {
                try
                {
                    subscriber(revenue);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Viva] An ad revenue subscriber failed: " + e);
                }
            }
        }

        /// <summary>Deja el flujo como recién cargado. Solo para tests.</summary>
        internal static void Reset()
        {
            _subscribers.Clear();
            PublishedCount = 0;
        }
    }
}
